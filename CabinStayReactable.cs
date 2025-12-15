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
    public class CabinStayReactable : ModReactableComponent
    {
        protected override string ModuleName => "CabinStayReactable";
        private MinionIdentity _minion;
        private int _targetCabinWorldId = -1;
        private bool _isActive = false;
        private bool _isCabinStay = false;


        private TeleportSuitTank _teleportSuitTank; // 联动核心：获取同体的 TeleportSuitTank
        private bool _isSubscribed;

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
        protected override void OnSpawn()
        {
            LogDebug("OnSpawn");
            base.OnSpawn();

            // 延迟0.1秒（确保 TeleportSuitTank 已完成自身初始化），然后推送实例
            GameScheduler.Instance.Schedule("PushInstanceToTank", 0.1f, (obj)=>
            {
                // 获取同体的 TeleportSuitTank（此时对方已初始化）
                _teleportSuitTank = GetComponent<TeleportSuitTank>();
                if (_teleportSuitTank == null)
                {
                    LogUtils.LogError(ModuleName, "未找到 TeleportSuitTank，无法推送实例");
                    Destroy(this);
                    return;
                }

                // 推送自身实例给对方
                _teleportSuitTank.AcceptCabinReactableInstance(this);
                LogDebug($"已推送自身实例给 TeleportSuitTank（自身ID：{this.GetInstanceID()}）");

                // 1. 从 TeleportSuitTank 共享穿戴者（复用已有状态，避免重复查找）
                _minion = _teleportSuitTank._ownerMinion;
                if (_minion == null)
                {
                    LogDebug( "当前无穿戴者，延迟重试初始化");
                    // 延迟重试（适配 TeleportSuitTank 穿戴者初始化稍晚的情况）
                    GameScheduler.Instance.Schedule("RetryInit", 0.2f, RetryInit);
                    return;
                }

                // 初始化核心：获取同体的 TeleportSuitTank（共享状态，无需重复获取小人/装备）
                _teleportSuitTank = GetComponent<TeleportSuitTank>();
                if (_teleportSuitTank == null)
                {
                    LogDebug( "未找到 TeleportSuitTank 组件，初始化失败");
                    Destroy(this);
                    return;
                }

                // 2. 订阅 ActiveWorldChanged 事件（核心初始化逻辑）
                SubscribeWorldChangedEvent();
                LogDebug( $"初始化完成，已联动 TeleportSuitTank（穿戴者：{_minion.GetProperName()}）");
            });
        }

        // 延迟重试初始化（适配时序差异）
        private void RetryInit(object data)
        {
            if (_teleportSuitTank == null) return;
            _minion = _teleportSuitTank._ownerMinion;
            _teleportSuitTank.AcceptCabinReactableInstance(this);
            if (_minion == null)
            {
                LogWarning("重试后仍无穿戴者，初始化失败");
                CleanUp();
                return;
            }
            SubscribeWorldChangedEvent();
            LogDebug( "重试初始化成功");
        }
        protected override void OnCleanUp()
        {
            CleanUp();
            base.OnCleanUp();
        }
        #endregion
        private void CleanUp()
        {
            if (_isSubscribed && _minion != null)
            {
                _minion.Unsubscribe((int)GameHashes.ActiveWorldChanged, OnMinionWorldChanged);
                _isSubscribed = false;
            }
            Destroy(this);
        }
        public void SubscribeWorldChangedEvent()
        {

            if (_minion == null || _isSubscribed) return;
            _minion.Subscribe((int)GameHashes.ActiveWorldChanged, OnMinionWorldChanged);
            LogDebug( $"小人[{_minion.GetProperName()}]已订阅世界变更事件");
            _isSubscribed = true;

        }

        #region 核心事件响应
        private void OnMinionWorldChanged(object data)
        {
            LogDebug("ActiveWorldChanged事件触发 OnMinionWorldChanged");
            if (data == null || _minion == null || _teleportSuitTank == null) return;

            try
            {
                int newWorldId = Convert.ToInt32(data);
                _targetCabinWorldId = newWorldId;

                WorldContainer newWorld = ClusterManager.Instance.GetWorld(newWorldId);
                if (newWorld != null && IsRocketCabinWorld(newWorld))
                {
                    _isCabinStay = true;
                    // 检查传送服状态，如果电池耗尽则可能限制某些功能
                    if (_teleportSuitTank.IsEmpty())
                    {
                        LogDebug( "传送服电池耗尽，限制舱内操作");
                    }
                    LogDebug("开始清理旧世界任务");
                    Activate();
                    LogDebug( $"世界变更，联动通知 TeleportSuitTank（世界ID：{newWorld.id}）");
                    // 联动：触发 TeleportSuitTank 的公开方法或事件
                    //_teleportSuitTank.HandleWorldChanged(_minion, newWorld.id);
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

        // 穿戴者变化时，从 TeleportSuitTank 同步更新（联动核心方法）
        // CabinStayReactable.cs 强化状态同步
        public void SyncWearer(MinionIdentity newMinion)
        {
            // 清理旧状态
            if (_minion != null)
            {
                if (_isSubscribed)
                {
                    _minion.Unsubscribe((int)GameHashes.ActiveWorldChanged, OnMinionWorldChanged);
                    _isSubscribed = false;
                }
                // 取消旧穿戴者的舱内状态
                if (_isActive)
                {
                    Cancel("穿戴者切换");
                }
            }

            // 同步新状态
            _minion = newMinion;
            if (_minion != null)
            {
                SubscribeWorldChangedEvent();
                LogDebug($"已同步新穿戴者：{_minion.GetProperName()}");
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
                LogDebug("CleanupOldWorldTasks");
                CleanupOldWorldTasks();
                LogDebug("SetCabinStayTarget");
                SetCabinStayTarget();
                LogDebug("SyncMinionWorldState");
                SyncMinionWorldState();

                LogDebug($"小人[{_minion.GetProperName()}]舱内响应激活 | 世界ID：{_targetCabinWorldId}");
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
                LogDebug(
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
                    LogDebug( $"小人[{_minion.GetProperName()}]取消当前任务");
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
                    LogDebug( $"小人[{_minion.GetProperName()}]清空任务队列");
                }
            }
            catch (Exception ex)
            {
                LogDebug( $"清空任务队列失败: {ex.Message}");
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
                    LogDebug( "未找到RocketPassengerMonitor类型");
                    return;
                }

                if (_stateMachineGetSMIMethod.Value == null)
                {
                    LogDebug( "未找到GetSMI方法");
                    return;
                }

                object smi = _stateMachineGetSMIMethod.Value
                    .MakeGenericMethod(rocketPassengerMonitorType)
                    .Invoke(_minion, null);

                if (smi == null)
                {
                    LogDebug( "获取RocketPassengerMonitor实例失败");
                    return;
                }

                int cabinCell = Grid.PosToCell(_minion.transform.position);
                MethodInfo setMoveTargetMethod = rocketPassengerMonitorType.GetMethod("SetMoveTarget", new[] { typeof(int) });
                setMoveTargetMethod?.Invoke(smi, new object[] { cabinCell });
            }
            catch (Exception ex)
            {
                LogError($"设置舱内目标失败: {ex.Message}");
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
                LogDebug( $"重置导航路径失败: {ex.Message}");
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
                    LogDebug( $"位置验证失败: {ex.Message}");
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
                LogDebug( $"获取随机位置失败: {ex.Message}");
            }

            // 兜底位置
            return cabinWorld.transform.position + new Vector3(1, 1, 0);
        }
        #endregion

    }
}