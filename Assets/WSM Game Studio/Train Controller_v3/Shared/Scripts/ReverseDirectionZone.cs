using UnityEngine;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class ReverseDirectionZone : MonoBehaviour
    {
        [FormerlySerializedAs("reverseDirectionMode")]
        [SerializeField] private ReverseDirectionMode _reverseDirectionMode;

        public ReverseDirectionMode ReverseDirectionMode
        {
            get { return _reverseDirectionMode; }
            set { _reverseDirectionMode = value; }
        }

        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
            {
                switch (_reverseDirectionMode)
                {
                    case ReverseDirectionMode.Always:
                        locomotive.Acceleration *= -1;
                        break;
                    case ReverseDirectionMode.OnlyIfMovingForward:
                        if (locomotive.Acceleration > 0f) locomotive.Acceleration *= -1;
                        break;
                    case ReverseDirectionMode.OnlyIfMovingBackwards:
                        if (locomotive.Acceleration < 0f) locomotive.Acceleration *= -1;
                        break;
                }
            }
        }
    }
}
