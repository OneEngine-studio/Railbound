using UnityEngine;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    public class RailSensor : MonoBehaviour
    {
        private bool _onRails = false;
        private bool _grounded = false;
        private RaycastHit _hit;
        private int _lastObjId = 0;
        private int _currentObjId;

        public bool OnRails { get { return _onRails; } }
        public bool Grounded { get { return _grounded; } }

        private void Update()
        {
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out _hit, 0.5f))
            {
                _grounded = true;

                _currentObjId = _hit.collider.gameObject.GetInstanceID();

                if (_currentObjId != _lastObjId) //Verify rails only when collision target is changed for performance optmization
                {
                    //Tag verification left for legacy support
                    Transform parent = _hit.transform.parent;
                    if (_hit.collider.gameObject.tag == "Rails" || (parent != null && (parent.gameObject.GetComponent<BakedSegment>() != null || parent.gameObject.GetComponent<SplineMeshRenderer>())))
                    {
                        _onRails = true;
#if UNITY_EDITOR
                        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * _hit.distance, Color.yellow);
#endif
                    }
                    else
                        _onRails = false; 
                }

                _lastObjId = _currentObjId;
            }
            else
            {
                _onRails = false;
                _grounded = false;
                _lastObjId = 0;
#if UNITY_EDITOR
                Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * 0.2f, Color.white);
#endif
            }
        }
    }
}
