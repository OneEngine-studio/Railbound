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
        public AIProfileData DefaultProfile = new AIProfileData();

        public AIProfileData CreateRuntimeProfileInstance(string customProfileId = null)
        {
            var instance = DefaultProfile != null ? DefaultProfile.Clone() : new AIProfileData();
            if (!string.IsNullOrEmpty(customProfileId))
            {
                instance.ProfileId = customProfileId;
            }
            return instance;
        }
    }
}
