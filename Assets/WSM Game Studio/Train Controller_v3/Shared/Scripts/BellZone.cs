using UnityEngine;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class BellZone : MonoBehaviour
    {
        [FormerlySerializedAs("triggerType")]
        [SerializeField] private ZoneTriggerType _triggerType;

        public ZoneTriggerType TriggerType
        {
            get { return _triggerType; }
            set { _triggerType = value; }
        }

        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
            {
                if ((_triggerType == ZoneTriggerType.Activate && !locomotive.BellOn) || (_triggerType == ZoneTriggerType.Deactivate && locomotive.BellOn))
                    locomotive.ToogleBell();
            }
        }
    }
}
