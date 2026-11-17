using System;
using System.Collections.Generic;

// Weapon mastery, the battle royale loadout, ability upgrades and the season battle pass. The VALUES live on the server (Nakama
// module, PROGRESSION_TABLES): the client gets them with every profile reply and only displays them.

[Serializable]
public sealed class WeaponMastery { public int level = 1; public int xp; public int kills; }

[Serializable]
public sealed class LoadoutData { public string sidearm = "p226"; public string melee = "none"; public string lethal = "none"; }

[Serializable]
public sealed class SeasonData { public string id = ""; public int xp; public List<int> claimed = new List<int>(); }

[Serializable]
public sealed class WeaponGain { public string weapon; public int kills, xp, oldLevel, newLevel; }

[Serializable]
public sealed class SkillDef
{
    public string name;
    public int maxLevel;
    public List<int> costs = new List<int>();
    public List<int> requiredLevel = new List<int>();
    public List<Dictionary<string, float>> effects = new List<Dictionary<string, float>>();
}

[Serializable]
public sealed class RewardDef { public string type; public int amount; public string kind; public string rarity; }

[Serializable]
public sealed class TierDef { public int tier; public RewardDef free; public RewardDef premium; }

[Serializable]
public sealed class SeasonDef { public string id; public int tiers; public int xpPerTier; public List<TierDef> rewards = new List<TierDef>(); }

[Serializable]
public sealed class ProgressionTables
{
    public Dictionary<string, string> weapons = new Dictionary<string, string>();
    public int weaponXpPerKill;
    public int weaponMaxLevel;
    public List<int> weaponLevelXp = new List<int>();
    public Dictionary<string, Dictionary<string, int>> loadoutSlots = new Dictionary<string, Dictionary<string, int>>();
    public Dictionary<string, SkillDef> skills = new Dictionary<string, SkillDef>();
    public SeasonDef season;

    /// <summary>XP needed from this level to the next (0 at max level).</summary>
    public int XpForWeaponLevel(int level)
    {
        return level >= 1 && level - 1 < weaponLevelXp.Count ? weaponLevelXp[level - 1] : 0;
    }

    /// <summary>The player level an item of a loadout slot unlocks at (int.MaxValue if unknown).</summary>
    public int UnlockLevel(string slot, string item)
    {
        return loadoutSlots != null && loadoutSlots.TryGetValue(slot, out var items) && items.TryGetValue(item, out int lvl) ? lvl : int.MaxValue;
    }

    /// <summary>An effect value of a skill at a level (from the server's table), or the fallback.</summary>
    public float SkillEffect(string skill, int level, string key, float fallback)
    {
        if (skills == null || !skills.TryGetValue(skill, out var def) || def.effects == null || def.effects.Count == 0) return fallback;
        var e = def.effects[Math.Max(0, Math.Min(level, def.effects.Count - 1))];
        return e != null && e.TryGetValue(key, out float v) ? v : fallback;
    }
}
