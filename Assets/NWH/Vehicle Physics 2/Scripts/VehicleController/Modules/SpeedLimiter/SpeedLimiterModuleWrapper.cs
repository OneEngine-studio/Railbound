using System;

namespace NWH.VehiclePhysics2.Modules.SpeedLimiter
{
    /// <summary>
    ///     MonoBehaviour wrapper for SpeedLimiter module.
    /// </summary>
    [Serializable]
    public partial class SpeedLimiterModuleWrapper : ModuleWrapper
    {
        public SpeedLimiterModule module = new SpeedLimiterModule();


        public override VehicleComponent GetModule()
        {
            return module;
        }


        public override void SetModule(VehicleComponent module)
        {
            this.module = module as SpeedLimiterModule;
        }
    }
}