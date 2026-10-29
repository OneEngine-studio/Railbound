using System.Collections.Generic;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [CreateAssetMenu(fileName = "New Train Profile", menuName = "WSM Game Studio/Train Controller/Train Profile", order = 1)]
    public class TrainProfile : ScriptableObject
    {
        public GameObject locomotivePrefab;
        public List<GameObject> wagonsPrefabs;
    } 
}
