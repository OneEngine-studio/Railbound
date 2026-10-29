using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical UI hover and click animation effect for the Yellow Line HUD.
    /// Provides smooth scale punch, tactical glow/tint, and audio feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticalUIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.96f;
        [SerializeField] private float animSpeed = 14f;

        [Header("Highlight Settings")]
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private bool tintOnHover = false;
        [SerializeField] private Color hoverColor = new Color(1.0f, 0.894f, 0.36f, 1.0f); // #FFE45C
        private Color normalColor = Color.white;

        private Vector3 baseScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private Selectable selectable;
        private Coroutine animRoutine;

        private void Awake()
        {
            baseScale = transform.localScale;
            targetScale = baseScale;
            selectable = GetComponent<Selectable>();

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }
            if (targetGraphic != null)
            {
                normalColor = targetGraphic.color;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;

            targetScale = baseScale * hoverScale;
            if (tintOnHover && targetGraphic != null)
            {
                targetGraphic.color = hoverColor;
            }

            RailboundYellowHUDController.Instance?.PlayHoverSound();

            StartAnimRoutine();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = baseScale;
            if (tintOnHover && targetGraphic != null)
            {
                targetGraphic.color = normalColor;
            }

            StartAnimRoutine();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;

            targetScale = baseScale * pressScale;
            StartAnimRoutine();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;

            targetScale = baseScale * hoverScale;
            StartAnimRoutine();
        }

        private void OnDisable()
        {
            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }
            transform.localScale = baseScale;
            if (tintOnHover && targetGraphic != null)
            {
                targetGraphic.color = normalColor;
            }
        }

        private void StartAnimRoutine()
        {
            if (gameObject.activeInHierarchy)
            {
                if (animRoutine != null) StopCoroutine(animRoutine);
                animRoutine = StartCoroutine(AnimateScale());
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateScale()
        {
            while (Vector3.Distance(transform.localScale, targetScale) > 0.002f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);
                yield return null;
            }
            transform.localScale = targetScale;
            animRoutine = null;
        }
    }
}
