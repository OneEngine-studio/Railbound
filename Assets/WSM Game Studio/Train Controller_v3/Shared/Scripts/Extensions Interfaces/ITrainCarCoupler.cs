using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public interface ITrainCarCoupler
    {
        bool IsBackJoint { get; }
        bool IsLocomotive { get; }
        bool IsWagon { get; }
    }
}
