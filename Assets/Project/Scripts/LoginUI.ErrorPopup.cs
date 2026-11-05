using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public partial class LoginUI
{
    // ------------------------------------------------------------------ Error Popup
    private void AutoWireErrorPopup()
    {
        // Try to find the ErrorPopup hierarchy created in the scene
        if (errorPopupCanvasGroup == null)
        {
            var popup = GameObject.Find("Canvas/ErrorPopup")?.GetComponent<CanvasGroup>();
            if (popup != null) errorPopupCanvasGroup = popup;
        }
        if (errorPopupTitle == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/ErrorTitle");
            if (obj != null) errorPopupTitle = obj.GetComponent<TMP_Text>();
        }
        if (errorPopupMessage == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/ErrorMessage");
            if (obj != null) errorPopupMessage = obj.GetComponent<TMP_Text>();
        }
        if (errorPopupOKButton == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton");
            if (obj != null) errorPopupOKButton = obj.GetComponent<Button>();
        }
        if (errorPopupCancelButton == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/CancelButton");
            if (obj != null) errorPopupCancelButton = obj.GetComponent<Button>();
        }
        if (errorPopupOKButton)
        {
            errorPopupOKButton.onClick.RemoveListener(OnPopupOKClicked);
            errorPopupOKButton.onClick.AddListener(OnPopupOKClicked);
        }
        if (errorPopupCancelButton)
        {
            errorPopupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);
            errorPopupCancelButton.onClick.AddListener(OnPopupCancelClicked);
        }
        // Ensure hidden on start
        HideErrorPopup();
    }

    /// <summary>Show a styled Battle Royale error popup.
    /// type: "error" (red), "warning" (orange), "info" (cyan)
    /// mode: "ok" = single CONFIRM, "retry" = TRY AGAIN + CANCEL, "cancel_only" = CANCEL only
    /// onRetry: callback when TRY AGAIN is pressed
    /// </summary>
    public void ShowErrorPopup(string title, string message, string type = "error", string mode = "ok", System.Action onRetry = null)
    {
        if (errorPopupCanvasGroup == null)
        {
            SetError($"{title}: {message}");
            return;
        }

        _onRetryAction = onRetry;

        // Pick accent color based on type
        string hex = type == "error" ? errorColor : type == "warning" ? warningColor : infoColor;
        ColorUtility.TryParseHtmlString(hex, out Color accent);
        Color darkAccent = accent * 0.5f;
        darkAccent.a = accent.a;

        // Title
        if (errorPopupTitle)
        {
            errorPopupTitle.text = title;
            errorPopupTitle.color = accent;
        }

        // Message
        if (errorPopupMessage)
        {
            errorPopupMessage.text = message;
        }

        // Color all accent elements
        SetAccentColor("TopBar", accent);
        SetAccentColor("BottomBar", darkAccent);
        SetAccentColor("LeftStripe", accent);
        SetAccentColor("RightStripe", darkAccent);
        SetAccentColor("SepLine", darkAccent);

        // Icon
        var iconBg = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg")?.GetComponent<Image>();
        if (iconBg) { Color a = accent; a.a = 0.15f; iconBg.color = a; }
        var iconBorder = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg/IconBorder")?.GetComponent<Image>();
        if (iconBorder) { Color a = accent; a.a = 0.8f; iconBorder.color = a; }
        var iconText = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg/Icon!")?.GetComponent<TMP_Text>();
        if (iconText) iconText.color = accent;

        // OK / Try Again button
        var btnImg = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton")?.GetComponent<Image>();
        if (btnImg) btnImg.color = accent;
        var btnBorder = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton/BtnBorder")?.GetComponent<Image>();
        if (btnBorder) { Color a = accent; a.a = 0.6f; btnBorder.color = a; }

        // Configure buttons based on mode
        if (errorPopupOKButton)
        {
            var okLabel = errorPopupOKButton.transform.Find("Label")?.GetComponent<TMP_Text>();
            var okRT = errorPopupOKButton.GetComponent<RectTransform>();

            if (mode == "retry")
            {
                // Two buttons: TRY AGAIN (left) + CANCEL (right)
                okLabel.text = "TRY AGAIN";
                okRT.anchorMin = new Vector2(0.12f, 0.04f);
                okRT.anchorMax = new Vector2(0.47f, 0.18f);
                errorPopupOKButton.gameObject.SetActive(true);
                if (errorPopupCancelButton) errorPopupCancelButton.gameObject.SetActive(true);
            }
            else if (mode == "cancel_only")
            {
                errorPopupOKButton.gameObject.SetActive(false);
                if (errorPopupCancelButton)
                {
                    var cCancelRT = errorPopupCancelButton.GetComponent<RectTransform>();
                    cCancelRT.anchorMin = new Vector2(0.25f, 0.04f);
                    cCancelRT.anchorMax = new Vector2(0.75f, 0.18f);
                    var cLabel = errorPopupCancelButton.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (cLabel) cLabel.text = "CANCEL";
                    errorPopupCancelButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Single CONFIRM button centered
                okLabel.text = "CONFIRM";
                okRT.anchorMin = new Vector2(0.25f, 0.04f);
                okRT.anchorMax = new Vector2(0.75f, 0.18f);
                errorPopupOKButton.gameObject.SetActive(true);
                if (errorPopupCancelButton) errorPopupCancelButton.gameObject.SetActive(false);
            }
        }

        // Corner decorations
        SetAccentColor("CornerTR", darkAccent);
        SetAccentColor("CornerBL", darkAccent);

        // Grid lines
        SetAccentColor("GridLine1", accent * 0.3f);
        SetAccentColor("GridLine2", accent * 0.3f);
        SetAccentColor("GridLine3", accent * 0.3f);

        // Show popup
        errorPopupCanvasGroup.alpha = 1f;
        errorPopupCanvasGroup.interactable = true;
        errorPopupCanvasGroup.blocksRaycasts = true;

        // Replay Reach UI scale-in animation on the card, if present
        var errorCard = GameObject.Find("Canvas/ErrorPopup/ErrorCard");
        var reachPopup = errorCard != null
            ? errorCard.GetComponent<Michsky.UI.Reach.UIPopup>()
            : null;
        if (reachPopup != null) reachPopup.PlayIn();

        // Play error sound
        PlayErrorSound();

        Debug.Log($"[LoginUI] ErrorPopup: [{type}] [{mode}] {title} - {message}");
    }

    private void SetAccentColor(string childName, Color col)
    {
        var obj = GameObject.Find($"Canvas/ErrorPopup/ErrorCard/{childName}");
        if (obj == null) return;
        var img = obj.GetComponent<Image>();
        if (img) img.color = col;
    }

    public void HideErrorPopup()
    {
        if (errorPopupCanvasGroup)
        {
            errorPopupCanvasGroup.alpha = 0f;
            errorPopupCanvasGroup.interactable = false;
            errorPopupCanvasGroup.blocksRaycasts = false;
        }
        // Play close sound
        if (_errorAudioSource != null && errorCloseSoundClip != null)
        {
            _errorAudioSource.PlayOneShot(errorCloseSoundClip, errorCloseVolume);
        }
    }

    private void OnPopupOKClicked()
    {
        HideErrorPopup();
        // If there's a retry action (e.g. server reconnect), invoke it
        if (_onRetryAction != null)
        {
            _onRetryAction.Invoke();
            _onRetryAction = null;
        }
    }

    private void OnPopupCancelClicked()
    {
        // Dismiss only — never quit from a dialog button.
        _onRetryAction = null;
        HideErrorPopup();
        Debug.Log("[LoginUI] Popup dismissed via Cancel/Close");
    }
}
