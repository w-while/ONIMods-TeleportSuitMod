using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using UnityEngine;

namespace TeleportSuitMod
{
    public class CabinTriggerData
    {
        public int WorldID { get; set; }
        public PassengerRocketModule Cabin { get; set; }
    }
    [DisallowMultipleComponent]
    [SerializationConfig(MemberSerialization.OptIn)]
    public class TeleportSuitTank : ModComponent, IGameObjectEffectDescriptor
    {

        protected override string ModuleName => "TeleportSuitTank";

        [Serialize]
        public float batteryCharge = 1f;

        public const float REFILL_PERCENT = 0.05f;

        public float batteryDuration = 200f;

        public float coolingOperationalTemperature = 333.15f;

        public Tag coolantTag;

        private TeleportSuitMonitor.Instance _teleportSuitMonitor;

        [MyCmpReq] private SuitTank suitTank;
        [MyCmpReq] private Equippable equippable;

        // 核心：Klei引擎标准事件委托
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
            SubscribeToEquipEvents();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            //Trigger((int)GameHashes.EquippedItemEquippable);
        }
        protected override void OnCleanUp()
        {
            UnsubscribeFromEquipEvents();

            base.OnCleanUp();
        }
        // 核心：注册装备事件映射
        private void SubscribeToEquipEvents()
        {
            // 注册穿戴事件（Klei引擎标准方式）
            Subscribe((int)GameHashes.EquippedItemEquippable, OnEquippedDelegate);
            Subscribe((int)GameHashes.UnequippedItemEquippable, OnUnequippedDelegate);
        }
        // 取消装备事件映射
        private void UnsubscribeFromEquipEvents()
        {

            Unsubscribe((int)GameHashes.EquippedItemEquippable, OnEquippedDelegate);
            Unsubscribe((int)GameHashes.UnequippedItemEquippable, OnUnequippedDelegate);
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
            //电池与氧气储量检查
            Equipment equipment = (Equipment)data;
            NameDisplayScreen.Instance.SetSuitBatteryDisplay(equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject(), PercentFull, bVisible: true);
            _teleportSuitMonitor = new TeleportSuitMonitor.Instance(this, equipment.GetComponent<MinionAssignablesProxy>().GetTargetGameObject());
            _teleportSuitMonitor.StartSM();
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
            if (_teleportSuitMonitor != null)
            {
                _teleportSuitMonitor.StopSM("Removed teleportsuit tank");
                _teleportSuitMonitor = null;
            }
        }
    }

}
