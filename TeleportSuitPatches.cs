
using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using ProcGen.Map;
using System;
using UnityEngine;
using System.Reflection;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.Options;
using System.Collections.Generic;
using UnityEngine.UI;
using YamlDotNet.Core.Tokens;
using System.Linq;
using PeterHan.PLib.Actions;
using TeleportSuitMod.PeterHan.BulkSettingsChange;
using Database;

namespace TeleportSuitMod
{
    public class TeleportSuitPatches : KMod.UserMod2
    {
        static FieldInfo SuitLockerFieldInfo = null;
        private static readonly IDetouredField<TransitionDriver, Navigator.ActiveTransition> TRANSITION =
            PDetours.DetourField<TransitionDriver, Navigator.ActiveTransition>("transition");
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new PPatchManager(harmony).RegisterPatchClass(typeof(TeleportSuitPatches));
            new POptions().RegisterOptions(this, typeof(TeleportSuitOptions));
            new TeleportSuitMod.SanchozzONIMods.Lib.KAnimGroupManager().RegisterInteractAnims("anim_teleport_suit_teleporting_kanim");
            PBuildingManager buildingManager = new PBuildingManager();
            buildingManager.Register(TeleportSuitLockerConfig.CreateBuilding());
            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            BulkChangePatches.BulkChangeAction = new PActionManager().CreateAction(TeleportSuitStrings.TELEPORT_RESTRICT_TOOL.ACTION_KEY,
                TeleportSuitStrings.TELEPORT_RESTRICT_TOOL.ACTION_TITLE);
            GameObject gameObject = new GameObject(nameof(TeleportSuitWorldCountManager));
            gameObject.AddComponent<TeleportSuitWorldCountManager>();
            gameObject.SetActive(true);
        }
        [PLibMethod(RunAt.BeforeDbInit)]
        internal static void BeforeDbInit()
        {
            SanchozzONIMods.Lib.Utils.InitLocalization(typeof(TeleportSuitStrings));
            var icon = SpriteRegistry.GetToolIcon();
            Assets.Sprites.Add(icon.name, icon);
        }

        //添加锻造台的配方
        [HarmonyPatch(typeof(SuitFabricatorConfig), "ConfigureRecipes")]
        public static class SuitFabricatorConfig_ConfigureRecipes_Patch
        {
            public static void Postfix()
            {
                ComplexRecipe.RecipeElement[] array15 = new ComplexRecipe.RecipeElement[2]
                {
                    new ComplexRecipe.RecipeElement(SimHashes.Tungsten.ToString(), 200f),
                    new ComplexRecipe.RecipeElement(SimHashes.Diamond.ToString(), 50f)
                };
                ComplexRecipe.RecipeElement[] array16 = new ComplexRecipe.RecipeElement[1]
                {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.ID.ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                };
                TeleportSuitConfig.recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SuitFabricator", array15, array16), array15, array16)
                {
                    time = TUNING.EQUIPMENT.SUITS.ATMOSUIT_FABTIME,
                    description = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.RECIPE_DESC,
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.ResultWithIngredient,
                    fabricators = new List<Tag> { "SuitFabricator" },
                    requiredTech = TeleportSuitStrings.TechString,
                    sortOrder = 1
                };
                ComplexRecipe.RecipeElement[] array17 = new ComplexRecipe.RecipeElement[2]
                {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.WORN_ID.ToTag(), 1f),
                    new ComplexRecipe.RecipeElement(SimHashes.Diamond.ToString(), 20f)
                };
                ComplexRecipe.RecipeElement[] array18 = new ComplexRecipe.RecipeElement[1]
                {
                    new ComplexRecipe.RecipeElement(TeleportSuitConfig.ID.ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                };
                TeleportSuitConfig.recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SuitFabricator", array17, array18), array17, array18)
                {
                    time = TUNING.EQUIPMENT.SUITS.ATMOSUIT_FABTIME,
                    description = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.RECIPE_DESC,
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.ResultWithIngredient,
                    fabricators = new List<Tag> { "SuitFabricator" },
                    requiredTech = TeleportSuitStrings.TechString,
                    sortOrder = 1
                };
            }
        }

