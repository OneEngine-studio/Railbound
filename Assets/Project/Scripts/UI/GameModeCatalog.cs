using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Railbound.UI
{
    public enum ModeCategory
    {
        CoreSurvival,
        RespawnCombat,
        ObjectiveExtraction,
    }

    /// <summary>How many players form one team in a mode that offers team sizes.</summary>
    public enum TeamSize
    {
        Solo = 1,
        Duo = 2,
        Trio = 3,
        Squad = 4,
    }

    /// <summary>
    /// One match mode as the mode screen presents it. <see cref="PlayableTeamSizes"/> is the honest part: it lists what the game can
    /// really run today. A mode with none is shown as COMING SOON and cannot be chosen, so the lobby never promises gameplay
    /// that does not exist.
    /// </summary>
    public sealed class GameModeDefinition
    {
        public string Id;                  // stable key, also sent to the matchmaker
        public string Name;                // full name on the mode screen
        public string CardTitle;           // short name for the lobby card
        public string Glyph;               // big typographic mark on the poster
        public string Tagline;
        public string Description;
        public string[] Rules;
        public ModeCategory Category;
        public int Players;
        public string Map;
        public string Length;              // typical match length, for the fact line
        public string FixedFormat;         // for team modes without a team-size choice, for example "6 V 6"
        public TeamSize[] TeamSizes = Array.Empty<TeamSize>();          // offered
        public TeamSize[] PlayableTeamSizes = Array.Empty<TeamSize>();  // really runnable today

        public bool OffersTeamSizes => TeamSizes.Length > 0;
        public bool IsPlayable => PlayableTeamSizes.Length > 0;
        public bool Offers(TeamSize t) => TeamSizes.Contains(t);
        public bool CanPlay(TeamSize t) => PlayableTeamSizes.Contains(t);
        public TeamSize DefaultTeamSize => IsPlayable ? PlayableTeamSizes[0] : (OffersTeamSizes ? TeamSizes[0] : TeamSize.Solo);
    }

    /// <summary>The list of modes, and which one the player has chosen (kept between sessions).</summary>
    public static class GameModeCatalog
    {
        private const string PrefsMode = "Railbound.Mode";
        private const string PrefsTeam = "Railbound.TeamSize";

        public static readonly IReadOnlyList<GameModeDefinition> All = new List<GameModeDefinition>
        {
            // ------------------------------------------------------------ core survival
            new GameModeDefinition
            {
                Id = "classic", Name = "CLASSIC BATTLE ROYALE", CardTitle = "CLASSIC BR", Glyph = "BR",
                Category = ModeCategory.CoreSurvival, Players = 20, Map = "MAP-2", Length = "ABOUT 12 MIN",
                Tagline = "DROP IN. LOOT UP. LAST ONE STANDING.",
                Description = "Land on a large map with almost nothing, scavenge weapons, and fight to be the last person or team alive while the safe zone closes in.",
                Rules = new[] { "Drop with minimal gear and scavenge weapons", "The safe zone shrinks: stay inside or take damage", "The last player or team alive wins" },
                TeamSizes = new[] { TeamSize.Solo, TeamSize.Duo, TeamSize.Trio, TeamSize.Squad },
                PlayableTeamSizes = new[] { TeamSize.Solo },
            },
            new GameModeDefinition
            {
                Id = "resurgence", Name = "RESURGENCE", CardTitle = "RESURGENCE", Glyph = "RS",
                Category = ModeCategory.CoreSurvival, Players = 20, Map = "MAP-2", Length = "ABOUT 8 MIN",
                Tagline = "FALL, AND RISE AGAIN.",
                Description = "A faster battle royale on a tighter circle. Fallen players come back after a short countdown, as long as one teammate is still alive.",
                Rules = new[] { "Faster pace and a smaller circle", "Eliminated players respawn after a short countdown", "Respawns stop once the last teammate falls" },
                TeamSizes = new[] { TeamSize.Duo, TeamSize.Trio, TeamSize.Squad },
            },
            new GameModeDefinition
            {
                Id = "tactical", Name = "ZERO BUILD / TACTICAL", CardTitle = "TACTICAL", Glyph = "ZB",
                Category = ModeCategory.CoreSurvival, Players = 20, Map = "MAP-2", Length = "ABOUT 12 MIN",
                Tagline = "NO TRICKS. JUST GUNPLAY.",
                Description = "The battle royale stripped of building and special abilities. Positioning, natural cover and aim decide every fight.",
                Rules = new[] { "No building and no special abilities", "Fights are decided by cover and positioning", "The same shrinking zone as the classic mode" },
                TeamSizes = new[] { TeamSize.Solo, TeamSize.Duo, TeamSize.Trio, TeamSize.Squad },
            },

            // ------------------------------------------------------------ respawn and combat
            new GameModeDefinition
            {
                Id = "tdm", Name = "TEAM DEATHMATCH", CardTitle = "TEAM DEATHMATCH", Glyph = "TDM",
                Category = ModeCategory.RespawnCombat, Players = 12, Map = "ARENA", Length = "ABOUT 10 MIN", FixedFormat = "6 V 6",
                Tagline = "TWO TEAMS. ONE KILL COUNT.",
                Description = "Two teams fight in a small arena with instant respawns. Reach the kill limit first, or lead on kills when time runs out.",
                Rules = new[] { "Two teams in a compact arena", "Instant respawns, no waiting", "First to the kill limit or most kills at time wins" },
            },
            new GameModeDefinition
            {
                Id = "ffa", Name = "FREE-FOR-ALL", CardTitle = "FREE-FOR-ALL", Glyph = "FFA",
                Category = ModeCategory.RespawnCombat, Players = 8, Map = "ARENA", Length = "ABOUT 8 MIN", FixedFormat = "EVERY PLAYER FOR THEMSELVES",
                Tagline = "EVERYONE IS AN ENEMY.",
                Description = "Every player fights alone with instant respawns. The first player to reach the kill target takes the match.",
                Rules = new[] { "Everyone fights for themselves", "Instant respawns", "First to the kill target wins" },
            },
            new GameModeDefinition
            {
                Id = "gungame", Name = "GUN GAME", CardTitle = "GUN GAME", Glyph = "GG",
                Category = ModeCategory.RespawnCombat, Players = 8, Map = "ARENA", Length = "ABOUT 10 MIN", FixedFormat = "EVERY PLAYER FOR THEMSELVES",
                Tagline = "EVERY KILL, A NEW WEAPON.",
                Description = "Start with a basic weapon and move up a fixed list of guns with every elimination. Get a kill with the final weapon to win.",
                Rules = new[] { "Everyone starts with the same basic weapon", "Each elimination promotes you to the next gun", "A kill with the final weapon wins the match" },
            },

            // ------------------------------------------------------------ objective and extraction
            new GameModeDefinition
            {
                Id = "control", Name = "CONTROL", CardTitle = "CONTROL", Glyph = "CTL",
                Category = ModeCategory.ObjectiveExtraction, Players = 12, Map = "ARENA", Length = "ABOUT 12 MIN", FixedFormat = "6 V 6",
                Tagline = "HOLD THE GROUND.",
                Description = "Teams fight to capture and hold zones across the map. Held zones earn points over time; the team with the most points wins.",
                Rules = new[] { "Capture and hold the zones on the map", "Held zones earn points over time", "The team with the most points wins" },
            },
            new GameModeDefinition
            {
                Id = "snd", Name = "SEARCH & DESTROY", CardTitle = "SEARCH & DESTROY", Glyph = "S&D",
                Category = ModeCategory.ObjectiveExtraction, Players = 12, Map = "ARENA", Length = "ABOUT 15 MIN", FixedFormat = "6 V 6",
                Tagline = "ONE LIFE. ONE BOMB.",
                Description = "A round-based mode with no respawns. One team plants a bomb at a target site while the other defends it or defuses the bomb.",
                Rules = new[] { "Round based, no respawns", "Attackers plant the bomb at a site", "Defenders stop the plant or defuse the bomb" },
            },
            new GameModeDefinition
            {
                Id = "extraction", Name = "EXTRACTION", CardTitle = "EXTRACTION", Glyph = "EX",
                Category = ModeCategory.ObjectiveExtraction, Players = 20, Map = "MAP-2", Length = "ABOUT 20 MIN",
                Tagline = "TAKE THE LOOT. GET OUT ALIVE.",
                Description = "Squads drop into the map to collect loot, cash and objectives, then must reach an extraction point without being wiped out.",
                Rules = new[] { "Collect loot, cash and objectives", "Reach an extraction point to keep what you found", "A squad wiped out loses its loot" },
                TeamSizes = new[] { TeamSize.Duo, TeamSize.Trio, TeamSize.Squad },
            },
        };

        public static string CategoryName(ModeCategory c)
        {
            switch (c)
            {
                case ModeCategory.CoreSurvival: return "CORE SURVIVAL";
                case ModeCategory.RespawnCombat: return "RESPAWN & COMBAT";
                default: return "OBJECTIVE & EXTRACTION";
            }
        }

        public static string TeamName(TeamSize t)
        {
            switch (t)
            {
                case TeamSize.Solo: return "SOLO";
                case TeamSize.Duo: return "DUO";
                case TeamSize.Trio: return "TRIO";
                default: return "SQUAD";
            }
        }

        public static GameModeDefinition Find(string id) => All.FirstOrDefault(m => m.Id == id);
        public static IEnumerable<GameModeDefinition> InCategory(ModeCategory c) => All.Where(m => m.Category == c);

        // ---------------- the player's choice ----------------
        private static GameModeDefinition selected;
        private static TeamSize selectedTeam = TeamSize.Solo;

        /// <summary>Raised when the chosen mode or team size changed.</summary>
        public static event Action Changed;

        public static GameModeDefinition Selected { get { EnsureLoaded(); return selected; } }
        public static TeamSize SelectedTeamSize { get { EnsureLoaded(); return selectedTeam; } }

        /// <summary>The key the matchmaker groups players by, for example "classic_solo": only equal keys are matched together.</summary>
        public static string MatchKey => Selected.Id + "_" + TeamName(SelectedTeamSize).ToLowerInvariant();

        /// <summary>The fact line under the title: team size or format, and the number of players.</summary>
        public static string Summary(GameModeDefinition m, TeamSize t)
        {
            string format = m.OffersTeamSizes ? TeamName(t) : m.FixedFormat;
            return format + "  |  " + m.Players + " PLAYERS";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { selected = null; }

        private static void EnsureLoaded()
        {
            if (selected != null) return;
            GameModeDefinition m = null;
            TeamSize t = TeamSize.Solo;
            try
            {
                m = Find(PlayerPrefs.GetString(PrefsMode, ""));
                t = (TeamSize)PlayerPrefs.GetInt(PrefsTeam, (int)TeamSize.Solo);
            }
            catch (Exception) { /* private mode / blocked storage: use the default */ }

            // a saved choice only counts while the game can really run it (a mode may have been playable in another build)
            if (m == null || !m.IsPlayable || !m.CanPlay(t)) { m = Find("classic"); t = m.DefaultTeamSize; }
            selected = m;
            selectedTeam = t;
        }

        /// <summary>Chooses a mode and team size. Returns false when the game cannot run that combination.</summary>
        public static bool TrySelect(GameModeDefinition mode, TeamSize team)
        {
            if (mode == null || !mode.IsPlayable) return false;
            if (mode.OffersTeamSizes && !mode.CanPlay(team)) return false;
            if (!mode.OffersTeamSizes) team = TeamSize.Solo;

            EnsureLoaded();
            bool changed = selected != mode || selectedTeam != team;
            selected = mode;
            selectedTeam = team;
            try
            {
                PlayerPrefs.SetString(PrefsMode, mode.Id);
                PlayerPrefs.SetInt(PrefsTeam, (int)team);
                PlayerPrefs.Save();
            }
            catch (Exception) { /* not saved, still chosen for this session */ }
            if (changed) Changed?.Invoke();
            return true;
        }
    }
}
