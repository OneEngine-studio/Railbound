using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Daily, weekly and season challenges. The definitions (and rewards) live here for display; periods roll over by UTC date (daily), ISO week
/// (weekly) and season id (season).
///
/// SIGNED IN: the SERVER counts the progress (from the match results the game server reports) and pays a claim; this class only shows the
/// state Nakama returned (<see cref="ApplyServer"/>) and asks <see cref="PlayerProgression.ClaimChallengeAsync"/> to claim. The definitions
/// are duplicated in charts/nakama/files/modules/index.js (CHALLENGE_DEFS): change both together.
/// NOT SIGNED IN: a local-only copy on this device is counted from finished offline matches and pays the local-only profile.
/// </summary>
public static class ChallengeService
{
    public enum Tab { Daily, Weekly, Season }
    public enum Metric { Matches, Wins, Kills, SurviveMinutes, Top5 }

    public const string SeasonId = "S01";
    private const string LocalKey = "oe.challenges";

    private sealed class Def
    {
        public string key, text; public Metric metric; public int goal, xp, coins;
        public Def(string key, string text, Metric metric, int goal, int xp, int coins) { this.key = key; this.text = text; this.metric = metric; this.goal = goal; this.xp = xp; this.coins = coins; }
    }

    private static readonly Dictionary<Tab, Def[]> Defs = new Dictionary<Tab, Def[]>
    {
        { Tab.Daily, new[] {
            new Def("d_play2", "Play 2 matches", Metric.Matches, 2, 300, 75),
            new Def("d_kill3", "Eliminate 3 players", Metric.Kills, 3, 400, 100),
            new Def("d_surv6", "Survive 6 minutes in matches", Metric.SurviveMinutes, 6, 300, 75),
        } },
        { Tab.Weekly, new[] {
            new Def("w_win1", "Win a match", Metric.Wins, 1, 1200, 300),
            new Def("w_kill15", "Eliminate 15 players", Metric.Kills, 15, 1200, 300),
            new Def("w_play7", "Play 7 matches", Metric.Matches, 7, 900, 225),
            new Def("w_top5x3", "Finish in the top 5 three times", Metric.Top5, 3, 1200, 300),
        } },
        { Tab.Season, new[] {
            new Def("s_play30", "Play 30 matches", Metric.Matches, 30, 3000, 750),
            new Def("s_win5", "Win 5 matches", Metric.Wins, 5, 4000, 1000),
            new Def("s_kill150", "Eliminate 150 players", Metric.Kills, 150, 5000, 1250),
        } },
    };

    // ---------------- stored state ----------------
    [Serializable] private sealed class Entry { public string key; public int progress; public bool claimed; }
    [Serializable] private sealed class Period { public string id; public List<Entry> items = new List<Entry>(); }
    [Serializable] private sealed class State { public int v = 1; public long updatedAt; public Period daily = new Period(), weekly = new Period(), season = new Period(); }

    private static State state;            // the one in use: serverState when signed in, localState otherwise
    private static State localState, serverState;
    private static bool usingServer;

    /// <summary>What the screens draw for one challenge.</summary>
    public struct View
    {
        public string key, text; public int progress, goal, xp, coins; public bool claimed;
        public bool Complete => progress >= goal;
        public bool CanClaim => Complete && !claimed;
    }

    public static event Action Changed;

    // ---------------- periods ----------------
    private static string DayId(DateTime utc) { return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    private static string WeekId(DateTime utc)
    {
        int week = ISOWeek.GetWeekOfYear(utc);
        int year = ISOWeek.GetYear(utc);
        return year + "-W" + week.ToString("00");
    }

    private static string CurrentId(Tab tab)
    {
        var now = DateTime.UtcNow;
        return tab == Tab.Daily ? DayId(now) : (tab == Tab.Weekly ? WeekId(now) : SeasonId);
    }

    private static Period PeriodOf(Tab tab) { return tab == Tab.Daily ? state.daily : (tab == Tab.Weekly ? state.weekly : state.season); }

    private static void Ensure()
    {
        usingServer = PlayerProgression.UsesServer;
        if (usingServer)
        {
            if (serverState == null) serverState = new State();
            state = serverState;
        }
        else
        {
            if (localState == null)
            {
                try
                {
                    string json = PlayerPrefs.GetString(LocalKey, "");
                    if (!string.IsNullOrEmpty(json)) localState = JsonUtility.FromJson<State>(json);
                }
                catch (Exception) { /* unreadable: start fresh */ }
                if (localState == null) localState = new State();
            }
            state = localState;
        }
        bool rolled = false;
        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            var p = PeriodOf(tab);
            string id = CurrentId(tab);
            // the server decides when a period ends: its state is only completed with empty entries, never reset by this device's clock
            bool stale = !usingServer && p.id != id;
            if (stale || p.items == null || p.items.Count != Defs[tab].Length || !Defs[tab].All(d => p.items.Any(e => e.key == d.key)))
            {
                if (!usingServer || string.IsNullOrEmpty(p.id)) p.id = id;
                p.items = Defs[tab].Select(d => new Entry { key = d.key }).ToList();
                rolled = true;
            }
        }
        if (rolled) Persist(false);
    }

