

using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;
using static GameNavGrids;
using static STRINGS.UI.USERMENUACTIONS;

namespace TeleportSuitMod
{
    internal class TeleportSuitConfig : IEquipmentConfig
    {
        public const string ID = "Teleport_Suit";
        public static ComplexRecipe recipe;
        public static PathFinder.PotentialPath.Flags TeleportSuitFlags = (PathFinder.PotentialPath.Flags)32;//必须是2的幂且大于8

        public static int TELEPORTCOUNT = 80;
        public static int OXYGENCAPACITY = 75;
        public static int ATHLETICS = -8;
        public static int SCALDING = 1000;
        public static float RADIATION_SHIELDING = 0.66f;
        public static int STRENGTH = 10;
        public static int INSULATION = 50;
        public static float THERMAL_CONDUCTIVITY_BARRIER = 0.3f;

        private AttributeModifier expertAthleticsModifier;

        static CellOffset[] bounding_offsets = new CellOffset[2]
            {
                        new CellOffset(0, 0),
                        new CellOffset(0, 1)
            };

        protected static bool IsCellPassable(int cell, bool is_dupe)
        {
            Grid.BuildFlags buildFlags = Grid.BuildMasks[cell] & ~(Grid.BuildFlags.FakeFloor | Grid.BuildFlags.Foundation | Grid.BuildFlags.Door);
            if (buildFlags == ~Grid.BuildFlags.Any)
            {
                return true;
            }
            if (is_dupe)
            {
                if ((buildFlags & Grid.BuildFlags.DupeImpassable) != 0)
                {
                    return false;
                }
                if ((buildFlags & Grid.BuildFlags.Solid) != 0)
                {
                    return (buildFlags & Grid.BuildFlags.DupePassable) != 0;
                }
                return true;
            }
            return (buildFlags & (Grid.BuildFlags.Solid | Grid.BuildFlags.CritterImpassable)) == 0;
        }
        static public bool CanTeloportTo(int cell)
        {
            int cell2;
            bool flag4 = false;
            foreach (CellOffset offset in bounding_offsets)
            {
                cell2 = Grid.OffsetCell(cell, offset);
                if (!Grid.IsWorldValidCell(cell2) || !IsCellPassable(cell2, true))
                {
                    return false;
                }
                int num = Grid.CellAbove(cell2);
                if (Grid.IsValidCell(num) && Grid.Element[num].IsUnstable)
                {
                    return false;
                }
            }
            if (FloorValidator.IsWalkableCell(cell, Grid.CellBelow(cell), true)||Grid.HasLadder[cell]||Grid.HasPole[cell])
            {
                flag4=true;
            }

            bool value = false;
            bool flag = false;

            int aboveCell = Grid.CellAbove(cell);
            bool cellValid = Grid.IsValidCell(cell);
            bool aboveCellValid = Grid.IsValidCell(aboveCell);
            flag = (!flag4 && cellValid && Grid.Solid[cell] && !Grid.DupePassable[cell]) || (aboveCellValid && Grid.Solid[aboveCell] && !Grid.DupePassable[aboveCell]) || (cellValid && Grid.DupeImpassable[cell]) || (aboveCellValid && Grid.DupeImpassable[aboveCell]);
            value = !flag4 && !flag;
            if (flag||value)
            {
                return false;
            }
            return true;
        }
        public EquipmentDef CreateEquipmentDef()
        {
            LocString.CreateLocStringKeys(typeof(TeleportSuitStrings.EQUIPMENT));

            List<AttributeModifier> list = new List<AttributeModifier>();

            //修改小人穿上服装之后的属性
            list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.ATHLETICS, ATHLETICS, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            list.Add(new AttributeModifier(Db.Get().Attributes.ScaldingThreshold.Id, SCALDING, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            list.Add(new AttributeModifier(Db.Get().Attributes.RadiationResistance.Id, RADIATION_SHIELDING, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            list.Add(new AttributeModifier(Db.Get().Attributes.Strength.Id, STRENGTH, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.INSULATION, INSULATION, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.THERMAL_CONDUCTIVITY_BARRIER, THERMAL_CONDUCTIVITY_BARRIER, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));

            //技能减免
            expertAthleticsModifier = new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.ATHLETICS, -ATHLETICS, Db.Get().Skills.Suits1.Name);

            EquipmentDef equipmentDef = EquipmentTemplates.CreateEquipmentDef("Teleport_Suit", TUNING.EQUIPMENT.SUITS.SLOT, SimHashes.Dirt, TUNING.EQUIPMENT.SUITS.ATMOSUIT_MASS, "suit_leadsuit_kanim", "", "body_leadsuit_kanim", 6, list, null, IsBody: true, EntityTemplates.CollisionShape.CIRCLE, 0.325f, 0.325f, new Tag[2]
            {
                GameTags.Suit,
                GameTags.Clothes
            });
            equipmentDef.wornID = "Worn_Teleport_Suit";
            equipmentDef.RecipeDescription = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.RECIPE_DESC;
            //免疫debuff
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("SoakingWet"));
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("WetFeet"));
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("PoppedEarDrums"));

            //穿上衣服的回调
            equipmentDef.OnEquipCallBack = delegate (Equippable eq)
            {
                Ownables soleOwner2 = eq.assignee.GetSoleOwner();
                if (soleOwner2 != null)
                {
                    GameObject targetGameObject2 = soleOwner2.GetComponent<MinionAssignablesProxy>().GetTargetGameObject();
                    Navigator component3 = targetGameObject2.GetComponent<Navigator>();
                    if (component3 != null)
                    {
                        component3.SetFlags(TeleportSuitFlags);
                        //if (TeleportSuitWorldCountManager.Instance!=null)
                        //{
                        if (TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(targetGameObject2.GetMyWorldId(), out int value))
                        {
                            TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject2.GetMyWorldId()]++;
                        }
                        else
                        {
                            TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject2.GetMyWorldId()]=1;
                        }
                        //}
                    }
                    MinionResume component4 = targetGameObject2.GetComponent<MinionResume>();
                    if (component4 != null && component4.HasPerk(Db.Get().SkillPerks.ExosuitExpertise.Id))
                    {
                        targetGameObject2.GetAttributes().Get(Db.Get().Attributes.Athletics).Add(expertAthleticsModifier);
                    }
                }
            };

