using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;

namespace Railbound.Editor
{
    /// <summary>
    /// Builds the remaining auxiliary UI prefabs without modifying existing screens:
    /// - PauseMenuModal.prefab
    /// - ScoreboardOverlay.prefab
    /// - LevelUpUnlockModal.prefab
    /// - ConfirmationModal.prefab
    /// - GlobalChatDrawer.prefab
    /// </summary>
    [InitializeOnLoad]
    public static class AuxiliaryUIBuilder
    {
        private static readonly Color DarkBg = new Color(0.055f, 0.067f, 0.086f, 0.88f);  // #0E1116
        private static readonly Color PanelBg = new Color(0.082f, 0.094f, 0.122f, 0.98f); // #15181F
        private static readonly Color InputBg = new Color(0.039f, 0.047f, 0.059f, 1f);    // #0A0C0F
        private static readonly Color AccentOrange = new Color(1f, 0.415f, 0f, 1f);        // #FF6A00
        private static readonly Color AccentGold = new Color(1f, 0.72f, 0f, 1f);           // #FFB800
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);
        private static readonly Color TextMuted = new Color(0.56f, 0.61f, 0.68f, 1f);      // #8F9CAE
        private static readonly Color BorderColor = new Color(0.165f, 0.192f, 0.239f, 1f); // #2A313D

        private static TMP_FontAsset _defaultFont;

