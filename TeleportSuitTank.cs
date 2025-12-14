using System.Collections.Generic;
using KSerialization;
using STRINGS;
using UnityEngine;

namespace TeleportSuitMod
{
    [DisallowMultipleComponent]
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

        private MinionIdentity ownerMinion;
        private bool isEventSubscribed; // 订阅状态标记，避免重复订阅
        [MyCmpReq] private SuitTank suitTank;
        [MyCmpReq] private Equippable equippable;

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

            isEventSubscribed = false;
            // 订阅读档完成事件（全局），用于恢复订阅
            Game.Instance.Subscribe((int)GameHashes.Loaded, OnGameLoaded);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            // 绑定装备穿戴/卸载事件
            if (equippable != null)
            {
                equippable.OnEquipped += OnEquipped;
                equippable.OnUnequipped += OnUnequipped;
            }

            // 检查当前是否已穿戴（读档后恢复订阅）
            CheckAndRestoreSubscription();
        }
        protected override void OnCleanUp()
        {
            // 取消装备事件绑定
            if (equippable != null)
            {
                equippable.OnEquipped -= OnEquipped;
                equippable.OnUnequipped -= OnUnequipped;
            }
            // 取消小人事件订阅
            UnsubscribeMinionEvents();
            // 取消全局读档事件
            Game.Instance.Unsubscribe((int)GameHashes.Loaded, OnGameLoaded);

            ownerMinion = null;
            isEventSubscribed = false;
            base.OnCleanUp();
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

            // 获取穿戴的小人
            ownerMinion = GetMinionFromEquippable((Equippable)data);
            if (ownerMinion == null)
            {
                LogUtils.LogWarning("TeleportSuitTank", "穿戴传送服时未找到小人对象");
                return;
            }

            // 订阅小人事件
            SubscribeMinionEvents();
            LogUtils.LogDebug("TeleportSuitTank", $"小人[{ownerMinion.GetProperName()}]穿戴传送服，已订阅EndChore事件");
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
        // 全局读档完成事件 - 恢复订阅
        private void OnGameLoaded(object data)
        {
            LogUtils.LogDebug("TeleportSuitTank", "游戏存档加载完成，检查并恢复传送服事件订阅");
            CheckAndRestoreSubscription();
        }
        // 检查并恢复订阅（读档/组件激活时）
        private void CheckAndRestoreSubscription()
        {
            // 若已订阅则跳过
            if (isEventSubscribed) return;

            // 检查当前是否已穿戴
            if (equippable?.assignee != null)
            {
                ownerMinion = GetMinionFromEquippable(equippable);
                if (ownerMinion != null)
                {
                    SubscribeMinionEvents();
                    LogUtils.LogDebug("TeleportSuitTank", $"读档后恢复小人[{ownerMinion.GetProperName()}]的事件订阅");
                }
            }
        }

        // 订阅小人的EndChore事件
        private void SubscribeMinionEvents()
        {
            if (ownerMinion == null || isEventSubscribed) return;

            // 订阅小人的EndChore事件（核心业务事件）
            ownerMinion.Subscribe((int)GameHashes.EndChore, OnMinionChoreCompleted);
            isEventSubscribed = true;
        }

        // 取消小人事件订阅
        private void UnsubscribeMinionEvents()
        {
            if (ownerMinion == null || !isEventSubscribed) return;

            ownerMinion.Unsubscribe((int)GameHashes.EndChore, OnMinionChoreCompleted);
            isEventSubscribed = false;
        }

        // 小人任务完成回调（核心业务逻辑）
        private void OnMinionChoreCompleted(object data)
        {
            Chore completedChore = data as Chore;
            if (completedChore == null) return;

            // 仅处理登舱任务（可根据业务调整筛选逻辑）
            if (IsRocketEnterChore(completedChore))
            {
                LogUtils.LogDebug("TeleportSuitTank", $"小人[{ownerMinion.GetProperName()}]完成登舱任务，执行舱内同步逻辑");
                // 调用CabinStateSyncManager的核心逻辑
                var cabinSync = GetComponent<CabinStateSyncManager>();
                cabinSync?.HandleRocketEnterChore(ownerMinion, completedChore);
            }
        }

        // 辅助：从Equippable获取小人对象
        private MinionIdentity GetMinionFromEquippable(Equippable eq)
        {
            if (eq?.assignee == null) return null;

            // 方式1：通过AssignableProxy获取
            if (eq.assignee is MinionAssignablesProxy proxy)
            {
                return proxy.GetTargetGameObject()?.GetComponent<MinionIdentity>();
            }

            // 方式2：直接从游戏对象获取
            return eq.assignee.gameObject.GetComponent<MinionIdentity>();
        }

        // 辅助：判断是否为登舱任务（可根据实际业务调整）
        private bool IsRocketEnterChore(Chore chore)
        {
            // 示例逻辑：根据任务名称/类型判断
            return chore.name.Contains("RocketEnter") ||
                   chore.GetType().Name.Contains("Rocket") ||
                   chore.driver?.GetComponent<MinionIdentity>() == ownerMinion;
        }

    }
}
