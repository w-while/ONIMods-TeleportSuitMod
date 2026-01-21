

using FMOD;
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;
using static STRINGS.DUPLICANTS.MODIFIERS;

namespace TeleportSuitMod
{
    internal class TeleportSuitConfig : IEquipmentConfig
    {
        public static KAnimFile InteractAnim
        {
            get
            {
                if (interactAnim == null)
                {
                    interactAnim = Assets.GetAnim("anim_teleport_suit_teleporting_kanim");
                }
                return interactAnim;
            }
        }
        public static KAnimFile AstomStandAnim
        {
            get
            {
                if (astomstandAnim == null)
                {
                    astomstandAnim = Assets.GetAnim("astom_stand_kanim");
                }
                return astomstandAnim;
            }
        }

        private static KAnimFile astomstandAnim = null;
        private static KAnimFile interactAnim = null;
        public static string ID = "Teleport_Suit";
        public static string WORN_ID = "Worn_Teleport_Suit";
        public static ComplexRecipe recipe;
        public static PathFinder.PotentialPath.Flags TeleportSuitFlags = (PathFinder.PotentialPath.Flags)32;//必须是2的幂且大于8

        public static int OXYGENCAPACITY = 75;
        public static int SCALDING = 2000;
        public static float RADIATION_SHIELDING = 0.66f;
        public static float STRENGTH = 10f;
        public static float INSULATION = 100f;
        public static float THERMAL_CONDUCTIVITY_BARRIER = 0.5f;
        public static int LEADSUIT_SCOLDING = -1000;
        public static float ATHLETICS = -8;
        public static readonly string ModuleName = "TeleportSuitConfig";

        public static bool TeleportAnyWhere = TeleNavigator.StandInSpaceEnable;
        public KBatchedAnimController astomStandAnim;
        //private AttributeModifier expertAthleticsModifier;

        static CellOffset[] bounding_offsets = new CellOffset[2]
            {
                        new CellOffset(0, 0),
                        new CellOffset(0, 1)
            };

        public EquipmentDef CreateEquipmentDef()
        {
            //LocString.CreateLocStringKeys(typeof(TeleportSuitStrings.EQUIPMENT));

            List<AttributeModifier> list = new List<AttributeModifier>();
            
            //修改小人穿上服装之后的属性
            //运动-8
            //list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.ATHLETICS, ATHLETICS, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            //烫伤阈值
            list.Add(new AttributeModifier(Db.Get().Attributes.ScaldingThreshold.Id , (float) SCALDING , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME, false, false, true));
            //冻伤阈值
            list.Add(new AttributeModifier(Db.Get().Attributes.ScoldingThreshold.Id, (float)LEADSUIT_SCOLDING, STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME, false, false, true));
            //辐射抗性
            list.Add(new AttributeModifier(Db.Get().Attributes.RadiationResistance.Id , RADIATION_SHIELDING , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME, false, false, true));
            //力量
            list.Add(new AttributeModifier(Db.Get().Attributes.Strength.Id , STRENGTH , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME, false, false, true));
            //隔热
            list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.INSULATION , INSULATION , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME, false, false, true));
            //隔热厚度
            list.Add(new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.THERMAL_CONDUCTIVITY_BARRIER , THERMAL_CONDUCTIVITY_BARRIER , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME, false, false, true));

            //技能减免
            //expertAthleticsModifier = new AttributeModifier(TUNING.EQUIPMENT.ATTRIBUTE_MOD_IDS.ATHLETICS, -ATHLETICS, Db.Get().Skills.Suits1.Name);

            EquipmentDef equipmentDef = EquipmentTemplates.CreateEquipmentDef(TeleportSuitConfig.ID , TUNING.EQUIPMENT.SUITS.SLOT ,
                SimHashes.Dirt, (float) TUNING.EQUIPMENT.SUITS.ATMOSUIT_MASS , "teleport_suit_kanim" ,
                "" , "teleport_suit_body_kanim" , 6 , list , null ,
                IsBody: true , EntityTemplates.CollisionShape.CIRCLE , 0.325f , 0.325f , new Tag[]
            {
                GameTags.Suit,
                GameTags.Clothes
            });
            equipmentDef.wornID = TeleportSuitConfig.WORN_ID;
            equipmentDef.RecipeDescription = TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.RECIPE_DESC;
            //免疫debuff
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("SoakingWet"));//全身湿透
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("WetFeet"));//双脚湿透
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("BionicWaterStress"));//渗水
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("ColdAir"));//
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("WarmAir"));//炎热环境
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("PoppedEarDrums"));//耳膜破裂
            equipmentDef.EffectImmunites.Add(Db.Get().effects.Get("RecentlySlippedTracker"));
            

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
                        if (TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(targetGameObject2.GetMyWorldId() , out int value))
                        {
                            TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject2.GetMyWorldId()]++;
                        }
                        else
                        {
                            TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject2.GetMyWorldId()] = 1;
                        }
                    }
                    MinionResume component4 = targetGameObject2.GetComponent<MinionResume>();
                    targetGameObject2.AddTag(GameTags.HasAirtightSuit);

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
                            Navigator component = targetGameObject.GetComponent<Navigator>();
                            if (component != null)
                            {
                                component.ClearFlags(TeleportSuitFlags);
                                if (TeleportSuitWorldCountManager.Instance.WorldCount.TryGetValue(targetGameObject.GetMyWorldId() , out int value))
                                {
                                    TeleportSuitWorldCountManager.Instance.WorldCount[targetGameObject.GetMyWorldId()]--;
                                }
                            }
                            Effects component2 = targetGameObject.GetComponent<Effects>();
                            if (component2 != null && component2.HasEffect("SoiledSuit"))
                            {
                                component2.Remove("SoiledSuit");
                            }
                        }
                        Tag elementTag = eq.GetComponent<SuitTank>().elementTag;
                        targetGameObject.RemoveTag(GameTags.HasAirtightSuit);
                        eq.GetComponent<Storage>().DropUnlessHasTag(elementTag);
                    }
                }
            };

            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs , TeleportSuitConfig.ID);
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs , "Helmet");
            return equipmentDef;
        }

        public void DoPostConfigure(GameObject go)
        {
            SuitTank suitTank = go.AddComponent<SuitTank>();
            suitTank.element = "Oxygen";
            suitTank.capacity = TeleportSuitOptions.Instance.suitOxygenCapacity;
            suitTank.elementTag = GameTags.Breathable;
            go.AddComponent<TeleportSuitTank>();
            go.AddComponent<HelmetController>();
            KPrefabID component = go.GetComponent<KPrefabID>();
            component.AddTag(GameTags.Clothes);
            component.AddTag(GameTags.PedestalDisplayable);
            component.AddTag(GameTags.AirtightSuit);

            Durability durability = go.AddComponent<Durability>();
            durability.wornEquipmentPrefabID = TeleportSuitConfig.WORN_ID;
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
