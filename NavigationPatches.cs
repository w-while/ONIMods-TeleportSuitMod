using HarmonyLib;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using static STRINGS.INPUT_BINDINGS;

namespace TeleportSuitMod
{
    public class NavigationPatches
    {
        private static readonly IDetouredField<TransitionDriver, Navigator.ActiveTransition> TRANSITION =
    PDetours.DetourField<TransitionDriver, Navigator.ActiveTransition>("transition");
        //记录小人的星球信息
        public static readonly Dictionary<Navigator, int> NavigatorWorldId = new Dictionary<Navigator, int>();

        public bool ClusterMoveTO = false;
        //退出一个存档时要把需要保存的数据设置为空，否则可能会影响下一个存档
        [HarmonyPatch(typeof(LoadScreen), nameof(LoadScreen.ForceStopGame))]
        public static class LoadScreen_ForceStopGame_Patch
        {
            internal static void Prefix()
            {
                TeleportationOverlay.TeleportRestrict = null;
                if (NavigatorWorldId != null)
                {
                    NavigatorWorldId.Clear();
                }
                if (TeleportSuitWorldCountManager.Instance != null && TeleportSuitWorldCountManager.Instance.WorldCount != null)
                {
                    TeleportSuitWorldCountManager.Instance.WorldCount.Clear();
                }
            }
        }

