using HarmonyLib;
using Klei.AI;
using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Operational;
using static STRINGS.UI.UISIDESCREENS.AUTOPLUMBERSIDESCREEN.BUTTONS;

namespace TeleportSuitMod
{
    [SerializationConfig(MemberSerialization.OptIn)]
    [AddComponentMenu("TeleportSuitMod/RocketCabinRestriction")]
    [RequireComponent(typeof(KPrefabID))]
    public class RocketCabinRestriction : KMonoBehaviour, IRender1000ms
    {
        [Serialize]
        private Dictionary<int, CabinState> _cabinStateCache;
        [Serialize]
        private Dictionary<MinionIdentity, int> _minionToCabinMap;

        // 舱状态类
        private class CabinState
        {
            public bool IsSummoning { get; set; }
            public HashSet<MinionIdentity> AssignedCrew { get; set; }

            public CabinState(bool isSummoning, HashSet<MinionIdentity> crew)
            {
                IsSummoning = isSummoning;
                AssignedCrew = crew ?? new HashSet<MinionIdentity>();
            }
        }

        private static readonly string ModuleName = "RocketCabinRestriction";
        private const int InvalidWorldId = -1;
        private bool _isInitialized = false;
        private bool _isMarkingCrew = false;

        private static readonly object _lockObj = new object();
        private static RocketCabinRestriction _instance;
        public static RocketCabinRestriction Instance
        {
            get
            {
                lock (_lockObj)
                {
                    if (_instance != null && _instance.isActiveAndEnabled)
                    {
                        return _instance;
                    }
                    var existingInstances = FindObjectsOfType<RocketCabinRestriction>();
                    foreach (var instance in existingInstances)
                    {
                        if (instance.isActiveAndEnabled)
                        {
                            LogUtils.LogDebug(ModuleName, $"找到已激活的现有实例：{instance.gameObject.name}");
                            _instance = instance;
                            return _instance;
                        }
                    }

                    // 创建单例（激活状态）
                    var singletonObj = new GameObject("RocketCabinRestriction_Singleton");
                    singletonObj.SetActive(true);

                    var kPrefabID = singletonObj.AddComponent<KPrefabID>();
                    kPrefabID.PrefabTag = TagManager.Create("RocketCabinRestriction");

                    DontDestroyOnLoad(singletonObj);
                    _instance = singletonObj.AddComponent<RocketCabinRestriction>();

                    // 同步初始化
                    LogUtils.LogDebug(ModuleName, "开始执行同步初始化");
                    _instance.OnPrefabInit();
                    _instance.OnSpawn();

                    LogUtils.LogDebug(ModuleName, $"新单例创建完成：{singletonObj.name}，_isInitialized={_instance._isInitialized}");
                    return _instance;
                }
            }
            private set => _instance = value;
        }

