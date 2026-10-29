using System.Collections.Generic;
using WSMGameStudio.Splines;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [System.Serializable]
    public class Route
    {
        [SerializeField] private string _name;
        [SerializeField] private List<Spline> _splines;
        [System.NonSerialized] private OrientedPoint[][] _normalizedRoute; //Calculate on loading time by the route manager

        public string Name { get { return _name; } set { _name = value; } }
        public List<Spline> Splines { get { return _splines; } set { _splines = value; } }
        public OrientedPoint[][] NormalizedRoute { get { return _normalizedRoute; } set { _normalizedRoute = value; } }

        /// <summary>
        /// True if route has splines assigned to it
        /// </summary>
        public bool IsValid { get { return (_splines != null && _splines.Count > 0); } }

        public int Length
        {
            get
            {
                int length = _splines == null ? 0 : _splines.Count;
                return length;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        public Route(string name)
        {
            _name = name;
            _splines = new List<Spline>();
        }
    }
}
