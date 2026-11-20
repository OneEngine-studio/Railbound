using System;
using Railbound.UI;
using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>
    /// Which map this peer builds when the match scene loads. A dedicated server takes <c>-map ID</c> (or <c>-mode ID</c>, the map made
    /// for that mode; a fleet server is told its map at the allocation, <see cref="ServerMapSwitch"/>). A client takes <c>-map ID</c>,
    /// otherwise the map of the mode the player queued for (for Classic BR the map the player chose). MAP-2 is the default, so
    /// Classic BR is unchanged. The server tells every client its map (<see cref="MapSync"/>), so a mismatch is caught at once.
    /// </summary>
    public static class MapSelection
    {
        private static MapDefinition overrideMap;

        /// <summary>The map the next match scene builds.</summary>
        public static MapDefinition Current
        {
            get
            {
                if (overrideMap != null) return overrideMap;
                var fromArgs = MapCatalog.Find(Arg("-map"));
                if (fromArgs != null) return fromArgs;
                var forMode = MapCatalog.ForMode(Arg("-mode"));
                if (forMode != null) return forMode;
                if (!IsDedicatedServer)
                {
                    try
                    {
                        var m = GameModeCatalog.SelectedMap;   // the mode's map, or the map chosen for Classic BR
                        if (m != null) return m;
                    }
                    catch (Exception) { /* no catalog (tests): default */ }
                }
                return MapCatalog.Map2;
            }
        }

        /// <summary>Chooses the map for the next match scene (the server's word wins over the client's own guess).</summary>
        public static void Set(MapDefinition map) { overrideMap = map; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { overrideMap = null; }

        public static bool IsDedicatedServer
        {
            get
            {
                if (Application.isBatchMode && HasArg("-server")) return true;
                return HasArg("-dedicated");
            }
        }

        private static bool HasArg(string name) => Array.Exists(Environment.GetCommandLineArgs(), a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        private static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }
    }
}
