using UnityEngine;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class SpeedChangeZone_v3 : MonoBehaviour
    {
        [FormerlySerializedAs("targetSpeed")]
        [SerializeField] private float _targetSpeed = 65f;

        public float TargetSpeed
        {
            get { return _targetSpeed; }
            set { _targetSpeed = Mathf.Abs(value); }
        }

        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
                locomotive.MaxSpeed = _targetSpeed;
        }
    } 
}
