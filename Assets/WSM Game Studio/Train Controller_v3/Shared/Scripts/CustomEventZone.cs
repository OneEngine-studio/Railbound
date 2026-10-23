using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class CustomEventZone : MonoBehaviour
    {
        [FormerlySerializedAs("customEvents")]
        [SerializeField] private UnityEvent _customEvents;

        public UnityEvent CustomEvents
        {
            get { return _customEvents; }
            set { _customEvents = value; }
        }

        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
            {
                if (_customEvents != null)
                    _customEvents.Invoke();
            }
        }
    } 
}
