using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleportableOverlay : OverlayModes.Mode
    {
        public static bool[] TeleportRestrict = null;
        public static readonly HashedString ID = "Teleportable";
        public override string GetSoundName()
        {
            return "SuitRequired";
        }

        public override HashedString ViewMode()
        {
            return ID;
        }

        public static Color GetOxygenMapColour(SimDebugView instance, int cell)
        {
            if (TeleportRestrict==null)
            {
                TeleportRestrict=new bool[Grid.CellCount];
            }
            Color result = Color.black;
            if (TeleportRestrict[cell])
            {
                result=Color.red;
            }
            else if (TeleportSuitConfig.CanTeloportTo(cell))
            {
                result=Color.blue;
            }
            return result;
        }
        public override List<LegendEntry> GetCustomLegendData()
        {
            return new List<LegendEntry>()
            {
                new LegendEntry(TeleportSuitStrings.UI.OVERLAYS.TELEPORTABLE.TELEPORTABLEAREA,
                TeleportSuitStrings.UI.OVERLAYS.TELEPORTABLE.ToolTip.TELEPORTABLEAREA, Color.blue),
                new LegendEntry(TeleportSuitStrings.UI.OVERLAYS.TELEPORTABLE.TELEPORTRESTRICTEDAREA,
                TeleportSuitStrings.UI.OVERLAYS.TELEPORTABLE.ToolTip.TELEPORTRESTRICTEDAREA, Color.red),
            };
        }
    }
}
