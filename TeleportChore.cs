using System;
using UnityEngine;
using Klei.AI;

namespace TeleportSuitMod
{
    public class TeleportData
    {
        public Navigator navigator;
        public int targetCell;
        public WorldContainer targetWorld;
        public Vector3 targetPos;
        public System.Action onTeleportComplete;
    }

    public class TeleportChore : Chore<TeleportChore.StatesInstance>
    {
        public TeleportChore(IStateMachineTarget master, TeleportData teleportData)
            : base(
                  Db.Get().ChoreTypes.Idle,
                  master,
                  master.GetComponent<ChoreProvider>(),
                  false,
                  null,
                  null,
                  null,
                  PriorityScreen.PriorityClass.compulsory,
                  (int)PriorityScreen.PriorityClass.topPriority, // 修复：优先级数值改为原版最大值999
                  false,
                  true,
                  0,
                  false,
                  ReportManager.ReportType.WorkTime
              )
        {
            showAvailabilityInHoverText = false;
            base.smi = new StatesInstance(this, teleportData);
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, TeleportChore, object>.GameInstance
        {
            public TeleportData TeleportData;

            public StatesInstance(TeleportChore master, TeleportData teleportData) : base(master)
            {
                this.TeleportData = teleportData;
            }

            public void DoTeleport()
            {
                var data = TeleportData;
                if (data == null || data.navigator == null) return;

                try
                {
                    if (data.targetWorld != null)
                    {
                        TeleportCore.ExecuteCrossWorldTeleport(data.navigator, data.targetPos, data.targetWorld);
                    }
                    else
                    {
                        int reservedCell = 0;
                        TeleportCore.ExecuteTeleportForce(data.navigator, data.targetCell, ref reservedCell);
                    }

                    if (data.onTeleportComplete != null)
                    {
                        data.onTeleportComplete();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[传送任务] 执行失败：{e.Message}\n{e.StackTrace}");
                }
            }
        }

        public class States : GameStateMachine<States, StatesInstance, TeleportChore>
        {
            public State executeTeleport;
            public State complete;

            public override void InitializeStates(out BaseState default_state)
            {
                default_state = executeTeleport;

                this.root
                    .Enter((smi) => { });

                executeTeleport
                    .Enter("DoTeleport", smi =>
                    {
                        smi.DoTeleport();
                        smi.GoTo(complete);
                    });

                complete
                    .Enter("CompleteTeleport", smi =>
                    {
                        smi.master.Cancel("TeleportCompleted");
                        if (smi.TeleportData?.navigator != null)
                        {
                            smi.TeleportData.navigator.enabled = true;
                        }
                    })
                    .ReturnSuccess();
            }
        }

        public new void Begin(Chore.Precondition.Context context)
        {
            var teleportData = smi.TeleportData;
            if (teleportData?.navigator != null)
            {
                teleportData.navigator.enabled = false;
            }
            base.Begin(context);
        }
    }
}