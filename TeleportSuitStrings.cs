using STRINGS;
using System.Security.Policy;
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
                    //STRINGS.UI.FormatAsLink("传送服", "TELEPORT_SUIT")
                    public static LocString NAME = STRINGS.UI.FormatAsLink("Teleport Suit", "TELEPORT_SUIT");

                    //"和通勤说拜拜"
                    public static LocString DESC = "Say goodbye to  commutes";

                    //"让复制人可以瞬间传送，并提供和<link=\"LEADSUIT\">铅服</link>相当的防护能力\n每次传送将消耗电量"
                    public static LocString EFFECT = "Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME + "\n Each teleportation consumes power"
;
                    //"让复制人可以瞬间传送，并提供和<link=\"LEADSUIT\">铅服</link>相当的防护能力"
                    public static LocString RECIPE_DESC = "Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME
;
                    //"传送服"
                    public static LocString GENERICNAME = "Teleport Suit";

                    //"传送服电量"
                    public static LocString BATTERY_EFFECT_NAME = "Teleport Suit Battery";

                    //"传送服电量为空"
                    public static LocString SUIT_OUT_OF_BATTERIES = "Teleport Suit Batteries Empty";

                    //STRINGS.UI.FormatAsLink("破损的传送服", "TELEPORT_SUIT")
                    public static LocString WORN_NAME = STRINGS.UI.FormatAsLink("Worn Teleport Suit", "TELEPORT_SUIT");

                    //"一件破损的 " + STRINGS.UI.FormatAsLink("传送服", "TELEPORT_SUIT") + "。\n可以在 " + STRINGS.UI.FormatAsLink("Exosuit Forge", "SUITFABRICATOR") + "中修复。"
                    public static LocString WORN_DESC = "A worn out " + STRINGS.UI.FormatAsLink("Teleport Suit", "TELEPORT_SUIT") + ".\n\nSuits can be repaired at an " + STRINGS.UI.FormatAsLink("Exosuit Forge", "SUITFABRICATOR") + "."
;
                }
            }
        }
        public static class BUILDINGS
        {
            public class PREFABS
            {
                public class TELEPORTSUITLOCKER
                {
                    //STRINGS.UI.FormatAsLink("传送服存放柜", "TELEPORTSUITLOCKER")
                    public static LocString NAME = STRINGS.UI.FormatAsLink("Teleport Suit Dock", "TELEPORTSUITLOCKER")
;
                    //"传送服存放柜可以为传送服补充资源，并清空传送服中的废物"
                    public static LocString DESC = "Teleport suit docks can refill teleport suits with air and empty them of waste."
;
                    //"存放"+TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME+"并为其补充氧气和电量\n清空传送服中的污染物\n" +
                    //    "不需要检查点，复制人会在工作时间穿上传送服并在休息时间脱下（可在模组设置中取消）"
                    public static LocString EFFECT = "Stores"+TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME+" and refuels them with Oxygen and power\nEmpties suits of pollution\n" +
                        "Checkpoints are not required, Duplicants wear teleport suits during work time and take them off during break time (Can be cancelled in mod settings)";
                }
            }
        }
        public static class UI
        {
            public static class FRONTEND
            {
                public static class TELEPORTSUIT
                {
                    //"复制人是否该在休息时间脱下传送服"
                    public static LocString SHOULD_DROP_DURING_BREAK_TITLE = "Should Duplicants take off teleport suit during break time"
;
                }
            }
            public static class OVERLAYS
            {
                public static class TELEPORTABLE
                {
                    //"传送概览"
                    public static LocString NAME = "TELEPORTABLE OVERLAY";
                    //"传送概览"
                    public static LocString BUTTON = "Teleportable Overlay";
                    //"可传送区域"
                    public static LocString TELEPORTABLEAREA = "Teleportable area";
                    public static class ToolTip
                    {
                        //"穿着传送服的复制人可以传送到这些位置"
                        public static LocString TELEPORTABLEAREA = "Duplicants wearing teleport suit can teleport to these location";
                    }
                }
            }
            public static class TOOLTIPS
            {
                //显示传送服可以到达的区域
                public static LocString TELEPORTABLEOVERLAYSTRING = "Display teleportablity of teleport suit";
            }
        }
        public static class DUPLICANTS
        {
            public static class CHORES
            {
                public static class PRECONDITIONS
                {
                    //"复制人没有穿戴需要补充资源的传送服"
                    public static LocString DOES_DUPE_HAS_TELEPORT_SUIT_AND_NEED_CHARGING = "Duplicant does not wearing teleport suit that need to be refilled "
;
                    //"复制人没有穿戴传送服"
                    public static LocString DOES_DUPE_HAS_TELEPORTSUIT = "Duplicant does not wearing teleport suit"
;
                    //"当前存放柜已有传送服"
                    public static LocString CAN_TELEPORT_SUIT_LOCKER_DROP_OFFSUIT = "There is already teleport suit in this dock"
;
                    //"当前存放柜没有可供复制人穿戴的传送服"
                    public static LocString DOES_TELEPORT_SUIT_LOCKER_HAS_AVAILABLE_SUIT = "There is no teleport suit avalible in this dock"
;
                    //"复制人不在工作时间"
                    public static LocString DOES_DUPE_AT_EQUIP_TELEPORT_SUIT_SCHEDULE = "Duplicant is not at work time";

                    //"复制人已经穿戴传送服"
                    public static LocString DOES_DUPE_HAS_NO_TELEPORT_SUIT = "Duplicant is already wearing a teleport suit";
                }
                public static class RETURNTELEPORTSUITURGENT
                {
                    //"补充传送服"
                    public static LocString NAME = "Refill Teleport Suit";

                    //"正在补充传送服"
                    public static LocString STATUS = "Refilling teleport suit";

                    //"这名复制人身上的传送服需要补充资源，送回存放柜"
                    public static LocString TOOLTIP = "This duplicant's teleport suit need refill.Docking suit";
                }
                public static class RETURNTELEPORTSUITBREAKTIME
                {
                    //"存储传送服"
                    public static LocString NAME = "Return Teleport Suit";

                    //"正在存储传送服"
                    public static LocString STATUS = "Returning teleport suit";

                    //"由于模组设置，复制人在休息时间会送回身上穿的传送服"
                    public static LocString TOOLTIP = "Due to MOD setting ,duplicants will return teleport suit during break time";
                }
                public static class EQUIPTELEPORTSUIT
                {
                    //"装备传送服"
                    public static LocString NAME = "Wear Teleport Suit";

                    //"正在前去装备传送服"
                    public static LocString STATUS = "Wearing teleport suit";

                    //"当有可用的传送服时，复制人会在工作时间穿上它"
                    public static LocString TOOLTIP = "When a teleport suit is available, replicants will wear it during work time";
                }
            }
        }

    }
}
