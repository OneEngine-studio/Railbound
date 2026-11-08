using System.Collections.Generic;
using JUTPS;
using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// One combatant of a Battle Royale match (a human player or an AI bot). Added at runtime by
    /// <see cref="BRMatchCoordinator"/> to every character it finds, so no scene or prefab edit is needed.
    /// Bots carry a believable gamertag, never "[BOT]": in the HUD, killfeed and results they look like players.
    /// </summary>
    [DisallowMultipleComponent]
    public class BRParticipant : MonoBehaviour
    {
        public string DisplayName = "";
        public bool IsBot;
        public int Kills;
        /// <summary>1 = winner. Otherwise the place this combatant finished in (e.g. 12 = eliminated when 12 were alive).</summary>
        public int Placement;
        public bool Eliminated;
        public float EliminatedAtTime;

        public JUHealth Health { get; internal set; }
        public JUCharacterController Controller { get; internal set; }

        /// <summary>Network client id of a human (ulong.MaxValue for bots and offline players).</summary>
        public ulong ClientId = ulong.MaxValue;

        public bool IsAlive => !Eliminated && this != null && gameObject.activeInHierarchy && Health != null && !Health.IsDead && Health.Health > 0f;
    }

    /// <summary>Believable player handles for the bots (the plan's "bot disguise").</summary>
    public static class BRIdentityPool
    {
        private static readonly string[] Handles =
        {
            "ShadowViper", "IronClad_X", "KryptonSniper", "DeltaRunner", "ApexPhantom", "ReconHawk", "Vanguard99", "EchoSlayer",
            "CanyonWolf", "TitanRider", "NomadZero", "GhostOperator", "Ghost_X9", "ViperRunner", "KevlarSniper", "DeltaApex42",
            "IronValkyrie", "NightFalcon", "StormBreaker", "RustyCrow", "LoneWolf_7", "SteelNomad", "FrostByte", "CobraKing",
            "BlackMamba", "ZeroHour", "TacticalTed", "RogueSignal", "AshenKnight", "VoidWalker", "HexRunner", "Maverick_11",
            "OverwatchOwl", "ScarletFox", "GraniteJack", "SilentBravo", "ColdFront", "RailGhost", "DepotDog", "QuarryKid",
            "SiloSurvivor", "BridgeTroll", "TunnelRat", "LakeScout", "CliffHanger", "RadarRook", "SwitchyardSam", "FreightFury"
        };

        public static string Take(HashSet<string> used, System.Random rng)
        {
            for (int tries = 0; tries < 80; tries++)
            {
                string name = Handles[rng.Next(Handles.Length)];
                if (rng.NextDouble() < 0.45) name += rng.Next(2, 99);
                if (used.Add(name)) return name;
            }
            string fallback = "Player" + rng.Next(1000, 9999);
            used.Add(fallback);
            return fallback;
        }
    }
}
