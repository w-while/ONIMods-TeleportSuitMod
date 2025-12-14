using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using HarmonyLib;

namespace TeleportSuitMod
{
    /// <summary>
    /// 舱内状态同步管理器（全量封装，包含挂载/初始化/清理）
    /// </summary>
    public class CabinStateSyncManager : KMonoBehaviour
    {
        // 单例实例（确保全局唯一）
        private static CabinStateSyncManager _instance;

        // 反射缓存（仅初始化一次）
        private static FieldInfo _choreTargetCellField;
        private static FieldInfo _minionBrainCurrentChoreField;
        private static FieldInfo _choreTypeField;
        private static FieldInfo _choreTransformField;

        // 延迟初始化辅助对象
        private GameObject _delayInitObj;

        static CabinStateSyncManager()
        {
            // 初始化反射缓存
            _choreTargetCellField = typeof(Chore).GetField("targetCell", BindingFlags.NonPublic | BindingFlags.Instance);
            _minionBrainCurrentChoreField = typeof(MinionBrain).GetField("currentChore", BindingFlags.NonPublic | BindingFlags.Instance);
            _choreTypeField = typeof(Chore).GetField("choreType", BindingFlags.NonPublic | BindingFlags.Instance);
            _choreTransformField = typeof(Chore).GetField("transform", BindingFlags.Public | BindingFlags.Instance)
                                 ?? typeof(Chore).GetField("m_Transform", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// 对外暴露的唯一初始化入口（OnLoad中仅需调用此方法）
        /// </summary>
        public static void InitializeGlobalManager()
        {
            if (_instance != null) return;

            // 1. 优先挂载到Game.Instance（全局单例）
            if (Game.Instance != null && Game.Instance.gameObject != null)
            {
                _instance = Game.Instance.gameObject.GetComponent<CabinStateSyncManager>();
                if (_instance == null)
                {
                    _instance = Game.Instance.gameObject.AddComponent<CabinStateSyncManager>();
                }
                _instance.InitCoreLogic();
                return;
            }

            // 2. 延迟初始化（Game.Instance未就绪时）
            _instance = new GameObject("CabinStateSyncManager_DelayInit")
                .AddComponent<CabinStateSyncManager>();
            _instance.StartCoroutine(_instance.DelayInitCoroutine());
        }

        /// <summary>
        /// 核心逻辑初始化（事件订阅）
        /// </summary>
        private void InitCoreLogic()
        {
            if (Game.Instance != null)
            {
                Game.Instance.Subscribe((int)GameHashes.EndChore, OnRocketEnterChoreCompleted);
                // 注册游戏退出清理逻辑
                RegisterGameQuitCleanup();
                Debug.Log("舱内状态同步管理器：核心逻辑初始化完成，已订阅EndChore事件");
            }
        }

        /// <summary>
        /// 延迟初始化协程（无GameManager依赖）
        /// </summary>
        private IEnumerator DelayInitCoroutine()
        {
            int waitFrames = 0;
            int maxWait = 60; // 最大等待60帧（1秒）

            // 等待Game.Instance就绪
            while (Game.Instance == null && waitFrames < maxWait)
            {
                waitFrames++;
                yield return null;
            }

            // 挂载到Game.Instance并初始化
            if (Game.Instance != null && Game.Instance.gameObject != null)
            {
                // 转移组件到Game.Instance
                transform.SetParent(Game.Instance.gameObject.transform);
                _instance = Game.Instance.gameObject.GetComponent<CabinStateSyncManager>();
                if (_instance == null)
                {
                    _instance = Game.Instance.gameObject.AddComponent<CabinStateSyncManager>();
                }
                // 销毁临时对象
                Destroy(gameObject);
                // 初始化核心逻辑
                _instance.InitCoreLogic();
            }
            else
            {
                Debug.LogWarning("舱内状态同步管理器：延迟初始化超时，Game.Instance未就绪");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 注册游戏退出清理逻辑
        /// </summary>
        private void RegisterGameQuitCleanup()
        {
            try
            {
                EventInfo onGameQuitEvent = typeof(Game).GetEvent("OnGameQuit", BindingFlags.Public | BindingFlags.Static);
                if (onGameQuitEvent != null)
                {
                    Delegate cleanupDel = Delegate.CreateDelegate(
                        onGameQuitEvent.EventHandlerType,
                        this,
                        nameof(CleanupCoreLogic)
                    );
                    onGameQuitEvent.AddEventHandler(null, cleanupDel);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"舱内状态同步管理器：注册清理逻辑失败 - {e.Message}");
            }
        }

        /// <summary>
        /// 核心逻辑清理（事件取消订阅）
        /// </summary>
        private void CleanupCoreLogic()
        {
            if (Game.Instance != null)
            {
                Game.Instance.Unsubscribe((int)GameHashes.EndChore, OnRocketEnterChoreCompleted);
                Debug.Log("舱内状态同步管理器：核心逻辑已清理，取消EndChore事件订阅");
            }
            _instance = null;
        }

        // EndChore事件回调（核心业务逻辑）
        private void OnRocketEnterChoreCompleted(object data)
        {
            if (data == null) return;
            Chore completedChore = data as Chore;
            if (completedChore == null) return;

            if (!IsRocketEnterExitChore(completedChore)) return;

            int targetCell = GetChoreTargetCell(completedChore);
            if (targetCell == Grid.InvalidCell) return;

            int targetWorldId = GetCellWorldId(targetCell);
            WorldContainer targetWorld = ClusterManager.Instance.GetWorld(targetWorldId);
            if (targetWorld == null || !IsModuleInteriorWorld(targetWorld)) return;

            MinionIdentity minion = GetMinionFromChore(completedChore);
            if (minion == null) return;

            PassengerRocketModule cabinModule = GetPassengerModuleFromWorld(targetWorld);
            if (cabinModule == null) return;

            // 核心操作：设置坐标 + 触发ActiveWorldChanged事件
            Vector3 cabinPos = Grid.CellToPos(targetCell);
            minion.transform.position = cabinPos;
            minion.Trigger((int)GameHashes.ActiveWorldChanged, (object)targetWorldId);

            Debug.Log($"小人[{minion.GetProperName()}]登舱任务完成，同步舱内状态（世界ID：{targetWorldId}）");
        }

        #region 内部辅助方法（无需外部调用）
        private bool IsRocketEnterExitChore(Chore chore)
        {
            if (_choreTypeField != null)
            {
                object choreType = _choreTypeField.GetValue(chore);
                if (choreType != null)
                {
                    string choreTypeStr = choreType.ToString();
                    return choreTypeStr.Contains("Rocket") && (choreTypeStr.Contains("Enter") || choreTypeStr.Contains("Cabin"));
                }
            }

            int targetCell = GetChoreTargetCell(chore);
            if (targetCell != Grid.InvalidCell)
            {
                int worldId = GetCellWorldId(targetCell);
                WorldContainer world = ClusterManager.Instance.GetWorld(worldId);
                if (world != null && IsModuleInteriorWorld(world))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetChoreTargetCell(Chore chore)
        {
            if (_choreTargetCellField != null)
            {
                object cellObj = _choreTargetCellField.GetValue(chore);
                if (cellObj != null) return Convert.ToInt32(cellObj);
            }

            if (_choreTransformField != null)
            {
                Transform choreTransform = _choreTransformField.GetValue(chore) as Transform;
                if (choreTransform != null)
                {
                    return Grid.PosToCell(choreTransform.position);
                }
            }

            MinionIdentity minion = GetMinionFromChore(chore);
            if (minion != null)
            {
                return Grid.PosToCell(minion.transform.position);
            }

            return Grid.InvalidCell;
        }

        private int GetCellWorldId(int cell)
        {
            PropertyInfo worldIdxProp = typeof(Grid).GetProperty("WorldIdx", BindingFlags.Public | BindingFlags.Static);
            if (worldIdxProp != null)
            {
                int[] worldIdxArray = (int[])worldIdxProp.GetValue(null, null);
                if (cell >= 0 && cell < worldIdxArray.Length)
                {
                    return worldIdxArray[cell];
                }
            }
            return -1;
        }

        private bool IsModuleInteriorWorld(WorldContainer world)
        {
            PropertyInfo isModuleProp = typeof(WorldContainer).GetProperty("IsModuleInterior", BindingFlags.Public | BindingFlags.Instance);
            if (isModuleProp != null)
            {
                return (bool)isModuleProp.GetValue(world);
            }

            return world.name.Contains("ModuleInterior") || world.name.Contains("RocketInterior") || world.name.Contains("Cabin");
        }

        private MinionIdentity GetMinionFromChore(Chore chore)
        {
            foreach (MinionIdentity minion in GameObject.FindObjectsOfType<MinionIdentity>())
            {
                MinionBrain brain = minion.GetComponent<MinionBrain>();
                if (brain == null) continue;

                object currentChore = _minionBrainCurrentChoreField?.GetValue(brain);
                if (currentChore != null && currentChore == chore)
                {
                    return minion;
                }
            }

            int targetWorldId = GetCellWorldId(GetChoreTargetCell(chore));
            if (targetWorldId == -1) return null;

            foreach (MinionIdentity minion in GameObject.FindObjectsOfType<MinionIdentity>())
            {
                int minionWorldId = Grid.WorldIdx[Grid.PosToCell(minion.transform.position)];
                if (minionWorldId == targetWorldId)
                {
                    return minion;
                }
            }

            return null;
        }

        private PassengerRocketModule GetPassengerModuleFromWorld(WorldContainer cabinWorld)
        {
            foreach (PassengerRocketModule module in GameObject.FindObjectsOfType<PassengerRocketModule>())
            {
                ClustercraftExteriorDoor door = module.GetComponent<ClustercraftExteriorDoor>();
                if (door == null) continue;

                WorldContainer doorTargetWorld = door.GetTargetWorld();
                if (doorTargetWorld != null && doorTargetWorld.id == cabinWorld.id)
                {
                    return module;
                }
            }
            return null;
        }
        #endregion

        // 组件销毁时自动清理
        private void OnDestroy()
        {
            CleanupCoreLogic();
        }
    }

}