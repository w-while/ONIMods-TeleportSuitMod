using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static StateMachine;

namespace TeleportSuitMod
{


    // 跨世界传送核心配置
    public static class ClusterTeleportConfig
    {
        // 跨世界传送开关（仅穿传送服时生效）
        public static bool IsClusterTeleportEnabled(Navigator navigator)
        {
            if (navigator == null) return false;
            if (!TeleportSuitOptions.Instance.clusterTeleportByMoveTo) return false;
            // 判断是否穿戴传送服
            return navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags);
        }

        // 验证跨世界目标合法性（核心：替代原生寻路校验）
        public static bool IsClusterWorldTargetValid(int targetCell, out WorldContainer targetWorld, out Vector3 targetWorldPos)
        {
            targetWorld = null;
            targetWorldPos = Vector3.zero;

            // 1. 基础校验：目标格子有效
            if (!Grid.IsValidCell(targetCell)) return false;

            // 2. 获取目标格子所属世界
            byte targetWorldIdx = Grid.WorldIdx[targetCell];
            if (targetWorldIdx == byte.MaxValue) return false;

            // 3. 通过ClusterManager获取目标世界容器//|| !targetWorld.GetStatus()
            targetWorld = ClusterManager.Instance?.GetWorld(targetWorldIdx);
            if (targetWorld == null ) return false;

            // 4. 验证目标格子在目标世界内的合法性（非固体、可站立）
            //Grid targetGrid = targetWorld.GetComponent<Grid>();
            //if (targetGrid == null || targetGrid.IsSolidCell(targetCell)) return false;

            // 5. 计算目标世界内的世界坐标（关键：跨世界坐标转换）
            targetWorldPos = Grid.CellToPos(targetCell, CellAlignment.Bottom, Grid.SceneLayer.Move);
            return true;
        }

        // 执行跨世界瞬移（核心逻辑）
        public static void ExecuteCrossWorldTeleport(Navigator navigator, Vector3 targetWorldPos, WorldContainer targetWorld)
        {
            if (navigator == null || targetWorld == null) return;

            // ========== 1：消耗传送服能量 ==========
            ConsumeTeleportSuitEnergy(navigator);

            // ========== 2：强制终止原生寻路/过渡 ==========
            navigator.Stop(); // 停止当前所有寻路
            if (navigator.transitionDriver != null)
                navigator.transitionDriver.EndTransition(); // 结束过渡状态

            // ========== 3：跨世界瞬移核心 ==========
            // 1. 切换小人的所属世界（关键：避免坐标异常）
            //var minionIdentity = navigator.GetComponent<MinionIdentity>();
            //if (minionIdentity != null)
            //{
            //    minionIdentity.worldContainer = targetWorld;
            //}

            // 2. 强制修改小人坐标到目标世界的目标位置
            navigator.transform.SetPosition(targetWorldPos);

            // ========== 4：重置导航状态（避免卡死） ==========
            // 重置当前导航类型（适配目标格子）
            int newCell = Grid.PosToCell(navigator.transform.position);
            if (Grid.HasLadder[newCell])
                navigator.CurrentNavType = NavType.Ladder;
            else if (Grid.HasPole[newCell])
                navigator.CurrentNavType = NavType.Pole;
            else if (GameNavGrids.FloorValidator.IsWalkableCell(newCell, Grid.CellBelow(newCell), true))
                navigator.CurrentNavType = NavType.Floor;

            // 重置状态机到正常移动状态
            navigator.smi.GoTo(navigator.smi.sm.normal.moving);

            // ========== 5：播放传送动画（视觉效果） ==========
            PlayTeleportAnim(navigator);

            // ========== 6：标记到达目标（关闭MoveTo面板） ==========
            navigator.Stop(arrived_at_destination: true, false);
        }

        #region 辅助方法
        // 消耗传送服能量
        private static void ConsumeTeleportSuitEnergy(Navigator navigator)
        {
            var equipment = navigator.GetComponent<MinionIdentity>()?.GetEquipment();
            if (equipment == null) return;

            var suitAssignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
            if (suitAssignable == null) return;

            var teleportTank = suitAssignable.GetComponent<TeleportSuitTank>();
            if (teleportTank != null && teleportTank.batteryCharge > 0)
            {
                teleportTank.batteryCharge -= 1f / TeleportSuitOptions.Instance.teleportTimesFullCharge;
            }
        }

        // 播放传送动画
        private static void PlayTeleportAnim(Navigator navigator)
        {
            var animController = navigator.GetComponent<KBatchedAnimController>();
            if (animController == null) return;

            // 播放传送动画（替换为你的动画名）
            animController.AddAnimOverrides(TeleportSuitConfig.InteractAnim, 1f);
            animController.Play("teleport_pre");
            animController.Queue("working_loop");
            animController.Queue("working_pst");

            // 动画结束后重置动画
            Action<object> onAnimComplete = null;
            onAnimComplete = (data) =>
            {
                animController.PlaySpeedMultiplier = 1f;
                animController.RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                navigator.Unsubscribe((int)GameHashes.AnimQueueComplete, onAnimComplete);
            };
            navigator.Subscribe((int)GameHashes.AnimQueueComplete, onAnimComplete);
        }
        #endregion
    }
}
