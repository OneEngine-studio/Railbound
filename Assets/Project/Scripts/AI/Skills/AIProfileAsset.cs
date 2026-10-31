using UnityEngine;

namespace Project.AI.Skills
{
    /// <summary>
    /// ScriptableObject asset representing the baseline / default AI profile for a given archetype or team.
    /// Designers can author templates (e.g. RangedTactical_Baseline, MeleeRusher_Baseline) in the Project window.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAIProfileAsset", menuName = "Project/AI/AI Profile Template")]
    public class AIProfileAsset : ScriptableObject
    {
        [SerializeField]
        private AIProfileData defaultProfile = new AIProfileData();

        /// <summary>
        /// The baseline profile data configured by designers.
        /// </summary>
        public AIProfileData DefaultProfile => defaultProfile;

        private void OnEnable()
        {
            // Ensure the default profile is never null when the asset is loaded
            if (defaultProfile == null)
            {
                defaultProfile = new AIProfileData();
            }
        }

        /// <summary>
        /// Creates a deep copy of the profile for runtime use.
        /// </summary>
        /// <param name="customProfileId">Optional ID to assign to the new runtime instance.</param>
        public AIProfileData CreateRuntimeProfileInstance(string customProfileId = null)
        {
            var instance = defaultProfile.Clone();
            
            if (!string.IsNullOrEmpty(customProfileId))
            {
                instance.ProfileId = customProfileId;
            }
            
            return instance;
        }
    }
}
