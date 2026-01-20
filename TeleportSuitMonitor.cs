using HarmonyLib;
using Klei.AI;
using STRINGS;
using System;
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
            public KBatchedAnimController tele_anim;

            public Instance(IStateMachineTarget master, GameObject owner)
                : base(master)
            {
                base.sm.owner.Set(owner, base.smi);
                navigator = owner.GetComponent<Navigator>();
                teleport_suit_tank = master.GetComponent<TeleportSuitTank>();
                noBatteryModifiers.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.INSULATION, -TeleportSuitConfig.INSULATION, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.SUIT_OUT_OF_BATTERIES));
                noBatteryModifiers.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.THERMAL_CONDUCTIVITY_BARRIER, 0f - TeleportSuitConfig.THERMAL_CONDUCTIVITY_BARRIER, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.SUIT_OUT_OF_BATTERIES));
            }
            #region 虚空站立功能//虚空强者
            private GameObject GetAssigneeGameObject(IAssignableIdentity ass_id)
            {
                GameObject result = null;
                MinionAssignablesProxy minionAssignablesProxy = ass_id as MinionAssignablesProxy;
                if (minionAssignablesProxy)
                {
                    result = minionAssignablesProxy.GetTargetGameObject();
                }
                else
                {
                    MinionIdentity minionIdentity = ass_id as MinionIdentity;
                    if (minionIdentity)
                    {
                        result = minionIdentity.gameObject;
                    }
                }
                return result;
            }
            private KBatchedAnimController GetAssigneeController()
            {
                Equippable component = base.GetComponent<Equippable>();
                if (component.assignee != null)
                {
                    GameObject assigneeGameObject = this.GetAssigneeGameObject(component.assignee);
                    if (assigneeGameObject)
                    {
                        return assigneeGameObject.GetComponent<KBatchedAnimController>();
                    }
                }
                return null;
            }
            private KBatchedAnimController AddTrackedAnim(string name, KAnimFile tracked_anim_file, string anim_clip, Grid.SceneLayer layer, string symbol_name, bool require_looping_sound = false)
            {
                KBatchedAnimController assigneeController = this.GetAssigneeController();
                if (assigneeController == null)return null;

                string name2 = assigneeController.name + "." + name;
                GameObject gameObject = new GameObject(name2);
                gameObject.SetActive(false);
                gameObject.transform.parent = assigneeController.transform;
                gameObject.AddComponent<KPrefabID>().PrefabTag = new Tag(name2);
                KBatchedAnimController kbatchedAnimController = gameObject.AddComponent<KBatchedAnimController>();
                kbatchedAnimController.AnimFiles = new KAnimFile[]
                {
                    tracked_anim_file
                };
                kbatchedAnimController.initialAnim = anim_clip;
                kbatchedAnimController.isMovable = true;
                kbatchedAnimController.sceneLayer = layer;
                if (require_looping_sound)
                {
                    gameObject.AddComponent<LoopingSounds>();
                }
                gameObject.AddComponent<KBatchedAnimTracker>().symbol = symbol_name;
                bool flag;
                Vector3 position = assigneeController.GetSymbolTransform(symbol_name, out flag).GetColumn(3);
                position.z = Grid.GetLayerZ(layer);
                gameObject.transform.SetPosition(position);
                gameObject.SetActive(true);
                kbatchedAnimController.Play(anim_clip, KAnim.PlayMode.Loop, 1f, 0f);
                return kbatchedAnimController;
            }
            public void UpdateFloat(Instance instance, float dt)
            {
                if (!TeleNavigator.StandInSpaceEnable || instance == null && navigator == null) return;
                int num = Grid.CellBelow(Grid.PosToCell(navigator));
                if (Grid.IsWorldValidCell(num))
                {
                    bool flag = Grid.Solid[num] || Grid.FakeFloor[num] || Grid.IsSubstantialLiquid(num);
                    if (!flag)
                    {
                        if (tele_anim != null) return;
                        tele_anim = AddTrackedAnim("teleSuit", Assets.GetAnim("tele_stand_kanim"), "loop", Grid.SceneLayer.Creatures, "foot", true);
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

            [HarmonyPatch(typeof(FallMonitor.Instance), nameof(FallMonitor.Instance.UpdateFalling))]
            public static class FallMonitor_updateFalling_Pathes
            {
                private static readonly string ModuleName = "FallMonitor_updateFalling_Pathes";
                public static bool Prefix(FallMonitor.Instance __instance)
                {
                    if (!TeleNavigator.StandInSpaceEnable || __instance == null) return true;

                    FieldInfo navigatorField = AccessTools.Field(typeof(FallMonitor.Instance), "navigator");
                    if (navigatorField != null)
                    {
                        Navigator navigator = (Navigator)navigatorField.GetValue(__instance);
                        if (navigator != null && navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            #endregion
            public override void StartSM()
            {
                base.StartSM();
            }
            public override void StopSM(string reason)
            {
                base.StopSM(reason);
            }
        }

        public WearingSuit wearingSuit;

        public TargetParameter owner;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = wearingSuit;
            Target(owner);

            wearingSuit
                .DefaultState(wearingSuit.hasBattery);

            wearingSuit.hasBattery
                .TagTransition(GameTags.SuitBatteryOut, wearingSuit.noBattery)
                .Update("UpdateFloatAnim", (smi,dt)=>smi.UpdateFloat(smi, dt), UpdateRate.SIM_200ms)
                .Exit((smi) => smi.UpdateFloat(smi, 1));

            wearingSuit.noBattery
                .Enter((smi) =>OnNoBattery(smi))
                .Exit(OnHasBattery)
                .TagTransition(GameTags.SuitBatteryOut, wearingSuit.hasBattery, on_remove: true);
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

        internal class Def:BaseDef
        {
        }
    }
}
