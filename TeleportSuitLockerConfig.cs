using PeterHan.PLib.Buildings;
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



        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(ID, 2, 4, "teleport_suit_locker_kanim", 30, 60f, BUILDINGS.CONSTRUCTION_MASS_KG.TIER3, MATERIALS.ALL_METALS, 1600f, BuildLocationRule.OnFloor, BUILDINGS.DECOR.PENALTY.TIER1, NOISE_POLLUTION.NONE, 1f);
            buildingDef.InputConduitType = ConduitType.Gas;
            buildingDef.UtilityInputOffset = new CellOffset(0, 2);
            buildingDef.BaseMeltingPoint = 1600f;
            buildingDef.PreventIdleTraversalPastBuilding = true;
            buildingDef.RequiresPowerInput = true;
            buildingDef.EnergyConsumptionWhenActive = 120f;

            //应该是是否启用
            //obj.Deprecated = !Sim.IsRadiationEnabled();
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs, ID);
            buildingDef.AddSearchTerms(global::STRINGS.SEARCH_TERMS.ATMOSUIT);
            return buildingDef;
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
                new Tag(ID),
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
