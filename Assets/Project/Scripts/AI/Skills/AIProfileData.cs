using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Skills
{
    /// <summary>
    /// JSON-serializable behavioral weight profile and skill set for an AI character.
    /// Loaded at match start, updated through in-game telemetry, and mutated post-match by AIProfileEvolutionSystem.
    /// </summary>
    [Serializable]
    public class AIProfileData
    {
        [Header("Profile Identity & Evolution Generation")]
        public string ProfileId = "DefaultTacticalProfile";
        public int EvolutionGeneration = 1;
        public int TotalMatchesPlayed = 0;

        [Header("Behavioral Weights [0.1 .. 1.0]")]
        [Range(0.1f, 1.0f)] public float AimAccuracy = 0.65f;
        [Range(0.1f, 1.0f)] public float FlankAwareness = 0.50f;
        [Range(0.1f, 1.0f)] public float CoverPriority = 0.55f;
        [Range(0.1f, 1.0f)] public float Aggression = 0.60f;
        [Range(0.5f, 2.5f)] public float HearingSensitivity = 1.0f;
        [Range(0.05f, 0.8f)] public float ReactionTime = 0.25f;
        [Range(5f, 30f)] public float OptimalRange = 14f;

        [Header("Unlocked Skills")]
        public AISkill UnlockedSkills = AISkill.None;

        public bool HasSkill(AISkill skill)
        {
            return (UnlockedSkills & skill) == skill;
        }

        public void UnlockSkill(AISkill skill)
        {
            UnlockedSkills |= skill;
        }

        public void ClampWeights()
        {
            AimAccuracy = Mathf.Clamp(AimAccuracy, 0.15f, 0.98f);
            FlankAwareness = Mathf.Clamp(FlankAwareness, 0.10f, 1.0f);
            CoverPriority = Mathf.Clamp(CoverPriority, 0.10f, 1.0f);
            Aggression = Mathf.Clamp(Aggression, 0.10f, 1.0f);
            HearingSensitivity = Mathf.Clamp(HearingSensitivity, 0.5f, 2.2f);
            ReactionTime = Mathf.Clamp(ReactionTime, 0.06f, 0.75f);
            OptimalRange = Mathf.Clamp(OptimalRange, 6f, 26f);
        }

        public AIProfileData Clone()
        {
            return new AIProfileData
            {
                ProfileId = this.ProfileId,
                EvolutionGeneration = this.EvolutionGeneration,
                TotalMatchesPlayed = this.TotalMatchesPlayed,
                AimAccuracy = this.AimAccuracy,
                FlankAwareness = this.FlankAwareness,
                CoverPriority = this.CoverPriority,
                Aggression = this.Aggression,
                HearingSensitivity = this.HearingSensitivity,
                ReactionTime = this.ReactionTime,
                OptimalRange = this.OptimalRange,
                UnlockedSkills = this.UnlockedSkills
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public static AIProfileData FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<AIProfileData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIProfileData] Failed to parse JSON profile: {ex.Message}");
                return null;
            }
        }
    }
}
