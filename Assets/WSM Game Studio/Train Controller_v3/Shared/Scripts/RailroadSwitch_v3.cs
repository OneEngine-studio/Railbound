using System.Collections.Generic;
using WSMGameStudio.Splines;
using UnityEngine.Events;
using UnityEngine;
using System.Linq;

namespace WSMGameStudio.RailroadSystem
{
    public class RailroadSwitch_v3 : MonoBehaviour
    {
        private bool _activated = false;

        //Variables
        [SerializeField] private List<GameObject> _railsColliders;
        [SerializeField] private List<Spline> _railsSplines; //Not being used, but kept anyway (hidden from inspector)
        [SerializeField] private List<int> _affectedRoutes;
        //Events
        [SerializeField] private UnityEvent _onActivate;
        [SerializeField] private UnityEvent _onDeactivate;
        [SerializeField] private UnityEvent _onSwitch;

        public bool Activated { get { return _activated; } }
        public List<GameObject> RailsColliders { get { return _railsColliders; } set { _railsColliders = value; } }
        public List<Spline> RailsSplines { get { return _railsSplines; } set { _railsSplines = value; } }
        public List<int> AffectedRoutes
        {
            get
            {
                ValidateAffectedRoutes();
                return _affectedRoutes;
            }
            set { _affectedRoutes = value; }
        }
        //Events
        public UnityEvent OnActivate { get { return _onActivate; } set { _onActivate = value; } }
        public UnityEvent OnDeactivate { get { return _onDeactivate; } set { _onDeactivate = value; } }
        public UnityEvent OnSwitch { get { return _onSwitch; } set { _onSwitch = value; } }

        /// <summary>
        /// Physics based rail switching
        /// OnActivate, OnDeactivate and OnSwitch events are triggered
        /// </summary>
        public void SwitchRails()
        {
            if (_railsColliders != null)
            {
                foreach (var collider in _railsColliders)
                    collider.SetActive(!collider.activeInHierarchy);
            }

            UpdateActivationStatus();
            _onSwitch.Invoke();
        }

        private void UpdateActivationStatus()
        {
            _activated = !_activated;

            if (_activated)
                _onActivate.Invoke();
            else
                _onDeactivate.Invoke();
        }

        /// <summary>
        /// Physics based rail switching
        /// OnSwitch event is triggered
        /// </summary>
        /// <param name="indexes"></param>
        /// <param name="activation"></param>
        public void SwitchRailsByIndex(int[] indexes, bool activation)
        {
            for (int i = 0; i < _railsColliders.Count; i++)
            {
                _railsColliders[i].SetActive(indexes.Contains(i) ? activation : !activation);
            }

            _onSwitch.Invoke();
        }

        /// <summary>
        /// Spline based rail switching
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="leftRouteIndex"></param>
        /// <param name="rightRouteIndex"></param>
        public void SplineBasedSwitchRails(ILocomotive locomotive, int leftRouteIndex, int rightRouteIndex)
        {
            if (locomotive.GetType() != typeof(SplineBasedLocomotive))
                return;

            if (!ValidateAffectedRoutes())
                return;
            
            if (leftRouteIndex >= _affectedRoutes.Count || rightRouteIndex >= _affectedRoutes.Count)
                return;

            if (_activated)
                RouteManager.Instance.ApplyRoute((SplineBasedLocomotive)locomotive, _affectedRoutes[leftRouteIndex]);
            else
                RouteManager.Instance.ApplyRoute((SplineBasedLocomotive)locomotive, _affectedRoutes[rightRouteIndex]);

            UpdateActivationStatus();
            _onSwitch.Invoke();
        }

        /// <summary>
        /// Validate if routes exists, if not, remove
        /// </summary>
        /// <returns></returns>
        public bool ValidateAffectedRoutes()
        {
            RouteManager routeManager = RouteManager.Instance;

#if UNITY_EDITOR
            routeManager = FindObjectOfType<RouteManager>();
#endif

            if (routeManager == null)
                return false;

            if (_affectedRoutes == null || _affectedRoutes.Count == 0)
                return false;

            if (routeManager.Routes == null)
            {
                _affectedRoutes = new List<int>();
            }
            else
            {
                for (int i = _affectedRoutes.Count - 1; i >= 0; i--)
                {
                    if (_affectedRoutes[i] > routeManager.Routes.Count - 1)
                        _affectedRoutes.RemoveAt(i);
                }
            }

            return true;
        }
    }
}
