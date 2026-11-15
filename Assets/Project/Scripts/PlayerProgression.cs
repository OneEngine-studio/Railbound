using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>What is known about a player's progress: level, XP into the level, coins, gems, the operators they own and their match stats.</summary>
[Serializable]
public sealed class ProgressData
{
    public int level = 1;
    public int xp;              // XP earned inside the current level
    public long totalXp;        // all XP ever earned
    public int coins;
    public int gems;
    public int matches;
    public int wins;
    public int kills;
    public long playSeconds;
    public int bestPlacement;   // best (lowest) finishing place, 0 = no match yet
    public List<string> operators = new List<string>();   // ids of the operators bought in the lobby store (the starting one is always owned)
    public ServerAward lastAward;                         // the reward of the last match the game server reported (online profiles only)
    public long updatedAt;      // UTC unix milliseconds of the last change
    public int version = 1;
}

/// <summary>What the server paid for one match (copied from the profile Nakama returns).</summary>
[Serializable]
public sealed class ServerAward
{
    public string matchId;
    public int xp, coins, levelBonusCoins, oldLevel, newLevel, placement, totalPlayers, kills, survivedSeconds;
    public long at;
    public bool LeveledUp => newLevel > oldLevel;
}

/// <summary>What one finished match was (used by the local-only profile and the match history).</summary>
public struct MatchSummary
{
    public string mode;
    public int placement, totalPlayers, kills;
    public float survivedSeconds;
    public bool won;
}

/// <summary>The result of one reward: what was earned and where the level and XP moved from and to.</summary>
public readonly struct ProgressAward
{
    public readonly int xp, coins, levelBonusCoins, oldLevel, newLevel, oldXp, newXp;
    public ProgressAward(int xp, int coins, int levelBonusCoins, int oldLevel, int newLevel, int oldXp, int newXp)
    { this.xp = xp; this.coins = coins; this.levelBonusCoins = levelBonusCoins; this.oldLevel = oldLevel; this.newLevel = newLevel; this.oldXp = oldXp; this.newXp = newXp; }
    public bool LeveledUp => newLevel > oldLevel;
}

public enum BuyResult { Bought, AlreadyOwned, NotEnoughCoins, UnknownOperator, Offline, Busy }

/// <summary>
/// Player level, XP, coins and owned operators.
///
/// SIGNED IN (guest or account): the numbers belong to Nakama. They live in the storage object player_economy/profile, which the client can
/// only READ; the Nakama module changes it. This class shows the profile the server returned (and keeps the last one on the device only to
/// have something to draw when Nakama is unreachable) and asks the server to act through RPCs:
///   railbound_profile           read (and create) the profile
///   railbound_buy_operator      buy an operator at the server's price
///   railbound_claim_challenge   claim a challenge the server counted as complete
/// XP and coins for a match are never sent by the client: the dedicated game server reports the result to Nakama, which pays it
/// (<see cref="WaitForMatchAwardAsync"/> waits for that). Nothing on the device can raise coins, XP or level.
///
/// NOT SIGNED IN (no Nakama session): a separate, clearly local-only profile on this device (<see cref="IsLocalOnly"/>) so offline play
/// still has a lobby. It is never uploaded and never merged with an online profile.
/// </summary>
public static class PlayerProgression
{
    public const int MaxLevel = 100;
    private const string LocalPrefix = "Railbound.Progress.";
    private const string AwardShownPrefix = "Railbound.Progress.award.";

    /// <summary>XP needed to go from <paramref name="level"/> to the next one (the server uses the same formula).</summary>
    public static int XpForLevel(int level) => 1000 + (Mathf.Max(1, level) - 1) * 150;

    /// <summary>Coins granted for reaching <paramref name="newLevel"/> (the server uses the same formula).</summary>
    public static int LevelBonusCoins(int newLevel) => 100 + newLevel * 10;

    public struct PendingLevelUp { public int from, to, bonusCoins; }
    /// <summary>Level-ups the lobby has not shown yet (survives scene changes).</summary>
    public static readonly Queue<PendingLevelUp> Pending = new Queue<PendingLevelUp>();

    public static event Action Changed;
    /// <summary>A match reward the server paid and this player has not seen yet.</summary>
    public static event Action<ServerAward> AwardReceived;

    /// <summary>The server's operator price table (operator id -> coins), as of the last profile read.</summary>
    public static readonly Dictionary<string, int> ServerPrices = new Dictionary<string, int>();

    private static ProgressData current;
    private static string loadedFor;     // "srv:<user id>" or "local"
    private static bool buying;

