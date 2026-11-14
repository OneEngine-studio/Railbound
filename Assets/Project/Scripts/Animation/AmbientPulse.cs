using UnityEngine;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Attach to any object that should pulse continuously at runtime
    /// (notification badges, warning icons). Keeps edit-mode previews static.
    /// </summary>
    public sealed class AmbientPulse : MonoBehaviour
    {
        [SerializeField] private float amount = 1.10f;
        [SerializeField] private float cycle = 1.2f;

        private void Start()
        {
            IconAnimator.Pulse((RectTransform)transform, amount, cycle, this);
        }

        private void OnDisable() => UITween.Kill(this);
    }
}
