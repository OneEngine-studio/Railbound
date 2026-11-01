using System;
using System.IO;
using UnityEngine;
using Project.AI.Skills;

namespace Project.AI.Learning
{
    /// <summary>
    /// Post-match evolutionary learning and JSON persistence system.
    /// Analyzes weaknesses, mutates AI profile weights, unlocks skills,
    /// and persists updates between matches.
    /// </summary>
    public static class AIProfileEvolutionSystem
    {
        private const string RELATIVE_DATA_PATH = "Assets/Project/Data/AIProfiles";
        private const string DEFAULT_PROFILE_ID = "DefaultTacticalProfile";

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
            string cleanId = SanitizeProfileId(profileId);
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

            AIProfileData newProfile = defaultTemplate != null
                ? defaultTemplate.CreateRuntimeProfileInstance(profileId)
                : new AIProfileData { ProfileId = SanitizeProfileId(profileId) };

            SaveProfile(newProfile);
            return newProfile;
        }

        /// <summary>
        /// Serializes an AI profile to local JSON storage.
        /// </summary>
        public static void SaveProfile(AIProfileData profile)
        {
            if (profile == null)
            {
                return;
            }

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
        /// Core post-match evolution algorithm:
        /// Diagnoses combat weaknesses and mutates behavioral weights / unlocks skills accordingly.
        /// </summary>
        public static AIProfileData EvolveProfile(AIProfileData profile, AIMatchMetrics metrics)
        {
            if (profile == null || metrics == null)
            {
                return profile;
            }

            int previousGeneration = profile.EvolutionGeneration;
            AISkill previousSkills = profile.UnlockedSkills;

            // 1. Weakness: Killed by flanks / high exposure to rear/side attacks
            if (metrics.TimesKilledByFlank > 0 || metrics.HitsReceivedFromBehindOrSide > 2)
            {
                float flankBoost = 0.08f * Mathf.Max(1, metrics.TimesKilledByFlank);
                profile.FlankAwareness = Mathf.Clamp01(profile.FlankAwareness + flankBoost);

                if (profile.FlankAwareness >= 0.65f)
                {
                    UnlockSkill(profile, AISkill.FlankScouting);
                }

                if (profile.FlankAwareness >= 0.82f)
                {
                    UnlockSkill(profile, AISkill.SoundTriangulation);
                }
            }

            // 2. Weakness: pinned down / heavy exposure damage
            bool isExposureWeakness =
                metrics.DamageTakenWhileExposed > metrics.DamageTakenWhileInCover * 1.25f ||
                !metrics.SurvivedMatch;

            if (isExposureWeakness)
            {
                profile.CoverPriority = Mathf.Clamp01(profile.CoverPriority + 0.07f);

                if (profile.CoverPriority >= 0.62f)
                {
                    UnlockSkill(profile, AISkill.TacticalRetreat);
                }
            }

            // 3. Weakness: low shooting accuracy
            if (metrics.ShotsFired >= 6)
            {
                float hitRatio = (float)metrics.ShotsHit / metrics.ShotsFired;
                if (hitRatio < 0.45f)
                {
                    profile.AimAccuracy = Mathf.Clamp01(profile.AimAccuracy + 0.06f);
                }

                if (profile.AimAccuracy >= 0.72f)
                {
                    UnlockSkill(profile, AISkill.PredictiveLeadShot);
                }
            }

            // 4. Acoustic stimulus adaptation
            if (metrics.GunshotsHeard > 0)
            {
                profile.HearingSensitivity = Mathf.Clamp(profile.HearingSensitivity + 0.06f, 0.5f, 2.2f);
                profile.ReactionTime = Mathf.Max(0.06f, profile.ReactionTime - 0.035f);
            }

            // 5. Aggression / dominance adaptation
            if (metrics.Kills >= 2 && metrics.SurvivedMatch)
            {
                profile.Aggression = Mathf.Clamp01(profile.Aggression + 0.05f);

                if (profile.Aggression >= 0.72f)
                {
                    UnlockSkill(profile, AISkill.SuppressingFire);
                }

                if (profile.Aggression >= 0.84f)
                {
                    UnlockSkill(profile, AISkill.AggressiveBreach);
                }
            }
            else if (metrics.MatchDurationSeconds < 25f && !metrics.SurvivedMatch)
            {
                // Died prematurely: tone down over-aggressive rushing
                profile.Aggression = Mathf.Clamp01(profile.Aggression - 0.05f);
            }

            // Increment generation & match counter
            profile.EvolutionGeneration++;
            profile.TotalMatchesPlayed++;
            profile.ClampWeights();

            SaveProfile(profile);

            AISkill newlyUnlocked = profile.UnlockedSkills & ~previousSkills;

            Debug.Log(
                $"[AIEvolution] Evolution Complete for '{profile.ProfileId}': Gen {previousGeneration} -> Gen {profile.EvolutionGeneration}. " +
                $"Accuracy: {profile.AimAccuracy:F2}, FlankAware: {profile.FlankAwareness:F2}, Cover: {profile.CoverPriority:F2}, Aggression: {profile.Aggression:F2}. " +
                $"New Skills: {newlyUnlocked}"
            );

            return profile;
        }

        private static string SanitizeProfileId(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return DEFAULT_PROFILE_ID;
            }

            string sanitized = profileId.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return string.IsNullOrEmpty(sanitized) ? DEFAULT_PROFILE_ID : sanitized;
        }

        private static void UnlockSkill(AIProfileData profile, AISkill skill)
        {
            if (profile == null || skill == AISkill.None)
            {
                return;
            }

            profile.UnlockSkill(skill);
        }
    }
}
