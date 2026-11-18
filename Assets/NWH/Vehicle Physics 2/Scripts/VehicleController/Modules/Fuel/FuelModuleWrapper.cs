using System;

namespace NWH.VehiclePhysics2.Modules.Fuel
{
    /// <summary>
    ///     MonoBehaviour wrapper for Fuel module.
    /// </summary>
    [Serializable]
    public partial class FuelModuleWrapper : ModuleWrapper
    {
        public FuelModule module = new FuelModule();


        public override VehicleComponent GetModule()
        {
            return module;
        }


        public override void SetModule(VehicleComponent module)
        {
            this.module = module as FuelModule;
        }
    }
}