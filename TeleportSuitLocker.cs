using Database;
using STRINGS;
using System;
using System.Reflection;
using TUNING;
using UnityEngine;
using static TeleportSuitMod.TeleportSuitLocker;


namespace TeleportSuitMod
{
    public class TeleportSuitLocker : StateMachineComponent<TeleportSuitLocker.StatesInstance>
    {
        [MyCmpAdd]
        public UnequipTeleportSuitWorkable unequipTeleportSuitWorkable;
        [MyCmpAdd]
        public EquipTeleportSuitWorkable equipTeleportSuitWorkable;
        public class UnequipTeleportSuitWorkable : Workable
        {
            public static readonly Chore.Precondition DoesDupeHasTeleportSuitAndNeedCharging = new Chore.Precondition
            {
                id = "DoesDupeHasTeleportSuitAndNeedCharging",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.DOES_DUPE_HAS_TELEPORT_SUIT_AND_NEED_CHARGING,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    Equipment equipment2 = context.consumerState.equipment;
                    if (equipment2 == null)
                    {
                        return false;
                    }
                    AssignableSlotInstance slot2 = equipment2.GetSlot(Db.Get().AssignableSlots.Suit);
                    if (slot2.assignable == null)
                    {
                        return false;
                    }
                    Equippable component2 = slot2.assignable.GetComponent<Equippable>();
                    if (component2 == null || !component2.isEquipped)
                    {
                        return false;
                    }
                    SuitTank component3 = slot2.assignable.GetComponent<SuitTank>();
                    TeleportSuitTank component5 = slot2.assignable.GetComponent<TeleportSuitTank>();
                    if (component5==null)
                    {
                        return false;
                    }
                    if (component3 != null && component3.NeedsRecharging())
                    {
                        return true;
                    }
                    return (component5 != null && component5.NeedsRecharging()) ? true : false;
                }
            };

            public static readonly Chore.Precondition DoesDupeHasTeleportSuit = new Chore.Precondition
            {
                id = "DoesDupeHasTeleportSuit",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.DOES_DUPE_HAS_TELEPORTSUIT,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    Equipment equipment = context.consumerState.equipment;
                    if (equipment == null)
                    {
                        return false;
                    }
                    AssignableSlotInstance slot = equipment.GetSlot(Db.Get().AssignableSlots.Suit);
                    if (slot.assignable == null)
                    {
                        return false;
                    }
                    Equippable component = slot.assignable.GetComponent<Equippable>();
                    if (component == null || !component.isEquipped)
                    {
                        return false;
                    }
                    if (slot.assignable.GetComponent<TeleportSuitTank>() != null)
                    {
                        return true;
                    }
                    return false;
                }
            };

