using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleportSuitMod
{
    // 太空舱传送限制常量类（全局复用，避免魔法值）
    public static class TeleportCabinConst
    {
        // 无效WorldId标识
        public const int InvalidWorldId = -1;

        // 缓存清理阈值（避免内存泄漏）
        public const int CacheCleanupThreshold = 1000;
    }
}
