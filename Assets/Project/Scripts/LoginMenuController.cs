using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires the login menu together:
/// - MoreButton opens the username/password AccountLoginPopup.
/// - The popup's CloseButton hides it again.
/// Auto-wires references by hierarchy path when left unassigned in the Inspector.
/// </summary>
public class LoginMenuController : MonoBehaviour
{
    [Header("Wiring (auto-found when left empty)")]
    [SerializeField] private Button moreButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup accountLoginPopup;

    private void Awake()
    {
        AutoWire();

        if (moreButton != null) moreButton.onClick.AddListener(ShowAccountLogin);
        if (closeButton != null) closeButton.onClick.AddListener(HideAccountLogin);

        // Start hidden (popup uses a CanvasGroup fade pattern).
        HideAccountLogin();
    }

    private void OnDestroy()
    {
        if (moreButton != null) moreButton.onClick.RemoveListener(ShowAccountLogin);
        if (closeButton != null) closeButton.onClick.RemoveListener(HideAccountLogin);
    }

    private void AutoWire()
    {
        // MoreButton is a sibling inside LoginPanel.
        if (moreButton == null)
            moreButton = transform.Find("MoreButton")?.GetComponent<Button>();

        // AccountLoginPopup lives directly under the Canvas.
        if (accountLoginPopup == null && transform.parent != null)
            accountLoginPopup = transform.parent.Find("AccountLoginPopup")?.GetComponent<CanvasGroup>();

        // CloseButton sits inside the popup card.
        if (closeButton == null && accountLoginPopup != null)
            closeButton = accountLoginPopup.transform
                .Find("AccountLoginCard/CloseButton")?.GetComponent<Button>();

        if (moreButton == null) Debug.LogWarning("[LoginMenuController] MoreButton not found.");
        if (accountLoginPopup == null) Debug.LogWarning("[LoginMenuController] AccountLoginPopup not found.");
        if (closeButton == null) Debug.LogWarning("[LoginMenuController] CloseButton not found.");
    }

    public void ShowAccountLogin()
    {
        if (accountLoginPopup == null) return;
        accountLoginPopup.gameObject.SetActive(true);
        accountLoginPopup.alpha = 1f;
        accountLoginPopup.interactable = true;
        accountLoginPopup.blocksRaycasts = true;
    }

    public void HideAccountLogin()
    {
        if (accountLoginPopup == null) return;
        accountLoginPopup.alpha = 0f;
        accountLoginPopup.interactable = false;
        accountLoginPopup.blocksRaycasts = false;
    }
}