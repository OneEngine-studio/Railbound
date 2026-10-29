using System.Collections.Generic;
using UnityEngine;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    [ExecuteInEditMode]
    public class RouteManager : MonoBehaviour
    {
        #region SINGLETON
        private static RouteManager _instance;
        public static RouteManager Instance { get { return _instance; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                    Destroy(this.gameObject);
                else
                    DestroyImmediate(this.gameObject);
            }
            else
            {
                _instance = this;
            }
        }
        #endregion

        [Range(0, 1)]
        [SerializeField] private float _positionAlongRails = 0.9f;
        [SerializeField] private List<Route> _routes;

        public List<Route> Routes { get { return _routes; } set { _routes = value; } }
        public float PositionAlongRails { get { return _positionAlongRails; } set { _positionAlongRails = value; } }

        /// <summary>
        /// On enable is used to normalize routes on loading
        /// </summary>
        private void OnEnable()
        {
            NormalizeRoutes();
        }

        /// <summary>
        /// Normalize routes for spline followers smooth transition between splines
        /// </summary>
        private void NormalizeRoutes()
        {
            if (_routes != null)
            {
                for (int routeIndex = 0; routeIndex < _routes.Count; routeIndex++)
                {
                    if (_routes[routeIndex].Splines == null || _routes[routeIndex].Splines.Count == 0)
                        continue;

                    _routes[routeIndex].NormalizedRoute = new OrientedPoint[_routes[routeIndex].Splines.Count][];

                    float optimalSpacing = 2f, splineLength;
                    for (int i = 0; i < _routes[routeIndex].Splines.Count; i++)
                    {
                        if (_routes[routeIndex].Splines[i] == null) continue;

                        splineLength = _routes[routeIndex].Splines[i].GetTotalDistance(true);
                        optimalSpacing = splineLength / Mathf.Floor(splineLength);
                        _routes[routeIndex].NormalizedRoute[i] = _routes[routeIndex].Splines[i].CalculateOrientedPoints(optimalSpacing, false);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new routes and add it to the end of the list
        /// </summary>
        public void CreateRoute()
        {
            _routes = _routes == null ? new List<Route>() : _routes;

            string routeName = string.Format("Route {0}", _routes.Count + 1);
            Route newRoute = new Route(routeName);

            _routes.Add(newRoute);
        }

        /// <summary>
        /// Delete route by index
        /// </summary>
        /// <param name="routeIndex"></param>
        public void DeleteRoute(int routeIndex)
        {
            if (_routes == null) return;
            if (routeIndex >= _routes.Count) return;

            _routes.RemoveAt(routeIndex);
        }

        /// <summary>
        /// Applies route to target locomotive
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="routeIndex"></param>
        /// <returns>True if route applied successfully</returns>
        public bool ApplyRoute(ILocomotive locomotive, int routeIndex, bool applyCustomPositionAlongRails = false)
        {
            routeIndex = Mathf.Abs(routeIndex);

            if (_routes == null || routeIndex >= _routes.Count)
                return false;

            if (applyCustomPositionAlongRails)
                locomotive.AssignRoute(_routes[routeIndex], _positionAlongRails);
            else
                locomotive.AssignRoute(_routes[routeIndex]);

            return true;
        }
    }
}
