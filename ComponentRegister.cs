using Database;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TeleportSuitMod.PeterHan.BulkSettingsChange;
using TeleportSuitMod.SanchozzONIMods.Lib;
using UnityEngine;
using static ComplexRecipe;

namespace TeleportSuitMod
{
    internal class ComponentRegister
    {
    }

    //添加锻造台的配方
    [HarmonyPatch(typeof(SuitFabricatorConfig), "ConfigureRecipes")]
    public static class SuitFabricatorConfig_ConfigureRecipes_Patch
    {
        public static void Postfix()
        {
            int index = 7;
            //传送服配方：陶瓷/隔热质 200 + 钨 50 + 铅 50
            ComplexRecipe.RecipeElement[] array30001 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(new Tag[]{SimHashes.Ceramic.CreateTag(),SimHashes.SuperInsulator.CreateTag() }, 200f),
                new ComplexRecipe.RecipeElement(new Tag[]
                {
                    SimHashes.Tungsten.CreateTag()
                }, 50f),
                new ComplexRecipe.RecipeElement(SimHashes.Lead.CreateTag(), 50f)
                //BasicFabric/FeatherFabric
            };
            ComplexRecipe.RecipeElement[] array30002 = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.ID.ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };
            string recipeID = ComplexRecipeManager.MakeRecipeID("SuitFabricator", array30001, array30002);
            TeleportSuitConfig.recipe = new ComplexRecipe(recipeID, array30001, array30002)
            {
                time = 20,
                description = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.RECIPE_DESC,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { "SuitFabricator" },
                sortOrder = index++
            };
            //维修配方：损坏传送服 1 + 陶瓷 20
            ComplexRecipe.RecipeElement[] array30003 = new ComplexRecipe.RecipeElement[2]
            {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.WORN_ID.ToTag(), 1f),
                    new ComplexRecipe.RecipeElement(SimHashes.Ceramic.ToString(), 20f)
            };
            ComplexRecipe.RecipeElement[] array30004 = new ComplexRecipe.RecipeElement[1]
            {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.ID.ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };
            ComplexRecipe complexRecipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SuitFabricator", array30003, array30004), array30003, array30004)
            {
                time = TUNING.EQUIPMENT.SUITS.ATMOSUIT_FABTIME,
                description = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.REPAIR_WORN_DESC,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                fabricators = new List<Tag> { "SuitFabricator" },
                requiredTech = TeleportSuitStrings.TechString,
                sortOrder = index++
            };
            complexRecipe.customName = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.REPAIR_WORN_RECIPE_NAME;
        }
    }


    //添加科技

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal class m
    {
        private static void Postfix()
        {
            Tech tech = Db.Get().Techs.TryGet(TeleportSuitStrings.TechString);
            if (tech != null)
            {
                tech.unlockedItemIDs.Add(TeleportSuitLockerConfig.ID);
            }
            ModUtil.AddBuildingToPlanScreen(BUILD_CATEGORY.Equipment, TeleportSuitLockerConfig.ID, BUILD_SUBCATEGORY.equipment, SuitFabricatorConfig.ID, ModUtil.BuildingOrdering.After);
        }
    }

    [HarmonyPatch(typeof(TechItems), nameof(TechItems.Init))]
    public static class TechItems_Init_Patch
    {
        public static void Prefix()
        {
            Db.Get().TechItems.AddTechItem(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.TECH_ITEM_NAME,
                TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.NAME,
                TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.DESC,
                (string anim, bool centered) => Def.GetUISprite(TeleportSuitConfig.ID.ToTag()).first);
            Db.Get().TechItems.AddTechItem(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.TECH_ITEM_NAME,
                TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.NAME,
                TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.DESC,
                (string anim, bool centered) => SpriteRegistry.GetOverlayIcon());
        }
    }

    [HarmonyPatch(typeof(Techs), nameof(Techs.Init))]
    public static class Techs_Init_Patch
    {
        public static void Postfix()
        {
            Tech techs = Db.Get().Techs.TryGet(TeleportSuitStrings.TechString);
            techs.unlockedItemIDs.Add(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.TECH_ITEM_NAME);
            techs.unlockedItemIDs.Add(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.TECH_ITEM_NAME);

        }


        [HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
        public static class OverlayScreen_RegisterModes_Patch
        {
            public static void Postfix(OverlayScreen __instance)
            {
                typeof(OverlayScreen).GetMethod("RegisterMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { new TeleportationOverlay() });
            }
        }
        //添加概览
        [HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
        public static class OverlayMenu_InitializeToggles_Patch
        {
            public static void Postfix(List<OverlayMenu.ToggleInfo> ___overlayToggleInfos)
            {
                if (!Assets.Sprites.ContainsKey(TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.ICON_NAME))
                {
                    Assets.Sprites.Add(TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.ICON_NAME, SpriteRegistry.GetOverlayIcon());
                }
                Type type = typeof(OverlayMenu).GetNestedType("OverlayToggleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                object[] parameters = new object[] {
                    TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.BUTTON.ToString(),
                    TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.ICON_NAME,
                    TeleportationOverlay.ID,
                    TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.TECH_ITEM_NAME,
                    Action.NumActions,
                    TeleportSuitStrings.UI.TOOLTIPS.TELEPORTATIONOVERLAYSTRING.ToString(),
                    TeleportSuitStrings.UI.OVERLAYS.TELEPORTATION.BUTTON.ToString()
                };
                object obj = Activator.CreateInstance(type, parameters);
                ___overlayToggleInfos.Add((KIconToggleMenu.ToggleInfo)obj);
            }
        }
    }
}