            public static readonly Chore.Precondition CanTeleportSuitLockerDropOffSuit = new Chore.Precondition
            {
                id = "CanTeleportSuitLockerDropOffSuit",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.CAN_TELEPORT_SUIT_LOCKER_DROP_OFFSUIT,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    if (data!=null)
                    {
                        return ((SuitLocker)data).CanDropOffSuit();
                    }
                    return false;
                }
            };
            private WorkChore<UnequipTeleportSuitWorkable> urgentUnequipChore;

            //todo:让小人在空闲的时候脱下传送服
            //private WorkChore<UnequipTeleportSuitWorkable> idleUnequipChore;

            private WorkChore<UnequipTeleportSuitWorkable> breakTimeUnequipChore;

            public static ChoreType urgentUnequipChoreType = null;
            public static ChoreType breakTimeUnequipChoreType = null;

            protected override void OnPrefabInit()
            {
                base.OnPrefabInit();
                resetProgressOnStop = true;
                workTime = 0.25f;
                synchronizeAnims = false;
            }

            public void CreateChore()
            {
                if (urgentUnequipChore == null)
                {
                    if (urgentUnequipChoreType==null)
                    {
                        urgentUnequipChoreType = (ChoreType)typeof(ChoreTypes).GetMethod("Add", BindingFlags.Instance|BindingFlags.NonPublic).
                            Invoke(Db.Get().ChoreTypes, new object[] {
                                "ReturnTeleportSuitUrgent", new string[0], "", new string[0],
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITURGENT.NAME.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITURGENT.STATUS.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITURGENT.TOOLTIP.ToString(),
                                false,
                                -1,
                                null
                            });
                    }
                    if (breakTimeUnequipChore==null)
                    {
                        breakTimeUnequipChoreType = (ChoreType)typeof(ChoreTypes).GetMethod("Add", BindingFlags.Instance|BindingFlags.NonPublic).
                            Invoke(Db.Get().ChoreTypes, new object[] {
                                "ReturnTeleportSuitBreakTime", new string[0], "", new string[0],
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITBREAKTIME.NAME.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITBREAKTIME.STATUS.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.RETURNTELEPORTSUITBREAKTIME.TOOLTIP.ToString(),
                                false,
                                -1,
                                null
                            });
                    }
                    SuitLocker component = GetComponent<SuitLocker>();
                    urgentUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(urgentUnequipChoreType,
                        this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true,
                        null, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false,
                        allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.topPriority, 5,
                        ignore_building_assignment: false, add_to_daily_report: false);
                    urgentUnequipChore.AddPrecondition(DoesDupeHasTeleportSuitAndNeedCharging);
                    urgentUnequipChore.AddPrecondition(CanTeleportSuitLockerDropOffSuit, component);
                    //idleUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(Db.Get().ChoreTypes.ReturnSuitIdle, this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true, null, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.idle, 5, ignore_building_assignment: false, add_to_daily_report: false);
                    //idleUnequipChore.AddPrecondition(DoesDupeHasTeleportSuit);
                    //idleUnequipChore.AddPrecondition(CanTeleportSuitLockerDropOffSuit, component);
                    if (TeleportSuitOptions.Instance.ShouldDropDuringBreak)
                    {
                        breakTimeUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(breakTimeUnequipChoreType,
                            this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true,
                            Db.Get().ScheduleBlockTypes.Hygiene, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false,
                            allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.topPriority, 5,
                            ignore_building_assignment: false, add_to_daily_report: false);
                        breakTimeUnequipChore.AddPrecondition(DoesDupeHasTeleportSuit);
                        breakTimeUnequipChore.AddPrecondition(CanTeleportSuitLockerDropOffSuit, component);
                    }
                }
            }

            public void CancelChore()
            {
                if (urgentUnequipChore != null)
                {
                    urgentUnequipChore.Cancel(nameof(UnequipTeleportSuitWorkable.CancelChore));
                    urgentUnequipChore = null;
                }
                //if (idleUnequipChore != null)
                //{
                //    idleUnequipChore.Cancel(nameof(UnequipTeleportSuitWorkable.CancelChore));
                //    idleUnequipChore = null;
                //}
                if (breakTimeUnequipChore!=null)
                {
                    breakTimeUnequipChore.Cancel(nameof(UnequipTeleportSuitWorkable.CancelChore));
                    breakTimeUnequipChore=null;
                }
            }

            protected override void OnStartWork(Worker worker)
            {
                ShowProgressBar(show: false);
            }

            protected override bool OnWorkTick(Worker worker, float dt)
            {
                return true;
            }

            protected override void OnCompleteWork(Worker worker)
            {
                Console.WriteLine("Unequip OnCompleteWork");

                Equipment equipment = worker.GetComponent<MinionIdentity>().GetEquipment();
                if (equipment.IsSlotOccupied(Db.Get().AssignableSlots.Suit))
                {
                    if (GetComponent<SuitLocker>().CanDropOffSuit())
                    {
                        GetComponent<SuitLocker>().UnequipFrom(equipment);
                    }
                    else
                    {
                        equipment.GetAssignable(Db.Get().AssignableSlots.Suit).Unassign();
                    }
                }
                if (urgentUnequipChore != null)
                {
                    CancelChore();
                    CreateChore();
                }
            }

            public override HashedString[] GetWorkAnims(Worker worker)
            {
                return new HashedString[1]
                {
                    new HashedString("none")
                };
            }
        }

        public class EquipTeleportSuitWorkable : Workable
        {
            public static readonly Chore.Precondition DoesTeleportSuitLockerHasAvailableSuit = new Chore.Precondition
            {
                id = "DoesTeleportSuitLockerHasAvailableSuit",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.DOES_TELEPORT_SUIT_LOCKER_HAS_AVAILABLE_SUIT,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    if (data==null)
                    {
                        return false;
                    }
                    return ((TeleportSuitLocker)data).IsOxygenTankAboveMinimumLevel()&&((TeleportSuitLocker)data).IsBatteryAboveMinimumLevel();
                }
            };
            public static readonly Chore.Precondition DoesDupeAtEquipTeleportSuitSchedule = new Chore.Precondition
            {
                id = "DoesDupeAtEquipTeleportSuitSchedule",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.DOES_DUPE_AT_EQUIP_TELEPORT_SUIT_SCHEDULE,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    if (context.consumerState.scheduleBlock?.GroupId!="Worktime")
                    {
                        return false;
                    }
                    return true;
                }
            };
            public static readonly Chore.Precondition DoesDupeHasNoTeleportSuit = new Chore.Precondition
            {
                id = "DoesDupeHasNoTeleportSuit",
                description = TeleportSuitStrings.DUPLICANTS.CHORES.PRECONDITIONS.DOES_DUPE_HAS_NO_TELEPORT_SUIT,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    Equipment equipment = context.consumerState.equipment;
                    if (equipment == null)
                    {
                        return false;
                    }
                    if (equipment.IsSlotOccupied(Db.Get().AssignableSlots.Suit)
                    &&equipment.GetSlot(Db.Get().AssignableSlots.Suit).assignable.GetComponent<TeleportSuitTank>()!=null)
                    {
                        return false;
                    }
                    return true;
                }
            };
            private WorkChore<EquipTeleportSuitWorkable> equipChore;
            public static ChoreType equipChoretype = null;

            protected override void OnPrefabInit()
            {
                base.OnPrefabInit();
                resetProgressOnStop = true;
                workTime = 0.25f;
                synchronizeAnims = false;
            }

            public void CreateChore()
            {
                if (equipChoretype==null)
                {
                    equipChoretype = (ChoreType)typeof(ChoreTypes).GetMethod("Add", BindingFlags.Instance|BindingFlags.NonPublic).
                        Invoke(Db.Get().ChoreTypes, new object[] {
                                "EquipTeleportSuit", new string[0], "", new string[0],
                                TeleportSuitStrings.DUPLICANTS.CHORES.EQUIPTELEPORTSUIT.NAME.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.EQUIPTELEPORTSUIT.STATUS.ToString(),
                                TeleportSuitStrings.DUPLICANTS.CHORES.EQUIPTELEPORTSUIT.TOOLTIP.ToString(),
                                false,
                                -1,
                                null
                        });
                }
                if (equipChore == null)
                {
                    TeleportSuitLocker component = GetComponent<TeleportSuitLocker>();
                    equipChore = new WorkChore<EquipTeleportSuitWorkable>(equipChoretype, this, null,
                        run_until_complete: true, null, null, null, allow_in_red_alert: true,
                        null, ignore_schedule_block: false, only_when_operational: true, null,
                        is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false,
                        PriorityScreen.PriorityClass.topPriority, 5, ignore_building_assignment: false, add_to_daily_report: false);
                    equipChore.AddPrecondition(DoesDupeAtEquipTeleportSuitSchedule);
                    equipChore.AddPrecondition(DoesTeleportSuitLockerHasAvailableSuit, component);
                    equipChore.AddPrecondition(DoesDupeHasNoTeleportSuit);
                }
            }

            public void CancelChore()
            {
                if (equipChore != null)
                {
                    equipChore.Cancel(nameof(EquipTeleportSuitWorkable.CancelChore));
                    equipChore = null;
                }
            }

            protected override void OnStartWork(Worker worker)
            {
                ShowProgressBar(show: false);
            }

            protected override bool OnWorkTick(Worker worker, float dt)
            {
                return true;
            }

            protected override void OnCompleteWork(Worker worker)
            {
                Equipment equipment = worker.GetComponent<MinionIdentity>().GetEquipment();
                if (equipment==null)
                {
                    return;
                }
                GetComponent<SuitLocker>().EquipTo(equipment);
                if (equipChore != null)
                {
                    CancelChore();
                    CreateChore();
                }
            }

            public override HashedString[] GetWorkAnims(Worker worker)
            {
                return new HashedString[1]
                {
                    new HashedString("none")
                };
            }
        }

        public class States : GameStateMachine<States, StatesInstance, TeleportSuitLocker>
        {
            public class ChargingStates : State
            {
                public State notoperational;

                public State operational;
            }

            public State empty;

            public ChargingStates charging;

            public State charged;

            public override void InitializeStates(out BaseState default_state)
            {
                default_state = empty;
                base.serializable = SerializeType.Both_DEPRECATED;
                root.Update("RefreshMeter", delegate (StatesInstance smi, float dt)
                {
                    smi.master.RefreshMeter();
                }, UpdateRate.RENDER_200ms);
                empty.EventTransition(GameHashes.OnStorageChange, charging, (StatesInstance smi) => smi.master.GetStoredOutfit() != null);
                charging.DefaultState(charging.notoperational)
                    .EventTransition(GameHashes.OnStorageChange, empty, (StatesInstance smi) => smi.master.GetStoredOutfit() == null);
                //.Transition(charged, (StatesInstance smi) => smi.master.IsSuitFullyCharged());
                charging.notoperational.TagTransition(GameTags.Operational, charging.operational);
                charging.operational.TagTransition(GameTags.Operational, charging.notoperational, on_remove: true).Update("FillBattery", delegate (StatesInstance smi, float dt)
                {
                    smi.master.FillBattery(dt);
                }, UpdateRate.SIM_1000ms)
                .Enter(delegate (StatesInstance smi)
                {
                    smi.setEnergeUsage(true);
                })
                .Exit(delegate (StatesInstance smi)
                {
                    smi.setEnergeUsage(false);
                });
                charged.EventTransition(GameHashes.OnStorageChange, empty, (StatesInstance smi) => smi.master.GetStoredOutfit() == null);
            }
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, TeleportSuitLocker, object>.GameInstance
        {
            public StatesInstance(TeleportSuitLocker teleport_suit_locker)
                : base(teleport_suit_locker)
            {
            }
            public void setEnergeUsage(bool additionalEnergeUsage)
            {
                EnergyConsumer energyConsumer = GetComponent<EnergyConsumer>();
                if (additionalEnergeUsage)
                {
                    energyConsumer.BaseWattageRating = TeleportSuitLockerConfig.AdditionalEnergyUsage+TeleportSuitLockerConfig.BaseEnergyUsage;
                }
                else
                {
                    energyConsumer.BaseWattageRating =TeleportSuitLockerConfig.BaseEnergyUsage;
                }
            }
        }

        [MyCmpReq]
        private Building building;

        [MyCmpReq]
        private Storage storage;

        [MyCmpReq]
        private SuitLocker suit_locker;

        [MyCmpReq]
        private KBatchedAnimController anim_controller;

        private MeterController o2_meter;

        private MeterController battery_meter;

        private float batteryChargeTime = 60f;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            equipTeleportSuitWorkable.CreateChore();
            o2_meter = new MeterController(GetComponent<KBatchedAnimController>(), "meter_target_top", "meter_oxygen", Meter.Offset.Infront, Grid.SceneLayer.NoLayer, Vector3.zero, "meter_target_top");
            battery_meter = new MeterController(GetComponent<KBatchedAnimController>(), "meter_target_side", "meter_petrol", Meter.Offset.Infront, Grid.SceneLayer.NoLayer, Vector3.zero, "meter_target_side");
            base.smi.StartSM();
        }
        protected override void OnCleanUp()
        {
            equipTeleportSuitWorkable.CancelChore();
            base.OnCleanUp();
        }

        public bool IsSuitFullyCharged()
        {
            KPrefabID storedOutfit = suit_locker.GetStoredOutfit();
            return suit_locker.IsSuitFullyCharged()&&storedOutfit.GetComponent<TeleportSuitTank>().IsFull();
        }

        public KPrefabID GetStoredOutfit()
        {
            return suit_locker.GetStoredOutfit();
        }

        private void FillBattery(float dt)
        {
            KPrefabID storedOutfit = suit_locker.GetStoredOutfit();
            if (!(storedOutfit == null))
            {
                TeleportSuitTank component = storedOutfit.GetComponent<TeleportSuitTank>();
                if (!component.IsFull())
                {
                    component.batteryCharge += dt / batteryChargeTime;
                }
                else
                {
                    component.batteryCharge=1;
                    smi.GoTo(smi.sm.charged);
                }
            }
        }

        private void RefreshMeter()
        {
            if (o2_meter==null)
            {
                //Console.WriteLine("o2_meter is null");
                return;
            }
            if (battery_meter==null)
            {
                //Console.WriteLine(value: "battery_meter is null");
                return;
            }
            KPrefabID storedOutfit = GetStoredOutfit();
            if (storedOutfit == null)
            {
                return;
            }
            o2_meter.SetPositionPercent(suit_locker.OxygenAvailable);
            battery_meter.SetPositionPercent(storedOutfit.GetComponent<TeleportSuitTank>().batteryCharge);
            anim_controller.SetSymbolVisiblity("oxygen_yes_bloom", IsOxygenTankAboveMinimumLevel());
            anim_controller.SetSymbolVisiblity("petrol_yes_bloom", IsBatteryAboveMinimumLevel());
        }

        public bool IsOxygenTankAboveMinimumLevel()
        {
            KPrefabID storedOutfit = GetStoredOutfit();
            if (storedOutfit != null)
            {
                SuitTank component = storedOutfit.GetComponent<SuitTank>();
                if (component == null)
                {
                    return true;
                }
                return component.PercentFull() >= TUNING.EQUIPMENT.SUITS.MINIMUM_USABLE_SUIT_CHARGE;
            }
            return false;
        }

        public bool IsBatteryAboveMinimumLevel()
        {
            KPrefabID storedOutfit = GetStoredOutfit();
            if (storedOutfit != null)
            {
                TeleportSuitTank component = storedOutfit.GetComponent<TeleportSuitTank>();
                if (component == null)
                {
                    return true;
                }
                return component.PercentFull() >= TUNING.EQUIPMENT.SUITS.MINIMUM_USABLE_SUIT_CHARGE;
            }
            return false;
        }
    }
}
