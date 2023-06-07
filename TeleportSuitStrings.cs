using STRINGS;
using System.Security.Policy;
using static PartyCake.CreatingStates;
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
                    public static LocString NAME = STRINGS.UI.FormatAsLink("Teleport Suit", "TELEPORT_SUIT");

                    public static LocString DESC = "Say goodbye to  commutes";

                    public static LocString EFFECT = "Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME + "\n Each teleportation consumes power"
;
                    public static LocString RECIPE_DESC = "Allowing Duplicants to teleport，and provides the protection as " + STRINGS.EQUIPMENT.PREFABS.LEAD_SUIT.NAME
;
                    public static LocString GENERICNAME = "Teleport Suit";

                    public static LocString BATTERY_EFFECT_NAME = "Teleport Suit Battery";

                    public static LocString SUIT_OUT_OF_BATTERIES = "Teleport Suit Batteries Empty";

                    public static LocString WORN_NAME = STRINGS.UI.FormatAsLink("Worn Teleport Suit", "TELEPORT_SUIT");

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
                    public static LocString NAME = STRINGS.UI.FormatAsLink("Teleport Suit Dock", "TELEPORTSUITLOCKER")
;
                    public static LocString DESC = "Teleport suit docks can refill teleport suits with air and empty them of waste."
;
                    public static LocString EFFECT = "Stores"+TeleportSuitStrings.EQUIPMENT.PREFABS.TELEPORT_SUIT.NAME+" and refuels them with Oxygen and power\nEmpties suits of pollution\n" +
                        "Checkpoints are not required, Duplicants wear teleport suits during work time and take them off during break time (Can be cancelled in mod settings)";
                }
            }
        }
        public static class UI
        {
            public static class FRONTEND
            {
                public static class TELEPORTSUITMOD
                {
                    public static LocString SHOULD_DROP_DURING_BREAK_TITLE = "Should Duplicants take off teleport suit during break time";
                }
            }
            public static class OVERLAYS
            {
                public static class TELEPORTABLE
                {
                    public static LocString NAME = "TELEPORTABLE OVERLAY";
                    public static LocString BUTTON = "Teleportable Overlay";
                    public static LocString TELEPORTABLEAREA = "Teleportable Area";
                    public static LocString TELEPORTRESTRICTEDAREA = "Teleport Restricted Area";
                    public static class ToolTip
                    {
                        public static LocString TELEPORTABLEAREA = "<b>Teleportable Area</b>\nDuplicants wearing <style=\"KKeyword\">Teleport Suit</style> can teleport to these location";

                        public static LocString TELEPORTRESTRICTEDAREA = "<b>Teleport Restricted Area</b>\nDuplicants are restricted from teleporting to these areas\nCan be added or removed using the <style=\"KKeyword\">Teleporting Restriction Tool</style> in the lower right corner";

                    }
                }
            }
            public static class TOOLTIPS
            {
                public static LocString TELEPORTABLEOVERLAYSTRING = "Display teleportablity of <style=\"KKeyword\">Teleport Suit</style>";
            }
        }
        public static class TELEPORT_RESTRICT_TOOL
        {
            public static string PLACE_ICON_NAME = "TELEPORTSUITMOD.TOOL.TELEPORTRESTRICTTOOL.PLACER";
            public static string TOOL_ICON_NAME = "TELEPORTSUITMOD.TOOL.TELEPORTRESTRICTTOOL.ICON";

            public static string ACTION_KEY = "TELEPORTSUITMOD.ACTION.CHANGESETTINGS";

            public static LocString TOOL_DESCRIPTION = "Add or remove teleport restrict area";
            public static LocString TOOL_TITLE = "Teleport Restrict Tool";
            public static LocString ACTION_TITLE = "Teleport Restrict Tool";
            public static LocString TOOL_NAME_APPLYFOG = "Add restrict area";
            public static LocString TOOL_NAME_REMOVEFOG = "Remove restrict area";
        }
        public static class DUPLICANTS
        {
            public static class CHORES
            {
                public static class PRECONDITIONS
                {
                    public static LocString DOES_DUPE_HAS_TELEPORT_SUIT_AND_NEED_CHARGING = "Duplicant does not wearing teleport suit that need to be refilled "
;
                    public static LocString DOES_DUPE_HAS_TELEPORTSUIT = "Duplicant does not wearing teleport suit"
;
                    public static LocString CAN_TELEPORT_SUIT_LOCKER_DROP_OFFSUIT = "There is already teleport suit in this dock"
;
                    public static LocString DOES_TELEPORT_SUIT_LOCKER_HAS_AVAILABLE_SUIT = "There is no teleport suit avalible in this dock"
;
                    public static LocString DOES_DUPE_AT_EQUIP_TELEPORT_SUIT_SCHEDULE = "Duplicant is not at work time";

                    public static LocString DOES_DUPE_HAS_NO_TELEPORT_SUIT = "Duplicant is already wearing a teleport suit";
                }
                public static class RETURNTELEPORTSUITURGENT
                {
                    public static LocString NAME = "Refill Teleport Suit";

                    public static LocString STATUS = "Refilling teleport suit";

                    public static LocString TOOLTIP = "This duplicant's teleport suit need refill.Docking suit";
                }
                public static class RETURNTELEPORTSUITBREAKTIME
                {
                    public static LocString NAME = "Return Teleport Suit";

                    public static LocString STATUS = "Returning teleport suit";

                    public static LocString TOOLTIP = "Due to MOD setting ,duplicants will return teleport suit during break time";
                }
                public static class EQUIPTELEPORTSUIT
                {
                    public static LocString NAME = "Wear Teleport Suit";

                    public static LocString STATUS = "Wearing teleport suit";

                    public static LocString TOOLTIP = "When a teleport suit is available, replicants will wear it during work time";
                }
            }
        }
    }
}
