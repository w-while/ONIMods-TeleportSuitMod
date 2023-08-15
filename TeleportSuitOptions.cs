using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace TeleportSuitMod
{
    [RestartRequired]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigFile("TeleportSuitModConfig.json" , SharedConfigLocation: true)]
    public class TeleportSuitOptions
    {
        public const int CURRENT_CONFIG_VERSION = 3;



        private static TeleportSuitOptions _instance;
        public static TeleportSuitOptions Instance
        {
            get
            {
                var opts = _instance;
                if (opts == null)
                {
                    opts = POptions.ReadSettings<TeleportSuitOptions>();
                    if (opts == null || opts.ConfigVersion < CURRENT_CONFIG_VERSION)
                    {
                        opts = new TeleportSuitOptions();
                        POptions.WriteSettings(opts);
                    }
                    _instance = opts;
                }
                return opts;
            }
        }


        public TeleportSuitOptions()
        {
            ConfigVersion = CURRENT_CONFIG_VERSION;
        }
        [JsonProperty]
        public int ConfigVersion
        {
            get; set;
        }
        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SHOULD_DROP_DURING_BREAK_TITLE" , "" , null)]
        [JsonProperty]
        public bool ShouldDropDuringBreak { get; set; } = true;
        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SHOULD_DROP_DURING_SLEEP_TITLE" , "" , null)]
        [JsonProperty]
        public bool ShouldDropDuringSleep { get; set; } = true;


        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.TELEPORT_TIMES_FULL_CHARGE_TITLE" , "" , null)]
        [Limit(1 , 10000)]
        [JsonProperty]
        public int teleportTimesFullCharge { get; set; } = 100;

        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SUIT_OXYGEN_CAPACITY_TITTLE" , "" , null)]
        [JsonProperty]
        [Limit(1 , 10000)]
        public int suitOxygenCapacity { get; set; } = 75;

        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SUIT_LOCKER_OXYGEN_CAPACITY_TITTLE" , "" , null)]
        [JsonProperty]
        [Limit(1 , 10000)]
        public int suitLockerOxygenCapacity { get; set; } = 100;

        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SUIT_LOCKER_POWER_INPUT_TITTLE" , "" , null)]
        [JsonProperty]
        [Limit(0 , 10000)]
        public int suitLockerPowerInput { get; set; } = 200;

        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SUIT_BATTERY_CHARGE_TIME_TITTLE" , "" , null)]
        [JsonProperty]
        [Limit(1 , 10000)]
        public float suitBatteryChargeTime { get; set; } = 60f;

        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.UNEQUIP_TIME_TITTLE" , "STRINGS.UI.FRONTEND.TELEPORTSUITMOD.UNEQUIP_TIME_TOOLTIP" , null)]
        [JsonProperty]
        [Limit(0 , 30)]
        public float unEquipTime { get; set; } = 3f;
    }
}