    /// <summary>True while the server is reachable (the last RPC worked). Meaningful only when <see cref="UsesServer"/>.</summary>
    public static bool ServerReachable { get; private set; } = true;

    /// <summary>True when a Nakama session exists: coins, XP, level and operators are the server's.</summary>
    public static bool UsesServer
    {
        get { var a = AuthenticationService.Instance; return a != null && a.IsAuthenticated; }
    }

    /// <summary>True when nobody is signed in: the profile is a separate local one (offline play) and is NOT saved online.</summary>
    public static bool IsLocalOnly => !UsesServer;

    /// <summary>Short text for the UI: empty for a normal online profile.</summary>
    public static string ModeLabel => IsLocalOnly ? "OFFLINE - LOCAL ONLY" : (ServerReachable ? "" : "SERVER UNREACHABLE");

    public static ProgressData Current
    {
        get { EnsureLocal(); return current; }
    }

    public static string LevelText => "LVL. " + Current.level;
    public static float XpFill => Current.level >= MaxLevel ? 1f : Mathf.Clamp01(Current.xp / (float)XpForLevel(Current.level));

    private static string UserKey
    {
        get
        {
            var a = AuthenticationService.Instance;
            return a != null && a.IsAuthenticated ? "srv:" + a.CurrentUserId : "local";
        }
    }

    private static string UserId => AuthenticationService.Instance != null ? AuthenticationService.Instance.CurrentUserId : "";

    private static void EnsureLocal()
    {
        string key = UserKey;
        if (current != null && loadedFor == key) return;
        current = ReadLocal(key) ?? new ProgressData();
        loadedFor = key;
    }

    private static ProgressData ReadLocal(string key)
    {
        try
        {
            string json = PlayerPrefs.GetString(LocalPrefix + key, "");
            if (!string.IsNullOrEmpty(json)) return Sanitize(JsonUtility.FromJson<ProgressData>(json));
        }
        catch (Exception) { /* unreadable: start fresh */ }
        return null;
    }

