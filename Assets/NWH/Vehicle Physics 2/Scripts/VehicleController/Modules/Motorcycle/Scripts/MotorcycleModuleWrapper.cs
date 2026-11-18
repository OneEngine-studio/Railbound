using System;

namespace NWH.VehiclePhysics2.Modules.MotorcycleModule
{
    /// <summary>
    ///     MonoBehaviour wrapper for example module.
    /// </summary>
    [Serializable]
    public partial class MotorcycleModuleWrapper : ModuleWrapper
    {
        public MotorcycleModule module = new MotorcycleModule();

        public override VehicleComponent GetModule()
        {
            return module;
        }

        public override void SetModule(VehicleComponent module)
        {
            this.module = module as MotorcycleModule;
        }
    }
}