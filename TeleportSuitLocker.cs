﻿using STRINGS;
using System;
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
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_URGENT,
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
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_IDLE,
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
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_IDLE,
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

            private WorkChore<UnequipTeleportSuitWorkable> idleUnequipChore;

            private WorkChore<UnequipTeleportSuitWorkable> breakTimeUnequipChore;


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
                    SuitLocker component = GetComponent<SuitLocker>();
                    urgentUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(Db.Get().ChoreTypes.ReturnSuitUrgent, this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true, null, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.topPriority, 5, ignore_building_assignment: false, add_to_daily_report: false);
                    urgentUnequipChore.AddPrecondition(DoesDupeHasTeleportSuitAndNeedCharging);
                    urgentUnequipChore.AddPrecondition(CanTeleportSuitLockerDropOffSuit, component);
                    //idleUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(Db.Get().ChoreTypes.ReturnSuitIdle, this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true, null, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.idle, 5, ignore_building_assignment: false, add_to_daily_report: false);
                    //idleUnequipChore.AddPrecondition(DoesDupeHasTeleportSuit);
                    //idleUnequipChore.AddPrecondition(CanTeleportSuitLockerDropOffSuit, component);
                    if (TeleportSuitOptions.Instance.ShouldDropDuringBreak)
                    {
                        breakTimeUnequipChore = new WorkChore<UnequipTeleportSuitWorkable>(Db.Get().ChoreTypes.ReturnSuitUrgent, this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true, Db.Get().ScheduleBlockTypes.Hygiene, ignore_schedule_block: false, only_when_operational: false, null, is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.topPriority, 5, ignore_building_assignment: false, add_to_daily_report: false);
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
                if (idleUnequipChore != null)
                {
                    idleUnequipChore.Cancel(nameof(UnequipTeleportSuitWorkable.CancelChore));
                    idleUnequipChore = null;
                }
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
                        //Console.WriteLine("CanDropOffSuit");
                        GetComponent<SuitLocker>().UnequipFrom(equipment);
                    }
                    else
                    {
                        //Console.WriteLine("Unassign");
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
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_URGENT,
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
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_URGENT,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    if (context.consumerState.scheduleBlock?.GroupId!="Worktime")
                    {
                        return false;
                    }
                    return true;
                }
            };
            public static readonly Chore.Precondition DoesDupeHasNoSuit = new Chore.Precondition
            {
                id = "DoesDupeHasNoSuit",
                description = DUPLICANTS.CHORES.PRECONDITIONS.DOES_SUIT_NEED_RECHARGING_IDLE,
                fn = delegate (ref Chore.Precondition.Context context, object data)
                {
                    Equipment equipment = context.consumerState.equipment;
                    if (equipment == null)
                    {
                        return false;
                    }
                    if (equipment.IsSlotOccupied(Db.Get().AssignableSlots.Suit))
                    {
                        return false;
                    }
                    return true;
                }
            };
            private WorkChore<EquipTeleportSuitWorkable> equipChore;

            protected override void OnPrefabInit()
            {
                base.OnPrefabInit();
                resetProgressOnStop = true;
                workTime = 0.25f;
                synchronizeAnims = false;
            }

            public void CreateChore()
            {
                if (equipChore == null)
                {
                    TeleportSuitLocker component = GetComponent<TeleportSuitLocker>();
                    equipChore = new WorkChore<EquipTeleportSuitWorkable>(Db.Get().ChoreTypes.ReturnSuitUrgent, this, null, run_until_complete: true, null, null, null, allow_in_red_alert: true, null, ignore_schedule_block: false, only_when_operational: true, null, is_preemptable: false, allow_in_context_menu: true, allow_prioritization: false, PriorityScreen.PriorityClass.topPriority, 5, ignore_building_assignment: false, add_to_daily_report: false);
                    equipChore.AddPrecondition(DoesDupeAtEquipTeleportSuitSchedule);
                    equipChore.AddPrecondition(DoesTeleportSuitLockerHasAvailableSuit, component);
                    equipChore.AddPrecondition(DoesDupeHasNoSuit);
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
                Console.WriteLine("EquipTeleportSuitWorkable OnCompleteWork called");
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
                charging.DefaultState(charging.notoperational).EventTransition(GameHashes.OnStorageChange, empty, (StatesInstance smi) => smi.master.GetStoredOutfit() == null).Transition(charged, (StatesInstance smi) => smi.master.IsSuitFullyCharged());
                charging.notoperational.TagTransition(GameTags.Operational, charging.operational);
                charging.operational.TagTransition(GameTags.Operational, charging.notoperational, on_remove: true).Update("FillBattery", delegate (StatesInstance smi, float dt)
                {
                    smi.master.FillBattery(dt);
                }, UpdateRate.SIM_1000ms);
                charged.EventTransition(GameHashes.OnStorageChange, empty, (StatesInstance smi) => smi.master.GetStoredOutfit() == null);
            }
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, TeleportSuitLocker, object>.GameInstance
        {
            public StatesInstance(TeleportSuitLocker teleport_suit_locker)
                : base(teleport_suit_locker)
            {
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