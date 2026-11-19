using System;
using System.Collections.Generic;
using System.Linq;
using Railbound.Maps.Layouts;
using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>One map: what it is for, how big it is, how it looks, and the layout that builds it (null for MAP-2 itself).</summary>
    public sealed class MapDefinition
    {
        public string Id;                 // stable key: -map ID, the check between server and client
        public string Name;               // as the mode screen shows it
        public string ModeId;             // the GameModeCatalog mode this map is made for
        public string Concept;
        public int Players;               // match capacity (players and bots)
        public Vector2 PlayArea;          // metres, x and z, centred on the origin
        public float TerrainSize;         // metres, square, centred on the origin (bigger than the play area: the view)
        public int Seed;
        public Func<MapLayout> Layout;
        public int Vehicles;              // vehicles spawned for BR-style maps (0 on arenas)
        public bool DesignedSpawns;       // players and bots start on the map's own spawn points (arenas)

        [Header("Look")]
        public float SunElevation = 45f;  // degrees above the horizon
        public float SunAzimuth = 135f;   // degrees from north, clockwise
        public Color SunTint = Color.white;
        public float FogDistance = 0f;    // metres of fog visibility (0: keep the scene's fog)

        public bool IsMap2 => Layout == null;
        public float PlayRadius => Mathf.Min(PlayArea.x, PlayArea.y) * 0.5f;
    }

    /// <summary>Every map in the game, one per mode.</summary>
    public static class MapCatalog
    {
        public const string Map2Id = "map2";

        public static readonly IReadOnlyList<MapDefinition> All = new List<MapDefinition>
        {
            new MapDefinition
            {
                Id = Map2Id, Name = "MAP-2", ModeId = "classic", Players = 20,
                Concept = "The large valley map of Classic Battle Royale.",
                PlayArea = new Vector2(1600f, 1600f), TerrainSize = 1600f,
            },
            new MapDefinition
            {
                Id = "railhead", Name = "RAILHEAD", ModeId = "resurgence", Players = 20, Seed = 4101,
                Concept = "A compact rail junction town: four districts around a rail yard, tight and fast for respawn fights.",
                PlayArea = new Vector2(520f, 520f), TerrainSize = 900f, Vehicles = 4,
                Layout = () => new RailheadLayout(),
                SunElevation = 38f, SunAzimuth = 120f, SunTint = new Color(1f, 0.95f, 0.88f), FogDistance = 1300f,
            },
            new MapDefinition
            {
                Id = "timberline", Name = "TIMBERLINE", ModeId = "tactical", Players = 20, Seed = 4202,
                Concept = "Forested highland of ridges, gullies and rock outcrops: natural cover only.",
                PlayArea = new Vector2(900f, 900f), TerrainSize = 1300f, Vehicles = 0,
                Layout = () => new TimberlineLayout(),
                SunElevation = 30f, SunAzimuth = 210f, SunTint = new Color(1f, 0.92f, 0.82f), FogDistance = 1500f,
            },
            new MapDefinition
            {
                Id = "depot9", Name = "DEPOT 9", ModeId = "tdm", Players = 12, Seed = 4303, DesignedSpawns = true,
                Concept = "A mirrored three-lane train depot: engine shed in the middle, containers and a platform on the flanks.",
                PlayArea = new Vector2(120f, 76f), TerrainSize = 500f,
                Layout = () => new Depot9Layout(),
                SunElevation = 52f, SunAzimuth = 160f, FogDistance = 600f,
            },
            new MapDefinition
            {
                Id = "foundry", Name = "FOUNDRY", ModeId = "ffa", Players = 8, Seed = 4404, DesignedSpawns = true,
                Concept = "An industrial courtyard laid out as a ring around a two-floor furnace hall.",
                PlayArea = new Vector2(76f, 76f), TerrainSize = 420f,
                Layout = () => new FoundryLayout(),
                SunElevation = 24f, SunAzimuth = 250f, SunTint = new Color(1f, 0.84f, 0.68f), FogDistance = 380f,
            },
            new MapDefinition
            {
                Id = "switchback", Name = "SWITCHBACK", ModeId = "gungame", Players = 8, Seed = 4505, DesignedSpawns = true,
                Concept = "Two tracks crossing at a signal box, wagons for cover, short loops that keep everyone fighting.",
                PlayArea = new Vector2(60f, 60f), TerrainSize = 400f,
                Layout = () => new SwitchbackLayout(),
                SunElevation = 60f, SunAzimuth = 100f, FogDistance = 500f,
            },
            new MapDefinition
            {
                Id = "signalhill", Name = "SIGNAL HILL", ModeId = "control", Players = 12, Seed = 4606, DesignedSpawns = true,
                Concept = "Three zones in a line: A relay hut, B radio mast on the hill, C pump house.",
                PlayArea = new Vector2(160f, 110f), TerrainSize = 560f,
                Layout = () => new SignalHillLayout(),
                SunElevation = 34f, SunAzimuth = 300f, SunTint = new Color(1f, 0.93f, 0.85f), FogDistance = 650f,
            },
            new MapDefinition
            {
                Id = "blackwater", Name = "BLACKWATER", ModeId = "snd", Players = 12, Seed = 4707, DesignedSpawns = true,
                Concept = "Attackers west, defenders east; site A at a fuel depot, site B in a warehouse, a rail cut between them.",
                PlayArea = new Vector2(150f, 96f), TerrainSize = 540f,
                Layout = () => new BlackwaterLayout(),
                SunElevation = 18f, SunAzimuth = 80f, SunTint = new Color(1f, 0.86f, 0.74f), FogDistance = 450f,
            },
            new MapDefinition
            {
                Id = "dustfall", Name = "DUSTFALL", ModeId = "extraction", Players = 20, Seed = 4808,
                Concept = "Dry canyon and mesa country: four loot areas and three extraction pads at the edges.",
                PlayArea = new Vector2(1000f, 1000f), TerrainSize = 1400f, Vehicles = 6,
                Layout = () => new DustfallLayout(),
                SunElevation = 48f, SunAzimuth = 190f, SunTint = new Color(1f, 0.9f, 0.78f), FogDistance = 2200f,
            },
        };

        public static MapDefinition Map2 => Find(Map2Id);

        /// <summary>A map by its ID or its name (any case, spaces and dashes ignored), or null.</summary>
        public static MapDefinition Find(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return null;
            string key = Normalize(idOrName);
            return All.FirstOrDefault(m => Normalize(m.Id) == key || Normalize(m.Name) == key);
        }

        /// <summary>The map made for a mode, or null.</summary>
        public static MapDefinition ForMode(string modeId) => All.FirstOrDefault(m => m.ModeId == modeId);

        private static string Normalize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
