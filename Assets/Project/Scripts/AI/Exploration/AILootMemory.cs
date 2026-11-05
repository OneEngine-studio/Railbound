using System.Collections.Generic;
using UnityEngine;
using JUTPS.InventorySystem;
using JUTPS.WeaponSystem;
using Railbound.Gameplay;

namespace Project.AI.Exploration
{
    public enum LootKind
    {
        Weapon = 0,
        Ammo = 1
    }

    public struct LootEntry
    {
        public Component Source;
        public LootKind Kind;

        public Vector3 Position => Source != null ? Source.transform.position : Vector3.zero;

        /// <summary>False once the object is destroyed, looted, or picked up by a character.</summary>
        public bool IsAvailable()
        {
            if (Source == null || !Source.gameObject.activeInHierarchy) return false;

            if (Kind == LootKind.Ammo)
            {
                var ammo = Source as AmmoBoxPickup;
                return ammo != null && !ammo.IsCollected;
            }

            return Source.GetComponentInParent<JUInventory>() == null;
        }
    }

    /// <summary>
    /// Shared, throttled world scan of loot on the ground (weapons lying in the map and ammo boxes).
    /// One scan serves every bot, so 20 agents do not each call FindObjectsOfType.
    /// </summary>
    public static class AILootRegistry
    {
        private static readonly List<LootEntry> all = new List<LootEntry>();
        private static float nextRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            nextRefreshTime = 0f;
        }

        public static List<LootEntry> GetAll()
        {
            if (Time.time >= nextRefreshTime)
            {
                nextRefreshTime = Time.time + 1.5f;
                Rebuild();
            }
            return all;
        }

        private static void Rebuild()
        {
            all.Clear();

            var weapons = Object.FindObjectsByType<Weapon>(FindObjectsSortMode.None);
            for (int i = 0; i < weapons.Length; i++)
            {
                var w = weapons[i];
                if (w == null || w.GetComponentInParent<JUInventory>() != null) continue;
                all.Add(new LootEntry { Source = w, Kind = LootKind.Weapon });
            }

            var ammoBoxes = Object.FindObjectsByType<AmmoBoxPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < ammoBoxes.Length; i++)
            {
                var a = ammoBoxes[i];
                if (a == null || a.IsCollected) continue;
                all.Add(new LootEntry { Source = a, Kind = LootKind.Ammo });
            }
        }
    }

    /// <summary>
    /// What one bot currently knows about: loot it has seen within its sense radius.
    /// Loot outside the radius stays unknown, so the bot has to explore to find it.
    /// </summary>
    public sealed class AILootMemory
    {
        private readonly List<LootEntry> known = new List<LootEntry>();
        private readonly HashSet<Component> ignored = new HashSet<Component>();

        public int Count => known.Count;

        public void Ignore(Component source)
        {
            if (source != null) ignored.Add(source);
        }

        public void Clear()
        {
            known.Clear();
            ignored.Clear();
        }

        public void Sense(Vector3 position, float radius)
        {
            for (int i = known.Count - 1; i >= 0; i--)
            {
                if (!known[i].IsAvailable()) known.RemoveAt(i);
            }

            float r2 = radius * radius;
            var world = AILootRegistry.GetAll();
            for (int i = 0; i < world.Count; i++)
            {
                LootEntry e = world[i];
                if (e.Source == null || ignored.Contains(e.Source) || !e.IsAvailable()) continue;
                if ((e.Position - position).sqrMagnitude > r2) continue;
                if (Contains(e.Source)) continue;
                known.Add(e);
            }
        }

        private bool Contains(Component source)
        {
            for (int i = 0; i < known.Count; i++)
            {
                if (known[i].Source == source) return true;
            }
            return false;
        }

        /// <summary>Nearest known loot of a wanted kind.</summary>
        public bool TryGetBest(Vector3 position, bool wantWeapon, bool wantAmmo, out LootEntry best, out float distance)
        {
            best = default;
            distance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < known.Count; i++)
            {
                LootEntry e = known[i];
                if (e.Kind == LootKind.Weapon && !wantWeapon) continue;
                if (e.Kind == LootKind.Ammo && !wantAmmo) continue;
                if (!e.IsAvailable() || ignored.Contains(e.Source)) continue;

                float d = Vector3.Distance(position, e.Position);
                if (d < distance)
                {
                    distance = d;
                    best = e;
                    found = true;
                }
            }

            return found;
        }
    }
}
