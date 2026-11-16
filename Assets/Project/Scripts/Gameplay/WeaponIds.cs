using JUTPS;

namespace Railbound.Gameplay
{
    /// <summary>
    /// The ids Nakama knows the weapons by (railbound_report_match weaponKills, weapon mastery, the loadout), from JU's item names.
    /// Keep in step with WEAPON_DEFS in the Nakama module.
    /// </summary>
    public static class WeaponIds
    {
        public static string FromItemName(string itemName)
        {
            switch ((itemName ?? "").Trim().ToLowerInvariant())
            {
                case "p226": return "p226";
                case "p226 dual": return "p226dual";
                case "ump": return "ump";
                case "benelli m4": return "benelli";
                case "m82": case "sniper m82": return "m82";
                case "baseball bat": case "baseballbat": return "bat";
                case "katana": return "katana";
                case "granade": case "grenade": return "grenade";
                default: return "";
            }
        }

        private static readonly string[] Known = { "p226", "p226dual", "ump", "benelli", "m82", "bat", "katana", "grenade" };

        public static bool IsKnown(string id) { return !string.IsNullOrEmpty(id) && System.Array.IndexOf(Known, id) >= 0; }

        /// <summary>The weapon in a character's right hand right now ("" for bare hands or an unknown item).</summary>
        public static string HeldBy(JUCharacterController c)
        {
            var inv = c != null ? c.GetComponent<JUTPS.InventorySystem.JUInventory>() : null;
            var item = inv != null ? inv.HoldableItemInUseInRightHand : null;
            return item != null ? FromItemName(item.ItemName) : "";
        }

        /// <summary>Display name for an id (the lobby shows names from the server's table when it has them).</summary>
        public static string Display(string id)
        {
            switch (id)
            {
                case "p226": return "P226";
                case "p226dual": return "P226 DUAL";
                case "ump": return "UMP";
                case "benelli": return "BENELLI M4";
                case "m82": return "M82";
                case "bat": return "BASEBALL BAT";
                case "katana": return "KATANA";
                case "grenade": return "GRENADE";
                case "none": return "NONE";
                default: return (id ?? "").ToUpperInvariant();
            }
        }
    }
}
