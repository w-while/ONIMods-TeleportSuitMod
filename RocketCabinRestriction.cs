using UnityEngine;
using Klei.AI;
using System.Collections.Generic;

namespace TeleportSuitMod
{
    /// <summary>
    /// 太空员舱限制核心逻辑：判断小人在舱内 + 阻止跨世界传送
    /// </summary>
    public static class RocketCabinRestriction
    {
        // 功能总开关
        public static bool IsRestrictionEnabled = true;
        // 模块名称（用于日志标识）
        private const string ModuleName = "火箭舱限制";

        #region 核心接口：检查传送权限
        /// <summary>
        /// 检查小人是否允许传送（核心逻辑）
        /// </summary>
        /// <param name="navigator">小人导航组件</param>
        /// <param name="targetCell">目标格子</param>
        /// <param name="targetWorld">目标世界（可选）</param>
        /// <returns>true=允许传送，false=禁止传送</returns>
        public static bool CheckTeleportPermission(Navigator navigator, int targetCell, WorldContainer targetWorld = null)
        {
            // 1. 基础校验
            if (!IsRestrictionEnabled || navigator == null || !Grid.IsValidCell(targetCell))
            {
                LogUtils.LogDebug(ModuleName, $"基础条件不满足，允许传送 | 限制开关：{IsRestrictionEnabled}");
                return true;
            }

            // 2. 获取小人组件
            MinionIdentity minion = navigator.GetComponent<MinionIdentity>();
            if (minion == null)
            {
                LogUtils.LogDebug(ModuleName, "非小人单位，允许传送");
                return true;
            }

            // 3. 判断小人是否被分配到已召集的太空员舱
            PassengerRocketModule assignedModule = GetMinionAssignedPassengerModule(minion);
            if (assignedModule == null)
            {
                LogUtils.LogDebug(ModuleName, $"小人未分配到太空员舱 | 小人：{minion.GetProperName()}");
                return true;
            }
            if (assignedModule.PassengersRequested != PassengerRocketModule.RequestCrewState.Request)
            {
                LogUtils.LogDebug(ModuleName, $"舱未触发召集 | 小人：{minion.GetProperName()}");
                return true;
            }

            // 4. 判断小人当前是否在舱内世界
            int cabinWorldId = GetCabinWorldId(assignedModule);
            int minionWorldId = GetMinionCurrentWorldId(minion);
            if (minionWorldId != cabinWorldId)
            {
                LogUtils.LogDebug(ModuleName, $"小人不在舱内世界 | 小人：{minion.GetProperName()} | 小人世界：{minionWorldId} | 舱世界：{cabinWorldId}");
                return true;
            }

            // 5. 判断目标是否在舱内世界（阻止跨世界传送）
            int targetWorldId = targetWorld?.id ?? GetTargetWorldId(targetCell);
            bool isTargetInCabinWorld = (targetWorldId == cabinWorldId);

            if (!isTargetInCabinWorld)
            {
                LogUtils.LogWarning(ModuleName, $"禁止跨世界传送 | 小人：{minion.GetProperName()} | 目标世界：{targetWorldId} | 舱世界：{cabinWorldId}");
            }

            return isTargetInCabinWorld;
        }
        #endregion

        #region 内部工具方法
        /// <summary>
        /// 获取太空员舱对应的世界ID
        /// </summary>
        private static int GetCabinWorldId(PassengerRocketModule module)
        {
            ClustercraftExteriorDoor exteriorDoor = module.GetComponent<ClustercraftExteriorDoor>();
            if (exteriorDoor == null || !exteriorDoor.HasTargetWorld())
                return -1;
            return exteriorDoor.GetTargetWorld().id;
        }

        /// <summary>
        /// 获取小人当前所在的世界ID
        /// </summary>
        private static int GetMinionCurrentWorldId(MinionIdentity minion)
        {
            int minionCell = Grid.PosToCell(minion.gameObject);
            return Grid.IsValidCell(minionCell) ? Grid.WorldIdx[minionCell] : -1;
        }

        /// <summary>
        /// 获取目标格子对应的世界ID
        /// </summary>
        private static int GetTargetWorldId(int targetCell)
        {
            return Grid.IsValidCell(targetCell) ? Grid.WorldIdx[targetCell] : -1;
        }

        /// <summary>
        /// 获取小人分配的太空员舱（通过AssignmentGroup.HasMember匹配）
        /// </summary>
        private static PassengerRocketModule GetMinionAssignedPassengerModule(MinionIdentity minion)
        {
            if (minion == null)
                return null;

            MinionAssignablesProxy minionProxy = minion.assignableProxy.Get();
            if (minionProxy == null)
            {
                LogUtils.LogDebug(ModuleName, $"小人无Proxy | 小人：{minion.GetProperName()}");
                return null;
            }

            // 遍历所有激活的太空员舱
            PassengerRocketModule[] allModules = Object.FindObjectsOfType<PassengerRocketModule>(true);
            foreach (var module in allModules)
            {
                if (!module.isSpawned)
                    continue;

                AssignmentGroupController agc = module.GetComponent<AssignmentGroupController>();
                if (agc == null)
                    continue;

                // 通过HasMember判断小人是否属于该舱的分配组
                if (Game.Instance.assignmentManager.assignment_groups.TryGetValue(agc.AssignmentGroupID, out AssignmentGroup group)
                    && group.HasMember(minionProxy))
                {
                    LogUtils.LogDebug(ModuleName, $"匹配到小人所属舱 | 小人：{minion.GetProperName()} | 舱：{module.name}");
                    return module;
                }
            }

            LogUtils.LogDebug(ModuleName, $"未匹配到小人所属舱 | 小人：{minion.GetProperName()}");
            return null;
        }
        #endregion
    }
}