using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Master controller for LoginMenu ambient VFX overlays (embers & drifting smoke).
    /// Handles automated pause/resume during modal screens and scene transitions.
    /// </summary>
    public class AmbientVFXController : MonoBehaviour
    {
        [Header("Layer Overlays")]
        [SerializeField] private UIEmberOverlay emberOverlay;
        [SerializeField] private UIEmberOverlay smokeOverlay;

        [Header("Runtime State")]
        [SerializeField] private bool autoPlayOnStart = true;

        private void Awake()
        {
            if (emberOverlay == null)
                emberOverlay = transform.Find("Embers")?.GetComponent<UIEmberOverlay>();

            if (smokeOverlay == null)
                smokeOverlay = transform.Find("Smoke")?.GetComponent<UIEmberOverlay>();

            if (autoPlayOnStart)
                Resume();
        }

        public void Pause()
        {
            if (emberOverlay != null) emberOverlay.Pause();
            if (smokeOverlay != null) smokeOverlay.Pause();
        }

        public void Resume()
        {
            if (emberOverlay != null) emberOverlay.Resume();
            if (smokeOverlay != null) smokeOverlay.Resume();
        }

        public void SetEmission(bool active)
        {
            if (emberOverlay != null) emberOverlay.SetEmission(active);
            if (smokeOverlay != null) smokeOverlay.SetEmission(active);
        }
    }
}