        //修改整个殖民地能否到达某个方块，会影响世界的库存等等
        [HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.IsReachable), new Type[] { typeof(int) })]
        public static class MinionGroupProber_IsReachable_AssumeLock_Patch
        {
            private static readonly string ModuleName = "MinionGroupProberPatch";
            public static void Postfix(int cell, ref bool __result)
            {
                //如果判定为无法常规到达，则开始判定是否能传送到达
                if (__result == false)
                {
                    __result = CanBeReachByMinionGroup(cell);
                }
            }
        }

        //修改穿着传送服的小人到各个格子的可达性,影响小人获取任务等等
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.GetNavigationCost), new Type[] { typeof(int) })]
        public static class Navigator_GetNavigationCost_Patch
        {
            private static readonly string ModuleName = "NavigationCostPath";
            public static bool Prefix(Navigator __instance, int cell, ref int __result)
            {
                if (TeleNavigator.isTeleMiniom(__instance))//穿着传送服
                {
                    __result = -1;
                    if (TeleNavigator.ShortRange > 0)
                    {
                        // 1. 获取PathGrid的原生ProberCells成本（ShortRange内已更新）
                        int nativeCost = __instance.PathGrid.GetCost(cell);
                        
                        // 2. 判定：成本<ShortRange则使用原生Cost，否则用Tele逻辑
                        if (nativeCost > 0 && nativeCost <= TeleNavigator.ShortRange && nativeCost != float.MaxValue)
                        {
                            __result = nativeCost; // 走原生寻路逻辑
                            return false;
                        }
                    }
                    if (NavigatorWorldId.TryGetValue(__instance, out int id) && id != -1)
                    {
                        if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue
                            && ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]) != null
                                && ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId == id
                                && TeleportSuitConfig.CanTeloportTo(cell))
                        {
                            int targetWorldId = Grid.WorldIdx[cell];
                            int mycell = Grid.PosToCell(__instance);
                            //===== 新增：太空舱拦截逻辑（最优先判断）=====
                            if (targetWorldId != Grid.WorldIdx[mycell] && __instance.TryGetComponent<MinionIdentity>(out var minion))
                            {
                                // 太空舱拦截：阻断则直接返回，不执行后续传送逻辑
                                if (RocketCabinRestriction.QuickCheckBlockTeleport(minion, targetWorldId))
                                {
                                    return false;
                                }
                            }
                            __result = 1;
                        }
                    }
                    return false;
                }

                return true;
            }
        }
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.GoTo), new Type[] {
            typeof(KMonoBehaviour), typeof(CellOffset[]), typeof(NavTactic)
        })]
        public static class Navigator_GoTo_Patch
        {
            private static readonly string ModuleName = "NavigatorGoToPatch";
            public static void Prefix(Navigator __instance, KMonoBehaviour target, CellOffset[] offsets, NavTactic tactic)
            {
                if (TeleNavigator.ShortRange <= 0) return;
                if (__instance == null || target == null) return;

                // 仅处理穿着Tele服的小人
                if (!TeleNavigator.isTeleMiniom(__instance)) return;

                // 1. 获取初始目标单元格（稳定值，不受后续路径重算影响）
                int initialTargetCell = Grid.PosToCell(target);
                // 2. 获取小人当前单元格（发起导航时的位置，而非移动过程中的位置）
                int currentCell = __instance.cachedCell;

                // 3. 计算初始目标与当前位置的距离
                int cellPrefernce = tactic.GetCellPreferences(currentCell,offsets,__instance);
                int distance = __instance.PathGrid.GetCost(cellPrefernce);
                // 4. 判定是否为短距离
                bool isShortRange = distance != -1 ? distance <= TeleNavigator.ShortRange : false;
                LogUtils.LogDebug("NaviP",$"currentCell:[{currentCell}] targetCell:[{initialTargetCell}] CellPrefernce:[{cellPrefernce}] cellPrefernce Cost:[{distance}]");
                // 5. 缓存结果（加锁保证线程安全）
                lock (TeleNavigator._naviTargetCacheLock)
                {
                    if (TeleNavigator.NavTargetCache.ContainsKey(__instance))
                        TeleNavigator.NavTargetCache[__instance] = (initialTargetCell, isShortRange);
                    else
                        TeleNavigator.NavTargetCache.Add(__instance, (initialTargetCell, isShortRange));
                }
            }
        }

        //取消穿着传送服的小人到各个格子的可达性更新（为了优化一点性能），并且记录小人的世界信息，
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.UpdateProbe), new Type[] { typeof(bool) })]
        public static class PathProber_UpdateProbe_Patch
        {
            private static readonly string ModuleName = "UpdateProbePath";
            public static bool Prefix(Navigator __instance, bool forceUpdate = false)
            {
                //LogUtils.LogDebug("NaviP",$"forceUpdate:[{forceUpdate}] executePathProbeTaskAsync: [{__instance.executePathProbeTaskAsync}]");
                if (__instance == null) return true;

                int cell = Grid.PosToCell(__instance.gameObject.transform.position);
                if (Grid.IsValidCell(cell) && TeleNavigator.isTeleMiniom(__instance))
                {
                    if (Grid.WorldIdx[cell] != byte.MaxValue)
                    {
                        //线程安全
                        lock (NavigatorWorldId)
                        {
                            if (ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]) != null)
                            {
                                NavigatorWorldId[__instance] = ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId;
                            }
                        }
                    }
                    else
                    {
                        lock (NavigatorWorldId)
                        {
                            NavigatorWorldId[__instance] = -1;
                        }
                    }
                    //核心: 步行边界设置后，需要始终调用UpdateProbe来更新ProberCells
                    if (TeleNavigator.ShortRange <= 0) return false;
                }
                return true;
            }
        }

        //修改穿着传送服的小人RunQuery的方式
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.RunQuery))]
        public static class Navigator_RunQuery_Patch
        {
            private static readonly string ModuleName = "RunQueryPatch";
            // 彻底简化：只保留“最近的可传送格子”或“目标格子是否可传送”
            static int maxCheckDistance = 20; // 进一步缩小范围（传送不需要远距寻路）
            public static bool Prefix(Navigator __instance, PathFinderQuery query)
            {
                if ((__instance.flags & TeleportSuitConfig.TeleportSuitFlags) == 0)
                    return true;

                int rootCell = Grid.PosToCell(__instance);
                if (!Grid.IsValidCell(rootCell))
                    return false;

                // 安全解析目标格子（只关注最终目标，不遍历所有格子）
                int targetCell = GetQueryTargetCellSafe(query, __instance);
                if (!Grid.IsValidCell(targetCell))
                    return true;

                // 核心简化：直接判断目标格子是否可传送，不遍历周边
                if (TeleportSuitConfig.CanTeloportTo(targetCell))
                {
                    int distance = TeleportCore.GetManhattanDistance(rootCell, targetCell);
                    if (distance <= maxCheckDistance)
                    {
                        NavType navType = TeleportCore.GetNavTypeForCell(targetCell);
                        query.SetResult(targetCell, distance, navType);
                        return false; // 找到目标，直接返回，不继续遍历
                    }
                }

                // 目标不可传送时，返回自身格子（告知AI“无有效传送目标”）
                query.SetResult(rootCell, 0, __instance.CurrentNavType);
                return false;
            }

            #region 辅助方法：安全解析目标格子
            private static int GetQueryTargetCellSafe(PathFinderQuery query, Navigator navigator)
            {
                try
                {
                    int targetCell = -1;
                    //反射获取targetCell字段
                    string[] possibleFields = new[] { "targetCell", "goalCell", "destinationCell", "m_TargetCell" };
                    foreach (var fieldName in possibleFields)
                    {
                        object targetCellObj;
                        if (Utils.GetField(query, fieldName, out targetCellObj))
                        {
                            if (targetCellObj != null) return (int)targetCellObj;
                            else return -1;
                        }
                    }
                    //从Navigator的target解析
                    if (navigator.target != null)
                    {
                        targetCell = Grid.PosToCell(navigator.target);
                        if (!Grid.IsValidCell(targetCell))
                            return Grid.PosToCell(navigator);

                        foreach (var offset in navigator.targetOffsets ?? new CellOffset[] { new CellOffset(0, 0), new CellOffset(0, 1) })
                        {
                            int offsetCell = Grid.OffsetCell(targetCell, offset);
                            if (Grid.IsValidCell(offsetCell))
                                return offsetCell;
                        }
                        return targetCell;
                    }
                    // 最终兜底：返回当前格子
                    return Grid.PosToCell(navigator);
                }
                catch (Exception e)
                {
                    LogUtils.LogDebug("TeleportSuit",$"Analy Cell Failed：{e.Message}\n{e.StackTrace}");
                    return Grid.PosToCell(navigator);
                }
            }
            #endregion
        }


        //穿上传送服之后禁用寻路并传送小人
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.AdvancePath))]
        public static class PathFinder_UpdatePath_Patch
        {
            private static readonly string ModuleName = "AdvancePathPatch";
            public static bool Prefix(Navigator __instance, ref NavTactic ___tactic, ref int ___reservedCell)
            {
                if (TeleNavigator.isTeleMiniom(__instance) && Grid.PosToCell(__instance) != ___reservedCell)
                {
                    int target_position_cell = Grid.PosToCell(__instance.target);
                    int targetWorldId = Grid.WorldIdx[target_position_cell];
                    int mycell = Grid.PosToCell(__instance);

                    if ((!Grid.IsValidCell(mycell)) || (!Grid.IsValidCell(target_position_cell)))
                    {
                        __instance.Stop();
                        return true;
                    }

                    //===关键逻辑：Blockers
                    if (TeleportBlockerManager.Instance != null && TeleportBlockerManager.Instance.IsTeleportBlocked(__instance, targetWorldId))
                    {
                        return true;
                    }
                    bool needTeleport = true;

                    for (int i = 0; i < __instance.targetOffsets.Length; i++)
                    {
                        int cell = Grid.OffsetCell(target_position_cell, __instance.targetOffsets[i]);
                        if (__instance.CanReach(cell) && mycell == cell)
                        {
                            needTeleport = false;
                        }
                    }
                    if (!needTeleport)
                    {
                        __instance.Stop(arrived_at_destination: true, false);
                        return false;
                    }

                    //计算目标格子
                    int cellPreferences = ___tactic.GetCellPreferences(target_position_cell, __instance.targetOffsets, __instance);
                    //释放原预留格子，占用新目标格子（避免和其他小人冲突）
                    NavigationReservations.Instance.RemoveOccupancy(___reservedCell);
                    ___reservedCell = cellPreferences;
                    NavigationReservations.Instance.AddOccupancy(cellPreferences);
                    if (___reservedCell != NavigationReservations.InvalidReservation)
                    {
                        //传送服消耗计算
                        Equipment equipment = __instance.GetComponent<MinionIdentity>().GetEquipment();
                        Assignable assignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
                        if (assignable != null)
                        {
                            TeleportSuitTank tank = assignable.GetComponent<TeleportSuitTank>();
                            if (tank != null && tank.batteryCharge > 0)
                            {
                                tank.batteryCharge -= 1f / TeleportSuitOptions.Instance.teleportTimesFullCharge;
                            }
                        }
                        // 结束当前的移动过渡状态（避免传送时状态卡死）
                        __instance.transitionDriver.EndTransition();
                        // 强制切换到“正常移动”状态（确保传送后状态正常）
                        __instance.smi.GoTo(__instance.smi.sm.normal.moving);
                        // 初始化过渡动画（避免空引用）
                        Navigator.ActiveTransition transition = TRANSITION.Get(__instance.transitionDriver);
                        transition = new Navigator.ActiveTransition();

                        int reservedCell = ___reservedCell;
                        KBatchedAnimController minion_anim = __instance.GetComponent<KBatchedAnimController>();
                        Action<object> action = null;

                        //「强制修改小人坐标」+「重置导航状态」
                        action = delegate (object data)
                        {
                            if (minion_anim != null) minion_anim.PlaySpeedMultiplier = 1f;
                            
                            if (__instance == null) return;
                            
                            // 移除传送动画覆盖
                            __instance.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                            // ========== 核心：瞬移到目标格子 ==========
                            // 计算目标格子的世界坐标（Bottom对齐，场景层25）
                            Vector3 position = Grid.CellToPos(reservedCell, CellAlignment.Bottom, (Grid.SceneLayer)25);
                            // 强制修改小人的世界坐标 → 实现“瞬移（传送）”
                            __instance.transform.SetPosition(position);
                            // ========== 重置导航状态（适配目标格子） ==========
                            TeleNavigator.resetNavType(__instance,reservedCell);
                            
                            // 标记“到达目标”，停止寻路 → 传送完成
                            __instance.Stop(arrived_at_destination: true, false);
                            // 取消动画回调订阅（避免内存泄漏）
                            __instance.Unsubscribe((int)GameHashes.AnimQueueComplete, action);
                            __instance.Trigger(1347184327);

                        };
                        
                        float PlaySpeedMultiplier = TeleportSuitOptions.Instance.teleportSpeedMultiplier;
                        if (PlaySpeedMultiplier != 0)
                        {
                            minion_anim.AddAnimOverrides(TeleportSuitConfig.InteractAnim, 1f);

                            minion_anim.SetLayer(0);
                            minion_anim.SetElapsedTime(0f);
                            minion_anim.Play("working_pst", KAnim.PlayMode.Once, PlaySpeedMultiplier, 0);

                            // 动画播放完成后执行瞬移逻辑
                            __instance.Subscribe((int)GameHashes.AnimQueueComplete, action);

                        }
                        else
                        {
                            action(null);
                        }
                    }
                    else
                    {
                        __instance.Stop();
                    }
                    return false;
                }
                return true;
            }
        }
        //虚空强者
        [HarmonyPatch(typeof(FallMonitor.Instance),nameof(FallMonitor.Instance.UpdateFalling))]
        public static class FallMonitor_updateFalling_Pathes
        {
            private static readonly string ModuleName = "FallMonitor_updateFalling_Pathes";
            public static bool Prefix(FallMonitor.Instance __instance)
            {
                if (__instance == null) return true;

                FieldInfo navigatorField = AccessTools.Field(typeof(FallMonitor.Instance), "navigator");
                if (navigatorField != null)
                {
                    Navigator navigator = (Navigator)navigatorField.GetValue(__instance);
                    if (navigator != null && navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags)) {
                        return false;
                    }
                }
                return true;
            }
        }
        [HarmonyPatch]
        public class HelmentController_OnPathAdvanced_Patch
        {
            static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(HelmetController),
                    "OnPathAdvanced",
                    new Type[] {typeof(object)});
            }
            [HarmonyPrefix]
            public static void Prefix(HelmetController __instance,object data)
            {
                if (__instance == null) return;
                if (__instance.gameObject.HasTag(TeleportSuitConfig.ID))
                {
                    object obj;
                    Utils.InvokeMethod(__instance, "UpdateJets",out obj,null);
                }
            }
        }
        [HarmonyPatch]
        public class HelmentController_Patch
        {
            private static KBatchedAnimController tele_anim;
            static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(HelmetController),
                    "UpdateJets");
            }
            [HarmonyPostfix]
            public static void Postfix(HelmetController __instance)
            {
                if (__instance == null) return;
                Navigator navigator;
                object nav_obj = null;
                GameObject miniom = null;
                if (Utils.GetField(__instance, "owner_navigator", out nav_obj) && nav_obj != null)
                {
                    navigator = nav_obj as Navigator;
                    
                }
                else{
                    miniom = Utils.GetAssigneeGameObject(__instance.GetComponent<Equippable>()?.assignee);
                    navigator = miniom.GetComponent<Navigator>();
                    if (navigator == null) return;
                }
                if (TeleNavigator.isTeleMiniom(navigator) || __instance.gameObject.HasTag(TeleportSuitConfig.ID))
                {
                    int num = -1;
                    if (navigator != null)
                        num = Grid.CellBelow(Grid.PosToCell(navigator));
                    else if (miniom != null)
                        num = Grid.CellBelow(Grid.PosToCell(miniom.transform.position));
                    else return;
                    if (Grid.IsWorldValidCell(num))
                    {
                        bool flag = Grid.Solid[num] || Grid.FakeFloor[num] || Grid.IsSubstantialLiquid(num);
                        if (!flag)
                        {
                            if (tele_anim != null) return;
                            object anim_obj;
                            Utils.InvokeMethod(__instance, "AddTrackedAnim", out anim_obj, new object[] {
                                "teleSuit", Assets.GetAnim("tele_stand_kanim"), "loop", Grid.SceneLayer.Creatures, "foot", true
                                });
                            if (anim_obj != null) tele_anim = anim_obj as KBatchedAnimController;
                        }
                        else
                        {
                            if (tele_anim != null)
                            {
                                UnityEngine.Object.Destroy(tele_anim.gameObject);
                                tele_anim = null;
                            }
                        }
                    }
                }
            }
        }


        public static void PeterHan_FastTrack_SensorPatches_IsReachable_Postfix_single(int cell, ref bool __result)
        {
            if (__result == false)
            {
                __result = CanBeReachByMinionGroup(cell);
            }
        }
        public static void PeterHan_FastTrack_SensorPatches_IsReachable_Postfix_multiple(int cell, CellOffset[] offsets, ref bool __result)
        {
            if (__result == false)
            {
                __result = CanBeReachByMinionGroup(cell);
            }
            else
            {
                return;
            }
            int n = offsets.Length;
            for (int i = 0; i < n; i++)
            {
                if (__result == false)
                {
                    int offs = Grid.OffsetCell(cell, offsets[i]);
                    if (Grid.IsValidCell(offs))
                    {
                        __result = CanBeReachByMinionGroup(offs);

                    }
                }
                else
                {
                    break;
                }
            }
        }
        public static bool CanBeReachByMinionGroup(int cell)
        {
            if (!Grid.IsValidCell(cell))
            {
                return false;
            }
            //是否找到对应cell的世界ID
            if (ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]) != null
                && TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(
                ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId, out int value)
                && value > 0)
            {
                return TeleportSuitConfig.CanTeloportTo(cell);
            }
            else
            {
                return false;
            }
        }
        [HarmonyPatch(typeof(MoveToLocationTool), nameof(MoveToLocationTool.CanMoveTo), new Type[] { typeof(int) })]
        public class MoveToLocationTool_CanMoveTo_patch
        {
            public static bool Prefix(MoveToLocationTool __instance, int target_cell, ref bool __result)
            {
                //Depes or Bonic 判断
                FieldInfo targetNavigatorField = AccessTools.Field(typeof(MoveToLocationTool), "targetNavigator");
                if (targetNavigatorField != null)
                {
                    Navigator targetNavigator = (Navigator)targetNavigatorField.GetValue(__instance);
                    if (TeleportCore.IsClusterTeleportEnabled(targetNavigator))
                    {
                        if (targetNavigator != null && ((targetNavigator.flags & TeleportSuitConfig.TeleportSuitFlags) != 0))
                        {
                            __result = CanBeReachByMinionGroup(target_cell);
                            __result = true;
                            return false;
                        }
                    }
                }
                //如果是物体移动，那就走原逻辑
                return true;
            }
        }

        [HarmonyPatch]
        public static class MoveToLocationTool_SetMoveToLocation_Patch
        {
            static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(MoveToLocationTool),
                    "SetMoveToLocation",
                    new[] { typeof(int) }
                );
            }

            [HarmonyPrefix]
            public static bool Prefix(MoveToLocationTool __instance, int target_cell)
            {
                // 1. 空值防护：提前校验关键对象
                if (__instance == null) return true;

                // 2. 获取targetNavigator（保留你的逻辑+空值防护）
                FieldInfo targetNavigatorField = AccessTools.Field(typeof(MoveToLocationTool), "targetNavigator");
                if (targetNavigatorField == null) return true;

                Navigator targetNavigator = (Navigator)targetNavigatorField.GetValue(__instance);
                if (targetNavigator == null || targetNavigator.gameObject == null) return true;

                // 3. 仅处理穿传送服且启用集群传送的小人（保留你的逻辑）
                if (!TeleportCore.IsClusterTeleportEnabled(targetNavigator)
                    || (targetNavigator.flags & TeleportSuitConfig.TeleportSuitFlags) == 0)
                {
                    return true;
                }

                ChoreProvider choreProvider = null;
                try
                {
                    // ========== 1. 安全获取ChoreProvider（避免空引用） ==========
                    choreProvider = targetNavigator.GetComponent<ChoreProvider>();
                    if (choreProvider != null)
                    {
                        // 清空当前小人的任务（仅自身，不影响其他）
                        ClearMinionSelfChores(choreProvider);
                    }

                    // ========== 2. 构建传送任务数据（空值防护） ==========
                    TeleportData teleportData = new TeleportData
                    {
                        navigator = targetNavigator,
                        targetCell = target_cell
                    };

                    // 跨世界传送判断（保留你的逻辑+空值防护）
                    if (TeleportCore.IsClusterWorldTargetValid(target_cell, out WorldContainer targetWorld, out Vector3 targetWorldPos))
                    {
                        if (targetWorld != null && targetWorldPos != Vector3.zero)
                        {
                            teleportData.targetWorld = targetWorld;
                            teleportData.targetPos = targetWorldPos;
                        }
                    }

                    // ========== 3. 安全创建并启动传送任务（核心修复：解决Context空引用） ==========
                    IStateMachineTarget master = targetNavigator.GetComponent<IStateMachineTarget>();
                    if (master != null && choreProvider != null)
                    {
                        TeleportChore teleportChore = new TeleportChore(master, teleportData);

                        // 修复1：ChoreConsumerState不能传null，用默认值/空实例
                        ChoreConsumer consumer = targetNavigator.GetComponent<ChoreConsumer>();
                        ChoreConsumerState defaultConsumerState = new ChoreConsumerState(consumer); // 传当前小人的ChoreConsumer
                                                                                                    // 修复2：Context构造函数参数补全，避免空引用
                        Chore.Precondition.Context choreContext = new Chore.Precondition.Context(
                            teleportChore,
                            defaultConsumerState, // 替换null，使用默认状态
                            false,
                            null
                        );

                        // 先添加任务到队列，再启动
                        choreProvider.AddChore(teleportChore);
                        teleportChore.Begin(choreContext);

                        // 同世界传送：更新预留格子（保留你的逻辑+空值防护）
                        if (teleportData.targetWorld == null)
                        {
                            Traverse navTraverse = Traverse.Create(targetNavigator);
                            int reservedCell = navTraverse.Field("reservedCell").GetValue<int>();
                            if (TeleportCore.ExecuteTeleportForce(targetNavigator, target_cell, ref reservedCell))
                            {
                                navTraverse.Field("reservedCell").SetValue(reservedCell);
                            }
                        }

                        return false;
                    }
                }
                catch (NullReferenceException nullEx)
                {
                    // 精准捕获空引用异常，定位问题
                    Debug.LogWarning($"[TelePortSuit] MoveTo 空引用错误：{nullEx.Message}\n涉及对象：ChoreProvider={(choreProvider == null ? "null" : "存在")}，Navigator={(targetNavigator == null ? "null" : targetNavigator.name)}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TelePortSuit] MoveTo 执行错误：{e.Message}\n{e.StackTrace}");
                }

                // 任何异常/失败，均走原生逻辑兜底
                return true;
            }

            /// <summary>
            /// 仅清空当前小人自身的所有任务（封装为独立方法，便于维护）
            /// </summary>
            private static void ClearMinionSelfChores(ChoreProvider choreProvider)
            {
                if (choreProvider == null) return;

                // 1. 清空待执行任务队列（chores字段）
                FieldInfo choresField = AccessTools.Field(typeof(ChoreProvider), "chores");
                if (choresField != null)
                {
                    List<Chore> selfChores = choresField.GetValue(choreProvider) as List<Chore>;
                    if (selfChores != null)
                    {
                        for (int i = selfChores.Count - 1; i >= 0; i--)
                        {
                            Chore chore = selfChores[i];
                            if (chore != null && !chore.isNull)
                            {
                                chore.Cancel("TeleportPreempt");
                                choreProvider.RemoveChore(chore);
                            }
                        }
                        selfChores.Clear();
                    }
                }

                // 2. 终止当前执行的主动任务（activeChore字段）
                FieldInfo activeChoreField = AccessTools.Field(typeof(ChoreProvider), "activeChore");
                if (activeChoreField != null)
                {
                    Chore activeChore = activeChoreField.GetValue(choreProvider) as Chore;
                    if (activeChore != null && !activeChore.isNull)
                    {
                        activeChore.Cancel("TeleportPreempt");
                        activeChoreField.SetValue(choreProvider, null);
                    }
                }
            }
        }
    }


}
