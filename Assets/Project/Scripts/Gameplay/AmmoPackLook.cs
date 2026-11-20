using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Shows one of the ammo pack's carton variants (its children). The choice comes from where the pack lies, so every
    /// client sees the same brand for the same pack without syncing anything.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmmoPackLook : MonoBehaviour
    {
        [Tooltip("Carton variants; exactly one is shown.")]
        [SerializeField] private GameObject[] variants;

        private void Start()
        {
            if (variants == null || variants.Length == 0) return;
            Vector3 p = transform.position;
            int hash = Mathf.RoundToInt(p.x * 7f) * 73856093 ^ Mathf.RoundToInt(p.z * 7f) * 19349663;
            int pick = (hash & int.MaxValue) % variants.Length;
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) variants[i].SetActive(i == pick);
        }
    }
}
