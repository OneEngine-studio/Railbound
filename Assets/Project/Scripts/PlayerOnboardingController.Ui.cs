using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI.Anim;

public sealed partial class PlayerOnboardingController
{
    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 size)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color, bool isBold = false)
    {
        var objectRoot = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        objectRoot.transform.SetParent(parent, false);
        var label = objectRoot.GetComponent<TextMeshProUGUI>();
        var font = isBold ? ResolveBoldFont() : ResolveMedFont();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private void CreateOperatorDossierCard(Transform parent)
    {
        var root = new GameObject("OperatorDossierCard", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.015f, 0.02f);
        rect.anchorMax = new Vector2(0.425f, 0.98f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.07f, 0.98f);
        ApplyChamfer(bg);

        // Chamfered tactical border
        var borderObj = new GameObject("ChamferBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(root.transform, false);
        Stretch(borderObj.GetComponent<RectTransform>());
        var borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = ResolveRingSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.24f, 0.32f, 0.44f, 0.55f);
        borderImg.raycastTarget = false;

        // Top Status Header Bar
        var topBar = new GameObject("TopBar", typeof(RectTransform));
        topBar.transform.SetParent(root.transform, false);
        var tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0f, 1f);
        tbRect.anchorMax = new Vector2(1f, 1f);
        tbRect.pivot = new Vector2(0.5f, 1f);
        tbRect.anchoredPosition = new Vector2(0f, -8f);
        tbRect.sizeDelta = new Vector2(-28f, 22f);

        // Green live pulsing dot
        var liveDot = new GameObject("LiveDot", typeof(RectTransform), typeof(Image));
        liveDot.transform.SetParent(topBar.transform, false);
        var ldRect = liveDot.GetComponent<RectTransform>();
        ldRect.anchorMin = new Vector2(0f, 0.5f);
        ldRect.anchorMax = new Vector2(0f, 0.5f);
        ldRect.pivot = new Vector2(0f, 0.5f);
        ldRect.anchoredPosition = new Vector2(4f, 0f);
        ldRect.sizeDelta = new Vector2(8f, 8f);
        var ldImg = liveDot.GetComponent<Image>();
        ldImg.color = new Color(0.2f, 0.9f, 0.4f, 1f);
        ldImg.raycastTarget = false;

        // Status Text
        var statusLabel = CreateText(topBar.transform, "StatusText", "OPERATOR ACTIVE // UNIT 01", 11f, TextAlignmentOptions.Left, new Color(0.65f, 0.75f, 0.85f, 0.9f), isBold: true);
        statusLabel.characterSpacing = 2.5f;
        var slRect = statusLabel.rectTransform;
        slRect.anchorMin = new Vector2(0f, 0f);
        slRect.anchorMax = new Vector2(1f, 1f);
        slRect.offsetMin = new Vector2(20f, 0f);
        slRect.offsetMax = Vector2.zero;

        // Artwork Clipping Mask (Chamfered)
        var clip = new GameObject("ArtClip", typeof(RectTransform), typeof(Image), typeof(Mask));
        clip.transform.SetParent(root.transform, false);
        var clipRect = clip.GetComponent<RectTransform>();
        clipRect.anchorMin = new Vector2(0.04f, 0.17f);
        clipRect.anchorMax = new Vector2(0.96f, 0.92f);
        clipRect.offsetMin = Vector2.zero;
        clipRect.offsetMax = Vector2.zero;
        var clipImg = clip.GetComponent<Image>();
        clipImg.sprite = ResolveChamferSprite();
        clipImg.type = Image.Type.Sliced;
        clip.GetComponent<Mask>().showMaskGraphic = false;

        // Artwork image
        var artObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(clip.transform, false);
        var artRect = artObj.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;
        var artImg = artObj.GetComponent<Image>();
        artImg.sprite = ResolveOperatorSprite();
        artImg.preserveAspect = true;
        artImg.raycastTarget = false;

        // Bottom Tactical Dogtag / Callsign Plate
        var badge = new GameObject("CallsignBadge", typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(root.transform, false);
        var badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.04f, 0.025f);
        badgeRect.anchorMax = new Vector2(0.96f, 0.155f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = Vector2.zero;
        var badgeImg = badge.GetComponent<Image>();
        badgeImg.sprite = ResolveChamferSprite();
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = new Color(0.02f, 0.03f, 0.06f, 0.95f);
        badgeImg.raycastTarget = false;

        // Badge border
        var bbObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        bbObj.transform.SetParent(badge.transform, false);
        Stretch(bbObj.GetComponent<RectTransform>());
        var bbImg = bbObj.GetComponent<Image>();
        bbImg.sprite = ResolveRingSprite();
        bbImg.type = Image.Type.Sliced;
        bbImg.color = new Color(0.2f, 0.28f, 0.38f, 0.45f);
        bbImg.raycastTarget = false;

        // Callsign header
        var csHeader = CreateText(badge.transform, "CodenameHeader", "TACTICAL CALLSIGN", 10f, TextAlignmentOptions.Left, GoldColor, isBold: true);
        csHeader.characterSpacing = 2.5f;
        var cshRect = csHeader.rectTransform;
        cshRect.anchorMin = new Vector2(0f, 1f);
        cshRect.anchorMax = new Vector2(1f, 1f);
        cshRect.pivot = new Vector2(0f, 1f);
        cshRect.anchoredPosition = new Vector2(14f, -7f);
        cshRect.sizeDelta = new Vector2(-28f, 16f);

        // Live codename text
        var codename = CreateText(badge.transform, "LiveCodename", "[UNASSIGNED]", 19f, TextAlignmentOptions.Left, GoldColor, isBold: true);
        codename.characterSpacing = 2f;
        var cnRect = codename.rectTransform;
        cnRect.anchorMin = new Vector2(0f, 0f);
        cnRect.anchorMax = new Vector2(1f, 0f);
        cnRect.pivot = new Vector2(0f, 0f);
        cnRect.anchoredPosition = new Vector2(14f, 8f);
        cnRect.sizeDelta = new Vector2(-28f, 26f);

        // Attach InteractiveDossierAnimator
        _dossierAnimator = root.AddComponent<InteractiveDossierAnimator>();
        _dossierAnimator.Initialize(artImg, borderImg, codename, ldImg, RandomizeCallsign);
    }

    private void CreateRegistrationForm(Transform parent)
    {
        var formObj = new GameObject("RegistrationForm", typeof(RectTransform));
        formObj.transform.SetParent(parent, false);
        var formRect = formObj.GetComponent<RectTransform>();
        formRect.anchorMin = new Vector2(0.455f, 0.02f);
        formRect.anchorMax = new Vector2(0.985f, 0.98f);
        formRect.offsetMin = Vector2.zero;
        formRect.offsetMax = Vector2.zero;

        var formLayout = formObj.AddComponent<VerticalLayoutGroup>();
        formLayout.spacing = 14f;
        formLayout.padding = new RectOffset(16, 16, 12, 12);
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;

        var formTitle = CreateText(formObj.transform, "FormHeader", "CALLSIGN REGISTRATION", 13.5f, TextAlignmentOptions.Left, GoldColor, isBold: true);
        formTitle.characterSpacing = 3f;
        SetPreferredHeight(formTitle.gameObject, 22f);

        var formDesc = CreateText(formObj.transform, "FormDesc", "Assign your tactical codename for convoy operations, squad comms, and global rankings.", 14f, TextAlignmentOptions.Left, MutedTextColor, isBold: false);
        SetPreferredHeight(formDesc.gameObject, 38f);

        _usernameInput = CreateInputField(formObj.transform);
        _usernameInput.characterLimit = 16;
        _usernameInput.onValueChanged.AddListener(val =>
        {
            SetError(string.Empty);
            if (_dossierAnimator != null)
                _dossierAnimator.SetCallsign(val);
        });

        var hintText = CreateText(formObj.transform, "Hint", "3–16 CHARACTERS • LETTERS, NUMBERS & UNDERSCORES", 11.5f, TextAlignmentOptions.Left, new Color(0.45f, 0.52f, 0.62f, 0.85f), isBold: true);
        hintText.characterSpacing = 2f;
        SetPreferredHeight(hintText.gameObject, 20f);

        // Tactical Identity Protocol Callout Panel
        var callout = new GameObject("ProtocolCallout", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        callout.transform.SetParent(formObj.transform, false);
        SetPreferredHeight(callout, 74f);
        var calloutImg = callout.GetComponent<Image>();
        calloutImg.sprite = ResolveChamferSprite();
        calloutImg.type = Image.Type.Sliced;
        calloutImg.color = new Color(0.02f, 0.03f, 0.06f, 0.92f);

        var calloutBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
        calloutBorder.transform.SetParent(callout.transform, false);
        Stretch(calloutBorder.GetComponent<RectTransform>());
        var cbImg = calloutBorder.GetComponent<Image>();
        cbImg.sprite = ResolveRingSprite();
        cbImg.type = Image.Type.Sliced;
        cbImg.color = new Color(0.2f, 0.28f, 0.38f, 0.4f);
        cbImg.raycastTarget = false;

        var calloutHeader = CreateText(callout.transform, "CalloutHeader", "// IDENTITY PROTOCOL", 10.5f, TextAlignmentOptions.Left, GoldColor, isBold: true);
        calloutHeader.characterSpacing = 2.5f;
        var chRt = calloutHeader.rectTransform;
        chRt.anchorMin = new Vector2(0f, 1f);
        chRt.anchorMax = new Vector2(1f, 1f);
        chRt.pivot = new Vector2(0f, 1f);
        chRt.anchoredPosition = new Vector2(16f, -10f);
        chRt.sizeDelta = new Vector2(-32f, 18f);

        var calloutBody = CreateText(callout.transform, "CalloutBody", "Callsign is broadcasted to squad mates during convoy operations and leaderboards. Can be modified later in settings.", 12f, TextAlignmentOptions.Left, MutedTextColor, isBold: false);
        var cbyRt = calloutBody.rectTransform;
        cbyRt.anchorMin = new Vector2(0f, 0f);
        cbyRt.anchorMax = new Vector2(1f, 0f);
        cbyRt.pivot = new Vector2(0f, 0f);
        cbyRt.anchoredPosition = new Vector2(16f, 8f);
        cbyRt.sizeDelta = new Vector2(-32f, 32f);

        _saveUsernameButton = CreateStyledButton(formObj.transform, "SaveNicknameButton", "CONFIRM CALLSIGN  ►►", GoldColor, Color.clear, OnGoldTextColor, isPrimary: true);
        _saveUsernameButton.onClick.AddListener(SaveUsername);
    }

    private TMP_InputField CreateInputField(Transform parent)
    {
        var root = new GameObject("NicknameInput", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.minHeight = 58f;
        rootLayout.preferredHeight = 58f;
        var rootImage = root.GetComponent<Image>();
        ApplyChamfer(rootImage);
        rootImage.color = InputColor;

        // Tactical 4-sided chamfered border
        var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(root.transform, false);
        Stretch(border.GetComponent<RectTransform>());
        var borderImg = border.GetComponent<Image>();
        borderImg.sprite = ResolveRingSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.28f, 0.38f, 0.52f, 0.75f);
        borderImg.raycastTarget = false;

        // Operator crosshair icon on left
        var crosshair = new GameObject("Icon_Crosshair", typeof(RectTransform), typeof(Image));
        crosshair.transform.SetParent(root.transform, false);
        var cRect = crosshair.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0f, 0.5f);
        cRect.anchorMax = new Vector2(0f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(24f, 0f);
        cRect.sizeDelta = new Vector2(24f, 24f);
        var cImg = crosshair.GetComponent<Image>();
        cImg.sprite = ResolveCrosshairSprite();
        cImg.color = GoldColor;
        cImg.raycastTarget = false;

        // Callsign randomize dice button on right
        var randBtnObj = new GameObject("Button_Randomize", typeof(RectTransform), typeof(Image), typeof(Button));
        randBtnObj.transform.SetParent(root.transform, false);
        var rRect = randBtnObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1f, 0.5f);
        rRect.anchorMax = new Vector2(1f, 0.5f);
        rRect.pivot = new Vector2(1f, 0.5f);
        rRect.anchoredPosition = new Vector2(-8f, 0f);
        rRect.sizeDelta = new Vector2(44f, 44f);
        var rBg = randBtnObj.GetComponent<Image>();
        rBg.sprite = ResolveChamferSprite();
        rBg.type = Image.Type.Sliced;
        rBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        var rBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
        rBorder.transform.SetParent(randBtnObj.transform, false);
        Stretch(rBorder.GetComponent<RectTransform>());
        var rbImg = rBorder.GetComponent<Image>();
        rbImg.sprite = ResolveRingSprite();
        rbImg.type = Image.Type.Sliced;
        rbImg.color = new Color(0.32f, 0.44f, 0.58f, 0.65f);
        rbImg.raycastTarget = false;

        var rIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        rIconObj.transform.SetParent(randBtnObj.transform, false);
        var riRect = rIconObj.GetComponent<RectTransform>();
        riRect.anchorMin = new Vector2(0.5f, 0.5f);
        riRect.anchorMax = new Vector2(0.5f, 0.5f);
        riRect.pivot = new Vector2(0.5f, 0.5f);
        riRect.anchoredPosition = Vector2.zero;
        riRect.sizeDelta = new Vector2(24f, 24f);
        var riImg = rIconObj.GetComponent<Image>();
        riImg.sprite = ResolveRandomizeSprite();
        riImg.color = GoldColor;
        riImg.raycastTarget = false;
        _randomizeIconRect = riRect;

        var rBtn = randBtnObj.GetComponent<Button>();
        var rAnim = randBtnObj.AddComponent<AnimatedButton>();
        rAnim.SetHoverStyle(AnimatedButton.HoverStyle.Light);
        rBtn.onClick.AddListener(RandomizeCallsign);

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObject.transform.SetParent(root.transform, false);
        var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
        var medFont = ResolveMedFont();
        if (medFont != null) placeholder.font = medFont;
        placeholder.text = "ENTER CALLSIGN (3 - 16 CHARS)";
        placeholder.fontSize = 17f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = PlaceholderColor;
        placeholder.enableWordWrapping = false;
        placeholder.raycastTarget = false;
        Stretch(placeholderObject.GetComponent<RectTransform>(), 46f, 56f, 0f, 0f);

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        var boldFont = ResolveBoldFont();
        if (boldFont != null) text.font = boldFont;
        text.fontSize = 19f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        Stretch(textObject.GetComponent<RectTransform>(), 46f, 56f, 0f, 0f);

        var input = root.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = root.GetComponent<Image>();
        input.selectionColor = new Color(1f, 0.75f, 0.2f, 0.35f);
        return input;
    }
}
