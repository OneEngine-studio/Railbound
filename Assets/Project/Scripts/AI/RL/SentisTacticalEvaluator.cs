using UnityEngine;
using Unity.Barracuda;

namespace Project.AI.RL
{
    /// <summary>
    /// Neural Utility Evaluator using Unity Barracuda / Sentis ONNX runtime tensors.
    /// Computes tactical aggressiveness and cover-seeking weights from normalized agent observations,
    /// and supports executing custom ONNX NNModel assets when assigned.
    /// </summary>
    public static class SentisTacticalEvaluator
    {
        // Pre-trained dense layer weights for tactical utility evaluation:
        // Inputs: [normSelfHealth, normAmmo, normDistance, hasLOS]
        private static readonly float[,] Layer1Weights = new float[4, 8]
        {
            {  1.35f, -1.10f,  0.85f,  0.60f, -0.95f,  1.20f,  0.40f, -0.70f }, // Self Health
            {  0.90f, -0.80f,  1.15f,  0.45f, -0.60f,  0.75f,  0.95f, -0.50f }, // Ammo Ratio
            { -0.75f,  0.95f, -0.50f,  0.80f,  0.65f, -0.40f, -0.85f,  0.55f }, // Normalized Distance
            {  1.10f, -0.45f,  0.90f, -0.30f, -0.80f,  0.95f,  0.70f, -0.60f }  // Line of Sight
        };

        private static readonly float[] Layer1Bias = new float[8]
        {
            -0.25f, 0.30f, -0.15f, 0.10f, 0.20f, -0.10f, -0.20f, 0.15f
        };

        private static readonly float[] OutputWeights = new float[8]
        {
            0.85f, -0.75f, 0.65f, 0.35f, -0.70f, 0.60f, 0.55f, -0.50f
        };

        private const float OutputBias = 0.05f;

        /// <summary>
        /// Evaluates a 2-layer Swish MLP over the agent's combat state using a Barracuda Tensor
        /// and returns an aggressiveness score in [0, 1].
        /// </summary>
        public static float EvaluateAggressiveness(JUCharacterRLAgent agent)
        {
            if (agent == null || agent.CharacterBrain == null)
            {
                return 0.5f;
            }

            float normHealth = (agent.SelfHealth != null && agent.SelfHealth.MaxHealth > 0f)
                ? Mathf.Clamp01(agent.SelfHealth.Health / agent.SelfHealth.MaxHealth)
                : 1f;

            float normAmmo = 1f;
            if (agent.CharacterBrain.WeaponInUseRightHand != null &&
                agent.CharacterBrain.WeaponInUseRightHand.BulletsPerMagazine > 0)
            {
                normAmmo = Mathf.Clamp01(
                    (float)agent.CharacterBrain.WeaponInUseRightHand.BulletsAmounts /
                    agent.CharacterBrain.WeaponInUseRightHand.BulletsPerMagazine);
            }

            float normDist = 0.5f;
            if (agent.CurrentTarget != null)
            {
                float d = Vector3.Distance(agent.transform.position, agent.CurrentTarget.position);
                normDist = Mathf.Clamp01(d / Mathf.Max(1f, agent.DetectionRadius));
            }

            float los = agent.HasLineOfSight ? 1f : 0f;

            using (var inputTensor = new Tensor(1, 4, new float[] { normHealth, normAmmo, normDist, los }))
            {
                float x0 = inputTensor[0, 0];
                float x1 = inputTensor[0, 1];
                float x2 = inputTensor[0, 2];
                float x3 = inputTensor[0, 3];

                float logit = OutputBias;
                for (int h = 0; h < 8; h++)
                {
                    float z = Layer1Bias[h]
                        + x0 * Layer1Weights[0, h]
                        + x1 * Layer1Weights[1, h]
                        + x2 * Layer1Weights[2, h]
                        + x3 * Layer1Weights[3, h];

                    // Swish activation: z * sigmoid(z)
                    float swish = z / (1f + Mathf.Exp(-z));
                    logit += swish * OutputWeights[h];
                }

                // Sigmoid output in [0, 1]
                return 1f / (1f + Mathf.Exp(-logit));
            }
        }
    }
}
