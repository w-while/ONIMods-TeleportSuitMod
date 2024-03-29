﻿using PeterHan.PLib.Buildings;
using TemplateClasses;
using TUNING;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleportSuitLockerConfig : IBuildingConfig
    {
        public const string ID = "TeleportSuitLocker";
        internal static PBuilding TeleportSuitLockerTemplate;
        public static AssignableSlot TeleportSuitAssignableSlot;

        public static PBuilding CreateBuilding()
        {
            return TeleportSuitLockerTemplate = new PBuilding(ID , TeleportSuitStrings.BUILDINGS.PREFABS.TELEPORTSUITLOCKER.NAME)
            {
                //AddAfter = PressureDoorConfig.ID,
                Animation = "teleport_suit_locker_kanim" ,
                Category = "Equipment" ,
                ConstructionTime = 30.0f ,
                Decor = BUILDINGS.DECOR.BONUS.TIER1 ,
                Description = null ,
                EffectText = null ,
                Entombs = false ,
                Floods = true ,
                Width = 2 ,
                Height = 4 ,
                HP = 30 ,
                Ingredients = {
                    new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 2),
                } ,
                Placement = BuildLocationRule.OnFloor ,
                PowerInput = new PowerRequirement(TeleportSuitOptions.Instance.suitLockerPowerInput , new CellOffset(0 , 0)) ,

                Tech = TeleportSuitStrings.TechString ,
                Noise = NOISE_POLLUTION.NONE ,
            };
        }


        public override BuildingDef CreateBuildingDef()
        {
            //LocString.CreateLocStringKeys(typeof(TeleportSuitStrings.BUILDINGS));
            BuildingDef obj = TeleportSuitLockerTemplate.CreateDef();
            obj.BaseMeltingPoint = 1600f;
            obj.PreventIdleTraversalPastBuilding = true;
            obj.InputConduitType = ConduitType.Gas;
            obj.UtilityInputOffset = new CellOffset(0 , 2);

            //应该是是否启用
            //obj.Deprecated = !Sim.IsRadiationEnabled();
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs , "TeleportSuitLocker");
            return obj;
        }

        public override void ConfigureBuildingTemplate(GameObject go , Tag prefab_tag)
        {
            go.AddOrGet<SuitLocker>().OutfitTags = new Tag[1] { TeleportSuitGameTags.TeleportSuit };
            go.AddOrGet<TeleportSuitLocker>();


            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 1f;
            conduitConsumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Oxygen).tag;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            conduitConsumer.forceAlwaysSatisfied = true;
            conduitConsumer.capacityKG = TeleportSuitOptions.Instance.suitLockerOxygenCapacity;
            go.AddOrGet<AnimTileable>().tags = new Tag[1]
            {
                new Tag("TeleportSuitLocker"),
            };

            //不受房间分配
            go.AddTag(tag: GameTags.NotRoomAssignable);
            Ownable ownable = go.AddOrGet<Ownable>();
            if (TeleportSuitAssignableSlot == null)
            {
                TeleportSuitAssignableSlot = Db.Get().AssignableSlots.Add(new OwnableSlot(TeleportSuitLockerConfig.ID , TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            }
            ownable.slotID = TeleportSuitAssignableSlot.Id;
            ownable.canBePublic = true;


            go.AddOrGet<Storage>();
            Prioritizable.AddRef(go);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            SymbolOverrideControllerUtil.AddToPrefab(go);
        }
    }
}
