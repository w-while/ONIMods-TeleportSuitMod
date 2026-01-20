using System;
using System.Collections.Generic;
using System.Linq;
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

        public static readonly bool StandInSpaceEnable = TeleportSuitOptions.Instance.teleportfloat;

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
    }
}
