using System.Collections;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Automatically fades out and destroys loot highlight lights after a few seconds.
    /// Used for green (medkit) and yellow (ammo box) loot drop markers.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LootLightAutoDestroy : MonoBehaviour
    {
        [Tooltip("Seconds before the light begins fading out.")]
        public float delay = 3.5f;

        [Tooltip("Duration of the smooth fade out.")]
        public float fadeDuration = 1.0f;

        private Light _light;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void Start()
        {
            StartCoroutine(FadeAndDestroyRoutine());
        }

        private IEnumerator FadeAndDestroyRoutine()
        {
            yield return new WaitForSeconds(delay);

            if (_light != null)
            {
                float initialIntensity = _light.intensity;
                float elapsed = 0f;

                while (elapsed < fadeDuration && _light != null)
                {
                    elapsed += Time.deltaTime;
                    _light.intensity = Mathf.Lerp(initialIntensity, 0f, elapsed / fadeDuration);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }
    }
}