        static AuxiliaryUIBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Assets/Project/UI/PauseMenuModal.prefab"))
                {
                    BuildAllAuxiliaryPrefabs();
                }
            };
        }

        [MenuItem("Railbound/Build Auxiliary UI Prefabs")]
        public static void BuildAllAuxiliaryPrefabs()
        {
            _defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            if (!Directory.Exists("Assets/Project/UI"))
                Directory.CreateDirectory("Assets/Project/UI");

            BuildPauseMenuPrefab();
            BuildScoreboardPrefab();
            BuildLevelUpPrefab();
            BuildConfirmationPrefab();
            BuildChatDrawerPrefab();
            BuildGuestNameModalPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AuxiliaryUIBuilder] All Auxiliary UI Prefabs successfully generated in Assets/Project/UI!");
        }

        // ------------------------------------------------------------------ 1. PauseMenuModal
        private static void BuildPauseMenuPrefab()
        {
            var root = CreateRootCanvasGroup("PauseMenuModal");
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<PauseMenuModal>();

            CreateImage("DimBackdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 420f));
            CreateBorder("Border", card.transform, BorderColor);
            CreateImage("TopAccent", card.transform, AccentOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            CreateText("Header", card.transform, "TACTICAL PAUSE", TextWhite, 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 30f), bold: true);
            CreateText("SubHeader", card.transform, "MISSION IN PROGRESS - COMBAT ACTIVE", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(400f, 20f));

            var btnContainer = CreatePanel("ButtonStack", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(380f, 260f));

            var resumeBtn = CreateButton("ResumeButton", btnContainer.transform, "RESUME COMBAT", AccentOrange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(360f, 48f), textColor: DarkBg);
            var settingsBtn = CreateButton("SettingsButton", btnContainer.transform, "SETTINGS", new Color(0.12f, 0.14f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(360f, 44f));
            var restartBtn = CreateButton("RestartButton", btnContainer.transform, "RESPAWN / SUICIDE", new Color(0.12f, 0.14f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(360f, 44f));
            var leaveBtn = CreateButton("LeaveMatchButton", btnContainer.transform, "LEAVE MATCH", new Color(0.2f, 0.08f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(360f, 44f), textColor: new Color(1f, 0.4f, 0.4f));

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("restartButton").objectReferenceValue = restartBtn;
            so.FindProperty("leaveMatchButton").objectReferenceValue = leaveBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/PauseMenuModal.prefab");
        }

        // ------------------------------------------------------------------ 2. ScoreboardOverlay
        private static void BuildScoreboardPrefab()
        {
            var root = CreateRootCanvasGroup("ScoreboardOverlay");
            var controller = root.AddComponent<ScoreboardOverlay>();

            CreateImage("Backdrop", root.transform, new Color(0.04f, 0.05f, 0.07f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var board = CreateImage("BoardCard", root.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 660f));
            CreateBorder("Border", board.transform, BorderColor);
            CreateImage("TopBar", board.transform, AccentOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            // Header info
            CreateText("HeaderTitle", board.transform, "RAILBOUND COMBAT RECORD", TextWhite, 18f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -35f), new Vector2(400f, 30f), bold: true);
            var mode = CreateText("ModeText", board.transform, "TEAM DEATHMATCH", AccentGold, 13f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -58f), new Vector2(400f, 20f));
            var timer = CreateText("TimerText", board.transform, "TIME REMAINING: 07:45", TextMuted, 13f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -35f), new Vector2(400f, 30f));

            // Score Banner
            var alphaScore = CreateText("AlphaScore", board.transform, "ALPHA SQUAD: 38", new Color(0.35f, 0.65f, 1f), 15f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -95f), new Vector2(300f, 26f), bold: true);
            var bravoScore = CreateText("BravoScore", board.transform, "BRAVO SQUAD: 32", AccentOrange, 15f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -95f), new Vector2(300f, 26f), bold: true);

            // Left Team Alpha Roster
            var alphaRoster = CreatePanel("AlphaRoster", board.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(20f, -80f), new Vector2(-40f, -240f));
            CreateTeamRosterHeader(alphaRoster.transform, "ALPHA OPERATORS", new Color(0.35f, 0.65f, 1f));
            PopulateDummyRows(alphaRoster.transform, 5, "Operator_A");

            // Right Team Bravo Roster
            var bravoRoster = CreatePanel("BravoRoster", board.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(20f, -80f), new Vector2(-40f, -240f));
            CreateTeamRosterHeader(bravoRoster.transform, "BRAVO OPERATORS", AccentOrange);
            PopulateDummyRows(bravoRoster.transform, 5, "Operator_B");

            CreateText("FooterHint", board.transform, "HOLD [TAB] TO DISPLAY COMBAT RECORD", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(600f, 20f));

            var so = new SerializedObject(controller);
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("matchModeText").objectReferenceValue = mode;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("alphaScoreText").objectReferenceValue = alphaScore;
            so.FindProperty("bravoScoreText").objectReferenceValue = bravoScore;
            so.FindProperty("alphaRosterContainer").objectReferenceValue = alphaRoster.transform;
            so.FindProperty("bravoRosterContainer").objectReferenceValue = bravoRoster.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/ScoreboardOverlay.prefab");
        }

        private static void CreateTeamRosterHeader(Transform parent, string title, Color accent)
        {
            var header = CreateImage("HeaderRow", parent, new Color(0.12f, 0.15f, 0.19f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, 32f));
            CreateText("Title", header.transform, title, accent, 13f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(140f, 24f), bold: true);
            CreateText("KDA", header.transform, "K / D / A", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.6f, 0.5f), new Vector2(0.6f, 0.5f), Vector2.zero, new Vector2(80f, 24f));
            CreateText("Score", header.transform, "SCORE", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.78f, 0.5f), new Vector2(0.78f, 0.5f), Vector2.zero, new Vector2(60f, 24f));
            CreateText("Ping", header.transform, "PING", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.93f, 0.5f), new Vector2(0.93f, 0.5f), Vector2.zero, new Vector2(50f, 24f));
        }

        private static void PopulateDummyRows(Transform parent, int count, string prefix)
        {
            for (int i = 0; i < count; i++)
            {
                float y = -58f - (i * 36f);
                var row = CreateImage($"Row_{i}", parent, (i % 2 == 0) ? new Color(0.09f, 0.11f, 0.14f, 0.8f) : new Color(0.07f, 0.08f, 0.11f, 0.8f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(0f, 32f));
                CreateText("Name", row.transform, $"{prefix}_{i + 1}", TextWhite, 12f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(140f, 24f));
                CreateText("KDA", row.transform, $"{12 - i * 2} / {4 + i} / {3 + i}", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.6f, 0.5f), new Vector2(0.6f, 0.5f), Vector2.zero, new Vector2(80f, 24f));
                CreateText("Score", row.transform, $"{1850 - i * 220}", AccentGold, 11f, TextAlignmentOptions.Center, new Vector2(0.78f, 0.5f), new Vector2(0.78f, 0.5f), Vector2.zero, new Vector2(60f, 24f));
                CreateText("Ping", row.transform, $"{24 + i * 8}ms", new Color(0.3f, 0.8f, 0.4f), 11f, TextAlignmentOptions.Center, new Vector2(0.93f, 0.5f), new Vector2(0.93f, 0.5f), Vector2.zero, new Vector2(50f, 24f));
            }
        }

        // ------------------------------------------------------------------ 3. LevelUpUnlockModal
        private static void BuildLevelUpPrefab()
        {
            var root = CreateRootCanvasGroup("LevelUpUnlockModal");
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<LevelUpUnlockModal>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 500f));
            CreateBorder("Border", card.transform, BorderColor);
            CreateImage("TopAccent", card.transform, AccentGold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            var lvlTitle = CreateText("LevelTitle", card.transform, "OPERATOR LEVEL 12", AccentGold, 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(460f, 30f), bold: true);
            var category = CreateText("Category", card.transform, "TACTICAL ASSET UNLOCKED", AccentOrange, 12f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(460f, 20f));

            // Reward Card
            var showcase = CreateImage("RewardShowcase", card.transform, InputBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(440f, 220f));
            CreateBorder("ShowcaseBorder", showcase.transform, AccentOrange);

            var rewardName = CreateText("RewardName", showcase.transform, "ASSAULT RIFLE - SCAR-H", TextWhite, 16f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(400f, 26f), bold: true);
            var iconImg = CreateImage("RewardIcon", showcase.transform, new Color(1f, 0.6f, 0.2f, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(160f, 80f));
            CreateText("RarityBadge", showcase.transform, "RARITY: MILITARY GRADE", new Color(0.35f, 0.7f, 1f), 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(300f, 20f));

            var claimBtn = CreateButton("ClaimButton", card.transform, "CLAIM & EQUIP", AccentOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(320f, 48f), textColor: DarkBg);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("levelTitleText").objectReferenceValue = lvlTitle;
            so.FindProperty("rewardCategoryText").objectReferenceValue = category;
            so.FindProperty("rewardNameText").objectReferenceValue = rewardName;
            so.FindProperty("rewardIconImage").objectReferenceValue = iconImg.GetComponent<Image>();
            so.FindProperty("claimButton").objectReferenceValue = claimBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/LevelUpUnlockModal.prefab");
        }

        // ------------------------------------------------------------------ 4. ConfirmationModal
        private static void BuildConfirmationPrefab()
        {
            var root = CreateRootCanvasGroup("ConfirmationModal");
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<ConfirmationModal>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 260f));
            CreateBorder("Border", card.transform, BorderColor);
            CreateImage("TopAccent", card.transform, AccentOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            var title = CreateText("Title", card.transform, "CONFIRM ACTION", TextWhite, 18f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(400f, 26f), bold: true);
            var msg = CreateText("Message", card.transform, "Are you sure you want to proceed? Any unsaved match progress will be lost.", TextMuted, 13f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(400f, 55f));

            var cancelBtn = CreateButton("CancelButton", card.transform, "CANCEL", new Color(0.12f, 0.14f, 0.18f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-105f, 40f), new Vector2(180f, 44f));
            var confirmBtn = CreateButton("ConfirmButton", card.transform, "CONFIRM", AccentOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(105f, 40f), new Vector2(180f, 44f), textColor: DarkBg);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = msg;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/ConfirmationModal.prefab");
        }

        // ------------------------------------------------------------------ 5. GlobalChatDrawer
        private static void BuildChatDrawerPrefab()
        {
            var root = CreateRootCanvasGroup("GlobalChatDrawer");
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<GlobalChatDrawer>();

            // Slide-out drawer on right edge
            var drawer = CreateImage("DrawerPanel", root.transform, PanelBg, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(420f, 0f));
            drawer.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            CreateBorder("LeftBorder", drawer.transform, BorderColor);

            // Header
            CreateImage("Header", drawer.transform, new Color(0.06f, 0.08f, 0.1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 50f));
            CreateText("HeaderTitle", drawer.transform, "COMMS CHANNELS", AccentOrange, 15f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -25f), new Vector2(160f, 30f), bold: true);
            var closeBtn = CreateButton("CloseButton", drawer.transform, "X", new Color(0.15f, 0.18f, 0.22f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(36f, 36f));

            // Messages Container
            var msgArea = CreateImage("MessageContainer", drawer.transform, InputBg, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 25f), new Vector2(-30f, -140f));
            CreateBorder("MsgBorder", msgArea.transform, BorderColor);

            // Dummy message previews
            CreateText("Msg1", msgArea.transform, "[TEAM] Bravo_01: Regroup at sector B.", new Color(0.4f, 0.7f, 1f), 12f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(15f, -20f), new Vector2(-30f, 24f));
            CreateText("Msg2", msgArea.transform, "[SQUAD] Alpha_02: Enemy sniper spotted.", AccentGold, 12f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(15f, -48f), new Vector2(-30f, 24f));

            // Bottom Input
            var inputGo = CreateImage("InputArea", drawer.transform, InputBg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 35f), new Vector2(-120f, 44f));
            CreateBorder("InputBorder", inputGo.transform, BorderColor);
            var inputField = inputGo.AddComponent<TMP_InputField>();
            var placeholder = CreateText("Placeholder", inputGo.transform, "Transmit message...", TextMuted, 13f, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-24f, 0f));
            var inputText = CreateText("Text", inputGo.transform, "", TextWhite, 13f, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-24f, 0f));
            inputField.placeholder = placeholder;
            inputField.textComponent = inputText;

            var sendBtn = CreateButton("SendButton", drawer.transform, "SEND", AccentOrange, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 35f), new Vector2(70f, 44f), textColor: DarkBg);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("messageInput").objectReferenceValue = inputField;
            so.FindProperty("sendButton").objectReferenceValue = sendBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("messageContainer").objectReferenceValue = msgArea.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/GlobalChatDrawer.prefab");
        }

        // ------------------------------------------------------------------ 6. GuestNamePopup
        public static void BuildGuestNameModalPrefab()
        {
            var root = CreateRootCanvasGroup("GuestNamePopup");
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<GuestNameModalController>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 320f));
            CreateBorder("Border", card.transform, BorderColor);
            CreateImage("TopAccent", card.transform, AccentOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            CreateText("Header", card.transform, "DEPLOY AS GUEST", TextWhite, 18f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(440f, 26f), bold: true);
            CreateText("SubHeader", card.transform, "ENTER OPERATOR CALLSIGN (3-16 CHARACTERS)", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(440f, 20f));

            var warningText = CreateText("WarningText", card.transform, "Callsign must be 3-16 alphanumeric characters.", new Color(1f, 0.35f, 0.35f, 1f), 12f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(440f, 20f));
            warningText.gameObject.SetActive(false);

            // Callsign Input Field
            var inputGo = CreateImage("CallsignInput", card.transform, InputBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(420f, 48f));
            CreateBorder("InputBorder", inputGo.transform, BorderColor);
            var inputField = inputGo.AddComponent<TMP_InputField>();
            var placeholder = CreateText("Placeholder", inputGo.transform, "Callsign_01", TextMuted, 14f, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-32f, 0f));
            var inputText = CreateText("Text", inputGo.transform, "", TextWhite, 14f, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-32f, 0f));
            inputField.placeholder = placeholder;
            inputField.textComponent = inputText;

            var cancelBtn = CreateButton("CancelButton", card.transform, "CANCEL", new Color(0.12f, 0.14f, 0.18f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 45f), new Vector2(190f, 44f));
            var confirmBtn = CreateButton("ConfirmButton", card.transform, "CONFIRM & DEPLOY", AccentOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 45f), new Vector2(190f, 44f), textColor: DarkBg);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("callsignInput").objectReferenceValue = inputField;
            so.FindProperty("warningText").objectReferenceValue = warningText;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("lobbySceneName").stringValue = "GameLobby";
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/GuestNamePopup.prefab");
        }

        // ------------------------------------------------------------------ UI Helpers
        private static GameObject CreateRootCanvasGroup(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            return go;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dim)
        {
            var go = CreatePanel(name, parent, anchorMin, anchorMax, pos, dim);
            go.AddComponent<CanvasRenderer>();
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return go;
        }

        private static void CreateBorder(string name, Transform parent, Color borderColor, float width = 1f)
        {
            var border = CreatePanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.AddComponent<CanvasRenderer>();
            var img = border.AddComponent<Image>();
            img.color = borderColor;
            img.raycastTarget = false;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, Color color, float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dim, bool bold = false)
        {
            var go = CreatePanel(name, parent, anchorMin, anchorMax, pos, dim);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_defaultFont != null) tmp.font = _defaultFont;
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = size;
            tmp.alignment = align;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color bgColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dim, Color? textColor = null)
        {
            var btnGo = CreateImage(name, parent, bgColor, anchorMin, anchorMax, pos, dim);
            var btn = btnGo.AddComponent<Button>();

            var fx = btnGo.AddComponent<RailboundButtonEffects>();

            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Click.wav");
            var hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Hover.wav");

            var fxSo = new SerializedObject(fx);
            if (clickClip != null) fxSo.FindProperty("clickSfx").objectReferenceValue = clickClip;
            if (hoverClip != null) fxSo.FindProperty("hoverSfx").objectReferenceValue = hoverClip;
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            CreateText("Label", btnGo.transform, label, textColor ?? TextWhite, 14f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, bold: true);
            return btn;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[AuxiliaryUIBuilder] Saved Prefab: {path}");
        }
    }
}