    private static void Persist(bool notify)
    {
        if (usingServer) { if (notify && Changed != null) Changed(); return; }   // the server's state is never written on this device
        state.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try { PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(state)); PlayerPrefs.Save(); } catch (Exception) { /* device copy only */ }
        if (notify && Changed != null) Changed();
    }

    // ---------------- server state ----------------
    /// <summary>Takes the challenge state of the profile Nakama returned (daily / weekly / season periods with the server's progress).</summary>
    public static void ApplyServer(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var remote = JsonUtility.FromJson<State>(json);
            if (remote == null) return;
            serverState = remote;
            if (usingServer || PlayerProgression.UsesServer) { state = serverState; Ensure(); }
            if (Changed != null) Changed();
        }
        catch (Exception ex) { Debug.LogWarning("[Challenges] Could not read the server state: " + ex.Message); }
    }

    // ---------------- reading ----------------
    public static List<View> Get(Tab tab)
    {
        Ensure();
        var list = new List<View>();
        foreach (var d in Defs[tab])
        {
            var e = PeriodOf(tab).items.First(x => x.key == d.key);
            list.Add(new View { key = d.key, text = d.text, progress = Mathf.Min(e.progress, d.goal), goal = d.goal, xp = d.xp, coins = d.coins, claimed = e.claimed });
        }
        return list;
    }

    public static int ClaimableCount()
    {
        int n = 0;
        foreach (Tab t in Enum.GetValues(typeof(Tab))) n += Get(t).Count(v => v.CanClaim);
        return n;
    }

    // ---------------- counting ----------------
    /// <summary>Counts a finished match towards every active challenge.</summary>
    public static void OnMatchFinished(MatchSummary m)
    {
        Ensure();
        if (usingServer) return;   // the server counts the matches it reports
        int place = m.won ? 1 : m.placement;
        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            foreach (var d in Defs[tab])
            {
                var e = PeriodOf(tab).items.First(x => x.key == d.key);
                if (e.claimed || e.progress >= d.goal) continue;
                int add = 0;
                switch (d.metric)
                {
                    case Metric.Matches: add = 1; break;
                    case Metric.Wins: add = m.won ? 1 : 0; break;
                    case Metric.Kills: add = Mathf.Max(0, m.kills); break;
                    case Metric.SurviveMinutes: add = Mathf.FloorToInt(Mathf.Max(0f, m.survivedSeconds) / 60f); break;
                    case Metric.Top5: add = (place > 0 && place <= 5) ? 1 : 0; break;
                }
                e.progress = Mathf.Min(d.goal, e.progress + add);
            }
        }
        Persist(true);
    }

    // ---------------- claiming ----------------
    /// <summary>Pays a completed challenge. Returns false when it is not complete or was already claimed.</summary>
    public static async Task<bool> ClaimAsync(Tab tab, string key)
    {
        Ensure();
        var d = Defs[tab].FirstOrDefault(x => x.key == key);
        var e = PeriodOf(tab).items.FirstOrDefault(x => x.key == key);
        if (d == null || e == null || e.claimed || e.progress < d.goal) return false;
        if (usingServer)
        {
            // the server checks its own count and pays; the profile it returns brings the new state (ApplyServer)
            bool paid = await PlayerProgression.ClaimChallengeAsync(tab.ToString().ToLowerInvariant(), key);
            if (Changed != null) Changed();
            return paid;
        }
        e.claimed = true;                 // mark first: a double click must not pay twice
        Persist(true);
        await PlayerProgression.AwardAsync(d.xp, d.coins, false);
        return true;
    }
}
