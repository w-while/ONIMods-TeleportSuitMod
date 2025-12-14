using HarmonyLib;
using Klei.AI;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace TeleportSuitMod
{
    /// <summary>
    /// 传送核心工具类（整合同世界+跨世界传送）
    /// </summary>
    public static class TeleportCore
    {
        // 近距离阈值（仅非强制传送场景使用）
        public static int ShortRangeThreshold = 5;

        #region 1. 通用传送入口（自动判断跨世界/同世界）
        /// <summary>
        /// 通用传送执行方法
        /// </summary>
        /// <param name="navigator">小人导航组件</param>
        /// <param name="targetCell">目标格子</param>
        /// <param name="reservedCell">预留格子（引用传递）</param>
        /// <param name="forceTeleport">是否强制传送（忽略距离）</param>
        /// <returns>是否传送成功</returns>
        public static bool ExecuteTeleport(Navigator navigator, int targetCell, ref int reservedCell, bool forceTeleport = true)
        {
            // 基础校验
            if (navigator == null || !Grid.IsValidCell(targetCell) || !navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags))
                return false;

            // ========== 核心修复：先判断是否真的跨世界 ==========
            // 1. 获取小人当前所属世界索引
            byte currentWorldIdx = (byte)navigator.GetMyWorldId();
            // 2. 获取目标格子所属世界索引
            byte targetWorldIdx = Grid.WorldIdx[targetCell];
            // 3. 仅当「跨世界开关开启 + 目标世界≠当前世界」时，才校验跨世界传送
            bool isCrossWorld = IsClusterTeleportEnabled(navigator)
                                && currentWorldIdx != targetWorldIdx
                                && currentWorldIdx != byte.MaxValue
                                && targetWorldIdx != byte.MaxValue;

            // 优先执行跨世界传送（仅真·跨世界时才触发）
            if (isCrossWorld && IsClusterWorldTargetValid(targetCell, out WorldContainer targetWorld, out Vector3 targetWorldPos))
            {
                Debug.Log("[TeleportSuit] Run ExecuteCrossWorldTeleport (Cross-World)");
                ExecuteCrossWorldTeleport(navigator, targetWorldPos, targetWorld);
                return true;
            }

            // 同世界传送：强制传送/距离达标则执行（本地寻路不再走跨世界逻辑）
            return forceTeleport
                ? ExecuteTeleportForce(navigator, targetCell, ref reservedCell)
                : ExecuteTeleportWithDistanceCheck(navigator, targetCell, ref reservedCell);
        }
        #endregion

        #region 2. 同世界传送逻辑
        /// <summary>
        /// 带距离判断的同世界传送
        /// </summary>
        private static bool ExecuteTeleportWithDistanceCheck(Navigator navigator, int targetCell, ref int reservedCell)
        {
            int currentCell = Grid.PosToCell(navigator);
            // 近距离 → 不传送
            if (GetManhattanDistance(currentCell, targetCell) <= ShortRangeThreshold)
                return false;

            return ExecuteTeleportForce(navigator, targetCell, ref reservedCell);
        }

        /// <summary>
        /// 强制同世界传送（忽略距离）
        /// </summary>
        public static bool ExecuteTeleportForce(Navigator navigator, int targetCell, ref int reservedCell)
        {
            //// 先校验传送权限
            //if (!RocketCabinRestriction.CheckTeleportPermission(navigator, targetCell))
            //{
            //    Debug.LogWarning($"[TeleprotSuit] Recalled Passengers MUST Stay inner ：{navigator.name}");
            //    return false;
            //}
            // 基础校验
            if (navigator == null || !Grid.IsValidCell(targetCell) || !navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags))
                return false;

            // 校验
            int mycell = Grid.PosToCell(navigator);
            if ((!Grid.IsValidCell(mycell)) || (!Grid.IsValidCell(targetCell)))
            {
                navigator.Stop();
                return false;
            }

            // 已到达目标 → 无需传送
            if (IsAlreadyAtTarget(navigator, targetCell))
            {
                navigator.Stop(arrived_at_destination: true, false);
                return false;
            }

            // 1. 预留目标格子（避免小人冲突）
            if (!ReserveTargetCell(navigator, targetCell, ref reservedCell))
                return false;

            // 2. 消耗能量（能量不足 → 传送失败）
            if (!ConsumeTeleportEnergy(navigator))
                return false;

            // 3. 重置导航状态（避免卡死）
            ResetNavigatorState(navigator);

            // 4. 播放动画+执行瞬移
            PlayTeleportAnimAndTeleport(navigator, targetCell, reservedCell);

            return true;
        }

        /// <summary>
        /// 最终执行同世界瞬移（修改坐标+重置状态）
        /// </summary>
        private static void DoTeleport(Navigator navigator, int targetCell, Action<object> animCallback)
        {
            if (navigator == null || !Grid.IsValidCell(targetCell))
            {
                navigator?.Stop();
                return;
            }

            // 重置动画状态
            var animController = navigator.GetComponent<KBatchedAnimController>();
            if (animController != null)
            {
                animController.RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                animController.PlaySpeedMultiplier = 1f;
            }

            // 核心：修改小人坐标到目标格子
            Vector3 targetPos = Grid.CellToPos(targetCell, CellAlignment.Bottom, (Grid.SceneLayer)25);
            navigator.transform.SetPosition(targetPos);

            // 适配目标格子的导航类型
            ResetNavType(navigator, targetCell);

            // 标记到达目标，停止寻路
            navigator.Stop(arrived_at_destination: true, false);

            // 解绑动画回调（避免内存泄漏）
            if (animCallback != null)
                navigator.Unsubscribe((int)GameHashes.AnimQueueComplete, animCallback);
        }
        #endregion

        #region 3. 跨世界传送逻辑
        /// <summary>
        /// 跨世界传送开关校验
        /// </summary>
        public static bool IsClusterTeleportEnabled(Navigator navigator)
        {
            if (navigator == null) return false;
            if (!TeleportSuitOptions.Instance.clusterTeleportByMoveTo) return false;
            return navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags);
        }

        /// <summary>
        /// 验证跨世界目标合法性
        /// </summary>
        public static bool IsClusterWorldTargetValid(int targetCell, out WorldContainer targetWorld, out Vector3 targetWorldPos)
        {
            targetWorld = null;
            targetWorldPos = Vector3.zero;

            // 1. 基础校验：目标格子有效
            if (!Grid.IsValidCell(targetCell)) return false;

            // 2. 获取目标世界索引
            byte targetWorldIdx = Grid.WorldIdx[targetCell];
            if (targetWorldIdx == byte.MaxValue) return false;

            // 3. 通过ClusterManager获取目标世界容器（强校验：世界必须存在且激活）
            targetWorld = ClusterManager.Instance?.GetWorld(targetWorldIdx);
            if (targetWorld == null || !targetWorld.isActiveAndEnabled) return false;

            // 4. 计算目标世界内的世界坐标（仅跨世界时生效）
            targetWorldPos = Grid.CellToPos(targetCell, CellAlignment.Bottom, Grid.SceneLayer.Move);

            if (targetWorld != null)
            {
                byte currentWorldIdx = (byte)ClusterManager.Instance.activeWorldId;
                Debug.Log($"[TeleportSuit] TargetCell={targetCell} | TargetWorld={targetWorld.GetMyWorldId()} | CurrentWorld={currentWorldIdx} | IsCrossWorld={currentWorldIdx != targetWorld.GetMyWorldId()}");
            }

            return true;
        }

        /// <summary>
        /// 执行跨世界瞬移
        /// </summary>
        public static void ExecuteCrossWorldTeleport(Navigator navigator, Vector3 targetWorldPos, WorldContainer targetWorld)
        {
            int targetCell = Grid.PosToCell(targetWorldPos);
            // 先校验传送权限
            //if (!RocketCabinRestriction.CheckTeleportPermission(navigator, targetCell, targetWorld))
            //{
            //    Debug.LogWarning($"[TeleprotSuit] Recalled Passengers MUST Stay inner ：{navigator.name}");
            //    return;
            //}
            if (navigator == null || targetWorld == null) return;


            // ========== 原有逻辑：消耗能量 + 终止寻路 ==========
            ConsumeTeleportEnergy(navigator);
            navigator.Stop();
            navigator.transitionDriver?.EndTransition();


            // ========== 坐标修改 + 状态重置 ==========
            navigator.transform.SetPosition(targetWorldPos);
            int newCell = Grid.PosToCell(navigator.transform.position);
            ResetNavType(navigator, newCell);

            PlayTeleportAnim(navigator);
            // 延迟标记完成，避免立即回滚
            GameScheduler.Instance.Schedule("CrossWorldTeleportComplete", 0.2f, (_) =>
            {
                navigator.Stop(arrived_at_destination: true, false);
            });
        }
        #endregion

        #region 4. 通用辅助方法
        /// <summary>
        /// 曼哈顿距离计算
        /// </summary>
        public static int GetManhattanDistance(int cellA, int cellB)
        {
            if (!Grid.IsValidCell(cellA) || !Grid.IsValidCell(cellB))
                return int.MaxValue;

            Vector2I posA = Grid.CellToXY(cellA);
            Vector2I posB = Grid.CellToXY(cellB);
            return Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.y - posB.y);
        }

        /// <summary>
        /// 检查是否已到达目标（含偏移格子）
        /// </summary>
        private static bool IsAlreadyAtTarget(Navigator navigator, int targetCell)
        {
            int currentCell = Grid.PosToCell(navigator);
            foreach (var offset in navigator.targetOffsets ?? new CellOffset[] { new CellOffset(0, 0) })
            {
                int offsetCell = Grid.OffsetCell(targetCell, offset);
                if (navigator.CanReach(offsetCell) && currentCell == offsetCell)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 预留目标格子（避免冲突）
        /// </summary>
        private static bool ReserveTargetCell(Navigator navigator, int targetCell, ref int reservedCell)
        {
            // 获取Navigator私有Tactic字段
            NavTactic tactic = Traverse.Create(navigator).Field("tactic").GetValue<NavTactic>();
            if (tactic == null) return false;

            // 获取目标偏好格子
            int cellPreferences = tactic.GetCellPreferences(targetCell, navigator.targetOffsets, navigator);

            // 释放原预留格子，占用新格子
            if (reservedCell != NavigationReservations.InvalidReservation)
                NavigationReservations.Instance.RemoveOccupancy(reservedCell);
            reservedCell = cellPreferences;
            NavigationReservations.Instance.AddOccupancy(reservedCell);

            return reservedCell != NavigationReservations.InvalidReservation;
        }

        /// <summary>
        /// 消耗传送服能量（同世界/跨世界通用）
        /// </summary>
        public static bool ConsumeTeleportEnergy(Navigator navigator)
        {
            var minionIdentity = navigator.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return false;

            var equipment = minionIdentity.GetEquipment();
            if (equipment == null) return false;

            var suitAssignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
            if (suitAssignable == null) return false;

            var teleportTank = suitAssignable.GetComponent<TeleportSuitTank>();
            if (teleportTank == null || teleportTank.batteryCharge <= 0)
                return false;

            // 扣除单次传送能量
            teleportTank.batteryCharge -= 1f / TeleportSuitOptions.Instance.teleportTimesFullCharge;
            return true;
        }

        /// <summary>
        /// 重置导航状态（避免卡死）
        /// </summary>
        private static void ResetNavigatorState(Navigator navigator)
        {
            navigator.transitionDriver?.EndTransition();
            if (navigator.smi != null && navigator.smi.sm?.normal?.moving != null)
                navigator.smi.GoTo(navigator.smi.sm.normal.moving);
        }

        /// <summary>
        /// 播放传送动画（通用）
        /// </summary>
        public static void PlayTeleportAnim(Navigator navigator)
        {
            float PlaySpeedMultiplier = TeleportSuitOptions.Instance.teleportSpeedMultiplier;
            var animController = navigator.GetComponent<KBatchedAnimController>();
            if (animController == null) return;

            // 播放传送动画
            animController.AddAnimOverrides(TeleportSuitConfig.InteractAnim, 1f);
            animController.PlaySpeedMultiplier = PlaySpeedMultiplier;
            //animController.Play("working_pre");
            animController.Queue("working_loop");
            animController.Queue("working_pst");

            // 动画结束后重置
            Action<object> onAnimComplete = null;
            onAnimComplete = (data) =>
            {
                animController.PlaySpeedMultiplier = 1f;
                animController.RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                navigator.Unsubscribe((int)GameHashes.AnimQueueComplete, onAnimComplete);
            };
            navigator.Subscribe((int)GameHashes.AnimQueueComplete, onAnimComplete);
        }

        /// <summary>
        /// 播放动画并执行瞬移（同世界）
        /// </summary>
        private static void PlayTeleportAnimAndTeleport(Navigator navigator, int targetCell, int reservedCell)
        {
            var animController = navigator.GetComponent<KBatchedAnimController>();
            float animSpeed = TeleportSuitOptions.Instance.teleportSpeedMultiplier;

            // 动画回调：执行瞬移
            Action<object> onAnimComplete = null;
            onAnimComplete = (data) => DoTeleport(navigator, reservedCell, onAnimComplete);

            // 有动画：播放后瞬移
            if (animController != null && animSpeed > 0)
            {
                PlayTeleportAnim(navigator);
                navigator.Subscribe((int)GameHashes.AnimQueueComplete, onAnimComplete);
            }
            // 无动画：直接瞬移
            else
            {
                DoTeleport(navigator, reservedCell, null);
            }
        }

        /// <summary>
        /// 重置导航类型（适配目标格子）
        /// </summary>
        private static void ResetNavType(Navigator navigator, int cell)
        {
            if (Grid.HasLadder[cell])
                navigator.CurrentNavType = NavType.Ladder;
            else if (Grid.HasPole[cell])
                navigator.CurrentNavType = NavType.Pole;
            else if (GameNavGrids.FloorValidator.IsWalkableCell(cell, Grid.CellBelow(cell), true))
                navigator.CurrentNavType = NavType.Floor;
            else if (Grid.HasTube[cell])
                navigator.CurrentNavType = NavType.Tube;
            else if (Grid.HasPole[cell])
                navigator.CurrentNavType = NavType.Pole;
            else if (Grid.IsSubstantialLiquid(cell))
                navigator.CurrentNavType = NavType.Swim;
        }

        /// <summary>
        /// 根据格子属性获取导航类型
        /// </summary>
        public static NavType GetNavTypeForCell(int cell)
        {
            if (!Grid.IsValidCell(cell))
                return NavType.NumNavTypes;

            if (Grid.HasLadder[cell]) return NavType.Ladder;
            if (Grid.HasPole[cell]) return NavType.Pole;
            if (GameNavGrids.FloorValidator.IsWalkableCell(cell, Grid.CellBelow(cell), true))
                return NavType.Floor;
            if (Grid.HasTube[cell]) return NavType.Tube;
            if (Grid.HasPole[cell]) return NavType.Pole;
            if (Grid.IsSubstantialLiquid(cell)) return NavType.Swim;

            return NavType.NumNavTypes;
        }
        #endregion
    }
}