using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleNavigator : KMonoBehaviour
    {
        // 缓存结构：Navigator实例 → (初始目标单元格, 是否短距离)
        public static readonly Dictionary<Navigator, (int initialTargetCell, bool isShortRange)> NavTargetCache = new Dictionary<Navigator, (int, bool)>();
        public static readonly object _naviTargetCacheLock = new object();
        public static readonly object _cacheLock = new object(); // 线程锁，保证多帧安全


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
        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            if (NavTargetCache != null)
            {
                NavTargetCache.Clear();
            }
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
        private static readonly CellOffset[] BoundingOffsets = { new CellOffset(0, 0), new CellOffset(0, 1) }; // Example

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

    }
}
