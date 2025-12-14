using Klei.AI;
using STRINGS;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleportSuitMonitor : GameStateMachine<TeleportSuitMonitor, TeleportSuitMonitor.Instance>
    {

        public class WearingSuit : State
        {
            public State hasBattery;

            public State noBattery;
        }

        public new class Instance : GameInstance
        {
            public Navigator navigator;

            public TeleportSuitTank teleport_suit_tank;

            public List<AttributeModifier> noBatteryModifiers = new List<AttributeModifier>();

            public Instance(IStateMachineTarget master, GameObject owner)
                : base(master)
            {
                base.sm.owner.Set(owner, base.smi);
                navigator = owner.GetComponent<Navigator>();
                teleport_suit_tank = master.GetComponent<TeleportSuitTank>();
                noBatteryModifiers.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.INSULATION, -TeleportSuitConfig.INSULATION, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.SUIT_OUT_OF_BATTERIES));
                noBatteryModifiers.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.THERMAL_CONDUCTIVITY_BARRIER, 0f - TeleportSuitConfig.THERMAL_CONDUCTIVITY_BARRIER, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.SUIT_OUT_OF_BATTERIES));
            }
        }

        public WearingSuit wearingSuit;

        public TargetParameter owner;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = wearingSuit;
            Target(owner);

            wearingSuit
                .DefaultState(wearingSuit.hasBattery)
                // 添加状态更新逻辑（替代重写UpdateStates）
                .Update("TeleportSuitUpdate", UpdateTeleportSuitState, UpdateRate.SIM_1000ms);

            wearingSuit.hasBattery
                .TagTransition(GameTags.SuitBatteryOut, wearingSuit.noBattery);

            wearingSuit.noBattery
                .Enter(OnNoBattery)
                .Exit(OnHasBattery)
                .TagTransition(GameTags.SuitBatteryOut, wearingSuit.hasBattery, on_remove: true);
        }
        // 新增：状态更新方法，处理舱内/舱外逻辑
        private void UpdateTeleportSuitState(Instance smi, float dt)
        {
            if (smi.teleport_suit_tank == null || smi.navigator == null) return;

            // 判断是否在舱内世界
            bool isInCabin = IsInCabinWorld(smi);

            // 根据舱内状态调整电池消耗
            float consumptionRate = isInCabin ? 0.5f : 1f; // 舱内消耗减半
            smi.teleport_suit_tank.batteryCharge -= consumptionRate / smi.teleport_suit_tank.batteryDuration * dt;

            // 电池耗尽逻辑
            if (smi.teleport_suit_tank.IsEmpty() && !smi.navigator.gameObject.HasTag(GameTags.SuitBatteryOut))
            {
                smi.navigator.gameObject.AddTag(GameTags.SuitBatteryOut);
            }
        }

        // 判断是否在舱内世界（复用CabinStayReactable的逻辑）
        private bool IsInCabinWorld(Instance smi)
        {
            int currentCell = Grid.PosToCell(smi.navigator.transform.position);
            if (!Grid.IsValidCell(currentCell)) return false;

            int worldId = Grid.WorldIdx[currentCell];
            WorldContainer world = ClusterManager.Instance.GetWorld(worldId);
            if (world == null) return false;

            // 优先使用IsModuleInterior属性判断
            var isModuleProp = typeof(WorldContainer).GetProperty("IsModuleInterior", BindingFlags.Public | BindingFlags.Instance);
            if (isModuleProp != null)
            {
                return (bool)isModuleProp.GetValue(world);
            }

            // 兜底判断
            return world.name.Contains("Rocket") || world.name.Contains("Module");
        }

        // 电池耗尽时的处理
        private void OnNoBattery(Instance smi)
        {
            var attributes = smi.sm.owner.Get(smi).GetAttributes();
            if (attributes == null) return;

            foreach (var modifier in smi.noBatteryModifiers)
            {
                attributes.Add(modifier);
            }
        }

        // 恢复电池时的处理
        private void OnHasBattery(Instance smi)
        {
            var attributes = smi.sm.owner.Get(smi).GetAttributes();
            if (attributes == null) return;

            foreach (var modifier in smi.noBatteryModifiers)
            {
                attributes.Remove(modifier);
            }
        }
        public static void CoolSuit(Instance smi, float dt)
        {
            //if (!smi.navigator)
            //{
            //    return;
            //}
            //GameObject gameObject = smi.sm.owner.Get(smi);
            //if (!gameObject)
            //{
            //    return;
            //}
            //ExternalTemperatureMonitor.Instance sMI = gameObject.GetSMI<ExternalTemperatureMonitor.Instance>();
            //if (sMI != null && sMI.AverageExternalTemperature >= smi.teleport_suit_tank.coolingOperationalTemperature)
            //{
            //    smi.teleport_suit_tank.batteryCharge -= 1f / smi.teleport_suit_tank.batteryDuration * dt;
            //    if (smi.teleport_suit_tank.IsEmpty())
            //    {
            //        gameObject.AddTag(GameTags.SuitBatteryOut);
            //    }
            //}
        }
    }
}