    // for a signed-in player this is only a display cache of what the server said; for the local-only profile it is the data itself
    private static void WriteLocal()
    {
        try
        {
            if (IsLocalOnly) current.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerPrefs.SetString(LocalPrefix + loadedFor, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
        }
        catch (Exception) { /* not saved on this device */ }
    }

    private static ProgressData Sanitize(ProgressData d)
    {
        if (d == null) return null;
        d.level = Mathf.Clamp(d.level, 1, MaxLevel);
        d.xp = Mathf.Max(0, d.xp);
        d.coins = Mathf.Max(0, d.coins);
        d.gems = Mathf.Max(0, d.gems);
        if (d.operators == null) d.operators = new List<string>();
        return d;
    }

    // ---------------- server ----------------
    private static async Task<JObject> CallAsync(string rpc, string payload)
    {
        var auth = AuthenticationService.Instance;
        if (auth == null || !auth.IsAuthenticated || auth.Client == null || auth.Session == null) return null;
        try
        {
            var response = await auth.Client.RpcAsync(auth.Session, rpc, payload ?? "{}");
            ServerReachable = true;
            return JObject.Parse(response.Payload);
        }
        catch (ApiResponseException ex)
        {
            ServerReachable = true;   // the server answered, it just refused or failed
            Debug.LogWarning("[Progress] " + rpc + " was refused: " + ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            ServerReachable = false;
            Debug.LogWarning("[Progress] " + rpc + " failed (no connection?): " + ex.Message);
            return null;
        }
    }

    /// <summary>Takes the profile out of an RPC reply and makes it the current one. Returns false when the reply has none.</summary>
    private static bool ApplyServerReply(JObject reply)
    {
        var profile = reply != null ? reply["profile"] as JObject : null;
        if (profile == null) return false;
        ProgressData data;
        try { data = Sanitize(profile.ToObject<ProgressData>()); }
        catch (Exception ex) { Debug.LogWarning("[Progress] Could not read the profile: " + ex.Message); return false; }
        if (data == null) return false;

        EnsureLocal();
        current = data;
        loadedFor = UserKey;

        var prices = reply["prices"] as JObject;
        if (prices != null)
        {
            ServerPrices.Clear();
            foreach (var p in prices.Properties()) { if (p.Value.Type == JTokenType.Integer) ServerPrices[p.Name] = (int)p.Value; }
        }
        var challenges = profile["challenges"];
        if (challenges != null) ChallengeService.ApplyServer(challenges.ToString(Formatting.None));

        WriteLocal();
        long serverTime = reply["serverTime"] != null && reply["serverTime"].Type == JTokenType.Integer ? (long)reply["serverTime"] : 0;
        CheckNewAward(data.lastAward, serverTime);
        Changed?.Invoke();
        return true;
    }

    // a match reward the player has not seen yet (a level-up popup is queued, listeners are told). Remembered per account on this device.
    private static void CheckNewAward(ServerAward award, long serverTime)
    {
        if (award == null || string.IsNullOrEmpty(award.matchId)) return;
        string shownKey = AwardShownPrefix + UserId;
        if (PlayerPrefs.GetString(shownKey, "") == award.matchId) return;
        PlayerPrefs.SetString(shownKey, award.matchId);
        PlayerPrefs.Save();
        // an old reward found on a device that never showed it (a fresh install) is not announced
        if (serverTime > 0 && award.at > 0 && serverTime - award.at > 6L * 3600L * 1000L) return;
        if (award.LeveledUp) Pending.Enqueue(new PendingLevelUp { from = award.oldLevel, to = award.newLevel, bonusCoins = award.levelBonusCoins });
        AwardReceived?.Invoke(award);
    }

    [Serializable] private sealed class BuyRequest { public string operatorId; }
    [Serializable] private sealed class ClaimRequest { public string tab; public string key; }

    /// <summary>Reads the profile from Nakama (creating it on the first call). Offline or signed out: keeps what is there.</summary>
    public static async Task LoadAsync()
    {
        EnsureLocal();
        if (!UsesServer) { Changed?.Invoke(); return; }
        var reply = await CallAsync("railbound_profile", "{}");
        if (reply == null || !ApplyServerReply(reply)) Changed?.Invoke();   // unreachable: the cached copy stays on screen, read-only in effect
    }

    // ---------------- operators (character models) ----------------
    /// <summary>True when the player may use this operator: the starting one, or one they own.</summary>
    public static bool OwnsOperator(string operatorId, bool isStarting)
    {
        if (isStarting) return true;
        EnsureLocal();
        return !string.IsNullOrEmpty(operatorId) && current.operators.Contains(operatorId);
    }

    /// <summary>The price the player pays: the server's table when it is known, otherwise <paramref name="fallback"/> (the catalog's, shown while offline).</summary>
    public static int PriceOf(string operatorId, int fallback)
    {
        return !string.IsNullOrEmpty(operatorId) && ServerPrices.TryGetValue(operatorId, out int p) ? p : fallback;
    }

    /// <summary>
    /// Buys an operator. Signed in: the SERVER checks the price and the coins and adds the operator; the profile it returns replaces the
    /// local copy. Not signed in: spends the coins of the local-only profile at <paramref name="localPriceCoins"/>.
    /// </summary>
    public static async Task<BuyResult> BuyOperatorAsync(string operatorId, int localPriceCoins)
    {
        EnsureLocal();
        if (string.IsNullOrEmpty(operatorId)) return BuyResult.UnknownOperator;
        if (current.operators.Contains(operatorId)) return BuyResult.AlreadyOwned;

        if (!UsesServer)
        {
            if (localPriceCoins < 0 || current.coins < localPriceCoins) return BuyResult.NotEnoughCoins;
            current.coins -= localPriceCoins;
            current.operators.Add(operatorId);
            WriteLocal();
            Changed?.Invoke();
            return BuyResult.Bought;
        }

        if (buying) return BuyResult.Busy;
        buying = true;
        try
        {
            var reply = await CallAsync("railbound_buy_operator", JsonUtility.ToJson(new BuyRequest { operatorId = operatorId }));
            if (reply == null) return BuyResult.Offline;
            ApplyServerReply(reply);
            switch ((string)reply["status"])
            {
                case "ok": return BuyResult.Bought;
                case "already_owned": return BuyResult.AlreadyOwned;
                case "insufficient_coins": return BuyResult.NotEnoughCoins;
                default: return BuyResult.UnknownOperator;
            }
        }
        finally { buying = false; }
    }

    // ---------------- challenges ----------------
    /// <summary>Asks the server to pay a completed challenge. True when it did.</summary>
    public static async Task<bool> ClaimChallengeAsync(string tab, string key)
    {
        var reply = await CallAsync("railbound_claim_challenge", JsonUtility.ToJson(new ClaimRequest { tab = tab, key = key }));
        if (reply == null) return false;
        ApplyServerReply(reply);
        var award = reply["award"] as JObject;
        if (award != null && (int)(award["newLevel"] ?? 0) > (int)(award["oldLevel"] ?? 0))
            Pending.Enqueue(new PendingLevelUp { from = (int)award["oldLevel"], to = (int)award["newLevel"], bonusCoins = (int)(award["levelBonusCoins"] ?? 0) });
        return (string)reply["status"] == "ok";
    }

    // ---------------- match rewards ----------------
    /// <summary>
    /// Waits for the reward of a match that ran on a dedicated server. The server reports the result to Nakama when the match ends and Nakama
    /// pays it; this polls the profile until the reward of <paramref name="matchId"/> is in it. Null when it did not arrive in time (it is
    /// still paid, the lobby shows it later).
    /// </summary>
    public static async Task<ServerAward> WaitForMatchAwardAsync(string matchId, float timeoutSeconds)
    {
        if (!UsesServer) return null;
        float end = Time.realtimeSinceStartup + timeoutSeconds;
        long startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60000;
        while (Time.realtimeSinceStartup < end)
        {
            var reply = await CallAsync("railbound_profile", "{}");
            if (reply != null && ApplyServerReply(reply))
            {
                var a = current.lastAward;
                if (a != null && (string.IsNullOrEmpty(matchId) ? a.at >= startedAt : a.matchId == matchId)) return a;
            }
            await Task.Delay(1500);
        }
        return null;
    }

    // ---------------- local-only profile (nobody signed in) ----------------
    /// <summary>Local-only: adds XP and coins, moves the level up as often as the XP allows and queues the level-up popup. Ignored when signed in.</summary>
    public static Task<ProgressAward> AwardAsync(int xp, int coins, bool countsAsMatch = true)
    {
        EnsureLocal();
        if (UsesServer)
        {
            Debug.LogWarning("[Progress] A signed-in player's rewards come from the server; the local award was ignored.");
            return Task.FromResult(new ProgressAward(0, 0, 0, current.level, current.level, current.xp, current.xp));
        }
        xp = Mathf.Max(0, xp); coins = Mathf.Max(0, coins);
        var d = current;
        int oldLevel = d.level, oldXp = d.xp, bonus = 0;

        d.coins += coins;
        d.totalXp += xp;
        if (countsAsMatch) d.matches++;
        if (d.level < MaxLevel)
        {
            d.xp += xp;
            while (d.level < MaxLevel && d.xp >= XpForLevel(d.level))
            {
                d.xp -= XpForLevel(d.level);
                d.level++;
                int b = LevelBonusCoins(d.level);
                bonus += b;
                d.coins += b;
            }
            if (d.level >= MaxLevel) d.xp = 0;
        }

        if (d.level > oldLevel) Pending.Enqueue(new PendingLevelUp { from = oldLevel, to = d.level, bonusCoins = bonus });
        WriteLocal();
        Changed?.Invoke();
        return Task.FromResult(new ProgressAward(xp, coins, bonus, oldLevel, d.level, oldXp, d.xp));
    }

    /// <summary>Local-only: records a finished offline match (wins, kills, time, best place) and awards its XP and coins.</summary>
    public static Task<ProgressAward> RecordMatchAsync(MatchSummary m, int xp, int coins)
    {
        EnsureLocal();
        var d = current;
        if (!UsesServer)
        {
            if (m.won) d.wins++;
            d.kills += Mathf.Max(0, m.kills);
            d.playSeconds += (long)Mathf.Max(0f, m.survivedSeconds);
            int place = m.won ? 1 : m.placement;
            if (place > 0 && (d.bestPlacement == 0 || place < d.bestPlacement)) d.bestPlacement = place;
        }
        return AwardAsync(xp, coins, true);
    }

    /// <summary>
    /// What a finished match is worth, for the LOCAL-ONLY profile. (The server computes the real one with the same numbers in
    /// charts/nakama/files/modules/index.js, function matchReward; a signed-in client never uses this to award anything.)
    /// </summary>
    public static void MatchReward(int placement, int totalPlayers, int kills, float survivedSeconds, bool won, out int xp, out int coins)
    {
        totalPlayers = Mathf.Max(totalPlayers, placement, 1);
        int placementBonus;
        if (won) placementBonus = 1000;
        else if (placement <= 0) placementBonus = 0;
        else placementBonus = Mathf.RoundToInt(600f * (totalPlayers - placement) / Mathf.Max(1, totalPlayers - 1));
        xp = 100 + kills * 75 + Mathf.FloorToInt(survivedSeconds / 60f) * 10 + placementBonus;
        coins = Mathf.RoundToInt(xp * 0.25f);
    }
}
