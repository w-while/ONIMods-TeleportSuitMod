using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    //可移动区域与限制区域图层
    public class TeleportationOverlay : OverlayModes.Mode
    {
        public static bool[] TeleportRestrict = null;
        public static readonly HashedString ID = "Teleportation";
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
            else if (TeleNavigator.CanTeloportTo(cell))
            {
                result=Color.blue;
            }
            return result;
        }
        public override List<LegendEntry> GetCustomLegendData()
        {
            return new List<LegendEntry>()
            {
                new LegendEntry(TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.TELEPORTABLEAREA,
                TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.ToolTip.TELEPORTABLEAREA, Color.blue),
                new LegendEntry(TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.TELEPORTRESTRICTEDAREA,
                TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.ToolTip.TELEPORTRESTRICTEDAREA, Color.red),
            };
        }
    }
}
