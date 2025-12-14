using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace TeleportSuitMod
{
    /// <summary>
    /// 舱内停留响应组件，处理小人进入火箭舱内世界后的行为逻辑
    /// </summary>
    public class CabinStayReactable : KMonoBehaviour
    {
        private MinionIdentity _minion;
        private int _targetCabinWorldId = -1;
        private bool _isActive = false;
        private bool _isCabinStay = false;

        // 反射缓存（使用惰性初始化提升性能）
        private static Lazy<FieldInfo> _minionBrainCurrentChoreField = new Lazy<FieldInfo>(() =>
            typeof(MinionBrain).GetField("currentChore", BindingFlags.NonPublic | BindingFlags.Instance));

        private static Lazy<MethodInfo> _choreCancelMethod = new Lazy<MethodInfo>(() =>
            typeof(Chore).GetMethod("Cancel", new[] { typeof(string) }));

        private static Lazy<MethodInfo> _minionBrainSetIdleMethod = new Lazy<MethodInfo>(() =>
            typeof(MinionBrain).GetMethod("SetIdle", BindingFlags.Public | BindingFlags.Instance));

        private static Lazy<PropertyInfo> _worldIsModuleInteriorProp = new Lazy<PropertyInfo>(() =>
            typeof(WorldContainer).GetProperty("IsModuleInterior", BindingFlags.Public | BindingFlags.Instance));

        private static Lazy<MethodInfo> _stateMachineGetSMIMethod = new Lazy<MethodInfo>(() =>
            typeof(StateMachineController).GetMethod("GetSMI", BindingFlags.Public | BindingFlags.Instance));

        private static Lazy<MethodInfo> _worldGetRandomCellMethod = new Lazy<MethodInfo>(() =>
            typeof(WorldContainer).GetMethod("GetRandomCellInWorld", BindingFlags.Public | BindingFlags.Instance));

        private static Lazy<PropertyInfo> _gridWorldIdxProp = new Lazy<PropertyInfo>(() =>
            typeof(Grid).GetProperty("WorldIdx", BindingFlags.Public | BindingFlags.Static));

        #region 生命周期
        private void Awake()
        {
            _minion = GetComponent<MinionIdentity>();
            if (_minion == null)
            {
                LogUtils.LogWarning("CabinStayReactable", "未找到MinionIdentity组件，销毁响应");
                Destroy(this);
                return;
            }

            _minion.Subscribe((int)GameHashes.ActiveWorldChanged, OnMinionWorldChanged);
            LogUtils.LogDebug("CabinStayReactable", $"小人[{_minion.GetProperName()}]已订阅世界变更事件");
        }

        private void OnDestroy()
        {
            if (_minion != null)
            {
                _minion.Unsubscribe((int)GameHashes.ActiveWorldChanged, OnMinionWorldChanged);
                Cancel("组件销毁");
            }
            _isActive = false;
            _isCabinStay = false;
            LogUtils.LogDebug("CabinStayReactable", $"小人[{_minion?.GetProperName()}]舱内响应组件已销毁");
        }
        #endregion

        #region 核心事件响应
        private void OnMinionWorldChanged(object data)
        {
            if (data == null || _minion == null) return;

            try
            {
                int newWorldId = Convert.ToInt32(data);
                _targetCabinWorldId = newWorldId;

                WorldContainer newWorld = ClusterManager.Instance.GetWorld(newWorldId);
                if (newWorld != null && IsRocketCabinWorld(newWorld))
                {
                    _isCabinStay = true;
                    Activate();
                }
                else
                {
                    _isCabinStay = false;
                    Cancel("非舱内世界");
                }
            }
            catch (Exception ex)
            {
                LogUtils.LogError("CabinStayReactable", $"世界变更处理失败: {ex.Message}");
            }
        }

        private bool IsRocketCabinWorld(WorldContainer world)
        {
            // 优先使用属性判断
            if (_worldIsModuleInteriorProp.Value != null)
            {
                return (bool)_worldIsModuleInteriorProp.Value.GetValue(world);
            }

            // 兜底判定
            return !string.IsNullOrEmpty(world.name) &&
                   (world.name.Contains("Rocket") || world.name.Contains("Module"));
        }
        #endregion

        #region 核心响应逻辑
        private void Activate()
        {
            if (_isActive || _minion == null) return;
            _isActive = true;

            try
            {
                CleanupOldWorldTasks();
                SetCabinStayTarget();
                SyncMinionWorldState();

                LogUtils.LogDebug("CabinStayReactable",
                    $"小人[{_minion.GetProperName()}]舱内响应激活 | 世界ID：{_targetCabinWorldId}");
            }
            catch (Exception ex)
            {
                LogUtils.LogError("CabinStayReactable", $"激活失败: {ex.Message}");
                _isActive = false;
            }
        }

        private void Cancel(string reason)
        {
            if (!_isActive) return;
            _isActive = false;

            try
            {
                ResetNavigator();
                LogUtils.LogDebug("CabinStayReactable",
                    $"小人[{_minion.GetProperName()}]舱内响应取消：{reason}");
            }
            catch (Exception ex)
            {
                LogUtils.LogError("CabinStayReactable", $"取消失败: {ex.Message}");
            }
            finally
            {
                _isCabinStay = false;
            }
        }
        #endregion

        #region 适配方法
        private void CleanupOldWorldTasks()
        {
            if (_minion == null) return;

            MinionBrain brain = _minion.GetComponent<MinionBrain>();
            if (brain == null) return;

            // 取消当前任务
            if (_minionBrainCurrentChoreField.Value != null)
            {
                object currentChore = _minionBrainCurrentChoreField.Value.GetValue(brain);
                if (currentChore != null && _choreCancelMethod.Value != null)
                {
                    _choreCancelMethod.Value.Invoke(currentChore, new object[] { "进入舱内世界，取消原任务" });
                    LogUtils.LogDebug("CabinStayReactable", $"小人[{_minion.GetProperName()}]取消当前任务");
                }
            }

            // 设置Idle状态
            _minionBrainSetIdleMethod.Value?.Invoke(brain, null);

            // 清空任务队列
            try
            {
                FieldInfo choreQueueField = brain.GetType().GetField("choreQueue", BindingFlags.NonPublic | BindingFlags.Instance);
                if (choreQueueField != null)
                {
                    object choreQueue = choreQueueField.GetValue(brain);
                    choreQueue?.GetType().GetMethod("Clear")?.Invoke(choreQueue, null);
                    LogUtils.LogDebug("CabinStayReactable", $"小人[{_minion.GetProperName()}]清空任务队列");
                }
            }
            catch (Exception ex)
            {
                LogUtils.LogWarning("CabinStayReactable", $"清空任务队列失败: {ex.Message}");
            }
        }

        private void SetCabinStayTarget()
        {
            if (_minion == null) return;

            try
            {
                Type rocketPassengerMonitorType = Type.GetType("RocketPassengerMonitor, Assembly-CSharp");
                if (rocketPassengerMonitorType == null)
                {
                    LogUtils.LogWarning("CabinStayReactable", "未找到RocketPassengerMonitor类型");
                    return;
                }

                if (_stateMachineGetSMIMethod.Value == null)
                {
                    LogUtils.LogWarning("CabinStayReactable", "未找到GetSMI方法");
                    return;
                }

                object smi = _stateMachineGetSMIMethod.Value
                    .MakeGenericMethod(rocketPassengerMonitorType)
                    .Invoke(_minion, null);

                if (smi == null)
                {
                    LogUtils.LogWarning("CabinStayReactable", "获取RocketPassengerMonitor实例失败");
                    return;
                }

                int cabinCell = Grid.PosToCell(_minion.transform.position);
                MethodInfo setMoveTargetMethod = rocketPassengerMonitorType.GetMethod("SetMoveTarget", new[] { typeof(int) });
                setMoveTargetMethod?.Invoke(smi, new object[] { cabinCell });
            }
            catch (Exception ex)
            {
                LogUtils.LogError("CabinStayReactable", $"设置舱内目标失败: {ex.Message}");
            }
        }

        private void SyncMinionWorldState()
        {
            if (_minion == null) return;

            WorldContainer cabinWorld = ClusterManager.Instance.GetWorld(_targetCabinWorldId);
            if (cabinWorld == null) return;

            // 挂载到舱内世界容器
            _minion.transform.SetParent(cabinWorld.transform);

            // 验证位置是否在舱内世界
            Vector3 minionPos = _minion.transform.position;
            if (!IsPositionInWorld(minionPos, cabinWorld))
            {
                Vector3 cabinPos = GetValidCabinPosition(cabinWorld);
                _minion.transform.position = cabinPos;
            }
        }

        private void ResetNavigator()
        {
            if (_minion == null) return;

            Navigator navigator = _minion.GetComponent<Navigator>();
            if (navigator == null) return;

            // 重置导航状态
            navigator.enabled = false;
            navigator.enabled = true;

            // 清空路径数据
            try
            {
                FieldInfo pathField = typeof(Navigator).GetField("path", BindingFlags.NonPublic | BindingFlags.Instance);
                pathField?.SetValue(navigator, null);
            }
            catch (Exception ex)
            {
                LogUtils.LogWarning("CabinStayReactable", $"重置导航路径失败: {ex.Message}");
            }
        }

        private bool IsPositionInWorld(Vector3 pos, WorldContainer world)
        {
            int cell = Grid.PosToCell(pos);
            if (_gridWorldIdxProp.Value != null)
            {
                try
                {
                    int[] worldIdxArray = (int[])_gridWorldIdxProp.Value.GetValue(null, null);
                    return worldIdxArray != null && cell >= 0 && cell < worldIdxArray.Length &&
                           worldIdxArray[cell] == world.id;
                }
                catch (Exception ex)
                {
                    LogUtils.LogWarning("CabinStayReactable", $"位置验证失败: {ex.Message}");
                }
            }
            return false;
        }

        private Vector3 GetValidCabinPosition(WorldContainer cabinWorld)
        {
            try
            {
                if (_worldGetRandomCellMethod.Value != null)
                {
                    int cell = (int)_worldGetRandomCellMethod.Value.Invoke(cabinWorld, null);
                    return Grid.CellToPos(cell);
                }
            }
            catch (Exception ex)
            {
                LogUtils.LogWarning("CabinStayReactable", $"获取随机位置失败: {ex.Message}");
            }

            // 兜底位置
            return cabinWorld.transform.position + new Vector3(1, 1, 0);
        }
        #endregion

        #region 全局初始化封装
        /// <summary>
        /// 初始化舱内响应系统（封装到CabinStayReactable内部）
        /// </summary>
        public static void InitializeCabinReactableSystem()
        {
            try
            {
                // 注册游戏退出时的清理逻辑
                Application.quitting += CabinReactableRegistrar.Cleanup;

                // 手动创建延迟初始化对象（替代PUtil.RegisterPostload）
                GameObject delayObj = new GameObject("CabinReactable_DelayInit");
                delayObj.hideFlags = HideFlags.HideAndDontSave; // 隐藏临时对象
                DelayInitComponent delayComp = delayObj.AddComponent<DelayInitComponent>();
                // 修复：移除Action委托，直接传递初始化标记
                delayComp.StartDelayInit();

                LogUtils.LogDebug("CabinStayReactable", "舱内响应系统初始化任务已注册，将在游戏就绪后执行");
            }
            catch (Exception ex)
            {
                LogUtils.LogError("CabinStayReactable", $"注册舱内响应系统失败: {ex.Message}");
            }
        }
        #endregion

        #region 内部延迟初始化组件（替代PUtil.RegisterPostload）
        private class DelayInitComponent : KMonoBehaviour
        {
            private int _maxWaitFrames = 120; // 最大等待2秒

            // 修复：移除Action参数，直接在内部调用初始化方法
            public void StartDelayInit()
            {
                StartCoroutine(DelayInitCoroutine());
            }

            private IEnumerator DelayInitCoroutine()
            {
                int waitFrames = 0;
                while (Game.Instance == null && waitFrames < _maxWaitFrames)
                {
                    waitFrames++;
                    yield return null; // 等待1帧
                }

                // 执行初始化（直接调用，无委托）
                if (Game.Instance != null)
                {
                    CabinReactableRegistrar.Initialize();
                    LogUtils.LogDebug("CabinStayReactable", "舱内响应系统已激活");
                }
                else
                {
                    LogUtils.LogWarning("CabinStayReactable", "游戏实例尚未就绪，无法初始化舱内响应系统");
                }

                // 销毁临时对象
                Destroy(gameObject);
            }
        }
        #endregion

        #region 内部注册器类
        /// <summary>
        /// 舱内响应组件注册器，负责组件的自动注册与清理
        /// </summary>
        private static class CabinReactableRegistrar
        {
            private static bool _isInitialized = false;

            /// <summary>
            /// 初始化注册器，为所有小人添加响应组件
            /// </summary>
            public static void Initialize()
            {
                if (_isInitialized) return;

                try
                {
                    // 检查Game实例是否存在
                    if (Game.Instance == null)
                    {
                        LogUtils.LogError("CabinReactableRegistrar", "Initialize failed: Game.Instance is null");
                        return;
                    }

                    // 为现有小人添加组件
                    foreach (var minion in UnityEngine.Object.FindObjectsOfType<MinionIdentity>())
                    {
                        if (minion != null && minion.GetComponent<CabinStayReactable>() == null)
                        {
                            minion.gameObject.AddComponent<CabinStayReactable>();
                            LogUtils.LogDebug("CabinReactableRegistrar",
                                $"为小人[{minion.GetProperName()}]手动注册舱内停留响应");
                        }
                    }

                    // 监听新小人生成
                    Game.Instance.Subscribe((int)GameHashes.MinionSpawned, OnMinionSpawned);
                    _isInitialized = true;
                    LogUtils.LogDebug("CabinReactableRegistrar", "舱内响应注册器初始化完成");
                }
                catch (Exception ex)
                {
                    LogUtils.LogError("CabinReactableRegistrar", $"初始化失败: {ex.Message}");
                }
            }

            private static void OnMinionSpawned(object data)
            {
                try
                {
                    // 双重空值检查
                    if (data is MinionIdentity newMinion && newMinion != null &&
                        newMinion.GetComponent<CabinStayReactable>() == null)
                    {
                        newMinion.gameObject.AddComponent<CabinStayReactable>();
                        LogUtils.LogDebug("CabinReactableRegistrar",
                            $"为新小人[{newMinion.GetProperName()}]注册舱内停留响应");
                    }
                }
                catch (Exception ex)
                {
                    LogUtils.LogError("CabinReactableRegistrar", $"处理新小人生成失败: {ex.Message}");
                }
            }

            /// <summary>
            /// 清理注册器资源
            /// </summary>
            public static void Cleanup()
            {
                if (!_isInitialized) return;

                try
                {
                    // 取消订阅时检查Game实例是否存在
                    if (Game.Instance != null)
                        Game.Instance.Unsubscribe((int)GameHashes.MinionSpawned, OnMinionSpawned);
                    _isInitialized = false;
                    LogUtils.LogDebug("CabinReactableRegistrar", "舱内响应注册器已清理");
                }
                catch (Exception ex)
                {
                    LogUtils.LogError("CabinReactableRegistrar", $"清理失败: {ex.Message}");
                }
            }
        }
        #endregion
    }
}