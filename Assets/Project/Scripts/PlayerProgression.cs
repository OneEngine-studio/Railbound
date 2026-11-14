using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>What is stored about a player's progress: level, XP into the level, coins and gems.</summary>
[Serializable]
public sealed class ProgressData
{
    public int level = 1;
    public int xp;              // XP earned inside the current level
    public long totalXp;        // all XP ever earned (decides which copy wins when the cloud and the device disagree)
    public int coins;
    public int gems;
    public int matches;
    public int wins;
    public int kills;
    public long playSeconds;
    public int bestPlacement;   // best (lowest) finishing place, 0 = no match yet
    public long updatedAt;      // UTC unix milliseconds of the last change
    public int version = 1;
}

/// <summary>What one finished match was, as the client saw it.</summary>
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

/// <summary>
/// Player level, XP and coins. Kept on the device (so the lobby always has something to show) and in Nakama storage
/// (collection "player_progress"), where the copy with more total XP wins. A level-up that happens in a match is queued here and the lobby
/// plays the level-up popup when the player is back.
///
/// NOTE: awards are computed and written by the client, so a modified client could grant itself XP. Before launch they should move to
/// the game server (it knows the real match result) calling a Nakama RPC; <see cref="AwardAsync"/> is the one place that would change.
/// </summary>
public static class PlayerProgression
{
    public const int MaxLevel = 100;
    private const string Collection = "player_progress";
    private const string Key = "progress";
    private const string LocalPrefix = "Railbound.Progress.";

    /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
    public static int XpForLevel(int level) => 1000 + (Mathf.Max(1, level) - 1) * 150;

    /// <summary>Coins granted for reaching <paramref name="newLevel"/>.</summary>
    public static int LevelBonusCoins(int newLevel) => 100 + newLevel * 10;

    public struct PendingLevelUp { public int from, to, bonusCoins; }
    /// <summary>Level-ups the lobby has not shown yet (survives scene changes).</summary>
    public static readonly Queue<PendingLevelUp> Pending = new Queue<PendingLevelUp>();

    public static event Action Changed;

    private static ProgressData current;
    private static string loadedFor;     // the user id (or "local") the data belongs to

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
            return a != null && a.IsAuthenticated ? a.CurrentUserId : "local";
        }
    }

    private static void EnsureLocal()
    {
        string user = UserKey;
        if (current != null && loadedFor == user) return;
        current = ReadLocal(user) ?? new ProgressData();
        loadedFor = user;
    }

    private static ProgressData ReadLocal(string user)
    {
        try
        {
            string json = PlayerPrefs.GetString(LocalPrefix + user, "");
            if (!string.IsNullOrEmpty(json)) return Sanitize(JsonUtility.FromJson<ProgressData>(json));
        }
        catch (Exception) { /* unreadable: start fresh */ }
        return null;
    }

    private static void WriteLocal()
    {
        try
        {
            current.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
        return d;
    }

    // ---------------- cloud ----------------
    /// <summary>Reads the cloud copy and keeps whichever copy (cloud or device) has more total XP.</summary>
    public static async Task LoadAsync()
    {
        EnsureLocal();
        var auth = AuthenticationService.Instance;
        if (auth == null || !auth.IsAuthenticated) { Changed?.Invoke(); return; }

        try
        {
            var id = new StorageObjectId { Collection = Collection, Key = Key, UserId = auth.CurrentUserId };
            var result = await auth.Client.ReadStorageObjectsAsync(auth.Session, new[] { id });
            bool found = false;
            foreach (var o in result.Objects)
            {
                found = true;
                var remote = string.IsNullOrWhiteSpace(o.Value) ? null : Sanitize(JsonUtility.FromJson<ProgressData>(o.Value));
                if (remote != null && remote.totalXp >= current.totalXp)
                {
                    current = remote;
                    WriteLocal();
                }
                else if (remote != null)
                {
                    await SaveRemoteAsync();   // the device is ahead (an award that never reached the cloud)
                }
            }
            if (!found) await SaveRemoteAsync();   // first sign-in of this account: create its progress record
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Progress] Could not read the cloud copy: " + ex.Message);
        }
        Changed?.Invoke();
    }

    private static async Task SaveRemoteAsync()
    {
        var auth = AuthenticationService.Instance;
        if (auth == null || !auth.IsAuthenticated) return;
        try
        {
            if (current.updatedAt == 0) current.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var write = new WriteStorageObject
            {
                Collection = Collection, Key = Key, Value = JsonUtility.ToJson(current),
                PermissionRead = 1, PermissionWrite = 1,
            };
            await auth.Client.WriteStorageObjectsAsync(auth.Session, new[] { write });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Progress] Could not save to the cloud (kept on this device): " + ex.Message);
        }
    }

    // ---------------- awards ----------------
    /// <summary>Adds XP and coins, moves the level up as often as the XP allows, queues the level-up popup and saves.</summary>
    public static async Task<ProgressAward> AwardAsync(int xp, int coins, bool countsAsMatch = true)
    {
        EnsureLocal();
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
        await SaveRemoteAsync();
        return new ProgressAward(xp, coins, bonus, oldLevel, d.level, oldXp, d.xp);
    }

    /// <summary>Records a finished match (wins, kills, time, best place) and awards its XP and coins.</summary>
    public static Task<ProgressAward> RecordMatchAsync(MatchSummary m, int xp, int coins)
    {
        EnsureLocal();
        var d = current;
        if (m.won) d.wins++;
        d.kills += Mathf.Max(0, m.kills);
        d.playSeconds += (long)Mathf.Max(0f, m.survivedSeconds);
        int place = m.won ? 1 : m.placement;
        if (place > 0 && (d.bestPlacement == 0 || place < d.bestPlacement)) d.bestPlacement = place;
        return AwardAsync(xp, coins, true);
    }

    /// <summary>What a finished match is worth: a base amount, kills, time survived and placement (a win is worth the most).</summary>
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
