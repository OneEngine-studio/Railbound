using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Loading animation built from the Railbound "R" icon: the icon breathes (scale and brightness)
    /// while a thin ring with a fading tail spins around it. Runs on unscaled time so it also plays while paused.
    /// </summary>
    public class LogoLoadingSpinner : MonoBehaviour
    {
        [SerializeField] private RectTransform icon;
        [SerializeField] private Image iconImage;
        [SerializeField] private RectTransform ring;
        [SerializeField] private float ringSpeed = 220f;
        [SerializeField] private float pulseSpeed = 3.2f;
        [SerializeField, Range(0f, 0.3f)] private float pulseScale = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.7f;

        private void Update()
        {
            float t = Time.unscaledTime;
            if (ring != null) ring.localRotation = Quaternion.Euler(0f, 0f, -(t * ringSpeed) % 360f);

            float wave = 0.5f + 0.5f * Mathf.Sin(t * pulseSpeed);
            if (icon != null) icon.localScale = Vector3.one * (1f - pulseScale + pulseScale * 2f * wave);
            if (iconImage != null)
            {
                var c = iconImage.color;
                c.a = Mathf.Lerp(minAlpha, 1f, wave);
                iconImage.color = c;
            }
        }
    }
}
