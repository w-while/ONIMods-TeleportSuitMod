using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    //限制&移动区域
    internal class SimDebugViewsPatches
    {
        
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
        //生成图层
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
        //把限制传送区域的数据保存到存档中
        [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
        public static class SaveGame_OnPrefabInit_Patch
        {
            internal static void Postfix(SaveGame __instance)
            {
                __instance.gameObject.AddOrGet<TeleportRestrictToolSaveData>();
            }
        }

        //修改显示路径
        [HarmonyPatch(typeof(NavPathDrawer), "OnPostRender")]
        public static class NavPathDrawer_OnPostRender_Patch
        {
            static bool preDraw = false;
            public static bool Prefix(NavPathDrawer __instance)
            {
                Navigator nav = __instance.GetNavigator();
                if (nav != null && (nav.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)
                {
                    if (OverlayScreen.Instance.mode != TeleportationOverlay.ID)
                    {
                        OverlayScreen.Instance.ToggleOverlay(TeleportationOverlay.ID);
                    }
                    preDraw = true;
                    return false;
                }
                if (preDraw)
                {
                    preDraw = false;
                    OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID);
                }
                return true;
            }
        }
        //取消选中穿着传送服的小人时绘制路径
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.DrawPath))]
        public static class Navigator_DrawPath_Patch
        {
            public static bool Prefix(Navigator __instance)
            {
                if (__instance.gameObject.activeInHierarchy && (__instance.flags & TeleportSuitConfig.TeleportSuitFlags) != 0)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
