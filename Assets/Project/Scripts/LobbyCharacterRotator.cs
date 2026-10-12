using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Rotates the 3D lobby character on horizontal mouse/touch drag and drives
    /// the Animator's "Left Turn", "Right Turn", and "Idle" states.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyCharacterRotator : MonoBehaviour
    {
        private static readonly int IsTurningLeftHash = Animator.StringToHash("IsTurningLeft");
        private static readonly int IsTurningRightHash = Animator.StringToHash("IsTurningRight");

        [SerializeField] private Animator characterAnimator;
        [SerializeField] private float rotationSpeed = 0.45f;
        [SerializeField] private float dragThreshold = 0.35f;
        [SerializeField] private float turnStateHoldTime = 0.18f;

        private bool _isDragging;
        private Vector3 _lastPointerPos;
        private float _lastTurnTime;
        private int _turnDirection; // -1 = Left Turn, 0 = Idle, +1 = Right Turn
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        private void Awake()
        {
            if (characterAnimator == null)
                characterAnimator = GetComponent<Animator>();
            if (characterAnimator != null)
                characterAnimator.applyRootMotion = false;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverInteractiveUI(Input.mousePosition))
                {
                    _isDragging = true;
                    _lastPointerPos = Input.mousePosition;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                _turnDirection = 0;
                ApplyAnimatorTurnState(0);
            }

            if (_isDragging && Input.GetMouseButton(0))
            {
                Vector3 currentPos = Input.mousePosition;
                float deltaX = currentPos.x - _lastPointerPos.x;
                _lastPointerPos = currentPos;

                if (Mathf.Abs(deltaX) > dragThreshold)
                {
                    // Rotate character around Y axis following mouse drag
                    transform.Rotate(0f, -deltaX * rotationSpeed, 0f, Space.Self);

                    // Trigger Left Turn or Right Turn animation based on drag direction
                    _turnDirection = deltaX < 0f ? -1 : 1;
                    _lastTurnTime = Time.unscaledTime;
                }
                else if (Time.unscaledTime - _lastTurnTime > turnStateHoldTime)
                {
                    _turnDirection = 0;
                }

                ApplyAnimatorTurnState(_turnDirection);
            }
        }

        private void ApplyAnimatorTurnState(int dir)
        {
            if (characterAnimator == null) return;
            characterAnimator.SetBool(IsTurningLeftHash, dir < 0);
            characterAnimator.SetBool(IsTurningRightHash, dir > 0);
        }

        private bool IsPointerOverInteractiveUI(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            _raycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                var go = _raycastResults[i].gameObject;
                if (go == null) continue;
                if (go.GetComponentInParent<Selectable>() != null ||
                    go.GetComponentInParent<ScrollRect>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
