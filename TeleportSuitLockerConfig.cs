using PeterHan.PLib.Buildings;
using TUNING;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleportSuitLockerConfig : IBuildingConfig
    {
        public const string ID = "TeleportSuitLocker";
        internal static PBuilding TeleportSuitLockerTemplate;
        public static AssignableSlot TeleportSuitSlot;
        public static string techStringDlc = "RadiationProtection";
        public static string techStringVanilla = "Jetpacks";

        public static PBuilding CreateBuilding()
        {
            string techString = "";
            if (DlcManager.IsExpansion1Id("EXPANSION1_ID"))
            {
                techString = techStringDlc;
            }
            else
            {
                techString = techStringVanilla;
            }
            return TeleportSuitLockerTemplate = new PBuilding(ID, TeleportSuitStrings.BUILDINGS.PREFABS.TELEPORTSUITLOCKER.NAME)
            {
                //AddAfter = PressureDoorConfig.ID,
                Animation = "changingarea_radiation_kanim",
                Category = "Equipment",
                ConstructionTime = 30.0f,
                Decor = BUILDINGS.DECOR.BONUS.TIER1,
                Description = null,
                EffectText = null,
                Entombs = false,
                Floods = true,
                Width = 2,
                Height = 4,
                HP = 30,
                Ingredients = {
                    new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 2),
                },
                Placement = BuildLocationRule.OnFloor,
                PowerInput = new PowerRequirement(120.0f, new CellOffset(0, 0)),

                Tech = techString,
                Noise=NOISE_POLLUTION.NONE,
            };
        }


        public override BuildingDef CreateBuildingDef()
        {
            LocString.CreateLocStringKeys(typeof(TeleportSuitStrings.BUILDINGS));
            BuildingDef obj = TeleportSuitLockerTemplate.CreateDef();
            obj.BaseMeltingPoint=1600f;
            //string[] rEFINED_METALS = MATERIALS.REFINED_METALS;
            //BuildingDef obj = BuildingTemplates.CreateBuildingDef(construction_mass: new float[2]
            //{
            //    BUILDINGS.CONSTRUCTION_MASS_KG.TIER2[0],
            //    BUILDINGS.CONSTRUCTION_MASS_KG.TIER1[0]
            //}, construction_materials: rEFINED_METALS, melting_point: 1600f, build_location_rule: BuildLocationRule.OnFloor, 
            //noise: NOISE_POLLUTION.NONE, id: "TeleportSuitLocker", width: 2, height: 4, anim: "changingarea_radiation_kanim", 
            //hitpoints: 30, construction_time: 30f, decor: BUILDINGS.DECOR.BONUS.TIER1);

            //obj.RequiresPowerInput = true;
            //obj.EnergyConsumptionWhenActive = 120f;
            obj.PreventIdleTraversalPastBuilding = true;
            obj.InputConduitType = ConduitType.Gas;
            obj.UtilityInputOffset = new CellOffset(0, 2);

            //应该是是否启用
            //obj.Deprecated = !Sim.IsRadiationEnabled();
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SuitIDs, "TeleportSuitLocker");
            return obj;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<SuitLocker>().OutfitTags = new Tag[1] { TeleportSuitGameTags.TeleportSuit };
            go.AddOrGet<TeleportSuitLocker>();
            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 1f;
            conduitConsumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Oxygen).tag;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            conduitConsumer.forceAlwaysSatisfied = true;
            conduitConsumer.capacityKG = 80f;
            go.AddOrGet<AnimTileable>().tags = new Tag[2]
            {
                new Tag("TeleportSuitLocker"),
                new Tag("TeleportSuitMarker")
            };

            Ownable ownable = go.AddOrGet<Ownable>();
            if (TeleportSuitSlot==null)
            {
                TeleportSuitSlot=Db.Get().AssignableSlots.Add(new OwnableSlot(TeleportSuitLockerConfig.ID, TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME));
            }
            ownable.slotID = TeleportSuitSlot.Id;
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
