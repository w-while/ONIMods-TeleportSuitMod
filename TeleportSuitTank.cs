using System.Collections.Generic;
using KSerialization;
using STRINGS;
using UnityEngine;

namespace TeleportSuitMod
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class TeleportSuitTank : KMonoBehaviour, IGameObjectEffectDescriptor
    {
        [Serialize]
        public float batteryCharge = 1f;

        public const float REFILL_PERCENT = 0.05f;

        public float batteryDuration = 200f;

        public float coolingOperationalTemperature = 333.15f;

        public Tag coolantTag;

        private TeleportSuitMonitor.Instance teleportSuitMonitor;

        private static readonly EventSystem.IntraObjectHandler<TeleportSuitTank> OnEquippedDelegate = new EventSystem.IntraObjectHandler<TeleportSuitTank>(delegate (TeleportSuitTank component, object data)
        {
            component.OnEquipped(data);
        });

        private static readonly EventSystem.IntraObjectHandler<TeleportSuitTank> OnUnequippedDelegate = new EventSystem.IntraObjectHandler<TeleportSuitTank>(delegate (TeleportSuitTank component, object data)
        {
            component.OnUnequipped(data);
        });

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.EquippedItemEquippable, OnEquippedDelegate);
            Subscribe((int)GameHashes.UnequippedItemEquippable, OnUnequippedDelegate);
        }

        public float PercentFull()
        {
            return batteryCharge;
        }

        public bool IsEmpty()
        {
            return batteryCharge <= 0f;
        }

        public bool IsFull()
        {
            return PercentFull() >= 1f;
        }

        public bool NeedsRecharging()
        {
            return PercentFull() <= REFILL_PERCENT;
        }

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> list = new List<Descriptor>();
            string text = string.Format(TeleportSuitStrings.TELEPORTSUIT_BATTERY, GameUtil.GetFormattedPercent(PercentFull() * 100f));
            list.Add(new Descriptor(text, text));
            return list;
        }

        private void OnEquipped(object data)
        {
            Equipment equipment = (Equipment)data;
            NameDisplayScreen.Instance.SetSuitBatteryDisplay(equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject(), PercentFull, bVisible: true);
            teleportSuitMonitor = new TeleportSuitMonitor.Instance(this, equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject());
            teleportSuitMonitor.StartSM();
            if (NeedsRecharging())
            {
                equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject().AddTag(GameTags.SuitBatteryLow);
            }
        }

        private void OnUnequipped(object data)
        {
            Equipment equipment = (Equipment)data;
            if (!equipment.destroyed)
            {
                equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject().RemoveTag(GameTags.SuitBatteryLow);
                equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject().RemoveTag(GameTags.SuitBatteryOut);
                NameDisplayScreen.Instance.SetSuitBatteryDisplay(equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject(), null, bVisible: false);
            }
            if (teleportSuitMonitor != null)
            {
                teleportSuitMonitor.StopSM("Removed teleportsuit tank");
                teleportSuitMonitor = null;
            }
        }
    }
}
