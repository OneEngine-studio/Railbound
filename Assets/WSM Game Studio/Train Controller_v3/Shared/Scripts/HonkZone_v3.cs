using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class HonkZone_v3 : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
                locomotive.Honk();
        }
    } 
}