            //脱下衣服的回调
            equipmentDef.OnUnequipCallBack = delegate (Equippable eq)
            {
                if (eq.assignee != null)
                {
                    Ownables soleOwner = eq.assignee.GetSoleOwner();
                    if (soleOwner != null)
                    {
                        GameObject targetGameObject = soleOwner.GetComponent<MinionAssignablesProxy>().GetTargetGameObject();
                        if ((bool)targetGameObject)
                        {
                            targetGameObject.GetAttributes()?.Get(Db.Get().Attributes.Athletics).Remove(expertAthleticsModifier);
                            Navigator component = targetGameObject.GetComponent<Navigator>();
                            if (component != null)
                            {
                                component.ClearFlags(TeleportSuitFlags);
                                //if (TeleportSuitWorldCountManager.Instance!=null)
                                //{
                                if (TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(targetGameObject.GetMyWorldId(), out int value))
                                {
                                    TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject.GetMyWorldId()]--;
                                }
                                //}
                            }
                            Effects component2 = targetGameObject.GetComponent<Effects>();
                            if (component2 != null && component2.HasEffect("SoiledSuit"))
                            {
                                component2.Remove("SoiledSuit");
                            }
                        }
                        Tag elementTag = eq.GetComponent<SuitTank>().elementTag;
                        eq.GetComponent<Storage>().DropUnlessHasTag(elementTag);
                    }
                }
            };

            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs, "Teleport_Suit");
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs, "Helmet");
            return equipmentDef;
        }

        public void DoPostConfigure(GameObject go)
        {
            SuitTank suitTank = go.AddComponent<SuitTank>();
            suitTank.element = "Oxygen";
            suitTank.capacity = OXYGENCAPACITY;
            suitTank.elementTag = GameTags.Breathable;
            go.AddComponent<TeleportSuitTank>();
            go.AddComponent<HelmetController>();
            KPrefabID component = go.GetComponent<KPrefabID>();
            component.AddTag(GameTags.Clothes);
            component.AddTag(GameTags.PedestalDisplayable);
            component.AddTag(GameTags.AirtightSuit);

            Durability durability = go.AddComponent<Durability>();
            durability.wornEquipmentPrefabID = "Worn_Teleport_Suit";
            durability.durabilityLossPerCycle = TUNING.EQUIPMENT.SUITS.ATMOSUIT_DECAY;

            Storage storage = go.AddOrGet<Storage>();
            storage.SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage);
            storage.showInUI = true;
            go.AddOrGet<AtmoSuit>();
            go.AddComponent<SuitDiseaseHandler>();
        }

        public string[] GetDlcIds()
        {
            //应该是让dlc和原版都可以使用
            return DlcManager.AVAILABLE_ALL_VERSIONS;
        }
    }
}
