using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [System.Serializable]
    public struct CustomWagonComponent
    {
        public GameObject prefab;
        public string customName;
        public SpawnPosition[] positions;
    } 
}