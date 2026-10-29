using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

namespace Project.AI.RL
{
    /// <summary>
    /// Coordinates Multi-Agent Reinforcement Learning (MA-POCA / PPO), Curriculum Stages,
    /// and runtime telemetry for all JUCharacterRLAgent instances in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Project/AI/RL/RL Training & Inference Manager")]
    public class RLTrainingManager : MonoBehaviour
    {
        public enum CurriculumStage
        {
            Stage1_TargetPractice = 0,
            Stage2_TacticalDuels = 1,
            Stage3_BattleRoyaleSquads = 2
        }

        [Header("Curriculum & Multi-Agent Settings")]
        public CurriculumStage ActiveStage = CurriculumStage.Stage3_BattleRoyaleSquads;
        public bool EnableMaPocaSquadGroup = true;
        public bool AutoRespawnDeadAgentsInTraining = false;
        public float EpisodeTimeoutSeconds = 180f;

        [Header("Registered RL Agents")]
        public List<JUCharacterRLAgent> RegisteredAgents = new List<JUCharacterRLAgent>();

        [Header("Live Squad Telemetry (Read-Only)")]
        public int AliveAgentCount;
        public float MeanSquadReward;
        public float CurrentEpisodeElapsed;

        private SimpleMultiAgentGroup squadAgentGroup;

        private void Awake()
        {
            if (RegisteredAgents == null || RegisteredAgents.Count == 0)
            {
                var found = FindObjectsOfType<JUCharacterRLAgent>(true);
                RegisteredAgents = new List<JUCharacterRLAgent>(found);
            }

            if (EnableMaPocaSquadGroup)
            {
                squadAgentGroup = new SimpleMultiAgentGroup();
                for (int i = 0; i < RegisteredAgents.Count; i++)
                {
                    if (RegisteredAgents[i] != null)
                    {
                        squadAgentGroup.RegisterAgent(RegisteredAgents[i]);
                    }
                }
            }

            ApplyCurriculumParameters();
        }

        private void Update()
        {
            CurrentEpisodeElapsed += Time.deltaTime;

            int alive = 0;
            float rewardSum = 0f;
            int validCount = 0;

            for (int i = 0; i < RegisteredAgents.Count; i++)
            {
                var agent = RegisteredAgents[i];
                if (agent == null) continue;

                validCount++;
                rewardSum += agent.CurrentEpisodeReward;

                bool isDead = agent.SelfHealth != null && agent.SelfHealth.IsDead;
                if (!isDead)
                {
                    alive++;
                }
                else if (AutoRespawnDeadAgentsInTraining)
                {
                    agent.ResetAgentToSpawn();
                    agent.OnEpisodeBegin();
                }
            }

            AliveAgentCount = alive;
            MeanSquadReward = validCount > 0 ? rewardSum / validCount : 0f;

            if (AutoRespawnDeadAgentsInTraining && CurrentEpisodeElapsed >= EpisodeTimeoutSeconds)
            {
                if (squadAgentGroup != null)
                {
                    squadAgentGroup.GroupEpisodeInterrupted();
                }
                CurrentEpisodeElapsed = 0f;
            }
        }

        public void ApplyCurriculumParameters()
        {
            // Read curriculum environment parameters from ML-Agents Academy if connected
            float stageParam = Academy.Instance.EnvironmentParameters.GetWithDefault("curriculum_stage", (float)ActiveStage);
            ActiveStage = (CurriculumStage)Mathf.Clamp(Mathf.RoundToInt(stageParam), 0, 2);

            for (int i = 0; i < RegisteredAgents.Count; i++)
            {
                var agent = RegisteredAgents[i];
                if (agent == null) continue;

                switch (ActiveStage)
                {
                    case CurriculumStage.Stage1_TargetPractice:
                        agent.DetectionRadius = 40f;
                        agent.OptimalCombatRange = 10f;
                        break;
                    case CurriculumStage.Stage2_TacticalDuels:
                        agent.DetectionRadius = 55f;
                        agent.OptimalCombatRange = 14f;
                        break;
                    case CurriculumStage.Stage3_BattleRoyaleSquads:
                        agent.DetectionRadius = 75f;
                        agent.OptimalCombatRange = 16f;
                        break;
                }
            }
        }
    }
}
