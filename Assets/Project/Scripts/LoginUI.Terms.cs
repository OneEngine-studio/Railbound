using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public partial class LoginUI
{
    // ------------------------------------------------------------------ Terms validation
    private bool CheckTermsAccepted(string action = "continue")
    {
        if (!ageConfirmed || !termsAccepted)
        {
            string missing = !ageConfirmed && !termsAccepted ? "confirm your age and accept the Terms of Service"
                : !ageConfirmed ? "confirm your age" : "accept the Terms of Service";
            ShowErrorPopup("One More Step", $"Please {missing} below to {action}.", "warning");
            UpdateCheckboxesVisual();
            return false;
        }
        return true;
    }

    public void ToggleAge()
    {
        ageConfirmed = !ageConfirmed;
        UpdateCheckboxesVisual();
        if (ageConfirmed && termsAccepted) SetError(null);
        Debug.Log($"[LoginUI] Age checkbox: {ageConfirmed}");
    }

    public void ToggleTerms()
    {
        termsAccepted = !termsAccepted;
        UpdateCheckboxesVisual();
        if (ageConfirmed && termsAccepted) SetError(null);
        Debug.Log($"[LoginUI] Terms checkbox: {termsAccepted}");
    }

    private void UpdateCheckboxesVisual()
    {
        if (ageCheckboxImage) ageCheckboxImage.color = ageConfirmed ? checkedColor : uncheckedColor;
        if (termsCheckboxImage) termsCheckboxImage.color = termsAccepted ? checkedColor : uncheckedColor;
        if (ageCheckmark) ageCheckmark.SetActive(ageConfirmed);
        if (termsCheckmark) termsCheckmark.SetActive(termsAccepted);
    }

    private void AutoWireTermsCheckboxes()
    {
        // Try hierarchy: Canvas/Terms/Checkbox1 = age, Checkbox2 = terms (matches screenshot order)
        if (ageCheckboxButton == null)
        {
            var t = transform.root.Find("Canvas/Terms/Checkbox1") ?? GameObject.Find("Checkbox1")?.transform;
            if (t) ageCheckboxButton = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
        }
        if (termsCheckboxButton == null)
        {
            var t = transform.root.Find("Canvas/Terms/Checkbox2") ?? GameObject.Find("Checkbox2")?.transform;
            if (t) termsCheckboxButton = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
        }
        if (ageCheckboxImage == null && ageCheckboxButton) ageCheckboxImage = ageCheckboxButton.GetComponent<Image>();
        if (termsCheckboxImage == null && termsCheckboxButton) termsCheckboxImage = termsCheckboxButton.GetComponent<Image>();
        // fallback search by path inside same canvas
        if (ageCheckboxImage == null) ageCheckboxImage = GameObject.Find("Checkbox1")?.GetComponent<Image>();
        if (termsCheckboxImage == null) termsCheckboxImage = GameObject.Find("Checkbox2")?.GetComponent<Image>();
    }

    private void EnsureCheckmarkLabels()
    {
        ageCheckmark = GetOrCreateCheckmark(ageCheckboxImage, "AgeCheck");
        termsCheckmark = GetOrCreateCheckmark(termsCheckboxImage, "TermsCheck");
    }

    /// <summary>Builds a two-bar vector checkmark (no font glyph needed).</summary>
    private GameObject GetOrCreateCheckmark(Image parentImage, string name)
    {
        if (parentImage == null) return null;
        var existing = parentImage.transform.Find(name);
        if (existing != null)
        {
            // Drop legacy TMP-based checkmarks from older builds
            var legacy = existing.GetComponent<TMP_Text>();
            if (legacy != null) Destroy(legacy);
            if (existing.childCount == 0) Destroy(existing.gameObject);
            else return existing.gameObject;
        }
        var go = new GameObject(name);
        go.transform.SetParent(parentImage.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        // Short arm (bottom-left) + long arm, rotated into a tick
        AddTickBar(go.transform, new Vector2(-0.13f, -0.03f), 0.30f, 45f);
        AddTickBar(go.transform, new Vector2(0.12f, 0.07f), 0.58f, -45f);
        go.SetActive(false);
        return go;
    }

    private void AddTickBar(Transform parent, Vector2 centerFrac, float lenFrac, float angle)
    {
        var go = new GameObject("bar");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var box = ((RectTransform)parent).rect;
        float w = Mathf.Max(box.width, 18f);
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w * lenFrac, Mathf.Max(2.5f, w * 0.14f));
        rt.anchoredPosition = new Vector2(centerFrac.x * w, centerFrac.y * w);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.color = checkTickColor;
        img.raycastTarget = false;
    }
}
