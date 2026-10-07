using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerOnboardingController : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.015f, 0.02f, 0.04f, 0.96f);
    private static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.12f, 0.98f);
    private static readonly Color AccentColor = new Color(0.12f, 0.86f, 1f, 1f);
    private static readonly Color SelectedColor = new Color(0.9f, 0.17f, 0.55f, 1f);

    [SerializeField] private bool showForIncompleteProfiles = true;
    [SerializeField] private Sprite inexpertArtwork;
    [SerializeField] private Sprite accomplishedArtwork;
    [SerializeField] private Sprite masterArtwork;

    private CanvasGroup _overlay;
    private TMP_Text _title;
    private TMP_Text _subtitle;
    private TMP_Text _error;
    private TMP_InputField _usernameInput;
    private Button _saveUsernameButton;
    private Button _backButton;
    private Button _continueButton;
    private GameObject _usernameStep;
    private GameObject _levelStep;
    private readonly List<SelectableLevelCard> _levelCards = new List<SelectableLevelCard>();
    private PlayerExperienceLevel _selectedLevel;
    private bool _levelSelected;
    private bool _busy;
    private bool _isDestroyed;

    private async void Start()
    {
        if (!showForIncompleteProfiles)
            return;

        try
        {
            var authentication = AuthenticationService.Instance;
            if (authentication == null)
                return;

            var waitFrames = 0;
            while (!authentication.IsAuthenticationReady && waitFrames++ < 480)
            {
                await Task.Yield();
                if (!IsAlive)
                    return;
            }

            if (!IsAlive || !authentication.IsAuthenticationReady || !authentication.IsAuthenticated)
                return;

            var profile = await PlayerProfileService.LoadAsync();
            if (!IsAlive)
                return;

            if (profile == null)
                ShowUsernameStep();
            else
            {
                ApplyProfileToLobby(profile);
                HideOverlay();
            }
        }
        catch (Exception exception)
        {
            if (!IsAlive)
                return;

            ShowUsernameStep();
            SetError("We could not load your profile. Check your connection and try again.");
            Debug.LogError($"[PlayerOnboardingController] Profile load failed: {exception}");
        }
    }

    private void OnDestroy()
    {
        _isDestroyed = true;

        if (_saveUsernameButton != null)
            _saveUsernameButton.onClick.RemoveListener(SaveUsername);
        if (_continueButton != null)
            _continueButton.onClick.RemoveListener(SaveLevel);
        if (_backButton != null)
            _backButton.onClick.RemoveListener(ShowUsernameStep);
    }

    private bool IsAlive => !_isDestroyed && this != null && gameObject != null;

    private void BuildOverlay()
    {
        if (!IsAlive)
            return;

        if (_overlay != null)
            return;

        var root = gameObject.transform as RectTransform;
        var overlayObject = new GameObject("PlayerOnboardingOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlayObject.transform.SetParent(root, false);
        var overlayRect = overlayObject.GetComponent<RectTransform>();
        Stretch(overlayRect);
        overlayObject.GetComponent<Image>().color = BackgroundColor;
        _overlay = overlayObject.GetComponent<CanvasGroup>();

        var card = CreatePanel(overlayObject.transform, "OnboardingCard", PanelColor, new Vector2(1100f, 780f));
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;

        _title = CreateText(card.transform, "Title", "WELCOME ABOARD", 32f, TextAlignmentOptions.Center, AccentColor);
        SetPreferredHeight(_title.gameObject, 48f);
        _subtitle = CreateText(card.transform, "Subtitle", "Set up your player profile before entering the lobby.", 18f, TextAlignmentOptions.Center, Color.white);
        SetPreferredHeight(_subtitle.gameObject, 52f);

        _usernameStep = new GameObject("UsernameStep", typeof(RectTransform));
        _usernameStep.transform.SetParent(card.transform, false);
        var usernameLayout = _usernameStep.AddComponent<VerticalLayoutGroup>();
        usernameLayout.spacing = 12f;
        usernameLayout.childControlWidth = true;
        usernameLayout.childControlHeight = false;
        usernameLayout.childForceExpandWidth = true;
        CreateText(_usernameStep.transform, "Label", "CHOOSE YOUR NICKNAME", 16f, TextAlignmentOptions.Left, AccentColor);
        _usernameInput = CreateInputField(_usernameStep.transform);
        _usernameInput.characterLimit = 16;
        _usernameInput.onValueChanged.AddListener(_ => SetError(string.Empty));
        _saveUsernameButton = CreateButton(_usernameStep.transform, "SAVE NICKNAME", SaveUsername);

        _levelStep = new GameObject("LevelStep", typeof(RectTransform));
        _levelStep.transform.SetParent(card.transform, false);
        var levelStepLayout = _levelStep.AddComponent<LayoutElement>();
        levelStepLayout.minHeight = 590f;
        levelStepLayout.preferredHeight = 590f;

        CreateLevelCard(_levelStep.transform, "InexpertCard", inexpertArtwork, PlayerExperienceLevel.Inexpert,
            new Vector2(0.02f, 0.20f), new Vector2(0.32f, 0.94f));
        CreateLevelCard(_levelStep.transform, "AccomplishedCard", accomplishedArtwork, PlayerExperienceLevel.Accomplished,
            new Vector2(0.35f, 0.20f), new Vector2(0.65f, 0.94f));
        CreateLevelCard(_levelStep.transform, "MasterCard", masterArtwork, PlayerExperienceLevel.Master,
            new Vector2(0.68f, 0.20f), new Vector2(0.98f, 0.94f));

        _continueButton = CreateAnchoredButton(_levelStep.transform, "CONTINUE",
            new Vector2(0.38f, 0.01f), new Vector2(0.62f, 0.11f), SaveLevel);
        _backButton = CreateAnchoredButton(_levelStep.transform, "BACK",
            new Vector2(0.43f, 0.12f), new Vector2(0.57f, 0.20f), ShowUsernameStep);

        _error = CreateText(card.transform, "Error", string.Empty, 15f, TextAlignmentOptions.Center, new Color(1f, 0.35f, 0.4f));
        SetPreferredHeight(_error.gameObject, 42f);
        _levelStep.SetActive(false);
    }

    private void ShowUsernameStep()
    {
        if (!IsAlive)
            return;

        BuildOverlay();
        if (!IsAlive || _usernameStep == null || _levelStep == null)
            return;

        _usernameStep.SetActive(true);
        _levelStep.SetActive(false);
        _title.gameObject.SetActive(true);
        _subtitle.gameObject.SetActive(true);
        _title.text = "WELCOME ABOARD";
        _subtitle.text = "Choose a unique nickname for the train.";
        _usernameInput.Select();
        SetBusy(false);
        ShowOverlay();
    }

    private void ShowLevelStep()
    {
        if (!IsAlive)
            return;

        _usernameStep.SetActive(false);
        _levelStep.SetActive(true);
        _title.gameObject.SetActive(false);
        _subtitle.gameObject.SetActive(false);
        SetError(string.Empty);
        SetBusy(false);
    }

    private async void SaveUsername()
    {
        if (_busy)
            return;

        var username = _usernameInput.text.Trim();
        var validationError = ValidateUsername(username);
        if (validationError != null)
        {
            SetError(validationError);
            return;
        }

        SetBusy(true);
        SetError("Checking nickname availability...");
        try
        {
            await PlayerProfileService.SaveAsync(username, PlayerExperienceLevel.Inexpert, false);
            if (!IsAlive)
                return;

            ApplyProfileToLobby(new PlayerProfile { username = username, experienceLevel = PlayerExperienceLevel.Inexpert });
            ShowLevelStep();
        }
        catch (Exception exception)
        {
            if (!IsAlive)
                return;

            SetBusy(false);
            SetError(GetFriendlySaveError(exception));
            Debug.LogWarning($"[PlayerOnboardingController] Username save failed: {exception.Message}");
        }
    }

    private async void SaveLevel()
    {
        if (_busy)
            return;
        if (!_levelSelected)
        {
            SetError("Select a level to continue.");
            return;
        }

        SetBusy(true);
        SetError("Saving profile...");
        try
        {
            await PlayerProfileService.SaveAsync(_usernameInput.text.Trim(), _selectedLevel);
            if (!IsAlive)
                return;

            ApplyProfileToLobby(new PlayerProfile
            {
                username = _usernameInput.text.Trim(),
                experienceLevel = _selectedLevel
            });
            HideOverlay();
        }
        catch (Exception exception)
        {
            if (!IsAlive)
                return;

            SetBusy(false);
            SetError("Profile could not be saved. Check your connection and try again.");
            Debug.LogWarning($"[PlayerOnboardingController] Profile save failed: {exception.Message}");
        }
    }

    private void CreateLevelCard(
        Transform parent,
        string name,
        Sprite artwork,
        PlayerExperienceLevel level,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = root.GetComponent<Image>();
        image.sprite = artwork;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = true;

        var button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 1f, 1f, 0.95f),
            pressedColor = new Color(1f, 1f, 1f, 0.8f),
            selectedColor = Color.white,
            disabledColor = Color.gray,
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        button.onClick.AddListener(() =>
        {
            if (!IsAlive)
                return;

            _selectedLevel = level;
            _levelSelected = true;
            SetError(string.Empty);
            RefreshLevelCardSelection(root);
        });
        var outline = root.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.75f, 0.1f, 1f);
        outline.effectDistance = new Vector2(4f, 4f);
        outline.enabled = false;
        _levelCards.Add(new SelectableLevelCard { root = root, level = level });
    }

    private static Button CreateAnchoredButton(
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction action)
    {
        var root = new GameObject(label.Replace(" ", string.Empty), typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.2f, 1f);
        var button = root.GetComponent<Button>();
        button.onClick.AddListener(action);
        var text = CreateText(root.transform, "Label", label, 16f, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private void RefreshLevelCardSelection(GameObject selectedRoot)
    {
        for (var i = 0; i < _levelCards.Count; i++)
        {
            if (_levelCards[i].root == null)
                continue;

            var outline = _levelCards[i].root.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = _levelCards[i].root == selectedRoot;
        }
    }

    [Serializable]
    private sealed class SelectableLevelCard
    {
        public GameObject root;
        public PlayerExperienceLevel level;
    }

    private static string ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Enter a nickname.";
        if (username.Length < 3 || username.Length > 16)
            return "Nickname must be 3–16 characters.";
        if (!char.IsLetter(username[0]))
            return "Nickname must start with a letter.";
        for (var i = 0; i < username.Length; i++)
        {
            if (!char.IsLetterOrDigit(username[i]) && username[i] != '_')
                return "Use letters, numbers, or underscores only.";
        }
        return null;
    }

    private static string GetFriendlySaveError(Exception exception)
    {
        var message = exception.Message ?? string.Empty;
        if (message.IndexOf("username", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (message.IndexOf("taken", StringComparison.OrdinalIgnoreCase) >= 0 ||
             message.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0 ||
             message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0))
            return "That nickname is already taken.";
        return "Nickname could not be saved. Check your connection and try again.";
    }

    private void SetBusy(bool busy)
    {
        if (!IsAlive)
            return;

        _busy = busy;
        if (_saveUsernameButton != null) _saveUsernameButton.interactable = !busy;
        if (_continueButton != null) _continueButton.interactable = !busy;
        if (_backButton != null) _backButton.interactable = !busy;
    }

    private void ApplyProfileToLobby(PlayerProfile profile)
    {
        if (!IsAlive)
            return;

        var lobbyController = GetComponent<Railbound.UI.LobbyUIController>();
        if (lobbyController != null && profile != null)
            lobbyController.SetPlayerInfo(0, profile.username);
    }

    private void SetError(string message)
    {
        if (!IsAlive)
            return;

        if (_error != null)
            _error.text = message ?? string.Empty;
    }

    private void ShowOverlay()
    {
        _overlay.alpha = 1f;
        _overlay.interactable = true;
        _overlay.blocksRaycasts = true;
    }

    private void HideOverlay()
    {
        if (_overlay == null)
            return;
        _overlay.alpha = 0f;
        _overlay.interactable = false;
        _overlay.blocksRaycasts = false;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 size)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var objectRoot = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        objectRoot.transform.SetParent(parent, false);
        var label = objectRoot.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        return label;
    }

    private static TMP_InputField CreateInputField(Transform parent)
    {
        var root = CreatePanel(parent, "NicknameInput", new Color(0.015f, 0.02f, 0.04f, 1f), new Vector2(0f, 56f));
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(textObject.GetComponent<RectTransform>(), 18f, 18f, 0f, 0f);
        var input = root.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.targetGraphic = root.GetComponent<Image>();
        return input;
    }

    private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var root = CreatePanel(parent, label.Replace(" ", string.Empty), new Color(0.1f, 0.12f, 0.2f, 1f), new Vector2(0f, 54f));
        var button = root.AddComponent<Button>();
        button.onClick.AddListener(action);
        var text = CreateText(root.transform, "Label", label, 16f, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private static void SetPreferredHeight(GameObject objectRoot, float height)
    {
        var layout = objectRoot.GetComponent<LayoutElement>();
        if (layout == null) layout = objectRoot.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
