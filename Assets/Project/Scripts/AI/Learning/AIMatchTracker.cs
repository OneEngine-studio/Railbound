using System;
using UnityEngine;
using JUTPS;
using JUTPS.CharacterBrain;

namespace Project.AI.Learning
{
    [Serializable]
    public class AIMatchMetrics
    {
        public string ProfileId;
        public int MatchNumber;
        public float MatchDurationSeconds;
        public bool SurvivedMatch;

        // Combat Accuracy & Output
        public int ShotsFired;
        public int ShotsHit;
        public float DamageDealt;
        public float DamageTaken;
        public int Kills;

        // Flank & Positional Awareness
        public int TimesKilledByFlank;
        public int HitsReceivedFromBehindOrSide;

        // Cover & Tactical Behavior
        public int TimesEnteredCover;
        public float DamageTakenWhileInCover;
        public float DamageTakenWhileExposed;

        // Acoustic Perception
        public int GunshotsHeard;
        public int SoundThreatsInvestigated;
        public float AverageReactionTimeSeconds;
    }

    /// <summary>
    /// Logs in-match performance telemetry for a single AI agent.
    /// Tracks shots fired/hit, damage received from flanks, cover effectiveness,
    /// and audio reaction times for post-match evolutionary adaptation.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Project/AI/Learning/AI Match Tracker")]
    public class AIMatchTracker : MonoBehaviour
    {
        [Header("Active Match Telemetry")]
        [SerializeField] private AIMatchMetrics currentMetrics = new AIMatchMetrics();

        private JUHealth selfHealth;
        private JUCharacterBrain selfBrain;
        
        private float matchStartTime;
        private float totalReactionTimeAccum;
        private int reactionTimeCount;
        private bool hasEndedMatch;

        public AIMatchMetrics CurrentMetrics => currentMetrics;

        private void Awake()
        {
            selfHealth = GetComponent<JUHealth>();
            selfBrain = GetComponent<JUCharacterBrain>();
            matchStartTime = Time.time;
        }

        private void OnEnable()
        {
            if (selfHealth != null)
            {
                selfHealth.OnTakeDamage += HandleDamageReceived;
                selfHealth.OnDeath.AddListener(HandleDeath);
            }
        }

        private void OnDisable()
        {
            if (selfHealth != null)
            {
                selfHealth.OnTakeDamage -= HandleDamageReceived;
                selfHealth.OnDeath.RemoveListener(HandleDeath);
            }
        }

        public void Initialize(string profileId, int matchNumber)
        {
            currentMetrics.ProfileId = profileId;
            currentMetrics.MatchNumber = matchNumber;
            currentMetrics.MatchDurationSeconds = 0f;
            currentMetrics.SurvivedMatch = true;
            
            matchStartTime = Time.time;
            hasEndedMatch = false;
            
            // Reset accumulators
            totalReactionTimeAccum = 0f;
            reactionTimeCount = 0;
        }

        public void RecordShotFired() => currentMetrics.ShotsFired++;
        public void RecordKill() => currentMetrics.Kills++;
        public void RecordThreatInvestigated() => currentMetrics.SoundThreatsInvestigated++;
        public void RecordCoverEntered() => currentMetrics.TimesEnteredCover++;

        public void RecordShotHit(float damage)
        {
            currentMetrics.ShotsHit++;
            currentMetrics.DamageDealt += damage;
        }

        public void RecordSoundHeard(float reactionLatencySeconds)
        {
            currentMetrics.GunshotsHeard++;
            totalReactionTimeAccum += reactionLatencySeconds;
            reactionTimeCount++;
            
            if (reactionTimeCount > 0)
            {
                currentMetrics.AverageReactionTimeSeconds = totalReactionTimeAccum / reactionTimeCount;
            }
        }

        private void HandleDamageReceived(float damage, Vector3 hitPosition)
        {
            currentMetrics.DamageTaken += damage;

            // Check if damage came from behind or flank (> 75 degrees away from forward)
            if (hitPosition != Vector3.zero)
            {
                Vector3 damageDir = (hitPosition - transform.position).normalized;
                damageDir.y = 0f; // Flatten to horizontal plane
                
                float angle = Vector3.Angle(transform.forward, damageDir);
                if (angle > 75f)
                {
                    currentMetrics.HitsReceivedFromBehindOrSide++;
                }
            }

            // Estimate whether agent was in cover based on crouch state
            bool isCrouched = selfBrain != null && selfBrain.IsCrouched;
            if (isCrouched)
            {
                currentMetrics.DamageTakenWhileInCover += damage;
            }
            else
            {
                currentMetrics.DamageTakenWhileExposed += damage;
            }
        }

        private void HandleDeath()
        {
            currentMetrics.SurvivedMatch = false;

            if (currentMetrics.HitsReceivedFromBehindOrSide > 0)
            {
                currentMetrics.TimesKilledByFlank++;
            }

            EndMatch();
        }

        public AIMatchMetrics EndMatch()
        {
            if (hasEndedMatch) return currentMetrics;
            hasEndedMatch = true;

            currentMetrics.MatchDurationSeconds = Time.time - matchStartTime;
            return currentMetrics;
        }
    }
}
