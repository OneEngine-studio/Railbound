using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// Marks a spot where match setup may place loot (for example the floor inside a house).
    /// BRMatchSetup puts part of the weapons, ammo and medkits on these points so buildings are worth searching.
    /// </summary>
    [AddComponentMenu("Railbound/BR/Loot Point")]
    public class BRLootPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.3f, 0.3f);
        }
    }
}
