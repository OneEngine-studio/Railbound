using System;
using UnityEngine;

namespace NWH.VehiclePhysics2.Modules.Trailer
{
    /// <summary>
    ///     MonoBehaviour wrapper for Trailer module.
    /// </summary>
    [Serializable]
    public partial class TrailerModuleWrapper : ModuleWrapper
    {
        public TrailerModule module = new TrailerModule();
        
        private VehicleController _vehicleController;

        private void Awake()
        {
            // Run some of the trailer initialization here since the trailer VehicleController
            // might be disabled and un-initialized at the time of attachment.
            _vehicleController = GetComponentInParent<VehicleController>();
            if (_vehicleController == null)
            {
                Debug.LogError($"No VehicleController attached to the TrailerModule {name}");
                return;
            }
            
            if (module.attachmentPoint == null)
            {
                Debug.LogError($"{name}: TrailerModule attachmentPoint is null.");
                return;
            }
            
            SphereCollider triggerCollider = module.attachmentPoint.GetComponent<SphereCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = module.attachmentPoint.gameObject.AddComponent<SphereCollider>();
                triggerCollider.radius = module.attachmentTriggerRadius;
                triggerCollider.isTrigger = true;
                triggerCollider.gameObject.layer = module.attachmentLayer;
            }

            module.vehicleController = _vehicleController;
        }
        

        public override VehicleComponent GetModule()
        {
            return module;
        }
        
        
        public override void SetModule(VehicleComponent module)
        {
            this.module = module as TrailerModule;
        }
    }
}