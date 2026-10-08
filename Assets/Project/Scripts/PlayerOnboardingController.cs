using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI.Anim;

/// <summary>
/// First-time onboarding flow: nickname step -> experience level step.
/// Visual language matches the lobby and login menu (dark navy panels, golden amber
/// primary actions, gold accent stripes). All motion runs through the
/// Railbound.UI.Anim animation system (UITween / AnimatedButton / LevelCardAnimator / MenuAnimator).
/// </summary>
public sealed class PlayerOnboardingController : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.015f, 0.02f, 0.04f, 0.96f);
    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.09f, 0.98f);        // lobby darkPanel
    private static readonly Color GoldColor = new Color(1f, 0.75f, 0.2f, 1f);                 // golden amber primary
    private static readonly Color ButtonIdleColor = new Color(0.08f, 0.09f, 0.14f, 1f);
    private static readonly Color OnGoldTextColor = new Color(0.07f, 0.055f, 0.02f, 1f);      // dark text on gold fills
    private static readonly Color InputColor = new Color(0.015f, 0.02f, 0.04f, 1f);
    private static readonly Color MutedTextColor = new Color(0.6f, 0.65f, 0.7f, 1f);
    private static readonly Color PlaceholderColor = new Color(0.45f, 0.5f, 0.58f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.35f, 0.4f, 1f);
    private static readonly Color UnselectedDim = new Color(0.45f, 0.45f, 0.5f, 1f);
    private static readonly Color ShelfColor = new Color(0.025f, 0.025f, 0.038f, 1f);

    private const float CardWidth = 1100f;
    private const float CardHeight = 780f;
    private const float UsernameCardHeight = 420f;   // compact card for the nickname form
    private const float LevelStepHeight = 540f;
    private const float FrameThickness = 5f;
    private const float BadgeCoverPad = 17f; // hides the cut-off badge baked into the card artwork
    private const float TitleLetterSpacing = 4f;
    private const float StepLabelLetterSpacing = 7f;
    private const float ChamferCut = 14f;   // corner cut of Onboarding_Chamfer.png (9-slice border)

    [SerializeField] private bool showForIncompleteProfiles = true;
    [SerializeField] private Sprite inexpertArtwork;
    [SerializeField] private Sprite accomplishedArtwork;
    [SerializeField] private Sprite masterArtwork;
    [SerializeField] private Sprite chamferSprite;   // 9-sliced corner-cut chrome, e.g. Assets/Project/UI/Onboarding_Chamfer.png

    private CanvasGroup _overlay;
    private bool _overlayOpen;
    private RectTransform _cardRect;
    private TMP_Text _stepLabel;
    private TMP_Text _title;
    private static Sprite _chamfer;
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
        // A scene-baked overlay (saved during Play Mode) must never survive at runtime:
        // this component owns the overlay and rebuilds it with the current styling.
        DestroyStaleOverlay();

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

        if (chamferSprite != null)
            _chamfer = chamferSprite;

        DestroyStaleOverlay();

        var root = gameObject.transform as RectTransform;
        var overlayObject = new GameObject("PlayerOnboardingOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlayObject.transform.SetParent(root, false);
        var overlayRect = overlayObject.GetComponent<RectTransform>();
        Stretch(overlayRect);
        overlayObject.GetComponent<Image>().color = BackgroundColor;
        _overlay = overlayObject.GetComponent<CanvasGroup>();
        _overlay.alpha = 0f;   // MenuAnimator fades it in (instant in edit-mode previews)
        _overlayOpen = false;

        var card = CreatePanel(overlayObject.transform, "OnboardingCard", PanelColor, new Vector2(CardWidth, CardHeight));
        _cardRect = card.GetComponent<RectTransform>();
        ApplyChamfer(card.GetComponent<Image>());
        AddCardAccents(card);
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _stepLabel = CreateText(card.transform, "StepLabel", "STEP 1 OF 2", 12f, TextAlignmentOptions.Center, GoldColor);
        _stepLabel.characterSpacing = StepLabelLetterSpacing;
        SetPreferredHeight(_stepLabel.gameObject, 20f);
        _title = CreateText(card.transform, "Title", "WELCOME ABOARD", 30f, TextAlignmentOptions.Center, Color.white);
        _title.characterSpacing = TitleLetterSpacing;
        SetPreferredHeight(_title.gameObject, 44f);
        _subtitle = CreateText(card.transform, "Subtitle", "Set up your player profile before entering the lobby.", 16f, TextAlignmentOptions.Center, MutedTextColor);
        SetPreferredHeight(_subtitle.gameObject, 36f);

        _usernameStep = new GameObject("UsernameStep", typeof(RectTransform));
        _usernameStep.transform.SetParent(card.transform, false);
        var usernameLayout = _usernameStep.AddComponent<VerticalLayoutGroup>();
        usernameLayout.spacing = 18f;
        usernameLayout.padding = new RectOffset(10, 10, 10, 10);
        usernameLayout.childControlWidth = true;
        usernameLayout.childControlHeight = true;
        usernameLayout.childForceExpandWidth = true;
        usernameLayout.childForceExpandHeight = false;
        var stepLabel = CreateText(_usernameStep.transform, "Label", "CHOOSE YOUR NICKNAME", 14f, TextAlignmentOptions.Left, GoldColor);
        SetPreferredHeight(stepLabel.gameObject, 26f);
        _usernameInput = CreateInputField(_usernameStep.transform);
        _usernameInput.characterLimit = 16;
        _usernameInput.onValueChanged.AddListener(_ => SetError(string.Empty));
        _saveUsernameButton = CreateStyledButton(_usernameStep.transform, "SaveNicknameButton", "SAVE NICKNAME", GoldColor, Color.clear, OnGoldTextColor);
        _saveUsernameButton.onClick.AddListener(SaveUsername);

        _levelStep = new GameObject("LevelStep", typeof(RectTransform));
        _levelStep.transform.SetParent(card.transform, false);
        var levelStepLayout = _levelStep.AddComponent<LayoutElement>();
        levelStepLayout.minHeight = LevelStepHeight;
        levelStepLayout.preferredHeight = LevelStepHeight;

        CreateLevelCard(_levelStep.transform, "InexpertCard", inexpertArtwork, PlayerExperienceLevel.Inexpert,
            new Vector2(0.000f, 0.145f), new Vector2(0.3245f, 1f));
        CreateLevelCard(_levelStep.transform, "AccomplishedCard", accomplishedArtwork, PlayerExperienceLevel.Accomplished,
            new Vector2(0.3378f, 0.145f), new Vector2(0.6622f, 1f));
        CreateLevelCard(_levelStep.transform, "MasterCard", masterArtwork, PlayerExperienceLevel.Master,
            new Vector2(0.6755f, 0.145f), new Vector2(1f, 1f));

        _continueButton = CreateAnchoredButton(_levelStep.transform, "ContinueButton", "CONTINUE",
            new Vector2(0.515f, 0f), new Vector2(0.72f, 0.12f), GoldColor, Color.clear, OnGoldTextColor);
        _continueButton.onClick.AddListener(SaveLevel);
        _backButton = CreateAnchoredButton(_levelStep.transform, "BackButton", "BACK",
            new Vector2(0.28f, 0f), new Vector2(0.485f, 0.12f), ButtonIdleColor, GoldColor, Color.white);
        _backButton.onClick.AddListener(ShowUsernameStep);

        _error = CreateText(card.transform, "Error", string.Empty, 15f, TextAlignmentOptions.Center, ErrorColor);
        SetPreferredHeight(_error.gameObject, 36f);

        // Layout must be resolved before aligning frames/shelves to the letterboxed artwork.
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        FinalizeLevelCards();

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
        if (_cardRect != null)
            _cardRect.sizeDelta = new Vector2(CardWidth, UsernameCardHeight + 40f);
        _stepLabel.gameObject.SetActive(true);
        _title.gameObject.SetActive(true);
        _subtitle.gameObject.SetActive(true);
        _stepLabel.text = "STEP 1 OF 2";
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
        if (_cardRect != null)
            _cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
        _stepLabel.gameObject.SetActive(true);
        _title.gameObject.SetActive(true);
        _subtitle.gameObject.SetActive(true);
        _stepLabel.text = "STEP 2 OF 2";
        _title.text = "SELECT EXPERIENCE LEVEL";
        _subtitle.text = "Choose how hard the ride should be - you can change it later.";
        // The artwork ships with Inexpert highlighted, so start with it pre-selected;
        // an explicit earlier choice (BACK navigation) is preserved.
        if (!_levelSelected && _levelCards.Count > 0 && _levelCards[0].root != null)
        {
            _selectedLevel = PlayerExperienceLevel.Inexpert;
            _levelSelected = true;
            RefreshLevelCardSelection(_levelCards[0].root);
        }
        ShowOverlay();   // opens on first run, quick step-swap pop when already visible
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
        SetStatus("Checking nickname availability...");
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
        SetStatus("Saving profile...");
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
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Transparent hit area - the artwork renders in a clipped child so it can zoom on hover.
        var hitArea = root.GetComponent<Image>();
        hitArea.sprite = null;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var clip = new GameObject("ArtClip", typeof(RectTransform), typeof(RectMask2D));
        clip.transform.SetParent(root.transform, false);
        Stretch(clip.GetComponent<RectTransform>());

        var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artObject.transform.SetParent(clip.transform, false);
        Stretch(artObject.GetComponent<RectTransform>());
        var art = artObject.GetComponent<Image>();
        art.sprite = artwork;
        art.preserveAspect = true;
        art.color = Color.white;
        art.raycastTarget = false;

        var button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.None;   // LevelCardAnimator owns the visuals
        button.targetGraphic = hitArea;

        // Gold selection frame on the artwork bounds - strips are toggled by LevelCardAnimator.
        var frame = new GameObject("SelectionFrame", typeof(RectTransform));
        frame.transform.SetParent(root.transform, false);
        Stretch(frame.GetComponent<RectTransform>());
        CreateFrameStrip(frame.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -FrameThickness), Vector2.zero);
        CreateFrameStrip(frame.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, FrameThickness));
        CreateFrameStrip(frame.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(FrameThickness, 0f));
        CreateFrameStrip(frame.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-FrameThickness, 0f), Vector2.zero);

        button.onClick.AddListener(() =>
        {
            if (!IsAlive)
                return;

            _selectedLevel = level;
            _levelSelected = true;
            SetError(string.Empty);
            RefreshLevelCardSelection(root);
        });
        _levelCards.Add(new SelectableLevelCard { root = root, frame = frame, art = art, level = level });

        // Hover / selected / checkmark / ambient animations.
        var animator = root.AddComponent<LevelCardAnimator>();
        animator.Initialize(art, frame, UnselectedDim);
    }

    private static Button CreateAnchoredButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color background,
        Color stripeColor,
        Color textColor)
    {
        var button = CreateStyledButton(parent, name, label, background, stripeColor, textColor);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    /// <summary>Corner-cut battle-royale button; optional menu-style accent stripe (gold).</summary>
    private static Button CreateStyledButton(Transform parent, string name, string label, Color background, Color stripeColor, Color textColor)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.minHeight = 54f;
        rootLayout.preferredHeight = 54f;
        var rootImage = root.GetComponent<Image>();
        rootImage.color = background;
        ApplyChamfer(rootImage);

        var button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f),
            pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        if (stripeColor.a > 0f)
        {
            var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(root.transform, false);
            var stripeRect = stripe.GetComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0f, 0f);
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.offsetMin = Vector2.zero;
            stripeRect.offsetMax = new Vector2(4f, 0f);
            var stripeImage = stripe.GetComponent<Image>();
            stripeImage.color = stripeColor;
            stripeImage.raycastTarget = false;
        }

        var text = CreateText(root.transform, "Label", label, 16f, TextAlignmentOptions.Center, textColor);
        Stretch(text.rectTransform, 14f, 10f, 0f, 0f);

        // Hover/click/press animation (soft gold glow halo + scale + brightness).
        root.AddComponent<AnimatedButton>();
        return button;
    }

    private static void CreateFrameStrip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var strip = new GameObject(name, typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(parent, false);
        var rect = strip.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = strip.GetComponent<Image>();
        image.color = GoldColor;
        image.raycastTarget = false;
    }

    private static void AddCardAccents(GameObject card)
    {
        // Top and left gold accent rails strictly bounded inside the card container.
        var top = new GameObject("Accent_Top", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        top.transform.SetParent(card.transform, false);
        top.GetComponent<LayoutElement>().ignoreLayout = true;
        var topRect = top.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(0f, 4f);
        var topImage = top.GetComponent<Image>();
        topImage.color = GoldColor;
        topImage.raycastTarget = false;

        var left = new GameObject("Accent_Left", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        left.transform.SetParent(card.transform, false);
        left.GetComponent<LayoutElement>().ignoreLayout = true;
        var leftRect = left.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0f, 1f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.anchoredPosition = Vector2.zero;
        leftRect.sizeDelta = new Vector2(4f, 0f);
        var leftImage = left.GetComponent<Image>();
        leftImage.color = GoldColor;
        leftImage.raycastTarget = false;
    }

    /// <summary>
    /// Aligns each card's selection frame to the letterboxed artwork bounds (preserveAspect)
    /// and adds a bottom shelf that covers the badge artifact cropped into the artwork.
    /// Requires a resolved layout (ForceRebuildLayoutImmediate) before being called.
    /// </summary>
    private void FinalizeLevelCards()
    {
        for (var i = 0; i < _levelCards.Count; i++)
        {
            var cardRoot = _levelCards[i].root;
            if (cardRoot == null)
                continue;

            var rect = cardRoot.GetComponent<RectTransform>();
            var art = _levelCards[i].art;
            var sprite = art != null ? art.sprite : null;
            if (rect == null || sprite == null)
                continue;

            var cardWidth = rect.rect.width;
            var cardHeight = rect.rect.height;
            if (cardWidth <= 0f || cardHeight <= 0f)
                continue;

            // Same fit math as Image.preserveAspect.
            var fitScale = Mathf.Min(cardWidth / sprite.rect.width, cardHeight / sprite.rect.height);
            var insetX = (cardWidth - sprite.rect.width * fitScale) * 0.5f;
            var insetY = (cardHeight - sprite.rect.height * fitScale) * 0.5f;

            var frame = _levelCards[i].frame;
            if (frame != null)
            {
                var frameRect = frame.GetComponent<RectTransform>();
                frameRect.anchorMin = new Vector2(insetX / cardWidth, insetY / cardHeight);
                frameRect.anchorMax = new Vector2(1f - insetX / cardWidth, 1f - insetY / cardHeight);
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
            }

            // Bottom shelf: covers the half-cropped selection badge in the artwork and
            // extends over the letterbox band so the card reads as having a solid base.
            var shelf = new GameObject("BottomShelf", typeof(RectTransform), typeof(Image));
            shelf.transform.SetParent(cardRoot.transform, false);
            shelf.transform.SetSiblingIndex(0);
            var shelfRect = shelf.GetComponent<RectTransform>();
            shelfRect.anchorMin = new Vector2(0f, 0f);
            shelfRect.anchorMax = new Vector2(1f, 0f);
            shelfRect.offsetMin = Vector2.zero;
            shelfRect.offsetMax = new Vector2(0f, insetY + BadgeCoverPad);
            var shelfImage = shelf.GetComponent<Image>();
            shelfImage.color = ShelfColor;
            shelfImage.raycastTarget = false;
            // Above the artwork (ArtClip), below the selection frame strips.
            shelf.transform.SetSiblingIndex(1);
        }
    }

    private void RefreshLevelCardSelection(GameObject selectedRoot)
    {
        for (var i = 0; i < _levelCards.Count; i++)
        {
            if (_levelCards[i].root == null)
                continue;

            var selected = _levelCards[i].root == selectedRoot;
            var animator = _levelCards[i].root.GetComponent<LevelCardAnimator>();
            if (animator != null)
            {
                // Animator drives frame strips, checkmark pop, ring glow, dimming and scale.
                animator.SetSelected(selected);
                continue;
            }

            // Fallback (animator missing): previous static behavior.
            if (_levelCards[i].frame != null && _levelCards[i].frame.transform.parent != null)
                _levelCards[i].frame.SetActive(selected);
            var image = _levelCards[i].art;
            if (image != null)
                image.color = selected ? Color.white : UnselectedDim;
        }
    }

    [Serializable]
    private sealed class SelectableLevelCard
    {
        public GameObject root;
        public GameObject frame;
        public Image art;
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
        SetButtonBusy(_saveUsernameButton, busy);
        SetButtonBusy(_continueButton, busy);
        SetButtonBusy(_backButton, busy);
    }

    /// <summary>Disables a button and shows the Loading state (spinning ring, Linear loop).</summary>
    private static void SetButtonBusy(Button button, bool busy)
    {
        if (button == null)
            return;
        button.interactable = !busy;
        var animated = button.GetComponent<AnimatedButton>();
        if (animated != null)
            animated.SetLoading(busy);
    }

    private void ApplyProfileToLobby(PlayerProfile profile)
    {
        if (!IsAlive)
            return;

        var lobbyController = GetComponent<Railbound.UI.LobbyUIController>();
        if (lobbyController != null && profile != null)
            lobbyController.SetPlayerInfo(0, profile.username);
    }

    private void SetError(string message) => SetMessage(message, true);

    private void SetStatus(string message) => SetMessage(message, false);

    private void SetMessage(string message, bool isError)
    {
        if (!IsAlive)
            return;

        if (_error != null)
        {
            _error.text = message ?? string.Empty;
            _error.color = isError ? ErrorColor : GoldColor;
        }
    }

    private void ShowOverlay()
    {
        if (_overlay == null)
            return;

        if (_overlayOpen)
        {
            // Already visible: quick pop for the step swap (no full re-open).
            MenuAnimator.SwapStep(_cardRect);
            return;
        }

        _overlayOpen = true;
        MenuAnimator.Open(_overlay, _cardRect);
    }

    private void HideOverlay()
    {
        if (_overlay == null || !_overlayOpen)
            return;

        _overlayOpen = false;
        MenuAnimator.Close(_overlay, _cardRect);
    }

    private void DestroyStaleOverlay()
    {
        var existing = transform.Find("PlayerOnboardingOverlay");
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    /// <summary>
    /// Rebuilds the overlay from scratch and shows the requested step.
    /// Used by tooling to refresh the scene-level preview after script changes.
    /// </summary>
    public void RebuildOverlayPreview(bool showLevelStep)
    {
        DestroyStaleOverlay();
        _overlay = null;
        _overlayOpen = false;
        _cardRect = null;
        _usernameStep = null;
        _levelStep = null;
        _title = null;
        _stepLabel = null;
        _subtitle = null;
        _error = null;
        _usernameInput = null;
        _saveUsernameButton = null;
        _backButton = null;
        _continueButton = null;
        _levelCards.Clear();
        _selectedLevel = PlayerExperienceLevel.Inexpert;
        _levelSelected = false;
        _busy = false;

        BuildOverlay();
        if (!IsAlive || _overlay == null)
            return;

        if (showLevelStep)
            ShowLevelStep();
        else
            ShowUsernameStep();
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
        label.raycastTarget = false;
        return label;
    }

    private static TMP_InputField CreateInputField(Transform parent)
    {
        var root = new GameObject("NicknameInput", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.minHeight = 56f;
        rootLayout.preferredHeight = 56f;
        var rootImage = root.GetComponent<Image>();
        ApplyChamfer(rootImage);
        rootImage.color = InputColor;

        // Gold menu-style rail on the left edge of the field (inset past the chamfer).
        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(root.transform, false);
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.offsetMin = new Vector2(0f, ChamferCut);
        accentRect.offsetMax = new Vector2(3f, -ChamferCut);
        var accentImage = accent.GetComponent<Image>();
        accentImage.color = GoldColor;
        accentImage.raycastTarget = false;

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObject.transform.SetParent(root.transform, false);
        var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
        placeholder.text = "Enter your nickname";
        placeholder.fontSize = 18f;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = PlaceholderColor;
        placeholder.enableWordWrapping = true;
        placeholder.raycastTarget = false;
        Stretch(placeholderObject.GetComponent<RectTransform>(), 18f, 18f, 0f, 0f);

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        Stretch(textObject.GetComponent<RectTransform>(), 18f, 18f, 0f, 0f);

        var input = root.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = root.GetComponent<Image>();
        input.selectionColor = new Color(0.12f, 0.86f, 1f, 0.35f);
        return input;
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

    /// <summary>Applies the 9-sliced corner-cut chrome sprite (battle-royale angular look).</summary>
    private static void ApplyChamfer(Image image)
    {
        if (image == null)
            return;
        var sprite = ResolveChamferSprite();
        if (sprite == null)
            return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    private static Sprite ResolveChamferSprite()
    {
        if (_chamfer != null)
            return _chamfer;
#if UNITY_EDITOR
        _chamfer = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/Onboarding_Chamfer.png");
#endif
        return _chamfer;
    }
}
