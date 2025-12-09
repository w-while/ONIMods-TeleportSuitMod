using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TeleportSuitMod
{
    public class WorkablePatches
    {
        //ReturnSuitWorkable
        [HarmonyPatch(typeof(SuitLocker.ReturnSuitWorkable), nameof(SuitLocker.ReturnSuitWorkable.CancelChore))]
        public static class SuitLocker_ReturnSuitWorkable_CancelChore_Patch
        {
            public static bool Prefix(SuitLocker.ReturnSuitWorkable __instance)
            {
                SuitLocker component = __instance.GetComponent<SuitLocker>();

                if (component != null && component.OutfitTags[0].Name == TeleportSuitGameTags.TeleportSuit.Name)
                {
                    TeleportSuitLocker teleportSuitLocker = component.gameObject.GetComponent<TeleportSuitLocker>();
                    if (teleportSuitLocker != null)
                    {
                        teleportSuitLocker.unequipTeleportSuitWorkable.CancelChore();
                    }
                }
                return true;
            }
        }

        //取消存放柜复制人主动归还的任务
        [HarmonyPatch(typeof(SuitLocker.ReturnSuitWorkable), nameof(SuitLocker.ReturnSuitWorkable.CreateChore))]
        public static class SuitLocker_ReturnSuitWorkable_CreateChore_Patch
        {
            public static bool Prefix(SuitLocker.ReturnSuitWorkable __instance)
            {
                SuitLocker component = __instance.GetComponent<SuitLocker>();
                if (component != null && component.OutfitTags[0] == TeleportSuitGameTags.TeleportSuit)
                {
                    component.returnSuitWorkable.CancelChore();
                    TeleportSuitLocker teleportSuitLocker = __instance.gameObject.GetComponent<TeleportSuitLocker>();
                    if (teleportSuitLocker != null)
                    {
                        teleportSuitLocker.unequipTeleportSuitWorkable.CreateChore();
                    }
                    return false;
                }
                return true;
            }
        }

        //传送服是否已经满了的判定修改
        [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.IsSuitFullyCharged))]
        public static class SuitLocker_IsSuitFullyCharged_Patch
        {
            public static bool Prefix(SuitLocker __instance, ref bool __result)
            {
                if (__instance.OutfitTags[0] == TeleportSuitGameTags.TeleportSuit)
                {
                    KPrefabID suit = __instance.GetStoredOutfit();
                    if (suit != null)
                    {
                        __result = true;
                        SuitTank suit_tank = suit.GetComponent<SuitTank>();
                        if (suit_tank != null && suit_tank.PercentFull() < 1f)
                        {
                            __result = false;
                        }
                        TeleportSuitTank teleport_suit_tank = suit.GetComponent<TeleportSuitTank>();
                        if (teleport_suit_tank != null && teleport_suit_tank.PercentFull() < 1f)
                        {
                            __result = false;
                        }
                    }
                    else
                    {
                        __result = false;
                    }
                    return false;
                }
                return true;
            }
        }


        //取消防护服储存柜状态标记 
        [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.UpdateSuitMarkerStates))]
        public static class SuitLocker_UpdateSuitMarkerStates_Patch
        {
            public static bool Prefix(GameObject self)
            {
                if (self == null) return false;
                SuitLocker component = self.GetComponent<SuitLocker>();
                if (component != null && component.OutfitTags[0].Name == TeleportSuitGameTags.TeleportSuit.Name)
                {
                    return false;
                }
                return true;
            }
        }


    }
}
