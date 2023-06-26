using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace TeleportSuitMod
{
    [RestartRequired]
    [JsonObject(MemberSerialization.OptIn)]
    public class TeleportSuitOptions
    {
        public const int CURRENT_CONFIG_VERSION = 2;

        private static TeleportSuitOptions instance;
        public static TeleportSuitOptions Instance
        {
            get
            {
                var opts = instance;
                if (opts == null)
                {
                    opts = POptions.ReadSettings<TeleportSuitOptions>();
                    if (opts == null || opts.ConfigVersion < CURRENT_CONFIG_VERSION)
                    {
                        opts = new TeleportSuitOptions();
                        POptions.WriteSettings(opts);
                    }
                    instance = opts;
                }
                return opts;
            }
        }
        public TeleportSuitOptions()
        {
            ConfigVersion = CURRENT_CONFIG_VERSION;
        }
        [JsonProperty]
        public int ConfigVersion { get; set; }
        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SHOULD_DROP_DURING_BREAK_TITLE", "", null)]
        [JsonProperty]
        public bool ShouldDropDuringBreak { get; set; } = true;
        [Option("STRINGS.UI.FRONTEND.TELEPORTSUITMOD.SHOULD_DROP_DURING_SLEEP_TITLE", "", null)]
        [JsonProperty]
        public bool ShouldDropDuringSleep { get; set; } = true;
    }
}
