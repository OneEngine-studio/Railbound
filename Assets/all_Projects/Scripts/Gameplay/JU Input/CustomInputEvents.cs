using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace JUTPS.InputEvents
{
    public class CustomInputEvents : MonoBehaviour
    {
        [Header("Actions")]
        public List<InputEvent> Actions = new List<InputEvent>();

        private void Awake()
        {
            // Remove any debug ragdoll actions mapped to G key
            if (Actions != null)
            {
                Actions.RemoveAll(a => a != null && a.IsGKeyAction());
            }
        }

        private void OnEnable()
        {
            foreach (InputEvent action in Actions)
            {
                if (action != null && !action.IsGKeyAction())
                {
                    action.SetupListeners();
                }
            }
        }

        private void OnDisable()
        {
            foreach (InputEvent action in Actions)
            {
                if (action != null) action.RemoveListeners();
            }
        }
    }

    [System.Serializable]
    public class InputEvent
    {
#pragma warning disable 0414
        [SerializeField]
        private string EventName = "Input Event";
#pragma warning restore 0414

        [InputControl(layout = "Button")]
        [SerializeField]
        private string Input;
        [HideInInspector] public InputAction targetInput;

        public UnityEngine.Events.UnityEvent OnInputEnter;
        public UnityEngine.Events.UnityEvent OnInputPerformed;
        public UnityEngine.Events.UnityEvent OnInputUp;

        public bool IsGKeyAction()
        {
            if (!string.IsNullOrEmpty(Input) && (Input.Contains("/g") || Input.Contains("/G"))) return true;
            if (!string.IsNullOrEmpty(EventName) && EventName.ToLower().Contains("ragdoll")) return true;
            return false;
        }

        private InputAction GenerateTargetInput()
        {
            targetInput = new InputAction("Actionn", InputActionType.Button, Input, expectedControlType: "Button");
            targetInput.Enable();
            return targetInput;
        }

        public void SetupListeners()
        {
            if (IsGKeyAction()) return;

            GenerateTargetInput();

            targetInput.started += OnEnter;
            targetInput.performed += OnPressing;
            targetInput.canceled += OnExit;
        }

        public void RemoveListeners()
        {
            if (targetInput == null) return;

            targetInput.started -= OnEnter;
            targetInput.performed -= OnPressing;
            targetInput.canceled -= OnExit;
        }

        private void OnEnter(InputAction.CallbackContext ctx)
        {
            OnInputEnter.Invoke();
        }

        private void OnPressing(InputAction.CallbackContext ctx)
        {
            OnInputPerformed.Invoke();
        }

        private void OnExit(InputAction.CallbackContext ctx)
        {
            OnInputUp.Invoke();
        }
    }
}
