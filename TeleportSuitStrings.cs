using STRINGS;
using static STRINGS.ELEMENTS;

namespace TeleportSuitMod
{
    public static class TeleportSuitStrings
    {
        public static LocString TELEPORTSUIT_BATTERY = string.Concat(EQUIPMENT.PREFABS.TELEPORT_SUIT.BATTERY_EFFECT_NAME, " ({0})");
        public static class EQUIPMENT
        {
            public static class PREFABS
            {
                public class TELEPORT_SUIT
                {
                    //STRINGS.UI.FormatAsLink("Teleport Suit", "TELEPORT_SUIT");
                    public static LocString NAME = STRINGS.UI.FormatAsLink("传送服", "TELEPORT_SUIT");

                    //Say goodbye to  commutes
                    public static LocString DESC = "和通勤说拜拜";

                    //"Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME + "\n Each teleportation consumes power"
                    public static LocString EFFECT = "让复制人可以瞬间传送，并提供和<link=\"LEADSUIT\">铅服</link>相当的防护能力\n每次传送将消耗电量";

                    //"Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME
                    public static LocString RECIPE_DESC = "让复制人可以瞬间传送，并提供和<link=\"LEADSUIT\">铅服</link>相当的防护能力";

                    //"Teleport Suit"
                    public static LocString GENERICNAME = "传送服";

                    //"Teleport Suit Battery"
                    public static LocString BATTERY_EFFECT_NAME = "传送服电量";

                    //"Teleport Suit Batteries Empty"
                    public static LocString SUIT_OUT_OF_BATTERIES = "传送服电量为空";

                    //STRINGS.UI.FormatAsLink("Worn Teleport Suit", "TELEPORT_SUIT")
                    public static LocString WORN_NAME = STRINGS.UI.FormatAsLink("破损的传送服", "TELEPORT_SUIT");

                    //"A worn out " + STRINGS.UI.FormatAsLink("Teleport Suit", "TELEPORT_SUIT") + ".\n\nSuits can be repaired at an " + STRINGS.UI.FormatAsLink("Exosuit Forge", "SUITFABRICATOR") + "."
                    public static LocString WORN_DESC = "一件破损的 " + STRINGS.UI.FormatAsLink("传送服", "TELEPORT_SUIT") + "。\n可以在 " + STRINGS.UI.FormatAsLink("Exosuit Forge", "SUITFABRICATOR") + "中修复。";
                }
            }
        }
        public static class BUILDINGS
        {
            public class PREFABS
            {
                public class TELEPORTSUITLOCKER
                {
                    //STRINGS.UI.FormatAsLink("Lead Suit Dock", "TELEPORTSUITLOCKER")
                    public static LocString NAME = STRINGS.UI.FormatAsLink("传送服存放柜", "TELEPORTSUITLOCKER");

                    //"Teleport suit docks can refill teleport suits with air and empty them of waste."
                    public static LocString DESC = "传送服存放柜可以为传送服补充资源，并清空传送服中的废物";

                    //"Stores"+TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME+"and refuels them with Oxygen and power\nEmpties suits of pollution\n" +
                    //    "Checkpoints are not required, Duplicants wear teleport suits during work time and take them off during break time (Can be cancelled in mod settings)";
                    public static LocString EFFECT = "存放"+TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME+"并为其补充氧气和电量\n清空传送服中的污染物\n" +
                        "不需要检查点，复制人会在工作时间穿上传送服并在休息时间脱下（可在模组设置中取消）";
                }
            }
        }
        public static class UI
        {
            public static class FRONTEND
            {
                public static class TELEPORTSUIT
                {
                    //"Should Duplicants take off teleport suit during break time"
                    public static LocString SHOULD_DROP_DURING_BREAK_TITLE = "复制人是否该在休息时间脱下传送服";
                }
            }
        }
    }
}
