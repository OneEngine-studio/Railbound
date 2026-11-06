using System.Collections;
using Michsky.UI.Reach;
using TMPro;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Put on a pause-menu settings row that needs special hardware (NVIDIA DLSS). When the GPU / driver cannot run it,
    /// the row is dimmed, made non-interactive and its label says why, so players never wonder why a setting does nothing.
    /// </summary>
    public class GraphicsFeatureGate : MonoBehaviour
    {
        public enum Feature { NvidiaDlss }

        [SerializeField] private Feature requires = Feature.NvidiaDlss;
        [SerializeField] private string unsupportedSuffix = "  (NVIDIA RTX REQUIRED)";

        private IEnumerator Start()
        {
            // HDRP detects DLSS while it initialises, so give it a moment before judging
            yield return new WaitForSecondsRealtime(1.5f);
            if (IsSupported()) yield break;

            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.4f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var label = GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && !label.text.EndsWith(unsupportedSuffix)) label.text += unsupportedSuffix;
        }

        private bool IsSupported()
        {
            switch (requires)
            {
                case Feature.NvidiaDlss: return GraphicsManager.IsDLSSSupported;
                default: return true;
            }
        }
    }
}
