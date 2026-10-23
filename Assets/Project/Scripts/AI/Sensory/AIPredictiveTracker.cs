using UnityEngine;

namespace Project.AI.Sensory
{
    /// <summary>
    /// Kinematic Motion Tracker and Predictive Aiming Solver.
    /// Tracks target velocity vector and acceleration to predict future intercept positions,
    /// enabling AI agents to lead moving/sprinting players rather than shooting behind them.
    /// </summary>
    [System.Serializable]
    public class AIPredictiveTracker
    {
        [Header("Kinematic State")]
        public Vector3 CurrentPosition;
        public Vector3 SmoothedVelocity;
        public Vector3 SmoothedAcceleration;

        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private float lastSampleTime;
        private bool hasInitialized;

        [Header("Filter Settings")]
        public float VelocitySmoothing = 12f;
        public float AccelerationSmoothing = 8f;
        public float MaxLeadTime = 1.25f;

        public void Reset()
        {
            hasInitialized = false;
            SmoothedVelocity = Vector3.zero;
            SmoothedAcceleration = Vector3.zero;
        }

        /// <summary>
        /// Updates the kinematic history of the observed target. Call every frame or fixed step.
        /// </summary>
        public void UpdateTargetKinematics(Vector3 targetPosition, float deltaTime)
        {
            if (deltaTime <= 0.0001f) return;

            if (!hasInitialized)
            {
                CurrentPosition = targetPosition;
                prevPosition = targetPosition;
                SmoothedVelocity = Vector3.zero;
                prevVelocity = Vector3.zero;
                SmoothedAcceleration = Vector3.zero;
                hasInitialized = true;
                return;
            }

            CurrentPosition = targetPosition;
            Vector3 rawVelocity = (targetPosition - prevPosition) / deltaTime;

            // Dampen absurd teleport/respawn velocity spikes
            if (rawVelocity.magnitude > 45f)
            {
                rawVelocity = rawVelocity.normalized * 45f;
            }

            SmoothedVelocity = Vector3.Lerp(SmoothedVelocity, rawVelocity, Mathf.Clamp01(VelocitySmoothing * deltaTime));

            Vector3 rawAcceleration = (SmoothedVelocity - prevVelocity) / deltaTime;
            if (rawAcceleration.magnitude > 50f)
            {
                rawAcceleration = rawAcceleration.normalized * 50f;
            }
            SmoothedAcceleration = Vector3.Lerp(SmoothedAcceleration, rawAcceleration, Mathf.Clamp01(AccelerationSmoothing * deltaTime));

            prevPosition = targetPosition;
            prevVelocity = SmoothedVelocity;
        }

        /// <summary>
        /// Predicts where the target will be deltaSeconds in the future using 2nd-order Taylor expansion:
        /// P(t) = P(0) + V * t + 0.5 * A * t^2
        /// </summary>
        public Vector3 PredictPosition(float deltaSeconds, bool useAcceleration = true)
        {
            float t = Mathf.Clamp(deltaSeconds, 0f, MaxLeadTime);
            Vector3 predicted = CurrentPosition + (SmoothedVelocity * t);
            if (useAcceleration)
            {
                predicted += 0.5f * SmoothedAcceleration * (t * t);
            }
            return predicted;
        }

        /// <summary>
        /// Computes the exact lead intercept aim point for a projectile fired from shooterPosition at bulletSpeed.
        /// AccuracyWeight [0..1] blends between direct line-of-sight and perfect lead intercept,
        /// adding micro-jitter for imperfect accuracy.
        /// </summary>
        public Vector3 CalculateLeadAimPoint(Vector3 shooterPosition, float bulletSpeed, float accuracyWeight, bool useAcceleration = true)
        {
            float speed = Mathf.Max(20f, bulletSpeed);
            float dist = Vector3.Distance(shooterPosition, CurrentPosition);
            float timeToImpact = Mathf.Clamp(dist / speed, 0.01f, MaxLeadTime);

            Vector3 perfectLead = PredictPosition(timeToImpact, useAcceleration);

            // Interpolate between current raw position (0 accuracy) and perfect lead (1 accuracy)
            float effectiveLeadWeight = Mathf.Clamp01(accuracyWeight);
            Vector3 blendedPoint = Vector3.Lerp(CurrentPosition, perfectLead, effectiveLeadWeight);

            // Micro-spread error inversely proportional to accuracy
            if (accuracyWeight < 0.95f)
            {
                float spreadMagnitude = (1f - accuracyWeight) * Mathf.Clamp(dist * 0.035f, 0.1f, 1.8f);
                Vector3 jitter = new Vector3(
                    Mathf.Sin(Time.time * 8.3f + CurrentPosition.x) * spreadMagnitude,
                    Mathf.Cos(Time.time * 6.7f + CurrentPosition.y) * (spreadMagnitude * 0.45f),
                    Mathf.Sin(Time.time * 9.1f + CurrentPosition.z) * spreadMagnitude
                );
                blendedPoint += jitter;
            }

            return blendedPoint;
        }
    }
}
