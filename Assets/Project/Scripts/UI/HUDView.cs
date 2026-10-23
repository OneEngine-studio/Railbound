using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// In-game tactical HUD controller for combat status, weapons, minimap, and kill-feed.
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Bottom-Left (Vitality & Stance)")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Slider shieldBar;
        [SerializeField] private Image shieldFill;
        [SerializeField] private TMP_Text shieldText;
        [SerializeField] private Image stanceIcon;

        [Header("Bottom-Right (Weapons & Ordnance)")]
        [SerializeField] private TMP_Text ammoCurrentText;
        [SerializeField] private TMP_Text ammoReserveText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TMP_Text grenadeCountText;
        [SerializeField] private Image grenadeIcon;
        [SerializeField] private Image fireModeIcon;

        [Header("Top-Center (Match & Objectives)")]
        [SerializeField] private TMP_Text matchTimerText;
        [SerializeField] private Slider scoreBar;
        [SerializeField] private TMP_Text scoreTeamAText;
        [SerializeField] private TMP_Text scoreTeamBText;

        [Header("Top-Right (Minimap & Killfeed)")]
        [SerializeField] private RectTransform minimapContainer;
        [SerializeField] private RectTransform killfeedContainer;
        [SerializeField] private GameObject killfeedItemPrefab;

        public void SetHealth(float current, float max)
        {
            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            if (healthBar != null) healthBar.value = pct;
            if (healthText != null) healthText.text = Mathf.CeilToInt(current).ToString();
        }

        public void SetShield(float current, float max)
        {
            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            if (shieldBar != null) shieldBar.value = pct;
            if (shieldText != null) shieldText.text = Mathf.CeilToInt(current).ToString();
        }

        public void SetAmmo(int current, int reserve)
        {
            if (ammoCurrentText != null) ammoCurrentText.text = current.ToString();
            if (ammoReserveText != null) ammoReserveText.text = $"/ {reserve}";
        }

        public void SetWeapon(string weaponName, Sprite icon)
        {
            if (weaponNameText != null) weaponNameText.text = weaponName.ToUpper();
            if (weaponIcon != null && icon != null) weaponIcon.sprite = icon;
        }

        public void SetMatchTimer(string formattedTime)
        {
            if (matchTimerText != null) matchTimerText.text = formattedTime;
        }

        public void SetScores(int teamA, int teamB, int targetScore = 100)
        {
            if (scoreTeamAText != null) scoreTeamAText.text = teamA.ToString();
            if (scoreTeamBText != null) scoreTeamBText.text = teamB.ToString();
            if (scoreBar != null) scoreBar.value = (float)teamA / Mathf.Max(1f, teamA + teamB);
        }

        public void AddKillfeedEntry(string killer, string victim, Sprite weaponSprite)
        {
            if (killfeedContainer == null || killfeedItemPrefab == null) return;
            GameObject item = Instantiate(killfeedItemPrefab, killfeedContainer);
            TMP_Text txt = item.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = $"<color=#FF7A00>{killer}</color> killed <color=#E0E0E0>{victim}</color>";
            StartCoroutine(FadeAndDestroyKillfeed(item, 4f));
        }

        private IEnumerator FadeAndDestroyKillfeed(GameObject item, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (item != null)
            {
                CanvasGroup cg = item.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float t = 0f;
                    while (t < 0.5f)
                    {
                        t += Time.deltaTime;
                        cg.alpha = 1f - (t / 0.5f);
                        yield return null;
                    }
                }
                Destroy(item);
            }
        }
    }
}
