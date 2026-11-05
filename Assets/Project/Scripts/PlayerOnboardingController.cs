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
public sealed partial class PlayerOnboardingController : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.015f, 0.02f, 0.04f, 0.94f);
    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.09f, 0.98f);        // lobby darkPanel
    private static readonly Color GoldColor = new Color(1f, 0.75f, 0.2f, 1f);                 // golden amber primary
    private static readonly Color ButtonIdleColor = new Color(0.09f, 0.12f, 0.18f, 1f);
    private static readonly Color OnGoldTextColor = new Color(0.07f, 0.055f, 0.02f, 1f);      // dark text on gold fills
    private static readonly Color InputColor = new Color(0.02f, 0.03f, 0.06f, 0.98f);
    private static readonly Color MutedTextColor = new Color(0.6f, 0.65f, 0.7f, 1f);
    private static readonly Color PlaceholderColor = new Color(0.42f, 0.48f, 0.56f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.35f, 0.4f, 1f);
    private static readonly Color UnselectedDim = new Color(0.6f, 0.65f, 0.72f, 1f);

    private const float UsernameCardWidth = 1100f;
    private const float UsernameCardHeight = 720f;
    private const float LevelCardWidth = 1100f;
    private const float LevelCardHeight = 720f;
    private const float LevelStepHeight = 510f;
    private const float FrameThickness = 4f;
    private const float TitleLetterSpacing = 3f;
    private const float StepLabelLetterSpacing = 5f;
    private const float ChamferCut = 14f;

    [SerializeField] private bool showForIncompleteProfiles = true;
    [SerializeField] private Sprite inexpertArtwork;
    [SerializeField] private Sprite accomplishedArtwork;
    [SerializeField] private Sprite masterArtwork;
    [SerializeField] private Sprite chamferSprite;

    private CanvasGroup _overlay;
    private bool _overlayOpen;
    private RectTransform _cardRect;
    private TMP_Text _stepLabel;
    private TMP_Text _title;
    private static Sprite _chamfer;
    private static Sprite _ring;
    private static TMP_FontAsset _boldFont;
    private static TMP_FontAsset _medFont;
    private TMP_Text _subtitle;
    private TMP_Text _error;
    private TMP_InputField _usernameInput;
    private Button _saveUsernameButton;
    private Button _backButton;
    private Button _continueButton;
    private GameObject _usernameStep;
    private GameObject _levelStep;
    private InteractiveDossierAnimator _dossierAnimator;
    private RectTransform _randomizeIconRect;
    private readonly object _diceSpinToken = new object();
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
        _overlay.alpha = 0f;
        _overlayOpen = false;

        var card = CreatePanel(overlayObject.transform, "OnboardingCard", PanelColor, new Vector2(LevelCardWidth, LevelCardHeight));
        _cardRect = card.GetComponent<RectTransform>();
        ApplyChamfer(card.GetComponent<Image>());

        // 4-sided tactical frame pinned directly to the card
        var cardBorder = new GameObject("Frame_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardBorder.transform.SetParent(card.transform, false);
        Stretch(cardBorder.GetComponent<RectTransform>());
        var borderLayout = cardBorder.GetComponent<LayoutElement>();
        borderLayout.ignoreLayout = true;
        var borderImg = cardBorder.GetComponent<Image>();
        borderImg.sprite = ResolveRingSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.24f, 0.32f, 0.44f, 0.55f);
        borderImg.raycastTarget = false;

        // Gold top accent rail pinned directly to top edge of card
        var cardTopAccent = new GameObject("Accent_Top", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardTopAccent.transform.SetParent(card.transform, false);
        var taRt = cardTopAccent.GetComponent<RectTransform>();
        taRt.anchorMin = new Vector2(0f, 1f);
        taRt.anchorMax = new Vector2(1f, 1f);
        taRt.pivot = new Vector2(0.5f, 1f);
        taRt.anchoredPosition = Vector2.zero;
        taRt.sizeDelta = new Vector2(-28f, 3f);
        var taLayout = cardTopAccent.GetComponent<LayoutElement>();
        taLayout.ignoreLayout = true;
        var taImg = cardTopAccent.GetComponent<Image>();
        taImg.color = GoldColor;
        taImg.raycastTarget = false;

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 28, 24);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _stepLabel = CreateText(card.transform, "StepLabel", "STEP 01 OF 02 // IDENTITY", 13f, TextAlignmentOptions.Center, GoldColor, isBold: true);
        _stepLabel.characterSpacing = StepLabelLetterSpacing;
        SetPreferredHeight(_stepLabel.gameObject, 22f);

        _title = CreateText(card.transform, "Title", "WELCOME ABOARD", 28f, TextAlignmentOptions.Center, Color.white, isBold: true);
        _title.characterSpacing = TitleLetterSpacing;
        SetPreferredHeight(_title.gameObject, 40f);

        _subtitle = CreateText(card.transform, "Subtitle", "Set up your player profile before entering the lobby.", 15f, TextAlignmentOptions.Center, MutedTextColor, isBold: false);
        SetPreferredHeight(_subtitle.gameObject, 28f);

        // Step 1: Nickname Input Step (2-Column Interactive Dossier + Form)
        _usernameStep = new GameObject("UsernameStep", typeof(RectTransform));
        _usernameStep.transform.SetParent(card.transform, false);
        var usernameStepLayout = _usernameStep.AddComponent<LayoutElement>();
        usernameStepLayout.minHeight = LevelStepHeight;
        usernameStepLayout.preferredHeight = LevelStepHeight;

        CreateOperatorDossierCard(_usernameStep.transform);
        CreateRegistrationForm(_usernameStep.transform);

        // Step 2: Experience Level Selection Step
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

        _continueButton = CreateAnchoredButton(_levelStep.transform, "ContinueButton", "CONTINUE  ►►",
            new Vector2(0.515f, 0f), new Vector2(0.72f, 0.12f), GoldColor, Color.clear, OnGoldTextColor, isPrimary: true);
        _continueButton.onClick.AddListener(SaveLevel);

        _backButton = CreateAnchoredButton(_levelStep.transform, "BackButton", "◄  BACK",
            new Vector2(0.28f, 0f), new Vector2(0.485f, 0.12f), ButtonIdleColor, Color.clear, Color.white, isPrimary: false);
        _backButton.onClick.AddListener(ShowUsernameStep);

        _error = CreateText(card.transform, "Error", string.Empty, 14f, TextAlignmentOptions.Center, ErrorColor, isBold: false);
        SetPreferredHeight(_error.gameObject, 26f);

        // Layout must be resolved before aligning frames to the letterboxed artwork.
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
        {
            _cardRect.sizeDelta = new Vector2(UsernameCardWidth, UsernameCardHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRect);
        }

        _stepLabel.gameObject.SetActive(true);
        _title.gameObject.SetActive(true);
        _subtitle.gameObject.SetActive(true);
        _stepLabel.text = "STEP 01 OF 02 // IDENTITY";
        _title.text = "OPERATOR REGISTRATION";
        _subtitle.text = "Enter your tactical callsign for convoy operations and combat records.";
        _usernameInput.Select();
        if (_dossierAnimator != null)
            _dossierAnimator.SetCallsign(_usernameInput.text);
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
        {
            _cardRect.sizeDelta = new Vector2(LevelCardWidth, LevelCardHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRect);
        }

        _stepLabel.gameObject.SetActive(true);
        _title.gameObject.SetActive(true);
        _subtitle.gameObject.SetActive(true);
        _stepLabel.text = "STEP 02 OF 02 // DIFFICULTY";
        _title.text = "SELECT EXPERIENCE LEVEL";
        _subtitle.text = "Choose your combat tier - matchmaking will calibrate to your skill.";

        if (!_levelSelected && _levelCards.Count > 0 && _levelCards[0].root != null)
        {
            _selectedLevel = PlayerExperienceLevel.Inexpert;
            _levelSelected = true;
            RefreshLevelCardSelection(_levelCards[0].root);
        }
        ShowOverlay();
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

        SetBusy(true, isNicknameStep: true);
        SetStatus("CHECKING CALLSIGN AVAILABILITY...");
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

        SetBusy(true, isNicknameStep: false);
        SetStatus("SAVING OPERATOR PROFILE...");
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

        // Transparent hit area
        var hitArea = root.GetComponent<Image>();
        hitArea.sprite = null;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var clip = new GameObject("ArtClip", typeof(RectTransform), typeof(Image), typeof(Mask));
        clip.transform.SetParent(root.transform, false);
        Stretch(clip.GetComponent<RectTransform>());
        var clipImage = clip.GetComponent<Image>();
        clipImage.sprite = ResolveChamferSprite();
        clipImage.type = Image.Type.Sliced;
        var mask = clip.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artObject.transform.SetParent(clip.transform, false);
        Stretch(artObject.GetComponent<RectTransform>());
        var art = artObject.GetComponent<Image>();
        art.sprite = artwork;
        art.preserveAspect = true;
        art.color = UnselectedDim;
        art.raycastTarget = false;

        var button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitArea;

        // Selection Frame: container for the 9-sliced chamfer border and hex badge
        var frame = new GameObject("SelectionFrame", typeof(RectTransform));
        frame.transform.SetParent(root.transform, false);
        Stretch(frame.GetComponent<RectTransform>());

        var borderObj = new GameObject("ChamferBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(frame.transform, false);
        Stretch(borderObj.GetComponent<RectTransform>());
        var borderImage = borderObj.GetComponent<Image>();
        borderImage.sprite = ResolveRingSprite();
        borderImage.type = Image.Type.Sliced;
        borderImage.color = new Color(0.24f, 0.32f, 0.44f, 0.5f);
        borderImage.raycastTarget = false;

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

        // Hover / selected animations (crisp frame + checkmark + lift)
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
        Color textColor,
        bool isPrimary = false)
    {
        var button = CreateStyledButton(parent, name, label, background, stripeColor, textColor, isPrimary);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    /// <summary>Tactical chamfered Railbound button matching the lobby design language.</summary>
    private static Button CreateStyledButton(
        Transform parent,
        string name,
        string label,
        Color background,
        Color stripeColor,
        Color textColor,
        bool isPrimary = false)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.minHeight = 54f;
        rootLayout.preferredHeight = 54f;
        var rootImage = root.GetComponent<Image>();
        rootImage.color = isPrimary ? new Color(0.98f, 0.68f, 0.16f, 1f) : background;
        ApplyChamfer(rootImage);

        // Tactical 9-sliced chamfered border for secondary/dark buttons
        if (!isPrimary)
        {
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(root.transform, false);
            Stretch(border.GetComponent<RectTransform>());
            var borderImg = border.GetComponent<Image>();
            borderImg.sprite = ResolveRingSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.24f, 0.32f, 0.44f, 0.4f);
            borderImg.raycastTarget = false;
        }

        // Tactical top accent highlight rail on primary buttons
        if (isPrimary)
        {
            var topRail = new GameObject("TopHighlight", typeof(RectTransform), typeof(Image));
            topRail.transform.SetParent(root.transform, false);
            var trRt = topRail.GetComponent<RectTransform>();
            trRt.anchorMin = new Vector2(0f, 1f);
            trRt.anchorMax = new Vector2(1f, 1f);
            trRt.pivot = new Vector2(0.5f, 1f);
            trRt.anchoredPosition = Vector2.zero;
            trRt.sizeDelta = new Vector2(-28f, 2.5f);
            var trImg = topRail.GetComponent<Image>();
            trImg.color = new Color(1f, 0.95f, 0.7f, 0.55f);
            trImg.raycastTarget = false;
        }

        var button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

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

        var text = CreateText(root.transform, "Label", label, 18f, TextAlignmentOptions.Center, isPrimary ? OnGoldTextColor : textColor, isBold: true);
        text.characterSpacing = 2.5f;
        Stretch(text.rectTransform, 14f, 14f, 0f, 0f);

        var anim = root.AddComponent<AnimatedButton>();
        anim.SetGlowColor(GoldColor);
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

            // Fit math matching preserveAspect
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

            var clipTransform = cardRoot.transform.Find("ArtClip");
            if (clipTransform != null)
            {
                var clipRect = clipTransform.GetComponent<RectTransform>();
                clipRect.anchorMin = new Vector2(insetX / cardWidth, insetY / cardHeight);
                clipRect.anchorMax = new Vector2(1f - insetX / cardWidth, 1f - insetY / cardHeight);
                clipRect.offsetMin = Vector2.zero;
                clipRect.offsetMax = Vector2.zero;
            }
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
                animator.SetSelected(selected);
                continue;
            }

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
            return "Enter a callsign.";
        if (username.Length < 3 || username.Length > 16)
            return "Callsign must be 3–16 characters.";
        if (!char.IsLetter(username[0]))
            return "Callsign must start with a letter.";
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
            return "That callsign is already taken.";
        return "Callsign could not be saved. Check your connection and try again.";
    }

    private void SetBusy(bool busy, bool isNicknameStep = false)
    {
        if (!IsAlive)
            return;

        _busy = busy;
        if (busy)
        {
            if (isNicknameStep)
            {
                if (_usernameInput != null) _usernameInput.interactable = false;
                SetButtonBusy(_saveUsernameButton, true);
            }
            else
            {
                if (_backButton != null) _backButton.interactable = false;
                SetButtonBusy(_continueButton, true);
            }
        }
        else
        {
            if (_usernameInput != null) _usernameInput.interactable = true;
            if (_saveUsernameButton != null)
            {
                _saveUsernameButton.interactable = true;
                var anim = _saveUsernameButton.GetComponent<AnimatedButton>();
                if (anim != null) anim.SetLoading(false);
            }
            if (_continueButton != null)
            {
                _continueButton.interactable = true;
                var anim = _continueButton.GetComponent<AnimatedButton>();
                if (anim != null) anim.SetLoading(false);
            }
            if (_backButton != null)
            {
                _backButton.interactable = true;
                var anim = _backButton.GetComponent<AnimatedButton>();
                if (anim != null) anim.SetLoading(false);
            }
        }
    }

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
            _error.characterSpacing = 2f;
        }
    }

    private void ShowOverlay()
    {
        if (_overlay == null)
            return;

        if (_overlayOpen)
        {
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

    public void RebuildOverlayPreview(bool showLevelStep)
    {
        DestroyStaleOverlay();
        _overlay = null;
        _overlayOpen = false;
        _cardRect = null;
        _usernameStep = null;
        _levelStep = null;
        _dossierAnimator = null;
        _randomizeIconRect = null;
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

    private static readonly string[] TacticalPrefixes = new[]
    {
        "Vanguard", "Apex", "Ghost", "Spectre", "Ironclad",
        "Reaper", "Falcon", "Shadow", "Titan", "Havoc",
        "Viper", "Nomad", "Cipher", "Echo", "Ranger",
        "Blaze", "Frost", "Kodiak", "Wraith", "Striker"
    };

    private void RandomizeCallsign()
    {
        if (_usernameInput == null)
            return;
        var prefix = TacticalPrefixes[UnityEngine.Random.Range(0, TacticalPrefixes.Length)];
        var number = UnityEngine.Random.Range(1, 100);
        var callsign = $"{prefix}_{number:D2}";
        _usernameInput.text = callsign;
        SetError(string.Empty);
        UISfx.PlayClick();

        if (_dossierAnimator != null)
            _dossierAnimator.SetCallsign(callsign);

        if (_randomizeIconRect != null)
        {
            UITween.Kill(_diceSpinToken);
            _randomizeIconRect.localEulerAngles = Vector3.zero;
            // Clean 360 degree spin roll without awkward scale distortion
            UITween.To(0f, 1f, 0.35f, Ease.OutCubic,
                p =>
                {
                    if (_randomizeIconRect != null)
                        _randomizeIconRect.localEulerAngles = new Vector3(0f, 0f, -360f * p);
                },
                () =>
                {
                    if (_randomizeIconRect != null)
                        _randomizeIconRect.localEulerAngles = Vector3.zero;
                },
                0f, _diceSpinToken);
        }
    }

}
