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

        //因为在Navigator_GetNavigationCost_Patch中获取世界可能会触发unity的gameobject获取报错
        static int maxQueryRange = 50;
        //顺序很重要
        static int[][] dr = new int[4][] { new int[] { 1, 1 }, new int[] { 1, -1 }, new int[] { -1, -1, }, new int[] { -1, 1 } };
        static Func<int, int>[] funs = new Func<int, int>[4] { Grid.CellBelow, Grid.CellLeft, Grid.CellAbove, Grid.CellRight };

        //修改穿着传送服的小人RunQuery的方式
        //具体为以小人为中心往外扩张查找
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.RunQuery))]
        public static class Navigator_RunQuery_Patch
        {
            public static bool Prefix(Navigator __instance, PathFinderQuery query)
            {
                if ((__instance.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)
                {
                    query.ClearResult();
                    int rootCell = Grid.PosToCell(__instance);
                    if (!Grid.IsValidCell(rootCell))
                        return false;
                    if (query.IsMatch(rootCell, rootCell, 0))
                    {
                        query.SetResult(rootCell, 0, __instance.CurrentNavType);
                        return false;
                    }
                    int worldIdx = __instance.GetMyWorldId();
                    for (int i = 0; i < maxQueryRange; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            int curCell = Grid.OffsetCell(rootCell, dr[j][0] * i, dr[j][1] * i);
                            for (int k = 0; k < i * 2; k++)
                            {
                                curCell = funs[j](curCell);
                                //判断是否在一个世界内
                                if (!Grid.IsValidCellInWorld(curCell, worldIdx)) break;
                                if (TeleportSuitConfig.CanTeloportTo(curCell) && query.IsMatch(curCell, rootCell, i))
                                {
                                    NavType navType = NavType.NumNavTypes;
                                    if (Grid.HasLadder[curCell])
                                    {
                                        navType = NavType.Ladder;
                                    }
                                    if (Grid.HasPole[curCell])
                                    {
                                        navType = NavType.Pole;
                                    }
                                    if (GameNavGrids.FloorValidator.IsWalkableCell(curCell, Grid.CellBelow(curCell), true))
                                    {
                                        navType = NavType.Floor;
                                    }
                                    query.SetResult(curCell, i, navType);
                                    return false;
                                }
                            }
                        }
                    }
                    return false;
                }
                return true;
            }
        }

        //穿上传送服之后禁用寻路并传送小人
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.AdvancePath))]
        public static class PathFinder_UpdatePath_Patch
        {
            public static bool Prefix(Navigator __instance, ref NavTactic ___tactic, ref int ___reservedCell)
            {
                if (__instance.target != null && __instance.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags) && Grid.PosToCell(__instance) != ___reservedCell)
                {
                    bool needTeleport = true;
                    int mycell = Grid.PosToCell(__instance);
                    int target_position_cell = Grid.PosToCell(__instance.target);
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
                            if (reactor_anim != null)
                            {
                                reactor_anim.PlaySpeedMultiplier = 1f;
                            }
                            if (__instance == null)
                            {
                                return;
                            }
                            // 移除传送动画覆盖
                            __instance.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                            // ========== 核心：瞬移到目标格子 ==========
                            // 计算目标格子的世界坐标（Bottom对齐，场景层25）
                            Vector3 position = Grid.CellToPos(reservedCell, CellAlignment.Bottom, (Grid.SceneLayer)25);
                            // 强制修改小人的世界坐标 → 实现“瞬移（传送）”
                            __instance.transform.SetPosition(position);
                            // ========== 重置导航状态（适配目标格子） ==========
                            // 若目标格子有梯子 → 切换为爬梯子状态
                            if (Grid.HasLadder[reservedCell]){
                                __instance.CurrentNavType = NavType.Ladder;
                            }
                            if (Grid.HasPole[reservedCell]){
                                __instance.CurrentNavType = NavType.Pole;
                            }
                            // 若为可走地面 → 切换为步行状态
                            if (GameNavGrids.FloorValidator.IsWalkableCell(reservedCell, Grid.CellBelow(reservedCell), true)){
                                __instance.CurrentNavType = NavType.Floor;
                            }
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

        //当小人检测到下落时直接传送到安全可达地点，可以不加，加的话用传送流畅
        [HarmonyPatch(typeof(FallMonitor.Instance), nameof(FallMonitor.Instance.Recover))]
        public static class FallMonitor_Instance_Recover
        {
            public static bool Prefix(Navigator ___navigator, bool ___flipRecoverEmote)
            {
                if ((___navigator.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)
                {
                    int cell = Grid.PosToCell(___navigator);
                    NavGrid.Transition[] transitions = ___navigator.NavGrid.transitions;
                    for (int i = 0; i < transitions.Length; i++)
                    {
                        NavGrid.Transition transition = transitions[i];
                        if (transition.isEscape && ___navigator.CurrentNavType == transition.start)
                        {
                            int num = transition.IsValid(cell, ___navigator.NavGrid.NavTable);
                            if (Grid.InvalidCell != num)
                            {
                                Vector2I vector2I = Grid.CellToXY(cell);
                                ___flipRecoverEmote = Grid.CellToXY(num).x < vector2I.x;
                                ___navigator.transform.SetPosition(Grid.CellToPosCBC(num, Grid.SceneLayer.Move));
                                ___navigator.CurrentNavType = transition.end;
                                FallMonitor.Instance sMI = ___navigator.GetSMI<FallMonitor.Instance>();
                                //sMI.UpdateFalling();
                                sMI.sm.isFalling.Set(false, sMI);
                                sMI.GoTo(sMI.sm.standing);
                                return false;
                            }
                        }
                    }

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
                        if(ClusterTeleportConfig.IsClusterTeleportEnabled(targetNavigator)){
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
        // 补丁代码
        //[HarmonyPatch]
        //public static class MoveToLocationTool_RefreshColor_Patch
        //{
        //    static MethodBase TargetMethod()
        //    {
        //        return AccessTools.Method(
        //            typeof(MoveToLocationTool),
        //            "RefreshColor"
        //            );
        //    }

        //    [HarmonyPrefix]
        //    public static bool Prefix(MoveToLocationTool __instance)
        //    {
        //        Console.WriteLine("[INFO] TeleportSuitMod  r:" + __instance.CanMoveTo(DebugHandler.GetMouseCell()));
        //        return true;
        //    }
        //}
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
            public static bool Prefix(MoveToLocationTool __instance,int target_cell)
            {
                FieldInfo targetNavigatorField = AccessTools.Field(typeof(MoveToLocationTool), "targetNavigator");
                if (targetNavigatorField != null)
                {
                    Navigator targetNavigator = (Navigator)targetNavigatorField.GetValue(__instance);
                    if (ClusterTeleportConfig.IsClusterTeleportEnabled(targetNavigator)){
                        if (targetNavigator != null && ((targetNavigator.flags & TeleportSuitConfig.TeleportSuitFlags) != 0))
                        {
                            ClusterTeleportConfig.IsClusterWorldTargetValid(target_cell, out WorldContainer targetWorld, out Vector3 targetWorldPos);
                            if (targetWorldPos != null && targetWorld != null)
                            {
                                ClusterTeleportConfig.ExecuteCrossWorldTeleport(targetNavigator, targetWorldPos, targetWorld);
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
        }
    }


}
