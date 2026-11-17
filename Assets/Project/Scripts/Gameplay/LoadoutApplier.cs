using System.Collections;
using JUTPS;
using JUTPS.InventorySystem;

using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Puts the player's battle royale loadout and ability upgrades (from the Nakama profile, validated there) on the character this
    /// machine controls when it spawns: the starting sidearm, melee and lethal are items the character already carries (locked until
    /// looted), so they are only unlocked; Sonar and Medkit get the values of their upgrade level from the server's table.
    /// Primaries are never given: in a battle royale they are found in the field.
    /// </summary>
    public static class LoadoutApplier
    {
        public static string LastApplied { get; private set; } = "";

        public static void ApplyWhenReady(MonoBehaviour host, JUCharacterController character)
        {
            if (host == null || character == null) return;
            host.StartCoroutine(ApplyRoutine(character));
        }

        private static IEnumerator ApplyRoutine(JUCharacterController character)
        {
            // the inventory gives its items their switch ids in Start
            yield return null;
            yield return new WaitForSeconds(0.4f);
            if (character == null) yield break;
            // a sign-in still running (a match joined straight away) gets a few seconds, then the profile is fetched if nobody did
            var auth = AuthenticationService.Instance;
            for (float t = 0f; auth != null && !auth.IsAuthenticated && t < 15f; t += Time.unscaledDeltaTime) yield return null;
            if (PlayerProgression.UsesServer && PlayerProgression.Tables == null)
            {
                var load = PlayerProgression.LoadAsync();
                for (float t = 0f; !load.IsCompleted && t < 15f; t += Time.unscaledDeltaTime) yield return null;
            }
            if (character == null) yield break;
            var tables = PlayerProgression.Tables;
            var p = PlayerProgression.Current;
            if (tables == null || p == null)
            {
                LastApplied = "none (no server profile)";
                Debug.Log("[Loadout] No server profile: spawning with the default kit.");
                yield break;
            }

            var inv = character.GetComponent<JUInventory>();
            var loadout = p.loadout ?? new LoadoutData();
            if (inv != null)
            {
                // a choice is only honoured if the player's level allows it (the server checked it when it was saved; checked again here)
                if (Allowed(tables, "sidearm", loadout.sidearm, p.level) && loadout.sidearm == "p226dual") Unlock(inv, "P226 Dual", 1);
                if (Allowed(tables, "melee", loadout.melee, p.level) && loadout.melee != "none") Unlock(inv, loadout.melee == "katana" ? "Katana" : "Baseball Bat", 1);
                if (Allowed(tables, "lethal", loadout.lethal, p.level) && loadout.lethal == "grenade") Unlock(inv, "Granade", 1);
                if (loadout.sidearm == "p226dual" && Allowed(tables, "sidearm", "p226dual", p.level)) SwitchTo(character, inv, "P226 Dual");
            }

            int sonarLevel = PlayerProgression.SkillLevel("sonar"), medkitLevel = PlayerProgression.SkillLevel("medkit");
            var sonar = character.GetComponentInChildren<PlayerSonarSkill>(true);
            if (sonar == null) sonar = Object.FindFirstObjectByType<PlayerSonarSkill>();
            if (sonar != null)
                sonar.ApplyUpgrade(tables.SkillEffect("sonar", sonarLevel, "radius", sonar.scanRadius), tables.SkillEffect("sonar", sonarLevel, "cooldown", sonar.cooldown),
                                   Mathf.RoundToInt(tables.SkillEffect("sonar", sonarLevel, "charges", sonar.MaxCharges)));
            var medkit = character.GetComponentInChildren<PlayerMedkitSystem>(true);
            if (medkit == null) medkit = PlayerMedkitSystem.Instance;
            if (medkit != null)
                medkit.ApplyUpgrade(tables.SkillEffect("medkit", medkitLevel, "heal", medkit.HealAmount), Mathf.RoundToInt(tables.SkillEffect("medkit", medkitLevel, "carry", medkit.MaxMedkits)),
                                    Mathf.RoundToInt(tables.SkillEffect("medkit", medkitLevel, "starting", 2)));

            LastApplied = $"sidearm={loadout.sidearm} melee={loadout.melee} lethal={loadout.lethal} sonar={sonarLevel} medkit={medkitLevel}";
            Debug.Log("[Loadout] Applied " + LastApplied);
        }

        private static bool Allowed(ProgressionTables t, string slot, string item, int level) { return !string.IsNullOrEmpty(item) && level >= t.UnlockLevel(slot, item); }

        private static void Unlock(JUInventory inv, string itemName, int quantity)
        {
            bool found = false;
            foreach (var list in new[] { inv.HoldableItensRightHand, inv.HoldableItensLeftHand })
            {
                if (list == null) continue;
                foreach (var item in list)
                {
                    if (item == null || !string.Equals(item.ItemName, itemName, System.StringComparison.OrdinalIgnoreCase)) continue;
                    item.Unlocked = true;
                    item.ItemQuantity = Mathf.Max(item.ItemQuantity, quantity);
                    found = true;
                }
            }
            if (!found) Debug.LogWarning("[Loadout] The character carries no item named " + itemName + ".");
        }

        private static void SwitchTo(JUCharacterController c, JUInventory inv, string itemName)
        {
            if (inv.HoldableItensRightHand == null) return;
            foreach (var item in inv.HoldableItensRightHand)
            {
                if (item == null || !string.Equals(item.ItemName, itemName, System.StringComparison.OrdinalIgnoreCase)) continue;
                c.SwitchToItem(item.ItemSwitchID, true);
                return;
            }
        }
    }
}
