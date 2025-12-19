using HarmonyLib;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.GetNavigationCost))]
        [HarmonyPatch(new Type[] { typeof(int) })]//GetNavigationCost函数有重载，需要确定参数类型
        public static class Navigator_GetNavigationCost_Patch
        {
            public static bool Prefix(Navigator __instance, int cell, ref int __result)
            {
                if ((__instance.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)//穿着传送服
                {
                    __result = -1;
                    if (NavigatorWorldId.TryGetValue(__instance, out int id) && id != -1)
                    {
                        if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue
                            && ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]) != null
                                && ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId == id
                                && TeleportSuitConfig.CanTeloportTo(cell))
                        {
                            //int target_position_cell = Grid.PosToCell(__instance.target);
                            int targetWorldId = Grid.WorldIdx[cell];
                            int mycell = Grid.PosToCell(__instance);
                            //LogUtils.LogDebug("NaviP", $"TWID:{targetWorldId} T:{cell} MWID:{Grid.WorldIdx[mycell]} M:{mycell}");

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

        //取消穿着传送服的小人到各个格子的可达性更新（为了优化一点性能），并且记录小人的世界信息，
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.UpdateProbe), new Type[] { typeof(bool) })]
        public static class PathProber_UpdateProbe_Patch
        {
            public static bool Prefix(Navigator __instance, bool forceUpdate = false)
            {
                if (__instance == null) return true;

                int cell = Grid.PosToCell(__instance.gameObject.transform.position);
                if (Grid.IsValidCell(cell) && (__instance.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)
                {
                    if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue)
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

                    return false;
                }
                return true;
            }
        }



        //修改穿着传送服的小人RunQuery的方式
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.RunQuery))]
        public static class Navigator_RunQuery_Patch
        {
            // 彻底简化：只保留“最近的可传送格子”或“目标格子是否可传送”
            static int maxCheckDistance = 30; // 进一步缩小范围（传送不需要远距寻路）
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
                    // 方式1：反射获取targetCell字段
                    var targetCellField = query.GetType().GetField(
                        "targetCell",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    if (targetCellField != null)
                    {
                        int targetCell = (int)targetCellField.GetValue(query);
                        if (Grid.IsValidCell(targetCell))
                            return targetCell;
                    }

                    // 方式2：兼容其他字段名
                    string[] possibleFields = new[] { "goalCell", "destinationCell", "m_TargetCell" };
                    foreach (var fieldName in possibleFields)
                    {
                        var field = query.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            int targetCell = (int)field.GetValue(query);
                            if (Grid.IsValidCell(targetCell))
                                return targetCell;
                        }
                    }

                    // 方式3：从Navigator的target解析
                    if (navigator.target != null)
                    {
                        int rootTargetCell = Grid.PosToCell(navigator.target);
                        if (!Grid.IsValidCell(rootTargetCell))
                            return Grid.PosToCell(navigator);

                        foreach (var offset in navigator.targetOffsets ?? new CellOffset[] { new CellOffset(0, 0) })
                        {
                            int offsetCell = Grid.OffsetCell(rootTargetCell, offset);
                            if (Grid.IsValidCell(offsetCell))
                                return offsetCell;
                        }
                        return rootTargetCell;
                    }

                    // 最终兜底：返回当前格子
                    return Grid.PosToCell(navigator);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TeleportSuit] Analy Cell Failed：{e.Message}\n{e.StackTrace}");
                    return Grid.PosToCell(navigator);
                }
            }
            #endregion
        }


        //穿上传送服之后禁用寻路并传送小人
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.AdvancePath))]
        public static class PathFinder_UpdatePath_Patch
        {
            public static bool Prefix(Navigator __instance, ref NavTactic ___tactic, ref int ___reservedCell)
            {
                if (__instance.target != null && __instance.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags) && Grid.PosToCell(__instance) != ___reservedCell)
                {
                    int target_position_cell = Grid.PosToCell(__instance.target);
                    int targetWorldId = Grid.WorldIdx[target_position_cell];
                    int mycell = Grid.PosToCell(__instance);
                    //LogUtils.LogDebug("NaviP",$"TWID:{targetWorldId} T:{target_position_cell} MWID:{Grid.WorldIdx[mycell]} M:{mycell}" );

                    //===== 新增：太空舱拦截逻辑（最优先判断）=====
                    if (targetWorldId != Grid.WorldIdx[mycell] && __instance.TryGetComponent<MinionIdentity>(out var minion))
                    {
                        if (Grid.IsValidCell(target_position_cell))
                        {
                            // 太空舱拦截：阻断则直接返回，不执行后续传送逻辑
                            if (RocketCabinRestriction.QuickCheckBlockTeleport(minion, targetWorldId))
                            {
                                __instance.Stop();
                                return false;
                            }
                        }
                    }
                    bool needTeleport = true;
                    if ((!Grid.IsValidCell(mycell)) || (!Grid.IsValidCell(target_position_cell)))
                    {
                        __instance.Stop();
                        return true;
                    }
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
                        KBatchedAnimController reactor_anim = __instance.GetComponent<KBatchedAnimController>();
                        Action<object> action = null;

                        //「强制修改小人坐标」+「重置导航状态」
                        action = delegate (object data)
                        {
                            if (reactor_anim != null)reactor_anim.PlaySpeedMultiplier = 1f;
                            
                            if (__instance == null)return;
                            
                            // 移除传送动画覆盖
                            __instance.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                            // ========== 核心：瞬移到目标格子 ==========
                            // 计算目标格子的世界坐标（Bottom对齐，场景层25）
                            Vector3 position = Grid.CellToPos(reservedCell, CellAlignment.Bottom, (Grid.SceneLayer)25);
                            // 强制修改小人的世界坐标 → 实现“瞬移（传送）”
                            __instance.transform.SetPosition(position);
                            // ========== 重置导航状态（适配目标格子） ==========
                            // 若目标格子有梯子 → 切换为爬梯子状态
                            if (Grid.HasLadder[reservedCell])__instance.CurrentNavType = NavType.Ladder;
                            
                            if (Grid.HasPole[reservedCell]) __instance.CurrentNavType = NavType.Pole;
                            
                            // 若为可走地面 → 切换为步行状态
                            if (GameNavGrids.FloorValidator.IsWalkableCell(reservedCell, Grid.CellBelow(reservedCell), true))
                                __instance.CurrentNavType = NavType.Floor;
                            
                            // 标记“到达目标”，停止寻路 → 传送完成
                            __instance.Stop(arrived_at_destination: true, false);
                            // 取消动画回调订阅（避免内存泄漏）
                            __instance.Unsubscribe((int)GameHashes.AnimQueueComplete, action);

                        };

                        float PlaySpeedMultiplier = TeleportSuitOptions.Instance.teleportSpeedMultiplier;
                        if (PlaySpeedMultiplier != 0)
                        {
                            // 播放传送动画（覆盖默认动画）
                            reactor_anim.AddAnimOverrides(TeleportSuitConfig.InteractAnim, 1f);
                            reactor_anim.PlaySpeedMultiplier = PlaySpeedMultiplier;
                            //reactor_anim.Play("working_pre");
                            reactor_anim.Queue("working_loop");
                            reactor_anim.Queue("working_pst");
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
                        if(TeleportCore.IsClusterTeleportEnabled(targetNavigator)){
                            if (targetNavigator != null && ((targetNavigator.flags & TeleportSuitConfig.TeleportSuitFlags) != 0))
                            {
                                //__result = CanBeReachByMinionGroup(target_cell);
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