        #region Klei生命周期
        protected override void OnPrefabInit()
        {
            try
            {
                base.OnPrefabInit();

                lock (_lockObj)
                {
                    if (_instance == null)
                    {
                        _instance = this;
                        DontDestroyOnLoad(gameObject);
                    }
                    else if (_instance != this)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }

                // 初始化字典
                if (_cabinStateCache == null)
                {
                    _cabinStateCache = new Dictionary<int, CabinState>();
                }
                if (_minionToCabinMap == null)
                {
                    _minionToCabinMap = new Dictionary<MinionIdentity, int>();
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnPrefabInit异常：{e.Message}\n{e.StackTrace}");
            }
        }

        protected override void OnSpawn()
        {
            try
            {
                base.OnSpawn();
                _isInitialized = true;
                RegisterUpdateHandlers();
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnSpawn异常：{e.Message}\n{e.StackTrace}");
                _isInitialized = false;
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
                base.OnCleanUp();

                lock (_lockObj)
                {
                    if (_instance == this)
                    {
                        _isInitialized = false;
                        if (_cabinStateCache != null)
                        {
                            _cabinStateCache.Clear();
                        }
                        if (_minionToCabinMap != null)
                        {
                            _minionToCabinMap.Clear();
                        }
                        _instance = null;
                    }
                }

            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnCleanUp异常：{e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

        #region 保护方法
        protected void RegisterUpdateHandlers()
        {
            try
            {
                if (gameObject.GetComponent<MinionUpdateChecker>() == null)
                {
                    gameObject.AddComponent<MinionUpdateChecker>();
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"RegisterUpdateHandlers异常：{e.Message}\n{e.StackTrace}");
            }
        }

        protected void OnMinionAdded(MinionIdentity minion)
        {
            if (!_isInitialized || minion == null)
            {
                return;
            }
            try
            {
                LogUtils.LogDebug(ModuleName, $"OnMinionAdded：检测到新小人：{minion.GetProperName()}");
                UpdateMinionCabinMapping(minion);
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnMinionAdded异常：{e.Message}\n{e.StackTrace}");
            }
        }

        protected void OnMinionRemoved(MinionIdentity minion)
        {
            if (!_isInitialized || minion == null)
            {
                return;
            }
            try
            {
                if (_minionToCabinMap != null)
                {
                    _minionToCabinMap.Remove(minion);
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnMinionRemoved异常：{e.Message}\n{e.StackTrace}");
            }
        }

        protected void UpdateMinionCabinMapping(MinionIdentity minion)
        {
            if (!_isInitialized || minion == null)
            {
                return;
            }
            try
            {
                int currentWorldId = GetMinionCurrentWorldId(minion);
                LogUtils.LogDebug(ModuleName, $"UpdateMinionCabinMapping：小人{minion.GetProperName()}，当前世界ID={currentWorldId}");

                if (_cabinStateCache != null)
                {
                    foreach (var cabinEntry in _cabinStateCache)
                    {
                        if (cabinEntry.Key == currentWorldId && cabinEntry.Value.IsSummoning)
                        {
                            if (_minionToCabinMap != null)
                            {
                                _minionToCabinMap[minion] = currentWorldId;
                                LogUtils.LogDebug(ModuleName, $"UpdateMinionCabinMapping：标记小人{minion.GetProperName()}到舱{currentWorldId}");
                            }
                            cabinEntry.Value.AssignedCrew.Add(minion);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"UpdateMinionCabinMapping异常：{e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

        #region IRender1000ms实现
        public void Render1000ms(float dt)
        {
            if (!_isInitialized)
            {
                LogUtils.LogWarning(ModuleName, "Render1000ms：实例未完成初始化");
            }
        }
        #endregion

        #region 核心判断方法
        public static bool QuickCheckBlockTeleport(MinionIdentity minion, int targetWorldId)
        {
            try
            {
                if (Instance == null || !Instance._isInitialized || minion == null)
                {
                    return false;
                }

                Instance.EnsureCacheInitialized();

                int cabinWorldId = InvalidWorldId;
                if (Instance._minionToCabinMap != null && Instance._minionToCabinMap.TryGetValue(minion, out cabinWorldId))
                {
                    //LogUtils.LogDebug(ModuleName, $"小人{minion.GetProperName()}的舱世界ID={cabinWorldId}");
                    if (cabinWorldId == InvalidWorldId || targetWorldId == cabinWorldId)
                    {
                        LogUtils.LogDebug(ModuleName, $"舱世界ID无效、或 非跨界传送 [{cabinWorldId}] To[{targetWorldId}]");
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                CabinState cabinState = null;
                if (Instance._cabinStateCache != null && Instance._cabinStateCache.TryGetValue(cabinWorldId, out cabinState))
                {
                    if (!cabinState.IsSummoning)
                    {
                        LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]未召集");
                        return false;
                    }
                }
                else
                {
                    LogUtils.LogDebug(ModuleName, $"未找到舱[{cabinWorldId}]的状态");
                    return false;
                }

                if (!cabinState.AssignedCrew.Contains(minion))
                {
                    LogUtils.LogDebug(ModuleName, $"小人[{minion.GetProperName()}]不在舱[{cabinWorldId}]的召集列表");
                    return false;
                }

                //LogUtils.LogDebug(ModuleName, $"阻断传送 [{cabinWorldId}] To [{targetWorldId}]");
                return true;
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"QuickCheckBlockTeleport异常：{e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        #endregion

        #region 缓存安全检查
        private void EnsureCacheInitialized()
        {
            if (_cabinStateCache == null)
            {
                _cabinStateCache = new Dictionary<int, CabinState>();
            }
            if (_minionToCabinMap == null)
            {
                _minionToCabinMap = new Dictionary<MinionIdentity, int>();
            }
        }
        #endregion

        #region 舱状态管理
        public void UpdateCabinSummonState(PassengerRocketModule canbinModule,int cabinWorldId, bool isSummoning)
        {
            if (!_isInitialized || cabinWorldId == InvalidWorldId)
            {
                LogUtils.LogDebug(ModuleName, $"UpdateCabinSummonState跳过：_isInitialized={_isInitialized} / cabinWorldId={cabinWorldId}");
                return;
            }
            try
            {
                EnsureCacheInitialized();

                if (_cabinStateCache.ContainsKey(cabinWorldId))
                {
                    var currentState = _cabinStateCache[cabinWorldId];
                    currentState.IsSummoning = isSummoning;
                    LogUtils.LogDebug(ModuleName, $"更新舱[{cabinWorldId}]的所有Crew isSummoning:{currentState.IsSummoning}");
                }
                else
                {
                    _cabinStateCache.Add(cabinWorldId, new CabinState(isSummoning, new HashSet<MinionIdentity>()));
                }
                LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]的状态IsSummoning：{isSummoning}");
                if (!isSummoning){
                    LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]的Notice所有Crew");
                    Instance.NoticeCrewOfCabin(cabinWorldId);
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"UpdateCabinSummonState异常：{e.Message}\n{e.StackTrace}");
            }
        }
        #endregion
        private void NoticeCrewOfCabin(int cabinWorldId)
        {
            if (!_cabinStateCache.ContainsKey(cabinWorldId))
            {
                LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]Assigned Crew列表为空,Notice失败");
                return;
            }
            LogUtils.LogDebug(ModuleName, $"AssignedCrew Count: {_cabinStateCache[cabinWorldId].AssignedCrew.Count}");
            foreach (var m in _cabinStateCache[cabinWorldId].AssignedCrew)
            {
                MinionIdentity mm = m as MinionIdentity;
                if (mm == null)
                {
                    LogUtils.LogWarning(ModuleName, $"AssignedCrew中存在非MinionIdentity对象：{m}");
                    continue;
                }

                TeleportSuitTank suitTank = Utils.GetMinionTeleportSuit(mm);
                if (suitTank == null)
                {
                    // 仅当mm无效时，才尝试通过InstanceID恢复
                    if (mm.gameObject == null || !mm.gameObject.activeInHierarchy)
                    {
                        mm = Utils.restoreMinionByInstanceID(m.GetInstanceID());
                        // 恢复后再次校验
                        if (mm == null)
                        {
                            LogUtils.LogWarning(ModuleName, $"无法恢复InstanceID[{m.GetInstanceID()}]的小人");
                            continue;
                        }
                        suitTank = Utils.GetMinionTeleportSuit(mm);
                    }
                }

                if (suitTank != null)
                {
                    LogUtils.LogDebug(ModuleName, $"重置小人权限[{mm.GetProperName()}] Summoning: [{_cabinStateCache[cabinWorldId].IsSummoning}]");
                    suitTank.HandleRocketEnterChore(mm);
                }
                else
                {
                    LogUtils.LogDebug(ModuleName, $"[{mm.GetProperName()}] 未找到有效传送服组件");
                }
            }
        }
        #region 船员标记
        public static void MarkCrewForCabin(PassengerRocketModule passengerModule)
        {
            // 前置判断
            if (Instance == null|| !Instance._isInitialized || passengerModule == null || Instance._isMarkingCrew) return;

            Instance._isMarkingCrew = true;
            try
            {
                Instance.EnsureCacheInitialized();

                // 获取舱世界ID
                int cabinWorldId = Instance.GetCabinWorldIdSafely(passengerModule);
                if (cabinWorldId == InvalidWorldId)
                {
                    LogUtils.LogError(ModuleName, "舱世界ID无效，返回");
                    Instance._isMarkingCrew = false;
                    return;
                }

                // ===== 兼容优化：处理member无gameObject的场景 =====
                List<MinionIdentity> assignedMinions = new List<MinionIdentity>();

                // 获取当前舱的AssignmentGroupController
                AssignmentGroupController agc = passengerModule.GetComponent<AssignmentGroupController>();
                if (agc == null)
                {
                    LogUtils.LogWarning(ModuleName, "当前舱无AssignmentGroupController组件，终止操作");
                    Instance._isMarkingCrew = false;
                    return;
                }

                // 获取当前舱的分配组
                if (!Game.Instance.assignmentManager.assignment_groups.TryGetValue(agc.AssignmentGroupID, out AssignmentGroup group))
                {
                    LogUtils.LogWarning(ModuleName, $"未找到舱[{passengerModule.name}]的分配组（ID：{agc.AssignmentGroupID}），终止操作");
                    Instance._isMarkingCrew = false;
                    return;
                }
                for (int i = 0; i < Components.LiveMinionIdentities.Count; i++)
                {
                    MinionIdentity m =  Components.LiveMinionIdentities[i];
                    if (Game.Instance.assignmentManager.assignment_groups[passengerModule.GetComponent<AssignmentGroupController>().AssignmentGroupID].HasMember(m.assignableProxy.Get()))
                    {
                        assignedMinions.Add(m);
                        LogUtils.LogDebug(ModuleName, $"匹配到分配给舱[{passengerModule.name} {cabinWorldId}]的小人[{m.GetProperName()}]");
                    }
                }


                // 无分配小人直接终止
                if (assignedMinions.Count == 0)
                {
                    LogUtils.LogWarning(ModuleName, "未找到任何分配给当前太空员舱的有效小人，终止操作");
                    Instance._isMarkingCrew = false;
                    return;
                }

                // 打印分配的船员名单
                string crewList = string.Join(", ", assignedMinions.Select(m => m.GetProperName()));
                LogUtils.LogDebug(ModuleName, $"分配给当前太空员舱的有效船员名单：{crewList}");

                // 标记分配的小人
                Instance.InternalMarkCrewForCabin(cabinWorldId, assignedMinions);

                // 最终标记结果
                if (Instance._cabinStateCache.ContainsKey(cabinWorldId))
                {
                    var cabinState = Instance._cabinStateCache[cabinWorldId];
                    string finalCrewList = cabinState.AssignedCrew.Count > 0
                        ? string.Join(", ", cabinState.AssignedCrew)
                        : "无";
                    LogUtils.LogDebug(ModuleName, $"舱{cabinWorldId}最终标记的船员名单：{finalCrewList}");
                }

            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"MarkCrewForCabin异常：{e.Message}\n{e.StackTrace}");
            }
            finally
            {
                Instance._isMarkingCrew = false;
            }
        }
        /// <summary>
        /// 获取游戏内所有有效小人（无组件依赖）
        /// </summary>
        private List<MinionIdentity> GetAllValidMinionsInGame()
        {
            var validMinions = new List<MinionIdentity>();
            try
            {

                // 直接遍历所有MinionIdentity组件
                var allMinionComps = UnityEngine.Object.FindObjectsOfType<MinionIdentity>();
                foreach (var minion in allMinionComps)
                {
                    // 过滤有效小人：激活、有名称（移除IsDead()判断）
                    if (minion == null || !minion.isActiveAndEnabled)
                    {
                        LogUtils.LogDebug(ModuleName, $"跳过非激活小人");
                        continue;
                    }

                    string minionName = minion.GetProperName();
                    if (string.IsNullOrEmpty(minionName))
                    {
                        LogUtils.LogDebug(ModuleName, "跳过无名称小人");
                        continue;
                    }

                    validMinions.Add(minion);
                    LogUtils.LogDebug(ModuleName, $"添加有效小人{minionName}");
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"GetAllValidMinionsInGame异常：{e.Message}\n{e.StackTrace}");
            }
            return validMinions;
        }

        /// <summary>
        /// 筛选归属当前火箭/距离近的小人（无组件依赖）
        /// </summary>
        private List<MinionIdentity> FilterMinionsForRocket(PassengerRocketModule passengerModule, List<MinionIdentity> allMinions)
        {
            var filteredMinions = new List<MinionIdentity>();
            try
            {

                // 获取火箭位置
                Vector3 rocketPos = passengerModule.transform.position;
                LogUtils.LogDebug(ModuleName, $"火箭位置={rocketPos}");

                // 遍历所有小人，按距离筛选（50米内）
                foreach (var minion in allMinions)
                {
                    if (minion == null) continue;

                    float distance = Vector3.Distance(rocketPos, minion.transform.position);
                    LogUtils.LogDebug(ModuleName, $"小人{minion.GetProperName()}，距离={distance}");

                    // 扩大到50米，确保能覆盖
                    if (distance <= 50f)
                    {
                        filteredMinions.Add(minion);
                        LogUtils.LogDebug(ModuleName, $"添加小人{minion.GetProperName()}（距离{distance}米）");
                    }
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"FilterMinionsForRocket异常：{e.Message}\n{e.StackTrace}");
            }
            return filteredMinions;
        }

        private int GetCabinWorldIdSafely(PassengerRocketModule passengerModule)
        {
            try
            {
                var worldContainer = passengerModule.GetComponent<ClustercraftExteriorDoor>().GetTargetWorld();
                if (worldContainer != null)
                {
                    int worldId = worldContainer.id;
                    return worldId;
                }
                    return InvalidWorldId;
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"GetCabinWorldIdSafely异常：{e.Message}\n{e.StackTrace}");
                return InvalidWorldId;
            }
        }

        private void InternalMarkCrewForCabin(int cabinWorldId, List<MinionIdentity> minions)
        {
            // 前置判断
            if (cabinWorldId == InvalidWorldId)
            {
                LogUtils.LogError(ModuleName, "舱世界ID无效，返回");
                return;
            }

            if (minions == null || minions.Count == 0)
            {
                LogUtils.LogWarning(ModuleName, "小人列表为空，返回");
                return;
            }

            try
            {
                // 新增舱状态
                if (!_cabinStateCache.ContainsKey(cabinWorldId))
                {
                    _cabinStateCache.Add(cabinWorldId, new CabinState(false, new HashSet<MinionIdentity>()));
                    LogUtils.LogDebug(ModuleName, $"新增舱[{cabinWorldId}]记录");
                }

                var cabinState = _cabinStateCache[cabinWorldId];
                LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]当前召集列表数量[{cabinState.AssignedCrew.Count}]");

                // 遍历小人标记
                foreach (var minion in minions)
                {
                    if (minion == null) continue;

                    cabinState.AssignedCrew.Add(minion);

                    if (_minionToCabinMap != null)
                    {
                        _minionToCabinMap[minion] = cabinWorldId;
                    }

                    LogUtils.LogDebug(ModuleName, $"标记小人[{minion.GetProperName()}]到舱[{cabinWorldId}]");
                }

                LogUtils.LogDebug(ModuleName, $"设置舱[{cabinWorldId}]的IsSummoning为[{cabinState.IsSummoning}]");
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"InternalMarkCrewForCabin异常：{e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

        #region 辅助方法
        public static int GetMinionCurrentWorldId(MinionIdentity minion)
        {
            if (minion == null)
            {
                return InvalidWorldId;
            }
            try
            {
                int cell = Grid.PosToCell(minion.gameObject);

                bool isValid = Grid.IsValidCell(cell);

                if (isValid)
                {
                    int worldId = Grid.WorldIdx[cell];
                    return worldId;
                }
                else
                {
                    return InvalidWorldId;
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"GetMinionCurrentWorldId异常：{e.Message}\n{e.StackTrace}");
                return InvalidWorldId;
            }
        }
        #endregion

        #region 小人更新检查器
        private class MinionUpdateChecker : MonoBehaviour
        {
            private float _checkInterval = 5f;
            private float _lastCheckTime;
            private HashSet<MinionIdentity> _knownMinions = new HashSet<MinionIdentity>();

            private void Update()
            {
                if (Time.time - _lastCheckTime < _checkInterval) return;

                _lastCheckTime = Time.time;
                try
                {
                    CheckMinionChanges();
                }
                catch (Exception e)
                {
                    LogUtils.LogError(ModuleName, $"MinionUpdateChecker异常：{e.Message}\n{e.StackTrace}");
                }
            }

            private void CheckMinionChanges()
            {
                var allMinions = UnityEngine.Object.FindObjectsOfType<MinionIdentity>()
                    .Where(m => m != null)
                    .Take(50);
                LogUtils.LogDebug(ModuleName, $"找到{allMinions.Count()}个小人");

                HashSet<MinionIdentity> currentMinions = new HashSet<MinionIdentity>();
                foreach (var minion in allMinions)
                {
                    currentMinions.Add(minion);
                }

                // 新增小人
                foreach (var mm in currentMinions)
                {
                    if (!_knownMinions.Contains(mm))
                    {
                        var minion = allMinions.FirstOrDefault(m => m == mm);
                        LogUtils.LogDebug(ModuleName, $"新增小人{name}");
                        if (RocketCabinRestriction.Instance != null)
                        {
                            RocketCabinRestriction.Instance.OnMinionAdded(minion);
                        }
                    }
                }

                // 移除小人
                foreach (var name in _knownMinions)
                {
                    if (!currentMinions.Contains(name) && RocketCabinRestriction.Instance != null)
                    {
                        LogUtils.LogDebug(ModuleName, $"移除小人{name}");
                        RocketCabinRestriction.Instance.OnMinionRemoved(name);
                    }
                }

                _knownMinions = currentMinions;
            }
        }
        #endregion
    }
}