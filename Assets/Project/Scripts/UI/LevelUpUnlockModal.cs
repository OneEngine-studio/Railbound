using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Post-match progression celebration modal displaying level up and unlocked rewards.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LevelUpUnlockModal : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Unlock Details")]
        [SerializeField] private TMP_Text levelTitleText;
        [SerializeField] private TMP_Text rewardCategoryText;
        [SerializeField] private TMP_Text rewardNameText;
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private Button claimButton;

        public event Action OnClaimed;

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();
            if (claimButton != null) claimButton.onClick.AddListener(ClaimReward);
        }

        public void Show(int level, string rewardName, string category = "NEW WEAPON UNLOCKED", Sprite icon = null)
        {
            if (levelTitleText != null) levelTitleText.text = $"OPERATOR LEVEL {level}";
            if (rewardCategoryText != null) rewardCategoryText.text = category;
            if (rewardNameText != null) rewardNameText.text = rewardName;
            if (rewardIconImage != null && icon != null)
            {
                rewardIconImage.sprite = icon;
                rewardIconImage.gameObject.SetActive(true);
            }

            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void ClaimReward()
        {
            OnClaimed?.Invoke();
            Hide();
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }
    }
}
