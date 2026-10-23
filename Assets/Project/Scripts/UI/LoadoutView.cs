using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical loadout customization view with slot selectors, weapon preview, stat bars, and equipment grid.
    /// </summary>
    public class LoadoutView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Slot Selectors")]
        [SerializeField] private Button primarySlotButton;
        [SerializeField] private Button secondarySlotButton;
        [SerializeField] private Button tacticalSlotButton;
        [SerializeField] private Button skinSlotButton;

        [Header("Weapon Preview")]
        [SerializeField] private Image weaponPreviewImage;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text weaponCategoryText;

        [Header("Stat Comparison Bars")]
        [SerializeField] private Slider damageBar;
        [SerializeField] private Slider fireRateBar;
        [SerializeField] private Slider accuracyBar;
        [SerializeField] private Slider rangeBar;
        [SerializeField] private Slider mobilityBar;

        [Header("Equipment Grid & Actions")]
        [SerializeField] private Transform equipmentGridContent;
        [SerializeField] private Button backButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button customizeButton;

        public event Action OnBackClicked;
        public event Action OnEquipClicked;
        public event Action OnCustomizeClicked;

        private int _selectedSlot = 0; // 0 = Primary, 1 = Secondary, 2 = Gadgets

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
            if (equipButton != null) equipButton.onClick.AddListener(() => OnEquipClicked?.Invoke());
            if (customizeButton != null) customizeButton.onClick.AddListener(() => OnCustomizeClicked?.Invoke());

            if (primarySlotButton != null) primarySlotButton.onClick.AddListener(() => SelectSlot(0));
            if (secondarySlotButton != null) secondarySlotButton.onClick.AddListener(() => SelectSlot(1));
            if (tacticalSlotButton != null) tacticalSlotButton.onClick.AddListener(() => SelectSlot(2));

            SelectSlot(0);
        }

        public void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            UpdateSlotButtonVisuals();

            switch (slotIndex)
            {
                case 0:
                    if (weaponNameText != null) weaponNameText.text = "MK-18 CARBINE";
                    if (weaponCategoryText != null) weaponCategoryText.text = "PRIMARY WEAPON // ASSAULT RIFLE";
                    SetStats(72f, 65f, 80f, 60f, 75f);
                    break;
                case 1:
                    if (weaponNameText != null) weaponNameText.text = "APEX-9 PISTOL";
                    if (weaponCategoryText != null) weaponCategoryText.text = "SECONDARY WEAPON // SIDEARM";
                    SetStats(45f, 85f, 70f, 35f, 95f);
                    break;
                case 2:
                    if (weaponNameText != null) weaponNameText.text = "EMP GRENADE MK.II";
                    if (weaponCategoryText != null) weaponCategoryText.text = "TACTICAL GADGET // ORDNANCE";
                    SetStats(90f, 20f, 95f, 40f, 80f);
                    break;
            }
        }

        private void UpdateSlotButtonVisuals()
        {
            Color activeColor = new Color(1f, 0.48f, 0f, 1f); // #FF7A00
            Color idleColor = new Color(0.12f, 0.15f, 0.19f, 1f);

            SetButtonColor(primarySlotButton, _selectedSlot == 0 ? activeColor : idleColor);
            SetButtonColor(secondarySlotButton, _selectedSlot == 1 ? activeColor : idleColor);
            SetButtonColor(tacticalSlotButton, _selectedSlot == 2 ? activeColor : idleColor);
        }

        private void SetButtonColor(Button btn, Color c)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = c;
        }

        public void Show()
        {
            SelectSlot(0);
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void SetStats(float damage, float fireRate, float accuracy, float range, float mobility)
        {
            if (damageBar != null) damageBar.value = Mathf.Clamp01(damage / 100f);
            if (fireRateBar != null) fireRateBar.value = Mathf.Clamp01(fireRate / 100f);
            if (accuracyBar != null) accuracyBar.value = Mathf.Clamp01(accuracy / 100f);
            if (rangeBar != null) rangeBar.value = Mathf.Clamp01(range / 100f);
            if (mobilityBar != null) mobilityBar.value = Mathf.Clamp01(mobility / 100f);
        }
    }
}
