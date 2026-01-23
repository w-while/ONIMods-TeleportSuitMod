using HarmonyLib;
using Klei.AI;
using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TeleportSuitMod
{
    public class RocketCabinRestriction : KMonoBehaviour, ITeleportBlocker
    {
        [Serialize]
        private Dictionary<int, CabinState> _cabinStateCache = new Dictionary<int, CabinState>();
        [Serialize]
        private Dictionary<Navigator, int> _navigatorToCabinMap = new Dictionary<Navigator, int>();

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

        private static readonly object _lockObj = new object();
        private static RocketCabinRestriction _instance;
        public static RocketCabinRestriction Instance
        {
            get
            {
                lock (_lockObj)
                {
                    if (_instance != null && _instance.isActiveAndEnabled) return _instance;

                    var existingInstances = UnityEngine.Object.FindObjectsOfType<RocketCabinRestriction>();
                    foreach (var instance in existingInstances)
                    {
                        if (instance.isActiveAndEnabled)
                        {
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
                    _instance.OnPrefabInit();
                    _instance.OnSpawn();

                    return _instance;
                }
            }
            private set => _instance = value;
        }

        #region Klei生命周期
        protected override void OnSpawn()
        {
            LogUtils.LogDebug(ModuleName, "RocketCabinRestriction OnSpawn");
            base.OnSpawn();
        }

        protected override void OnCleanUp()
        {
            LogUtils.LogDebug(ModuleName, "RocketCabinRestriction OncleanUP");
            base.OnCleanUp();
        }
        #endregion

        #region 保护方法

        // 示例：监听小人销毁事件时调用
        //minionIdentity.OnCleanUp += () => {
        //RocketCabinRestriction.Instance.OnMinionRemoved(minionIdentity);
        //};
        protected void OnMinionAdded(MinionIdentity minion)
        {
            if (minion == null)
            {
                return;
            }
            try
            {
                UpdateMinionCabinMapping(minion);
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnMinionAdded异常：{e.Message}\n{e.StackTrace}");
            }
        }
        // 示例：小人死亡时调用
        //if (minionIdentity.GetComponent<Health>().IsDead()) {
        //    RocketCabinRestriction.Instance.OnMinionRemoved(minionIdentity);
        //}
        protected void OnMinionRemoved(MinionIdentity minion)
        {
            if (minion == null)return;
            try
            {
                lock (_lockObj)
                {
                    // 1. 从映射字典移除小人
                    Navigator navigator = minion.gameObject.GetComponent<Navigator>();
                    if (navigator == null) return;
                    if (_navigatorToCabinMap != null && _navigatorToCabinMap.ContainsKey(navigator))
                    {
                        int cabinWorldId = _navigatorToCabinMap[navigator];
                        _navigatorToCabinMap.Remove(navigator);

                        // 2. 从对应舱的船员列表移除小人（关键补充：双端清理）
                        if (_cabinStateCache.TryGetValue(cabinWorldId, out var cabinState))
                        {
                            cabinState.AssignedCrew.Remove(minion);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"OnMinionRemoved异常：{e.Message}\n{e.StackTrace}");
            }
        }

        protected void UpdateMinionCabinMapping(MinionIdentity minion)
        {
            if (minion == null) return;
            try
            {
                lock (_lockObj)
                {
                    int currentWorldId = GetMinionCurrentWorldId(minion);

                    if (_cabinStateCache != null)
                    {
                        foreach (var cabinEntry in _cabinStateCache)
                        {
                            if (cabinEntry.Key == currentWorldId && cabinEntry.Value.IsSummoning)
                            {
                                // 绑定小人到该舱
                                Navigator navigator = minion.gameObject.GetComponent<Navigator>();
                                if(navigator != null) _navigatorToCabinMap[navigator] = currentWorldId;
                                cabinEntry.Value.AssignedCrew.Add(minion);
                                LogUtils.LogDebug(ModuleName, $"小人[{minion.GetProperName()}]绑定到舱世界ID：{currentWorldId}");
                                break;
                            }
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

        #region 核心判断方法
        public static bool IsMarkedCanbinWorld(int canbinWorldId)
        {
            if(Instance == null || Instance._cabinStateCache == null ) return false;
            return Instance._cabinStateCache.TryGetValue(canbinWorldId,out var cabinState) && cabinState.IsSummoning;
        }
        public static bool QuickCheckBlockTeleport(Navigator navigator, int canbinWorldId)
        {
            if(Instance == null || navigator == null) return false;

            int foundCabinWorldId = InvalidWorldId;
            if (Instance._navigatorToCabinMap != null && Instance._navigatorToCabinMap.TryGetValue(navigator, out foundCabinWorldId))
            {
                //缓存世界invalid or 目标世界是缓存的世界 ：不需要阻断传送
                if(foundCabinWorldId == InvalidWorldId || canbinWorldId == foundCabinWorldId) return false;
            }
            else
            {
                //没有缓存：不阻止传送
                return false;
            }

            //舱内世界是否发起召集请求
            CabinState cabinState = null;
            if (Instance._cabinStateCache != null && Instance._cabinStateCache.TryGetValue(foundCabinWorldId, out cabinState))
            {
                if (!cabinState.IsSummoning) return false;
            }
            else
            {
                return false;
            }
            return true;
        }
        #endregion

        #region 舱状态管理
        public void UpdateCabinSummonState(int cabinWorldId, bool isSummoning)
        {
            if (cabinWorldId == InvalidWorldId) { return; }
            try
            {
                lock (_lockObj)
                {
                    if (_cabinStateCache.ContainsKey(cabinWorldId))
                    {
                        var currentState = _cabinStateCache[cabinWorldId];
                        currentState.IsSummoning = isSummoning;

                        // 新增：取消召集时，清空该舱所有小人的映射关系
                        if (!isSummoning)
                        {
                            ClearCabinMinionMapping(cabinWorldId);
                        }
                    }
                    else
                    {
                        _cabinStateCache.Add(cabinWorldId, new CabinState(isSummoning, new HashSet<MinionIdentity>()));
                    }

                    LogUtils.LogDebug(ModuleName, $"舱世界ID[{cabinWorldId}]召集状态更新为：{isSummoning}");
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"UpdateCabinSummonState异常：{e.Message}\n{e.StackTrace}");
            }
        }

        // 新增：清空某舱的所有小人映射关系
        private void ClearCabinMinionMapping(int cabinWorldId)
        {
            if (cabinWorldId == InvalidWorldId) return;

            // 1. 获取该舱的船员列表
            if (!_cabinStateCache.TryGetValue(cabinWorldId, out var cabinState)) return;

            // 2. 遍历船员，移除映射关系
            var crewList = cabinState.AssignedCrew.ToList(); // 转List避免遍历中修改集合
            foreach (var minion in crewList)
            {
                Navigator navigator = minion.gameObject.GetComponent<Navigator>();

                if ( navigator!= null && _navigatorToCabinMap.ContainsKey(navigator))
                {
                    _navigatorToCabinMap.Remove(navigator);
                }
            }

            // 3. 清空舱的船员列表
            cabinState.AssignedCrew.Clear();
        }
        #endregion

        #region 船员标记
        public static void MarkCrewForCabin(PassengerRocketModule passengerModule)
        {
            // 前置判断
            if (Instance == null || passengerModule == null ) return;

            try
            {
                // 获取舱世界ID
                int cabinWorldId = Instance.GetCabinWorldIdSafely(passengerModule);
                if (cabinWorldId == InvalidWorldId)
                {
                    LogUtils.LogError(ModuleName, "舱世界ID无效，返回");
                    return;
                }

                // ===== 兼容优化：处理member无gameObject的场景 =====
                List<MinionIdentity> assignedMinions = new List<MinionIdentity>();

                // 获取当前舱的AssignmentGroupController
                AssignmentGroupController agc = passengerModule.GetComponent<AssignmentGroupController>();
                if (agc == null)
                {
                    LogUtils.LogWarning(ModuleName, "当前舱无AssignmentGroupController组件，终止操作");
                    return;
                }

                // 获取当前舱的分配组
                if (!Game.Instance.assignmentManager.assignment_groups.TryGetValue(agc.AssignmentGroupID, out AssignmentGroup group))
                {
                    LogUtils.LogWarning(ModuleName, $"未找到舱[{passengerModule.name}]的分配组（ID：{agc.AssignmentGroupID}），终止操作");
                    return;
                }

                // 遍历所有存活小人，筛选分配给该舱的
                for (int i = 0; i < Components.LiveMinionIdentities.Count; i++)
                {
                    MinionIdentity m = Components.LiveMinionIdentities[i];
                    if (Game.Instance.assignmentManager.assignment_groups[passengerModule.GetComponent<AssignmentGroupController>().AssignmentGroupID].HasMember(m.assignableProxy.Get()))
                    {
                        assignedMinions.Add(m);
                    }
                }

                // 无分配小人直接终止
                if (assignedMinions.Count == 0)
                {
                    LogUtils.LogWarning(ModuleName, "未找到任何分配给当前太空员舱的有效小人，终止操作");
                    return;
                }

                // 打印分配的船员名单
                string crewList = string.Join(", ", assignedMinions.Select(m => m.GetProperName()));
                LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]分配船员：{crewList}");

                // 标记分配的小人
                Instance.InternalMarkCrewForCabin(cabinWorldId, assignedMinions);

                // 最终标记结果
                if (Instance._cabinStateCache.ContainsKey(cabinWorldId))
                {
                    var cabinState = Instance._cabinStateCache[cabinWorldId];
                    string finalCrewList = cabinState.AssignedCrew.Count > 0
                        ? string.Join(", ", cabinState.AssignedCrew.Select(m => m.GetProperName()))
                        : "无";
                    LogUtils.LogDebug(ModuleName, $"舱[{cabinWorldId}]最终船员列表：{finalCrewList}");
                }

            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"MarkCrewForCabin异常：{e.Message}\n{e.StackTrace}");
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
                    // 过滤有效小人：激活、有名称
                    if (minion == null || !minion.isActiveAndEnabled) continue;

                    string minionName = minion.GetProperName();
                    if (string.IsNullOrEmpty(minionName)) continue;

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
                LogUtils.LogWarning(ModuleName, "舱世界ID无效，返回");
                return;
            }

            if (minions == null || minions.Count == 0)
            {
                LogUtils.LogWarning(ModuleName, "小人列表为空，返回");
                return;
            }

            try
            {
                lock (_lockObj)
                {
                    // 新增舱状态（若不存在）
                    if (!_cabinStateCache.ContainsKey(cabinWorldId))
                    {
                        _cabinStateCache.Add(cabinWorldId, new CabinState(false, new HashSet<MinionIdentity>()));
                    }

                    var cabinState = _cabinStateCache[cabinWorldId];

                    var list = new List<Navigator>();
                    foreach (var data in _navigatorToCabinMap)
                    {
                        if(data.Value == cabinWorldId) list.Add(data.Key);
                    }
                    foreach (var nav in list) {
                        _navigatorToCabinMap.Remove(nav);
                    }
                    cabinState.AssignedCrew.Clear();
                    // 遍历小人标记（核心：维护_minionToCabinMap）
                    foreach (var minion in minions)
                    {
                        if (minion == null) continue;

                        // 1. 添加到舱的船员列表
                        cabinState.AssignedCrew.Add(minion);

                        // 2. 绑定小人-舱映射（覆盖旧映射）
                        Navigator navigator = minion.gameObject.GetComponent<Navigator>();
                        if (_navigatorToCabinMap.ContainsKey(navigator)){
                            _navigatorToCabinMap[navigator] = cabinWorldId;
                            LogUtils.LogDebug(ModuleName,$"更新 乘员：[{minion.name}] Navigator:[{navigator.GetHashCode()}] canbinWorldId:[{cabinWorldId}]");
                        }
                        else
                        {
                            LogUtils.LogDebug(ModuleName, $"添加 乘员：[{minion.name}] Navigator:[{navigator.GetHashCode()}] canbinWorldId:[{cabinWorldId}]");
                            _navigatorToCabinMap.Add(navigator, cabinWorldId);
                        }
                    }
                }
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

        // 新增：手动刷新所有小人的舱映射关系（外部可调用）
        public void RefreshAllMinionCabinMapping()
        {
            try
            {
                lock (_lockObj)
                {
                    // 1. 获取所有有效小人
                    var allMinions = GetAllValidMinionsInGame();

                    // 2. 遍历刷新映射
                    foreach (var minion in allMinions)
                    {
                        UpdateMinionCabinMapping(minion);
                    }

                    LogUtils.LogDebug(ModuleName, $"已刷新[{allMinions.Count}]个小人的舱映射关系");
                }
            }
            catch (Exception e)
            {
                LogUtils.LogError(ModuleName, $"RefreshAllMinionCabinMapping异常：{e.Message}\n{e.StackTrace}");
            }
        }

        public bool ShouldBlockTeleport(Navigator navigator, int targetWorldId)
        {
            int mycell = Grid.PosToCell(navigator);
            //===== 新增：太空舱拦截逻辑（最优先判断）=====
            if (targetWorldId != Grid.WorldIdx[mycell])
            {
                // 太空舱拦截：阻断则直接返回，不执行后续传送逻辑
                if (RocketCabinRestriction.QuickCheckBlockTeleport(navigator, targetWorldId))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}