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

        // 存储所有小人的 ID-实例映射
        private Dictionary<int, MinionIdentity> _allMinions = new Dictionary<int, MinionIdentity>();

        public MinionIdentity _ownerMinion;
        private bool _isEventSubscribed; // 订阅状态标记，避免重复订阅
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
            _isEventSubscribed = false;

            if (equippable == null)
            {
                LogError( "缺少Equippable组件，功能将不可用");
                return; // 提前退出，避免后续空引用
            }
            SubscribeToEquipEvents();
            Game.Instance.Subscribe((int)GameHashes.Loaded, OnGameLoaded);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            CollectAllMinions();
            if (equippable.isEquipped)
            {
                //初始化小人状态、订阅小人事件与CabinStayReactbale准备同步
                if (InitializeMinion())
                {
                    SubscribeMinionEvents();
                }
            }
        }
        protected override void OnCleanUp()
        {
            // 取消小人事件订阅
            UnsubscribeMinionEvents();
            // 取消全局读档事件
            Game.Instance.Unsubscribe((int)GameHashes.Loaded, OnGameLoaded);

            _ownerMinion = null;
            _isEventSubscribed = false;
            base.OnCleanUp();
        }
        // 核心：注册装备事件映射
        private void SubscribeToEquipEvents()
        {
            if (equippable == null) return;

            // 移除重复注册（避免多次OnPrefabInit导致重复）
            UnsubscribeFromEquipEvents();

            // 注册穿戴事件（Klei引擎标准方式）
            Subscribe((int)GameHashes.EquippedItemEquippable, OnEquippedDelegate);
            Subscribe((int)GameHashes.UnequippedItemEquippable, OnUnequippedDelegate);
        }
        // 取消装备事件映射
        private void UnsubscribeFromEquipEvents()
        {
            if (equippable == null) return;

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

            //初始化小人状态、订阅小人事件与CabinStayReactbale准备同步
            if (InitializeMinion())
            {
                SubscribeMinionEvents();
                LogDebug($"OnEquipped :{_ownerMinion.GetProperName()}@WID_{_ownerMinion.GetMyWorldId()}");
            }

        }
        private bool InitializeMinion()
        {
            _ownerMinion = GetWearerMinion();
            if (_ownerMinion != null) return true;
            return false;
        }
        private void OnUnequipped(object data)
        {
            // 先清理旧订阅（防止重复订阅/漏取消）
            UnsubscribeMinionEvents();

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
        private MinionIdentity GetWearerMinion()
        {
            MinionIdentity m = null; 
            // 方式1：从父节点获取
            if (transform.parent != null)
            {
                m = transform.parent.GetComponent<MinionIdentity>();
            }

            // 方式2：从Equippable获取
            if (m == null && equippable?.assignee != null)
            {
                m = Utils.GetMinionFromEquippable(equippable);
            }

            // 兜底：返回null（原逻辑存在空引用风险）
            if(m != null)
            {

                return GetMinionById(m.GetInstanceID());
            }
            return null;
        }
        // 全局读档完成事件 - 恢复订阅
        private void OnGameLoaded(object data)
        {
            if (InitializeMinion())
            {
                SubscribeMinionEvents();
                LogDebug( $"读档后恢复小人[{_ownerMinion.GetProperName()}]的事件订阅");
            }
        }

        // 订阅小人的EndChore事件
        private void SubscribeMinionEvents()
        {
            if (_ownerMinion == null || _isEventSubscribed) return;

            // 订阅小人的EndChore事件（核心业务事件）
            _ownerMinion.Subscribe((int)GameHashes.EndChore, OnMinionChoreCompleted);
            LogDebug($"订阅小人[{_ownerMinion.GetProperName()}] EndChore 事件完成");
            _isEventSubscribed = true;
        }

        // 取消小人事件订阅
        private void UnsubscribeMinionEvents()
        {
            if (_ownerMinion == null || !_isEventSubscribed) return;

            _ownerMinion.Unsubscribe((int)GameHashes.EndChore, OnMinionChoreCompleted);
            _isEventSubscribed = false;
        }

        // 小人任务完成回调（核心业务逻辑）
        private void OnMinionChoreCompleted(object data)
        {
            Chore completedChore = data as Chore;
            if (completedChore == null) return;

            // 仅处理登舱任务（可根据业务调整筛选逻辑）
            if (IsRocketEnterChore(completedChore))
            {
                // 调用CabinStateSyncManager的核心逻辑
                HandleRocketEnterChore(_ownerMinion);
            }
        }

        // 辅助：判断是否为登舱任务（可根据实际业务调整）
        private bool IsRocketEnterChore(Chore chore)
        {
            // 示例逻辑：根据任务名称/类型判断
            return chore.choreType == Db.Get().ChoreTypes.RocketEnterExit ||
                   chore.GetType().Name.Contains("Rocket") ||
                   chore.driver?.GetComponent<MinionIdentity>() == _ownerMinion;
        }
        // 小人登舱任务回调（核心业务逻辑）
        public void HandleRocketEnterChore(MinionIdentity minion)
        {
            WorldContainer targetWorld = minion.GetMyWorld();
            int targetWorldId = minion.GetMyWorldId();

            PassengerRocketModule cabinModule = Utils.GetPassengerModuleFromWorld(targetWorld);
            if (cabinModule == null) return;

            // 执行传送服特定的舱内同步逻辑
            // 核心操作：设置坐标 + 触发ActiveWorldChanged事件

            bool isRestrict = RocketCabinRestriction.QuickCheckBlockTeleport(minion, targetWorldId);
            sycMinionStat(cabinModule, isRestrict);

        }
        // 初始化时收集
        public void CollectAllMinions()
        {
            foreach (MinionIdentity minion in Components.LiveMinionIdentities)
            {
                if (!_allMinions.ContainsKey(minion.GetInstanceID()))
                {
                    _allMinions.Add(minion.GetInstanceID(), minion);
                }
            }
        }
        // 根据 ID 获取唯一小人
        public MinionIdentity GetMinionById(int instanceId)
        {
            _allMinions.TryGetValue(instanceId, out MinionIdentity minion);
            return minion;
        }

        public void sycMinionStat(PassengerRocketModule Cabin,bool isRestrict)
        {
            int cell = Cabin.GetComponent<NavTeleporter>().GetCell();
            int num = Cabin.GetComponent<ClustercraftExteriorDoor>().TargetCell();
            RocketPassengerMonitor.Instance smi = _ownerMinion.GetSMI<RocketPassengerMonitor.Instance>();
            smi.sm.targetCell.Set(num, smi, false);
            smi.ClearMoveTarget(num);
            Component interiorDoor = Cabin.GetComponent<ClustercraftExteriorDoor>().GetInteriorDoor();
            AccessControl component = Cabin.GetComponent<AccessControl>();
            AccessControl component2 = interiorDoor.GetComponent<AccessControl>();

            if (isRestrict) {
                component.SetPermission(_ownerMinion.assignableProxy.Get(), AccessControl.Permission.Both);
                component2.SetPermission(_ownerMinion.assignableProxy.Get(), AccessControl.Permission.Neither);
            }
            else
            {
                component.SetPermission(_ownerMinion.assignableProxy.Get(), AccessControl.Permission.Both);
                component2.SetPermission(_ownerMinion.assignableProxy.Get(), AccessControl.Permission.Both);
            }
        }

    }

}