        //修改整个殖民地能否到达某个方块，会影响世界的库存等等
        [HarmonyPatch(typeof(MinionGroupProber), "IsReachable_AssumeLock")]
        public static class MinionGroupProber_IsReachable_AssumeLock_Patch
        {
            public static bool Prefix(int cell, ref bool __result)
            {
                if (TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(
                    ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId, out int value)&&value>0)
                {
                    __result=TeleportSuitConfig.CanTeloportTo(cell);
                    return false;
                }
                return true;
            }
        }

        //修改穿着传送服的小人到各个格子的可达性,影响小人获取任务等等
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.GetNavigationCost))]
        [HarmonyPatch(new Type[] { typeof(int) })]//GetNavigationCost函数有重载，需要确定参数类型
        public static class Navigator_GetNavigationCost_Patch
        {
            public static bool Prefix(Navigator __instance, int cell, ref int __result)
            {
                if ((__instance.flags&TeleportSuitConfig.TeleportSuitFlags)!=0)//穿着传送服
                {
                    __result=-1;
                    if (worldId.TryGetValue(__instance.PathProber, out int id)&&id!=-1)
                    {
                        if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue
                                &&ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId==id
                                &&TeleportSuitConfig.CanTeloportTo(cell))
                        {
                            __result=1;
                        }
                    }
                    return false;
                }

                return true;
            }
        }

        //当小人检测到下落时直接传送到安全可达地点，可以不加，加的话用传送流畅
        [HarmonyPatch(typeof(FallMonitor.Instance), nameof(FallMonitor.Instance.Recover))]
        public static class FallMonitor_Instance_Recover
        {
            public static bool Prefix(Navigator ___navigator, bool ___flipRecoverEmote)
            {
                if ((___navigator.flags&TeleportSuitConfig.TeleportSuitFlags)!=0)
                {
                    int cell = Grid.PosToCell(___navigator);
                    NavGrid.Transition[] transitions = ___navigator.NavGrid.transitions;
                    for (int i = 0; i < transitions.Length; i++)
                    {
                        NavGrid.Transition transition = transitions[i];
                        if (transition.isEscape && ___navigator.CurrentNavType == transition.start)
                        {
                            int num = transition.IsValid(cell, ___navigator.NavGrid.NavTable);
                            if (Grid.InvalidCell != num)
                            {
                                Vector2I vector2I = Grid.CellToXY(cell);
                                ___flipRecoverEmote = Grid.CellToXY(num).x < vector2I.x;
                                ___navigator.transform.SetPosition(Grid.CellToPosCBC(num, Grid.SceneLayer.Move));
                                ___navigator.CurrentNavType = transition.end;
                                FallMonitor.Instance sMI = ___navigator.GetSMI<FallMonitor.Instance>();
                                //sMI.UpdateFalling();
                                sMI.sm.isFalling.Set(false, sMI);
                                sMI.GoTo(sMI.sm.standing);
                                return false;
                            }
                        }
                    }

                }
                return true;
            }
        }

        public static Dictionary<PathProber, int> worldId = new Dictionary<PathProber, int>();
        //取消穿着传送服的小人到各个格子的可达性更新（为了优化一点性能），并且记录小人的世界信息，
        //因为在Navigator_GetNavigationCost_Patch中获取世界可能会触发unity的gameobject获取报错
        [HarmonyPatch(typeof(PathProber), nameof(PathProber.UpdateProbe))]
        public static class PathProber_UpdateProbe_Patch
        {
            public static bool Prefix(PathFinder.PotentialPath.Flags flags, PathProber __instance, int cell)
            {
                if ((flags&TeleportSuitConfig.TeleportSuitFlags)!=0)
                {
                    if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue)
                    {
                        worldId[__instance]= ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]).ParentWorldId;
                    }
                    else
                    {
                        worldId[__instance]=-1;
                    }

                    return false;
                }
                return true;
            }
        }

        //穿上传送服之后禁用寻路并传送小人
        [HarmonyPatch(typeof(Navigator), nameof(Navigator.AdvancePath))]
        public static class PathFinder_UpdatePath_Patch
        {
            public static bool Prefix(Navigator __instance, ref NavTactic ___tactic, ref int ___reservedCell)
            {
                if (__instance.target!=null&&__instance.flags.HasFlag(TeleportSuitConfig.TeleportSuitFlags)&&Grid.PosToCell(__instance) != ___reservedCell)
                {
                    bool needTeleport = true;
                    int mycell = Grid.PosToCell(__instance);
                    int target_position_cell = Grid.PosToCell(__instance.target);
                    for (int i = 0; i<__instance.targetOffsets.Length; i++)
                    {
                        int cell = Grid.OffsetCell(target_position_cell, __instance.targetOffsets[i]);
                        if (__instance.CanReach(cell)&&mycell==cell)
                        {
                            needTeleport=false;
                        }
                    }
                    if (!needTeleport)
                    {
                        __instance.Stop(arrived_at_destination: true, false);
                        return false;
                    }
                    int cellPreferences = ___tactic.GetCellPreferences(target_position_cell, __instance.targetOffsets, __instance);
                    NavigationReservations.Instance.RemoveOccupancy(___reservedCell);
                    ___reservedCell =cellPreferences;
                    NavigationReservations.Instance.AddOccupancy(cellPreferences);
                    if (___reservedCell != NavigationReservations.InvalidReservation)
                    {
                        Equipment equipment = __instance.GetComponent<MinionIdentity>().GetEquipment();
                        if (equipment!=null)
                        {
                            Assignable assignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
                            if (assignable!=null)
                            {
                                TeleportSuitTank tank = assignable.GetComponent<TeleportSuitTank>();
                                if (tank!=null&&tank.batteryCharge > 0)
                                {
                                    tank.batteryCharge -=1f/TeleportSuitConfig.TELEPORTCOUNT;
                                }
                            }
                        }

                        __instance.transitionDriver.EndTransition();
                        __instance.smi.GoTo(__instance.smi.sm.normal.moving);
                        Navigator.ActiveTransition transition = TRANSITION.Get(__instance.transitionDriver);
                        transition= new Navigator.ActiveTransition();
                        KBatchedAnimController reactor_anim = __instance.GetComponent<KBatchedAnimController>();
                        reactor_anim.AddAnimOverrides(TeleportSuitConfig.InteractAnim, 1f);
                        //reactor_anim.Play("working_pre");
                        reactor_anim.Queue("working_loop");
                        reactor_anim.Queue("working_pst");
                        int reservedCell = ___reservedCell;
                        Action<object> action = null;
                        action = delegate (object data)
                        {
                            __instance.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(TeleportSuitConfig.InteractAnim);
                            Vector3 position = Grid.CellToPos(reservedCell, CellAlignment.Bottom, (Grid.SceneLayer)25);
                            __instance.transform.SetPosition(position);
                            if (Grid.HasLadder[reservedCell])
                            {
                                __instance.CurrentNavType = NavType.Ladder;
                            }
                            if (Grid.HasPole[reservedCell])
                            {
                                __instance.CurrentNavType = NavType.Pole;
                            }
                            if (GameNavGrids.FloorValidator.IsWalkableCell(reservedCell, Grid.CellBelow(reservedCell), true))
                            {
                                __instance.CurrentNavType = NavType.Floor;
                            }
                            __instance.Stop(arrived_at_destination: true, false);
                            __instance.Unsubscribe((int)GameHashes.AnimQueueComplete, action);

                        };
                        __instance.Subscribe((int)GameHashes.AnimQueueComplete, action);
                    }
                    else
                    {
                        __instance.Stop();
                    }
                    return false;
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
                if (__instance.gameObject.activeInHierarchy&&(__instance.flags&TeleportSuitConfig.TeleportSuitFlags)!=0)
                {
                    return false;
                }
                return true;
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
                if (nav!=null&&(nav.flags&TeleportSuitConfig.TeleportSuitFlags)!=0)
                {
                    if (OverlayScreen.Instance.mode != TeleportationOverlay.ID)
                    {
                        OverlayScreen.Instance.ToggleOverlay(TeleportationOverlay.ID);
                    }
                    preDraw=true;
                    return false;
                }
                if (preDraw)
                {
                    preDraw=false;
                    OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID);
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
                if (component.OutfitTags[0]==TeleportSuitGameTags.TeleportSuit)
                {
                    component.returnSuitWorkable.CancelChore();
                    TeleportSuitLocker teleportSuitLocker = __instance.gameObject.GetComponent<TeleportSuitLocker>();
                    if (teleportSuitLocker!=null)
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
                if (__instance.OutfitTags[0]==TeleportSuitGameTags.TeleportSuit)
                {
                    KPrefabID suit = __instance.GetStoredOutfit();
                    if (suit!=null)
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

        [HarmonyPatch(typeof(SuitLocker.ReturnSuitWorkable), nameof(SuitLocker.ReturnSuitWorkable.CancelChore))]
        public static class SuitLocker_ReturnSuitWorkable_CancelChore_Patch
        {
            public static bool Prefix(SuitLocker.ReturnSuitWorkable __instance)
            {
                SuitLocker component = __instance.GetComponent<SuitLocker>();

                if (component.OutfitTags[0].Name==TeleportSuitGameTags.TeleportSuit.Name)
                {
                    TeleportSuitLocker teleportSuitLocker = component.gameObject.GetComponent<TeleportSuitLocker>();
                    if (teleportSuitLocker!=null)
                    {
                        teleportSuitLocker.unequipTeleportSuitWorkable.CancelChore();
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.UpdateSuitMarkerStates))]
        public static class SuitLocker_UpdateSuitMarkerStates_Patch
        {
            public static bool Prefix(GameObject self)
            {
                if (self==null) return false;
                SuitLocker component = self.GetComponent<SuitLocker>();
                if (component != null&&component.OutfitTags[0].Name==TeleportSuitGameTags.TeleportSuit.Name)
                {
                    return false;
                }
                return true;
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
                Type type = typeof(OverlayMenu).GetNestedType("OverlayToggleInfo", BindingFlags.NonPublic|BindingFlags.Instance);
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

        [HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
        public static class SimDebugView_OnPrefabInit_Patch
        {
            public static void Postfix(Dictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs)
            {
                ___getColourFuncs.Add(TeleportationOverlay.ID, TeleportationOverlay.GetOxygenMapColour);
            }
        }

        [HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
        public static class OverlayScreen_RegisterModes_Patch
        {
            public static void Postfix(OverlayScreen __instance)
            {
                typeof(OverlayScreen).GetMethod("RegisterMode", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(__instance, new object[] { new TeleportationOverlay() });
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
                info.mode=TeleportationOverlay.ID;
                info.infoUnits=new List<OverlayLegend.OverlayInfoUnit>();
                info.isProgrammaticallyPopulated=true;
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

        //退出一个存档时要把需要保存的数据设置为空，否则可能会影响下一个存档
        [HarmonyPatch(typeof(LoadScreen), nameof(LoadScreen.ForceStopGame))]
        public static class LoadScreen_ForceStopGame_Patch
        {
            internal static void Prefix()
            {
                TeleportationOverlay.TeleportRestrict=null;
            }
        }

        //添加科技
        [HarmonyPatch(typeof(TechItems), nameof(TechItems.Init))]
        public static class TechItems_Init_Patch
        {
            public static void Prefix()
            {
                Db.Get().TechItems.AddTechItem(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.TECH_ITEM_NAME,
                    TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.NAME,
                    TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORT_SUIT.DESC,
                    (string anim, bool centered) => Def.GetUISprite(TeleportSuitConfig.ID.ToTag()).first,
                    DlcManager.AVAILABLE_ALL_VERSIONS);
                Db.Get().TechItems.AddTechItem(TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.TECH_ITEM_NAME,
                    TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.NAME,
                    TeleportSuitStrings.RESEARCH.OTHER_TECH_ITEMS.TELEPORTATION_OVERLAY.DESC,
                    (string anim, bool centered) => SpriteRegistry.GetOverlayIcon(), DlcManager.AVAILABLE_ALL_VERSIONS);
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
        }
    }
}
