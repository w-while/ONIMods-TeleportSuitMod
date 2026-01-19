using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleportSuitMod
{
    public class WalkBouldsTeleportBlocker : ITeleportBlocker
    {
        public bool ShouldBlockTeleport(Navigator navigator, int targetWorldId)
        {
            LogUtils.LogDebug("WalkBouldsTeleportBlocker", $"Checking teleport block for Navigator at cell {navigator.GetHashCode()} to world {targetWorldId} [{TeleNavigator.ShortRange}].");
            // 距离判断（强制传送则跳过）
            if (TeleNavigator.ShortRange > 0 && TeleNavigator.IsInShortRange(navigator))
                return true;
            return false;
        }
    }
}
