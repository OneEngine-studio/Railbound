using System;

namespace Project.AI.Skills
{
    /// <summary>
    /// Unlockable & upgradable tactical abilities unlocked through post-match evolutionary learning.
    /// Alter the AI's state machine, sensory perception, and combat decision trees.
    /// </summary>
    [Flags]
    public enum AISkill
    {
        None = 0,

        /// <summary>
        /// Fires controlled bursts at suspected threat cover or gunshot origin even before visual confirmation.
        /// </summary>
        SuppressingFire = 1 << 0,

        /// <summary>
        /// Sprints to heavy cover / high ground when taking heavy damage or out-gunned rather than trading hits.
        /// </summary>
        TacticalRetreat = 1 << 1,

        /// <summary>
        /// Approaches sound stimuli and threat markers from a perpendicular flanking angle (45-75 degrees)
        /// rather than bee-lining directly down the player's line of sight.
        /// </summary>
        FlankScouting = 1 << 2,

        /// <summary>
        /// Applies 2nd-order kinematic predictive tracking (velocity + acceleration) to lead sprinting targets.
        /// </summary>
        PredictiveLeadShot = 1 << 3,

        /// <summary>
        /// Pinpoints exact coordinate of distant gunshots with zero noise/offset after hearing 1-2 shots.
        /// </summary>
        SoundTriangulation = 1 << 4,

        /// <summary>
        /// Rapidly closes distance with aggressive strafe-jumping when enemy is reloading or facing away.
        /// </summary>
        AggressiveBreach = 1 << 5
    }
}
