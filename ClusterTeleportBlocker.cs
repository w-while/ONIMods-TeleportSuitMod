using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleportSuitMod
{
    internal class ClusterTeleportBlocker : ITeleportBlocker
    {
        public bool ShouldBlockTeleport(Navigator navigator, int targetWorldId)
        {
            if (navigator == null || TeleportSuitOptions.Instance == null ||
                !TeleportSuitOptions.Instance.clusterTeleportByMoveTo) return true;

            return false;
            
        }
    }
}
