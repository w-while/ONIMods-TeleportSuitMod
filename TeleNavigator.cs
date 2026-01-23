using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace TeleportSuitMod
{
    public class TeleNavigator : KMonoBehaviour, ISimEveryTick
    {
        // 缓存结构：Navigator实例 → (初始目标单元格, 是否短距离)
        public static readonly Dictionary<Navigator, (int initialTargetCell, bool isShortRange)> NavTargetCache = new Dictionary<Navigator, (int, bool)>();
        public static readonly object _naviTargetCacheLock = new object();
        public static readonly object _cacheLock = new object(); // 线程锁，保证多帧安全

        public static int activeWorldIdx = 0;


        // 短距离阈值
        public static readonly bool ShortRangeEnable = TeleportSuitOptions.Instance.teleportrestrictionBounds;
        public static int ShortRange = 100;

        private static TeleNavigator _instance;
        public static TeleNavigator Instance
        {
            get
            {
                lock (_cacheLock)
                {
                    if (_instance != null && _instance.isActiveAndEnabled) return _instance;

                    var existingInstances = UnityEngine.Object.FindObjectsOfType<TeleNavigator>();
                    foreach (var instance in existingInstances)
                    {
                        if (instance.isActiveAndEnabled)
                        {
                            _instance = instance;
                            return _instance;
                        }
                    }

                    // 创建单例（激活状态）
                    var singletonObj = new GameObject("TeleNavigator_Singleton");
                    singletonObj.SetActive(true);

                    var kPrefabID = singletonObj.AddComponent<KPrefabID>();
                    kPrefabID.PrefabTag = TagManager.Create("TeleNavigator");

                    DontDestroyOnLoad(singletonObj);
                    _instance = singletonObj.AddComponent<TeleNavigator>();

                    // 同步初始化
                    _instance.OnPrefabInit();
                    _instance.OnSpawn();

                    return _instance;
                }
            }
            private set => _instance = value;
        }
        protected override void OnSpawn()
        {
            LogUtils.LogDebug("TeleNavigator",$"OnSpawn");
            base.OnSpawn();
            InitializeTelePathGrid();

            SimAndRenderScheduler.instance.Add(this, false);

            if(Game.Instance != null){
                Game.Instance.Subscribe((int)GameHashes.ActiveWorldChanged, OnWorldChanged);
            }

            if (ClusterManager.Instance != null) activeWorldIdx = ClusterManager.Instance.activeWorldId;

            //Subscribe((int)GameHashes.GameLoaded, OnGameLoaded);
        }

        protected override void OnCleanUp()
        {
            LogUtils.LogDebug("TeleNavigator", $"OnCleanUp");
            if (NavTargetCache != null) NavTargetCache.Clear();
            if(NavigatorWorldId != null) NavigatorWorldId.Clear();

            //Game.Instance.Unsubscribe((int)GameHashes.ActiveWorldChanged);
            TelePathGrid = null;
            base.OnCleanUp();
        }
        public static bool IsInShortRange(Navigator __instance)
        {
            if (!TeleNavigator.NavTargetCache.TryGetValue(__instance, out var cacheData)){
                return false;
            }
            if (cacheData.isShortRange) return true;
            return false;
        }
        // 辅助：判断单元格是否在ShortRange内（曼哈顿距离，更贴合ONI导航逻辑）
        public static bool IsInShortRange(int rootCell, int targetCell)
        {
            if (!Grid.IsValidCell(rootCell) || !Grid.IsValidCell(targetCell))
                return false;

            Vector2 rootPos = Grid.CellToPos2D(rootCell);
            Vector2 targetPos = Grid.CellToPos2D(targetCell);
            // 曼哈顿距离（ONI导航优先走直线，比欧氏距离更适配）
            float distance = Mathf.Abs(rootPos.x - targetPos.x) + Mathf.Abs(rootPos.y - targetPos.y);
            return distance <= ShortRange;
        }
        public static bool isTeleMiniom(Navigator navigator)
        {
            return navigator != null && navigator.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags);
        }
        /// <summary>
        /// 根据格子属性获取导航类型
        /// </summary>
        public static NavType GetNavTypeForCell(int cell)
        {
            if (!Grid.IsValidCell(cell))
                return NavType.NumNavTypes;

            if (Grid.HasLadder[cell]) return NavType.Ladder;
            if (Grid.HasPole[cell]) return NavType.Pole;
            if (GameNavGrids.FloorValidator.IsWalkableCell(cell, Grid.CellBelow(cell), true))
                return NavType.Floor;
            if (Grid.HasTube[cell]) return NavType.Tube;

            return NavType.NumNavTypes;
        }
        public static void resetNavType(Navigator navigator,int cell)
        {
            if (navigator == null) return;
            if (Grid.HasLadder[cell]) navigator.CurrentNavType = NavType.Ladder;
            if (Grid.HasPole[cell]) navigator.CurrentNavType = NavType.Pole;
            if (GameNavGrids.FloorValidator.IsWalkableCell(cell, Grid.CellBelow(cell), true))
                navigator.CurrentNavType = NavType.Floor;
            if (Grid.HasTube[cell]) navigator.CurrentNavType = NavType.Tube;
        }

        // --- 性能优化: 缓存 CellCount 和 TeleportRestrict ---
        private static int CachedGridCellCount = -1;

        // --- 配置 ---
        public static readonly bool StandInSpaceEnable = TeleportSuitOptions.Instance.teleportfloat;
                                                      // 假设 bounding_offsets 是固定的 { (0, 0), (0, 1) } 代表 1x2 单位 (脚, 头) 或类似定义
        private static readonly CellOffset[] BoundingOffsets = { new CellOffset(0, 0), new CellOffset(0, 1) }; 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public bool CanTeloportTo(int targetcell)
        {
            // --- 1. 检查: 限制区域 ---
            if (TeleportationOverlay.TeleportRestrict[targetcell])
            {
                return false;
            }

            // --- 2. 基础有效性检查 ---
            if (!Grid.IsValidCell(targetcell) || !Grid.IsVisible(targetcell))
            {
                return false;
            }

            // --- 3. 遍历 bounding_offsets 区域 ---
            foreach (CellOffset offset in BoundingOffsets)
            {
                int cell2 = Grid.OffsetCell(targetcell, offset);
                if(cell2 < 0 || cell2 > Grid.CellCount) continue;
                // --- 内联 IsCellPassable(cell2, true) 逻辑 ---
                // 注意: 这里假设 is_dupe 总是 true
                {
                    Grid.BuildFlags buildFlags = Grid.BuildMasks[cell2] & ~(Grid.BuildFlags.FakeFloor | Grid.BuildFlags.Foundation | Grid.BuildFlags.Door);
                    if (buildFlags != ~Grid.BuildFlags.Any) // Not completely passable
                    {
                        // Check DupeImpassable first (often a quick exit)
                        if ((buildFlags & Grid.BuildFlags.DupeImpassable) != 0)
                        {
                            return false; // 立即失败
                        }
                        if ((buildFlags & Grid.BuildFlags.Solid) != 0)
                        {
                            // If solid, check if dupe-passable
                            if ((buildFlags & Grid.BuildFlags.DupePassable) == 0) // Not DupePassable
                            {
                                return false; // 立即失败
                            }
                            // Else it's Solid but DupePassable -> continue checking
                        }
                        // If not Solid and not DupeImpassable -> continue checking
                    }
                    // If completely passable -> continue checking
                }

                // 检查该 cell 正上方的单元格是否不稳定
                int num = Grid.CellAbove(cell2);
                if (Grid.IsValidCell(num) && Grid.Element[num].IsUnstable)
                {
                    return false; // 立即失败
                }
            }

            // --- 4. 检查: TeleportAnyWhere 模式 ---
            if (StandInSpaceEnable)
            {
                // 在此模式下，只要通过了 bounding_offsets 检查，就成功
                return true;
            }

            // --- 5. 普通模式: 综合检查 ---
            // a. 检查目标点是否是标准位置 (flag4)
            bool isStandardLocation = GameNavGrids.FloorValidator.IsWalkableCell(targetcell, Grid.CellBelow(targetcell), true) ||
                                        Grid.HasLadder[targetcell] ||
                                        Grid.HasPole[targetcell];

            if (!isStandardLocation)
            {
                // 如果不是标准位置，直接失败
                return false;
            }

            // b. 检查目标点上方单元格 (1x2 单位的头部)
            int aboveCell = Grid.CellAbove(targetcell);

            // b1. 检查头部单元格是否有效
            if (!Grid.IsValidCell(aboveCell))
            {
                // 头部单元格无效，无法容纳 1x2 单位
                return false;
            }

            // b2. 检查目标点 (脚部) 和上方 (头部) 是否是 DupeImpassable
            if (Grid.DupeImpassable[targetcell] || Grid.DupeImpassable[aboveCell])
            {
                return false; // 有障碍
            }

            // b3. 检查上方 (头部) 是否是 Solid 且 !DupePassable (注意: 脚部 Solid 且 !DupePassable is okay if it's a standard location)
            if (Grid.Solid[aboveCell] && !Grid.DupePassable[aboveCell])
            {
                return false; // 有障碍
            }

            // 所有条件满足：是标准位置，且上方没有 DupeImpassable 或 Solid&&!DupePassable 障碍
            return true;
        }

        // --- 新增: TelePathGrid 相关 ---
        private static bool[] TelePathGrid = null;
        // 引入一个简单的更新周期计数器或标志
        private static readonly int UPDATE_CYCLE_INTERVAL = 100; // 例如，每 100 个 Sim Tick 周期启动一次完整更新
        //标记
        private static bool dirtyData = false;
        private static int updateCycleCounter = 0; // 用于模拟定期全量更新
        private static int TelePathGridUpdateStartFrame = -1; // 标记开始更新的帧
        private static int TelePathGridUpdateCurrentIndex = 0; // 当前更新到的索引

        private static int TelePathGridUpdateBatchSize = 0; // 每帧更新的单元格数量
        private static int TelePathGridUpdateTotalBatches = 0; // 总批次数
        private static int TelePathGridUpdateCompletedBatches = 0; // 已完成批次数
        private static readonly int TelePathGridUpdateTargetFrames = 30; // 配置: 目标在多少帧内完成更新 (可变)

        // --- 新增: 性能评测 ---
        private static Stopwatch updateStopwatch = new Stopwatch();
        private static long lastFullUpdateDurationMs = 0;
        private static int lastFullUpdateCellCount = 0;
        private static int debugUpdateCount = 0;

        private void OnWorldChanged(object obj)
        {
            //世界切换
            LogUtils.LogDebug("TeleNavigator",$"OnWorldChanged [{((Tuple <int,int>)obj).second}] To [{((Tuple<int, int>)obj).first}]");
            activeWorldIdx = ((Tuple<int, int>)obj).first;
            dirtyData = true;
            //中断正在更新的TelePathGrid
            TelePathGridUpdateStartFrame = -1;
            TelePathGridUpdateCurrentIndex = 0;
            updateCycleCounter = 0;
            //全新更新
        }
        /// <summary>
        /// 初始化 TelePathGrid。应在 Grid 初始化后调用。
        /// </summary>
        public static void InitializeTelePathGrid()
        {
            if (Grid.CellCount > 0 && (TelePathGrid == null || TelePathGrid.Length != Grid.CellCount))
            {
                TelePathGrid = new bool[Grid.CellCount];
                LogUtils.LogDebug("TeleNavigator", $"TelePathGrid initialized/reinitialized with size {Grid.CellCount}");
            }
            else if (Grid.CellCount == 0)
            {
                LogUtils.LogError("TeleNavigator", $"Grid.CellCount is 0 during InitializeTelePathGrid!");
            }
        }
        // MarkTelePathGridDirty becomes less critical or can be removed/modified
        // If you want to force an *earlier* update than the scheduled interval, you could reset the counter
        public static void MarkTelePathGridDirty() // Optional: Force update
        {
            LogUtils.LogDebug("TeleNavigator", "MarkTelePathGridDirty called, forcing early update.");
            // Option 1: Reset the counter to trigger an update on the next tick
            updateCycleCounter = UPDATE_CYCLE_INTERVAL - 1; // Next tick will trigger
                                                            // Option 2: Or, just ensure that PerformTelePathGridUpdateFrame starts immediately if not running
                                                            // if (TelePathGridUpdateStartFrame == -1) { PerformTelePathGridUpdateFrame(); }
                                                            // But Option 1 integrates better with the tick-based schedule.
        }
        /// <summary>
        /// 在每一帧的 Sim 更新中调用此方法，以推进分片更新。
        /// </summary>
        private static void OnSimEveryTick_FrameUpdate(float dt)
        {
            if(TelePathGridUpdateStartFrame == -1)
            {
                updateCycleCounter++;

                if (updateCycleCounter >= UPDATE_CYCLE_INTERVAL)
                {
                    if (TelePathGrid != null && Grid.CellCount > 0) // 确保网格有效
                    {
                        if (TelePathGridUpdateStartFrame == -1) // 确保不在已有更新中
                        {
                            PerformTelePathGridUpdateFrame(); // 这次调用会从头开始
                        }
                    } else
                    {
                        LogUtils.LogDebug("TeleNavigator", "Scheduled update skipped: TelePathGrid or Grid.CellCount invalid.");
                    }
                }
                else
                {
                    // 在非更新周期，也可以做一些轻量级的检查或增量更新（如果需要）
                }
            }
            else
            {
                PerformTelePathGridUpdateFrame();
            }
        }
        /// <summary>
        /// 查询 TelePathGrid 中指定单元格的可达性。
        /// </summary>
        /// <param name="cell">要查询的单元格索引</param>
        /// <returns>该单元格是否传送可达</returns>
        public static bool IsCellTeleportAccessible(int cell)
        {
            if (dirtyData || TelePathGrid == null || cell < 0 || cell >= TelePathGrid.Length)
            {
                return false; // 安全检查
            }
            return TelePathGrid[cell];
        }
        /// <summary>
        /// 获取最后一次完整更新的性能信息。
        /// </summary>
        /// <returns>包含耗时、更新单元格数、更新次数的元组</returns>
        public static (long durationMs, int cellCount, int updateCount) GetLastUpdatePerformanceInfo()
        {
            return (lastFullUpdateDurationMs, lastFullUpdateCellCount, debugUpdateCount);
        }
        // --- 新增: 分片更新核心逻辑 ---

        /// <summary>
        /// 执行单帧的 TelePathGrid 更新。
        /// </summary>
        private static void PerformTelePathGridUpdateFrame()
        {
            if (TelePathGrid == null)
            {
                TelePathGridUpdateStartFrame = -1;
                TelePathGridUpdateCurrentIndex = 0;
                updateCycleCounter = 0; // Also reset scheduler if grid is gone
                LogUtils.LogError("TeleNavigator", "TelePathGrid is null during PerformTelePathGridUpdateFrame!");
                return;
            }

            int currentFrame = Time.frameCount;
            if (TelePathGridUpdateStartFrame == -1)
            {
                // --- 开始新的完整更新周期 ---
                TelePathGridUpdateStartFrame = currentFrame;
                TelePathGridUpdateCurrentIndex = 0;
                TelePathGridUpdateCompletedBatches = 0;

                // 计算批次大小和总数
                if (Grid.CellCount > 0)
                {
                    TelePathGridUpdateTotalBatches = Math.Max(1, TelePathGridUpdateTargetFrames);
                    TelePathGridUpdateBatchSize = (int)Math.Ceiling((double)Grid.CellCount / TelePathGridUpdateTotalBatches);
                    LogUtils.LogDebug("TeleNavigator", $"Starting new TelePathGrid update. Total Cells: {Grid.CellCount}, Target Frames: {TelePathGridUpdateTotalBatches}, Batch Size: {TelePathGridUpdateBatchSize}");
                }
                else
                {
                    LogUtils.LogError("TeleNavigator", $"Grid.CellCount is 0, cannot start update.");
                    TelePathGridUpdateStartFrame = -1; // Ensure it can start again next time
                    return;
                }

                // --- 性能评测开始 (完整更新) ---
                updateStopwatch.Restart();
                lastFullUpdateCellCount = 0;
                debugUpdateCount++;
                // --- 性能评测结束 ---
            }
            // --- 执行当前帧的批次更新 ---
            int endIndex = Math.Min(TelePathGridUpdateCurrentIndex + TelePathGridUpdateBatchSize, TelePathGrid.Length);

            for (int i = TelePathGridUpdateCurrentIndex; i < endIndex; i++)
            {
                // 注意：CanTeloportTo 可能访问无效的 i，虽然 Min 应该防止，但保险起见加个检查
                if (i < TelePathGrid.Length)
                {
                    bool canTeloportTo = false;
                    {
                       canTeloportTo = CanTeloportTo(i);
                    }
                    TelePathGrid[i] = canTeloportTo;
                    if (TelePathGrid[i]) lastFullUpdateCellCount++;
                }
                else
                {
                    LogUtils.LogWarning("TeleNavigator", $"Index {i} exceeds TelePathGrid length {TelePathGrid.Length} during update. This shouldn't happen with Min calculation.");
                    break; // Safety break
                }
            }

            TelePathGridUpdateCurrentIndex = endIndex;
            TelePathGridUpdateCompletedBatches++;

            // --- 检查是否完成 ---
            if (TelePathGridUpdateCurrentIndex >= TelePathGrid.Length)
            {
                // --- 更新完成 ---
                TelePathGridUpdateStartFrame = -1; // <--- CRITICAL: Allows next scheduled cycle to start fresh
                updateCycleCounter = 0; // 重置计数器
                if(dirtyData) dirtyData = false;

                // --- 性能评测结束 (完整更新) ---
                updateStopwatch.Stop();
                lastFullUpdateDurationMs = updateStopwatch.ElapsedMilliseconds;

                LogUtils.LogDebug("TeleNavigator", $"TelePathGrid update completed. Frame: {currentFrame}, Cells Updated: {lastFullUpdateCellCount}/{TelePathGrid.Length}");
                LogUtils.LogDebug("TeleNavigator", $"Update Performance: Duration={lastFullUpdateDurationMs}ms, UpdatedCells={lastFullUpdateCellCount}, UpdatesThisSession={debugUpdateCount}");
                // --- 性能评测结束 ---
            }
        }

        public void SimEveryTick(float dt)
        {
            OnSimEveryTick_FrameUpdate(dt);
        }


        //记录小人的星球信息
        public static Dictionary<Navigator, int> NavigatorWorldId = new Dictionary<Navigator, int>();
        public static readonly object NavigatorWorldIdLocker = new object();
        public static void AddOrUpdateNavigatorWorldId(Navigator navigator)
        {
            if (navigator == null) return;
            int cell = Grid.PosToCell(navigator.gameObject.transform.position);
            if(Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue){
                lock (NavigatorWorldIdLocker)
                {
                    if (ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]) != null)
                    {
                        NavigatorWorldId[navigator] = ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId;
                    }
                }
            }else {
                lock (NavigatorWorldIdLocker)
                {
                    NavigatorWorldId[navigator] = -1;
                }
            }
        }
        public static bool GetNavigatorWorldId(Navigator navigator,out int worldId) {
            worldId = NavigatorWorldId.TryGetValue(navigator,out var wid) ? wid : -1;
            return true;
        }

    }
}
