using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modular tactical operator selection screen view.
    /// Displays roster cards, operator bio, abilities, and deployment readiness.
    /// </summary>
    public class OperatorSelectView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Navigation & Actions")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button selectOperatorButton;

        [Header("Inspector Readouts")]
        [SerializeField] private TextMeshProUGUI operatorNameText;
        [SerializeField] private TextMeshProUGUI operatorClassText;
        [SerializeField] private TextMeshProUGUI tacticalAbilityText;
        [SerializeField] private TextMeshProUGUI passivePerkText;
        [SerializeField] private TextMeshProUGUI operatorBioText;

        public event Action OnBackClicked;
        public event Action<int> OnOperatorSelected;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => OnBackClicked?.Invoke());

            if (selectOperatorButton != null)
                selectOperatorButton.onClick.AddListener(() => OnOperatorSelected?.Invoke(0));
        }

        public void Show()
        {
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }
    }
}
