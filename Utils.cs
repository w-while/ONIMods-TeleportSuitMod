using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    public class Utils
    {
        private static readonly string ModuleName = "Utils";

        public static int GetCellWorldId(int cell)
        {
            PropertyInfo worldIdxProp = typeof(Grid).GetProperty("WorldIdx", BindingFlags.Public | BindingFlags.Static);
            if (worldIdxProp != null)
            {
                int[] worldIdxArray = (int[])worldIdxProp.GetValue(null, null);
                if (cell >= 0 && cell < worldIdxArray.Length)
                {
                    return worldIdxArray[cell];
                }
            }
            return -1;
        }
        public static bool IsModuleInteriorWorld(WorldContainer world)
        {
            PropertyInfo isModuleProp = typeof(WorldContainer).GetProperty("IsModuleInterior", BindingFlags.Public | BindingFlags.Instance);
            if (isModuleProp != null)
            {
                return (bool)isModuleProp.GetValue(world);
            }

            return world.name.Contains("ModuleInterior") || world.name.Contains("RocketInterior") || world.name.Contains("Cabin");
        }

        // 从 Equipment 中获取穿戴的传送服（Equippable）
        // 核心：从 Klei Equipment 中获取传送服 Equippable（完全适配引擎）
        /// <summary>
        /// 从小人的 Equipment 组件中获取已穿戴的传送服 Equippable 实例
        /// </summary>
        /// <param name="equipment">小人的装备管理器（Equipment 实例）</param>
        /// <returns>传送服的 Equippable 实例（未找到返回 null）</returns>
        public static Equippable GetEquippedTeleportSuit(Equipment equipment)
        {
            if (equipment == null)
            {
                Debug.LogWarning("TeleportSuitTank: Equipment 实例为 null，无法获取传送服");
                return null;
            }

            foreach (AssignableSlotInstance slotInstance in equipment.Slots)
            {
                if (!slotInstance.IsAssigned()) continue;

                Equippable currentEquip = slotInstance.assignable as Equippable;
                if (currentEquip == null) continue;

                if (currentEquip.gameObject.HasTag(TeleportSuitGameTags.TeleportSuit))
                {
                    Debug.Log($"TeleportSuitTank: 找到已穿戴的传送服，插槽类型={slotInstance.slot.Name}");
                    return currentEquip;
                }
            }

            Debug.LogWarning("TeleportSuitTank: 未在小人的装备插槽中找到传送服");
            return null;
        }
        /// <summary>
        /// 从 Equipment 组件获取其归属的小人（MinionIdentity）
        /// 核心逻辑：Equipment 挂载在小人 GameObject 上，直接从父节点提取组件
        /// </summary>
        /// <param name="equipment">小人的装备管理器（Equipment 实例）</param>
        /// <returns>归属的 MinionIdentity（未找到返回 null）</returns>
        public static MinionIdentity GetMinionFromEquipment(Equipment equipment)
        {
            // 1. 空值防护
            if (equipment == null)
            {
                LogUtils.LogWarning(ModuleName,$"TeleportSuitTank: Equipment 实例为 null，无法获取小人");
                return null;
            }

            // 2. 方式1：直接从 Equipment 挂载的 GameObject 获取（最优，适配99%场景）
            // 游戏源码中：Equipment 组件直接挂载在小人的 GameObject 上
            MinionIdentity minion = equipment.gameObject.GetComponent<MinionIdentity>();
            if (minion != null)
            {
                LogUtils.LogWarning(ModuleName, $"从 Equipment 直接找到小人[{minion.GetProperName()}]");
                return minion;
            }

            // 3. 方式2：递归查找父节点（兼容特殊挂载场景，备用）
            Transform currentTransform = equipment.transform;
            while (currentTransform != null)
            {
                minion = currentTransform.GetComponent<MinionIdentity>();
                if (minion != null)
                {
                    LogUtils.LogError(ModuleName, $"递归找到小人[{minion.GetProperName()}]（父节点）");
                    return minion;
                }
                currentTransform = currentTransform.parent;
            }


            // 未找到的兜底提示
            LogUtils.LogWarning(ModuleName," 无法从 Equipment 中找到对应的 MinionIdentity");
            return null;
        }
        public static PassengerRocketModule GetPassengerModuleFromWorld(WorldContainer cabinWorld)
        {
            foreach (PassengerRocketModule module in GameObject.FindObjectsOfType<PassengerRocketModule>())
            {
                ClustercraftExteriorDoor door = module.GetComponent<ClustercraftExteriorDoor>();
                if (door == null) continue;

                WorldContainer doorTargetWorld = door.GetTargetWorld();
                if (doorTargetWorld != null && doorTargetWorld.id == cabinWorld.id)
                {
                    return module;
                }
            }
            return null;
        }

        // 保留之前的核心方法：从 Chore 获取真实小人
        public static MinionIdentity GetRealMinionFromChore(Chore chore)
        {
            if (chore == null || chore.driver == null)
            {
                LogUtils.LogWarning(ModuleName, "Chore 或 Chore.driver 为 null");
                return null;
            }

            // 从 Chore.driver（ChoreDriver）获取 MinionIdentity
            MinionIdentity minion = chore.driver.GetComponent<MinionIdentity>();
            if (minion != null)
            {
                return minion;
            }

            // 递归查找父节点
            Transform current = chore.driver.transform;
            int depth = 0;
            while (current != null && depth < 10)
            {
                minion = current.GetComponent<MinionIdentity>();
                if (minion != null)
                {
                    return minion;
                }
                current = current.parent;
                depth++;
            }

            return null;
        }

        // 保留之前的核心方法：修复无效 minion
        public static MinionIdentity FixInvalidMinion(MinionIdentity minion)
        {
            if (minion == null) return null;

            // 使用自定义方法检查路径是否有效
            string path = GetTransformFullPath(minion.transform);
            if (path != "[Transform is null]" && path != "[Path is empty]")
            {
                return minion;
            }

            LogUtils.LogWarning(ModuleName, "传入的 minion 是无效对象，尝试全局查找");

            // 全局查找同名小人
            foreach (MinionIdentity realMinion in Resources.FindObjectsOfTypeAll<MinionIdentity>())
            {
                if (realMinion != null && realMinion.GetProperName() == minion.GetProperName())
                {
                    LogUtils.LogDebug(ModuleName, $"全局查找找到同名小人：{realMinion.GetProperName()}");
                    return realMinion;
                }
            }

            return null;
        }

        /// <summary>
        /// 通用方法：获取 Transform 的完整路径（替代不存在的 GetFullPath()）
        /// </summary>
        /// <param name="transform">目标 Transform</param>
        /// <returns>完整路径（如 "World/Minions/卡米耶"）</returns>
        public static string GetTransformFullPath(Transform transform)
        {
            if (transform == null)
            {
                return "[Transform is null]";
            }

            string path = transform.name;
            Transform current = transform.parent;

            // 递归拼接父节点名称，生成完整路径
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            // 处理空名称/特殊字符
            return string.IsNullOrEmpty(path) ? "[Path is empty]" : path;
        }
        // 根据 ID 获取唯一小人,某些特殊情况下时候，比如：获取到了ID但是其他组件不全的情况
        public static MinionIdentity restoreMinionByInstanceID(int instanceId)
        {
            // 遍历所有活跃的MinionIdentity（过滤销毁/未激活对象）
            foreach (MinionIdentity m in UnityEngine.Object.FindObjectsOfType<MinionIdentity>())
            {
                // 关键校验：实例ID匹配 + GameObject有效 + 活跃状态
                if (m.GetInstanceID() == instanceId
                    && m.gameObject != null
                    && m.gameObject.activeInHierarchy)
                {
                    LogUtils.LogDebug(ModuleName, $"通过InstanceID[{instanceId}]找到有效小人：{m.name}");
                    return m;
                }
            }

            // 兜底：尝试从非活跃对象中查找（仅日志提示）
            foreach (MinionIdentity m in Resources.FindObjectsOfTypeAll<MinionIdentity>())
            {
                if (m.GetInstanceID() == instanceId)
                {
                    LogUtils.LogWarning(ModuleName, $"找到InstanceID[{instanceId}]的小人，但对象已失效：{m.name}");
                    return null; // 失效对象直接返回null，避免后续调用异常
                }
            }

            LogUtils.LogWarning(ModuleName, $"未找到InstanceID[{instanceId}]的小人");
            return null;
        }
        public static MinionIdentity GetMinionFromEquippable(Equippable eq)
        {
            if (eq?.assignee == null) return null;

            // 方式1：通过AssignableProxy获取
            if (eq.assignee is MinionAssignablesProxy proxy)
            {
                return proxy.GetTargetGameObject()?.GetComponent<MinionIdentity>();
            }

            return null;
        }
        /// <summary>
        /// 通过小人获取其穿戴的传送服组件
        /// </summary>
        /// <param name="minion">目标小人（MinionIdentity）</param>
        /// <returns>传送服组件（TeleportSuit），无则返回null</returns>
        public static TeleportSuitTank GetMinionTeleportSuit(MinionIdentity minion)
        {
            if (minion == null || minion.gameObject == null || !minion.gameObject.activeInHierarchy)
            {
                LogUtils.LogWarning(ModuleName, $"[{minion.name}] 小人GameObject无效/未激活");
                return null;
            }

            Equipment equipment = minion.GetEquipment();
            if (equipment == null) return null;

            Equippable equipped = GetEquippedTeleportSuit(equipment);
            if (equipped == null)
            {
                LogUtils.LogDebug(ModuleName, $"[{minion.name}] 未穿戴传送服");
                return null;
            }

            TeleportSuitTank suitTank = equipped.GetComponent<TeleportSuitTank>();
            if (suitTank == null)
            {
                LogUtils.LogWarning(ModuleName, $"[{minion.name}] 传送服装备上无TeleportSuitTank组件");
            }
            return suitTank;
        }

    }
}
