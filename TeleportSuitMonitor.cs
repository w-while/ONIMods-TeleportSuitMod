using System.Collections.Generic;
using Klei.AI;
using STRINGS;
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
            wearingSuit.DefaultState(wearingSuit.hasBattery);
            wearingSuit.hasBattery.Update(CoolSuit).TagTransition(GameTags.SuitBatteryOut, wearingSuit.noBattery);
            wearingSuit.noBattery.Enter(delegate (Instance smi)
            {
                Attributes attributes2 = smi.sm.owner.Get(smi).GetAttributes();
                if (attributes2 != null)
                {
                    foreach (AttributeModifier noBatteryModifier in smi.noBatteryModifiers)
                    {
                        attributes2.Add(noBatteryModifier);
                    }
                }
            }).Exit(delegate (Instance smi)
            {
                Attributes attributes = smi.sm.owner.Get(smi).GetAttributes();
                if (attributes != null)
                {
                    foreach (AttributeModifier noBatteryModifier2 in smi.noBatteryModifiers)
                    {
                        attributes.Remove(noBatteryModifier2);
                    }
                }
            }).TagTransition(GameTags.SuitBatteryOut, wearingSuit.hasBattery, on_remove: true);
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
