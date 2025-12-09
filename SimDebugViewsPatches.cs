using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    internal class SimDebugViewsPatches
    {
        //调试试图，初始化中添加功能
        [HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
        public static class SimDebugView_OnPrefabInit_Patch
        {
            public static void Postfix(Dictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs)
            {
                ___getColourFuncs.Add(TeleportationOverlay.ID, TeleportationOverlay.GetOxygenMapColour);
            }
        }


        [HarmonyPatch(typeof(StatusItem), "GetStatusItemOverlayBySimViewMode")]
        public static class StatusItem_GetStatusItemOverlayBySimViewMode_Patch
        {
            public static void Prefix(Dictionary<HashedString, StatusItem.StatusItemOverlays> ___overlayBitfieldMap)
            {
                if (!___overlayBitfieldMap.ContainsKey(TeleportationOverlay.ID))
                {
                    ___overlayBitfieldMap.Add(TeleportationOverlay.ID, StatusItem.StatusItemOverlays.None);
                }
            }
        }

        [HarmonyPatch(typeof(OverlayLegend), "OnSpawn")]
        public static class OverlayLegend_OnSpawn_Patch
        {
            public static void Prefix(List<OverlayLegend.OverlayInfo> ___overlayInfoList)
            {
                OverlayLegend.OverlayInfo info = new OverlayLegend.OverlayInfo();
                info.name = "STRINGS.UI.OVERLAYS.TELEPORTATION.NAME";
                info.mode = TeleportationOverlay.ID;
                info.infoUnits = new List<OverlayLegend.OverlayInfoUnit>();
                info.isProgrammaticallyPopulated = true;
                ___overlayInfoList.Add(info);
            }
        }
    }
}
