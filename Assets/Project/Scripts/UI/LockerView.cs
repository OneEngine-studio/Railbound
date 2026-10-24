using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modular tactical locker & customization view.
    /// Manages cosmetics, weapon camos, balaclavas, and tactical gear vaults.
    /// </summary>
    public class LockerView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Navigation & Actions")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button equipItemButton;

        [Header("Item Inspector")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemCategoryText;
        [SerializeField] private TextMeshProUGUI itemRarityText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;

        public event Action OnBackClicked;
        public event Action<int> OnItemEquipped;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => OnBackClicked?.Invoke());

            if (equipItemButton != null)
                equipItemButton.onClick.AddListener(() => OnItemEquipped?.Invoke(0));
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
