using System;
using System.IO;
using UnityEngine;
using Project.AI.Skills;

namespace Project.AI.Learning
{
    /// <summary>
    /// Post-Match Evolutionary Learning and JSON Persistence System.
    /// Analyzes match weaknesses (flank kills, missed shots, delayed gunshot reactions, exposure damage),
    /// mutates behavioral weights, unlocks tactical skills, and persists updated AI profiles between matches.
    /// </summary>
    public static class AIProfileEvolutionSystem
    {
        private const string RELATIVE_DATA_PATH = "Assets/Project/Data/AIProfiles";

        public static string GetStorageDirectory()
        {
#if UNITY_EDITOR
            string dir = Path.Combine(Directory.GetCurrentDirectory(), RELATIVE_DATA_PATH);
#else
            string dir = Path.Combine(Application.persistentDataPath, "AIProfiles");
#endif
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetProfileFilePath(string profileId)
        {
            string cleanId = string.IsNullOrEmpty(profileId) ? "DefaultTacticalProfile" : profileId;
            return Path.Combine(GetStorageDirectory(), $"{cleanId}.json");
        }

        /// <summary>
        /// Loads an existing evolved JSON profile for profileId, or creates a new one from defaultTemplate.
        /// Call at match initialization.
        /// </summary>
        public static AIProfileData LoadOrCreateProfile(string profileId, AIProfileAsset defaultTemplate = null)
        {
            string filePath = GetProfileFilePath(profileId);
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    AIProfileData loaded = AIProfileData.FromJson(json);
                    if (loaded != null)
                    {
                        Debug.Log($"[AIEvolution] Loaded Gen {loaded.EvolutionGeneration} profile '{loaded.ProfileId}' from {filePath}. (Skills: {loaded.UnlockedSkills})");
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIEvolution] Error reading {filePath}: {ex.Message}. Initializing from template.");
                }
            }

            // Create initial profile from template or defaults
            AIProfileData newProfile = defaultTemplate != null
                ? defaultTemplate.CreateRuntimeProfileInstance(profileId)
                : new AIProfileData { ProfileId = profileId };

            SaveProfile(newProfile);
            return newProfile;
        }

        /// <summary>
        /// Serializes an AI profile to local JSON storage.
        /// </summary>
        public static void SaveProfile(AIProfileData profile)
        {
            if (profile == null) return;

            try
            {
                string filePath = GetProfileFilePath(profile.ProfileId);
                string json = profile.ToJson();
                File.WriteAllText(filePath, json);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
#endif
                Debug.Log($"[AIEvolution] Saved Gen {profile.EvolutionGeneration} profile '{profile.ProfileId}' to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEvolution] Failed to save profile '{profile.ProfileId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Core Post-Match Evolution Algorithm:
        /// Diagnoses combat weaknesses and mutates behavioral weights / unlocks skills accordingly.
        /// </summary>
        public static AIProfileData EvolveProfile(AIProfileData profile, AIMatchMetrics metrics)
        {
            if (profile == null || metrics == null) return profile;

            int prevGen = profile.EvolutionGeneration;
            AISkill prevSkills = profile.UnlockedSkills;

            // 1. Weakness: Killed by Flanks
            if (metrics.TimesKilledByFlank > 0 || metrics.HitsReceivedFromBehindOrSide > 2)
            {
                float flankBoost = 0.08f * Mathf.Max(1, metrics.TimesKilledByFlank);
                profile.FlankAwareness += flankBoost;

                if (profile.FlankAwareness >= 0.65f)
                {
                    profile.UnlockSkill(AISkill.FlankScouting);
                }
                if (profile.FlankAwareness >= 0.82f)
                {
                    profile.UnlockSkill(AISkill.SoundTriangulation);
                }
            }

            // 2. Weakness: Pinned Down / High Exposure Damage
            if (metrics.DamageTakenWhileExposed > metrics.DamageTakenWhileInCover * 1.25f || !metrics.SurvivedMatch)
            {
                profile.CoverPriority += 0.07f;
                if (profile.CoverPriority >= 0.62f)
                {
                    profile.UnlockSkill(AISkill.TacticalRetreat);
                }
            }

            // 3. Weakness: Low Shooting Accuracy
            if (metrics.ShotsFired >= 6)
            {
                float hitRatio = (float)metrics.ShotsHit / metrics.ShotsFired;
                if (hitRatio < 0.45f)
                {
                    // Improve aim accuracy & predictive tracking
                    profile.AimAccuracy += 0.06f;
                }

                if (profile.AimAccuracy >= 0.72f)
                {
                    profile.UnlockSkill(AISkill.PredictiveLeadShot);
                }
            }

            // 4. Acoustic Stimulus Adaptation
            if (metrics.GunshotsHeard > 0)
            {
                profile.HearingSensitivity += 0.06f;
                profile.ReactionTime = Mathf.Max(0.06f, profile.ReactionTime - 0.035f);
            }

            // 5. Aggression & Dominance Adaptation
            if (metrics.Kills >= 2 && metrics.SurvivedMatch)
            {
                profile.Aggression += 0.05f;
                if (profile.Aggression >= 0.72f)
                {
                    profile.UnlockSkill(AISkill.SuppressingFire);
                }
                if (profile.Aggression >= 0.84f)
                {
                    profile.UnlockSkill(AISkill.AggressiveBreach);
                }
            }
            else if (metrics.MatchDurationSeconds < 25f && !metrics.SurvivedMatch)
            {
                // Died prematurely: tone down over-aggressive rushing
                profile.Aggression -= 0.05f;
            }

            // Increment generation & match counter
            profile.EvolutionGeneration++;
            profile.TotalMatchesPlayed++;
            profile.ClampWeights();

            SaveProfile(profile);

            Debug.Log($"[AIEvolution] Evolution Complete for '{profile.ProfileId}': Gen {prevGen} -> Gen {profile.EvolutionGeneration}. " +
                      $"Accuracy: {profile.AimAccuracy:F2}, FlankAware: {profile.FlankAwareness:F2}, Cover: {profile.CoverPriority:F2}, Aggression: {profile.Aggression:F2}. " +
                      $"New Skills: {profile.UnlockedSkills & ~prevSkills}");

            return profile;
        }
    }
}
