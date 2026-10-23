using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;

namespace Railbound.Editor
{
    [InitializeOnLoad]
    public static class RailboundUIBuilder
    {
        private static readonly Color DarkBg = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        private static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.14f, 0.88f);
        private static readonly Color AccentOrange = new Color(1f, 0.48f, 0f, 1f);
        private static readonly Color AccentGold = new Color(1f, 0.72f, 0f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);
        private static readonly Color TextMuted = new Color(0.54f, 0.57f, 0.63f, 1f);
        private static readonly Color BorderColor = new Color(0.16f, 0.19f, 0.24f, 1f);

        private static TMP_FontAsset _defaultFont;

        static RailboundUIBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Assets/Project/UI/SettingsView.prefab"))
                {
                    BuildAllPrefabs();
                }
            };
        }

        [MenuItem("Railbound/Build All UI Prefabs")]
        public static void BuildAllPrefabs()
        {
            _defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            if (!Directory.Exists("Assets/Project/UI"))
                Directory.CreateDirectory("Assets/Project/UI");

            BuildSettingsPrefab();
            BuildMatchmakingPrefab();
            BuildHUDPrefab();
            BuildLoadoutPrefab();
            BuildGameOverPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RailboundUIBuilder] All 5 UI Prefabs successfully generated in Assets/Project/UI!");
        }

        private static void BuildSettingsPrefab()
        {
            GameObject root = CreateRoot("SettingsView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<SettingsView>();

            // Modal Background Backdrop
            GameObject backdrop = CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Settings Center Card (1000 x 700)
            GameObject card = CreateImage("SettingsCard", backdrop.transform, PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 700f));
            CreateBorder("CardBorder", card.transform);

            // Title
            CreateText("Title", card.transform, "TACTICAL SETTINGS", TextWhite, 32, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(400f, 50f));

            // Tabs Bar
            GameObject tabsBar = CreatePanel("TabsBar", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(-80f, 50f));
            var hlg = tabsBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            Button audioTab = CreateButton("AudioTab", tabsBar.transform, "AUDIO", AccentOrange);
            Button graphicsTab = CreateButton("GraphicsTab", tabsBar.transform, "GRAPHICS", PanelBg);
            Button controlsTab = CreateButton("ControlsTab", tabsBar.transform, "CONTROLS", PanelBg);

            // Tab Contents Container
            GameObject contentContainer = CreatePanel("ContentContainer", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-80f, -240f));

            // Audio Content
            GameObject audioContent = CreatePanel("AudioContent", contentContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var vlgAudio = audioContent.AddComponent<VerticalLayoutGroup>();
            vlgAudio.spacing = 20f;
            vlgAudio.childControlWidth = true;
            vlgAudio.childControlHeight = false;
            Slider masterSlider = CreateLabeledSlider("MasterVolume", audioContent.transform, "MASTER VOLUME");
            Slider sfxSlider = CreateLabeledSlider("SFXVolume", audioContent.transform, "SFX VOLUME");
            Slider musicSlider = CreateLabeledSlider("MusicVolume", audioContent.transform, "MUSIC VOLUME");
            Slider voiceSlider = CreateLabeledSlider("VoiceVolume", audioContent.transform, "VOICE VOLUME");

            // Graphics Content
            GameObject graphicsContent = CreatePanel("GraphicsContent", contentContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            graphicsContent.SetActive(false);
            var vlgGfx = graphicsContent.AddComponent<VerticalLayoutGroup>();
            vlgGfx.spacing = 20f;
            vlgGfx.childControlWidth = true;
            vlgGfx.childControlHeight = false;
            TMP_Dropdown resDrop = CreateLabeledDropdown("ResolutionDropdown", graphicsContent.transform, "RESOLUTION");
            TMP_Dropdown qualDrop = CreateLabeledDropdown("QualityDropdown", graphicsContent.transform, "QUALITY PRESET");
            Toggle fsToggle = CreateLabeledToggle("FullscreenToggle", graphicsContent.transform, "FULLSCREEN MODE");
            TMP_Dropdown fpsDrop = CreateLabeledDropdown("FPSLimitDropdown", graphicsContent.transform, "FRAME RATE LIMIT");

            // Controls Content
            GameObject controlsContent = CreatePanel("ControlsContent", contentContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            controlsContent.SetActive(false);
            var vlgCtrl = controlsContent.AddComponent<VerticalLayoutGroup>();
            vlgCtrl.spacing = 20f;
            vlgCtrl.childControlWidth = true;
            vlgCtrl.childControlHeight = false;
            Slider sensSlider = CreateLabeledSlider("SensitivitySlider", controlsContent.transform, "MOUSE SENSITIVITY");
            Toggle invertToggle = CreateLabeledToggle("InvertYToggle", controlsContent.transform, "INVERT VERTICAL LOOK");

            // Bottom Buttons Bar
            GameObject bottomBar = CreatePanel("BottomBar", card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(-80f, 50f));
            Button applyBtn = CreateButton("ApplyButton", bottomBar.transform, "APPLY", AccentOrange, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(160f, 48f));
            Button resetBtn = CreateButton("ResetButton", bottomBar.transform, "RESET DEFAULT", PanelBg, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(160f, 48f));
            Button closeBtn = CreateButton("CloseButton", bottomBar.transform, "BACK", PanelBg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(160f, 48f));

            // Wire Serialized Properties
            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("audioTabButton").objectReferenceValue = audioTab;
            so.FindProperty("graphicsTabButton").objectReferenceValue = graphicsTab;
            so.FindProperty("controlsTabButton").objectReferenceValue = controlsTab;
            so.FindProperty("audioTabContent").objectReferenceValue = audioContent;
            so.FindProperty("graphicsTabContent").objectReferenceValue = graphicsContent;
            so.FindProperty("controlsTabContent").objectReferenceValue = controlsContent;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            so.FindProperty("voiceVolumeSlider").objectReferenceValue = voiceSlider;
            so.FindProperty("resolutionDropdown").objectReferenceValue = resDrop;
            so.FindProperty("qualityDropdown").objectReferenceValue = qualDrop;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fsToggle;
            so.FindProperty("fpsLimitDropdown").objectReferenceValue = fpsDrop;
            so.FindProperty("sensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("invertYToggle").objectReferenceValue = invertToggle;
            so.FindProperty("applyButton").objectReferenceValue = applyBtn;
            so.FindProperty("resetDefaultsButton").objectReferenceValue = resetBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/SettingsView.prefab");
        }

        private static void BuildMatchmakingPrefab()
        {
            GameObject root = CreateRoot("MatchmakingView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<MatchmakingView>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Header (1920x90, top anchored)
            GameObject header = CreateImage("HeaderBar", root.transform, PanelBg, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(0f, 90f));
            CreateBorder("HeaderBorder", header.transform);
            Button backBtn = CreateButton("BackButton", header.transform, "< BACK", PanelBg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(120f, 44f));
            TMP_Text playerName = CreateText("PlayerName", header.transform, "OPERATOR_01", TextWhite, 20, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(200f, 30f));
            TMP_Text playerLevel = CreateText("PlayerLevel", header.transform, "LVL 42", AccentGold, 16, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(440f, 0f), new Vector2(100f, 30f));
            TMP_Text ping = CreateText("Ping", header.transform, "24 ms", TextMuted, 16, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-180f, 0f), new Vector2(120f, 30f));
            TMP_Text region = CreateText("Region", header.transform, "US-EAST", AccentOrange, 16, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(120f, 30f));

            // Rosters: Alpha (Red/Orange) vs Bravo (Blue/Cyan)
            GameObject alphaPanel = CreateImage("RosterAlpha", root.transform, PanelBg, new Vector2(0.26f, 0.55f), new Vector2(0.26f, 0.55f), Vector2.zero, new Vector2(480f, 540f));
            CreateBorder("AlphaBorder", alphaPanel.transform);
            CreateText("AlphaTitle", alphaPanel.transform, "SQUAD ALPHA", AccentOrange, 22, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 35f));
            TMP_Text alphaCount = CreateText("AlphaCount", alphaPanel.transform, "4 / 4 READY", TextMuted, 16, TextAlignmentOptions.TopRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(-50f, 35f));

            GameObject bravoPanel = CreateImage("RosterBravo", root.transform, PanelBg, new Vector2(0.74f, 0.55f), new Vector2(0.74f, 0.55f), Vector2.zero, new Vector2(480f, 540f));
            CreateBorder("BravoBorder", bravoPanel.transform);
            CreateText("BravoTitle", bravoPanel.transform, "SQUAD BRAVO", new Color(0.2f, 0.6f, 1f), 22, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 35f));
            TMP_Text bravoCount = CreateText("BravoCount", bravoPanel.transform, "3 / 4 READY", TextMuted, 16, TextAlignmentOptions.TopRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(-50f, 35f));

            // Bottom Panel: Map, Mode & Start
            GameObject bottomPanel = CreateImage("BottomPanel", root.transform, PanelBg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 75f), new Vector2(0f, 150f));
            CreateBorder("BottomBorder", bottomPanel.transform);
            Image mapThumb = CreateImage("MapThumbnail", bottomPanel.transform, new Color(0.12f, 0.15f, 0.2f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(180f, 100f)).GetComponent<Image>();
            TMP_Text mapName = CreateText("MapName", bottomPanel.transform, "TERMINAL DEPOT", TextWhite, 20, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 15f), new Vector2(220f, 30f));
            TMP_Dropdown modeDrop = CreateDropdownOnly("ModeDropdown", bottomPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, -25f), new Vector2(220f, 36f));

            TMP_Text timer = CreateText("CountdownTimer", bottomPanel.transform, "MATCH STARTING IN 15s", AccentGold, 22, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350f, 40f));
            Button readyBtn = CreateButton("ReadyButton", bottomPanel.transform, "DEPLOY / READY", AccentOrange, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(240f, 60f));
            Image pulse = CreateImage("ReadyPulse", readyBtn.transform, new Color(1f, 0.48f, 0f, 0.3f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();

            // Wire Serialized Properties
            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("playerNameText").objectReferenceValue = playerName;
            so.FindProperty("playerLevelText").objectReferenceValue = playerLevel;
            so.FindProperty("pingText").objectReferenceValue = ping;
            so.FindProperty("regionText").objectReferenceValue = region;
            so.FindProperty("teamAlphaRosterContainer").objectReferenceValue = alphaPanel.transform;
            so.FindProperty("teamBravoRosterContainer").objectReferenceValue = bravoPanel.transform;
            so.FindProperty("teamAlphaCountText").objectReferenceValue = alphaCount;
            so.FindProperty("teamBravoCountText").objectReferenceValue = bravoCount;
            so.FindProperty("mapThumbnailImage").objectReferenceValue = mapThumb;
            so.FindProperty("mapNameText").objectReferenceValue = mapName;
            so.FindProperty("modeDropdown").objectReferenceValue = modeDrop;
            so.FindProperty("countdownTimerText").objectReferenceValue = timer;
            so.FindProperty("readyButton").objectReferenceValue = readyBtn;
            so.FindProperty("readyPulseAccent").objectReferenceValue = pulse;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/MatchmakingView.prefab");
        }

        private static void BuildHUDPrefab()
        {
            GameObject root = CreateRoot("HUDView");
            var view = root.AddComponent<HUDView>();

            // Bottom-Left (Health & Armor Cluster)
            GameObject vitalityCluster = CreateImage("VitalityCluster", root.transform, PanelBg, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(190f, 85f), new Vector2(340f, 110f));
            CreateBorder("VitalityBorder", vitalityCluster.transform);
            Image stance = CreateImage("StanceIcon", vitalityCluster.transform, AccentGold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(36f, 36f)).GetComponent<Image>();

            CreateText("HPLabel", vitalityCluster.transform, "HP", TextMuted, 14, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(65f, -30f), new Vector2(40f, 20f));
            Slider hpBar = CreateSliderOnly("HealthBar", vitalityCluster.transform, AccentOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(65f, -30f), new Vector2(-150f, 14f));
            TMP_Text hpText = CreateText("HPText", vitalityCluster.transform, "100", TextWhite, 18, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -30f), new Vector2(60f, 20f));

            CreateText("ArmorLabel", vitalityCluster.transform, "AP", TextMuted, 14, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(65f, 35f), new Vector2(40f, 20f));
            Slider shieldBar = CreateSliderOnly("ShieldBar", vitalityCluster.transform, new Color(0.2f, 0.7f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(65f, 35f), new Vector2(-150f, 14f));
            TMP_Text shieldText = CreateText("ShieldText", vitalityCluster.transform, "100", TextWhite, 18, TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 35f), new Vector2(60f, 20f));

            // Bottom-Right (Weapons & Ammo Cluster)
            GameObject ammoCluster = CreateImage("AmmoCluster", root.transform, PanelBg, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 85f), new Vector2(340f, 110f));
            CreateBorder("AmmoBorder", ammoCluster.transform);
            TMP_Text weaponName = CreateText("WeaponName", ammoCluster.transform, "VK-47 ASSAULT", TextWhite, 18, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 25f));
            TMP_Text ammoCur = CreateText("AmmoCurrent", ammoCluster.transform, "30", AccentOrange, 42, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(25f, 40f), new Vector2(80f, 50f));
            TMP_Text ammoRes = CreateText("AmmoReserve", ammoCluster.transform, "/ 120", TextMuted, 22, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 40f), new Vector2(80f, 40f));
            TMP_Text nadeCount = CreateText("GrenadeCount", ammoCluster.transform, "x2", AccentGold, 20, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-25f, 40f), new Vector2(60f, 40f));

            // Top-Center (Timer & Match Score)
            GameObject scorePanel = CreateImage("MatchScorePanel", root.transform, PanelBg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(400f, 70f));
            CreateBorder("ScoreBorder", scorePanel.transform);
            TMP_Text timer = CreateText("MatchTimer", scorePanel.transform, "08:42", TextWhite, 24, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(120f, 30f));
            Slider scoreBar = CreateSliderOnly("ScoreBar", scorePanel.transform, AccentOrange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(340f, 8f));
            TMP_Text scoreA = CreateText("ScoreTeamA", scorePanel.transform, "48", AccentOrange, 18, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(25f, 10f), new Vector2(50f, 25f));
            TMP_Text scoreB = CreateText("ScoreTeamB", scorePanel.transform, "39", new Color(0.2f, 0.6f, 1f), 18, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-25f, 10f), new Vector2(50f, 25f));

            // Top-Right (Minimap Container & Killfeed Stack)
            GameObject minimap = CreateImage("MinimapContainer", root.transform, PanelBg, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-125f, -125f), new Vector2(210f, 210f));
            CreateBorder("MinimapBorder", minimap.transform);

            GameObject killfeed = CreatePanel("KillfeedStack", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -320f), new Vector2(300f, 160f));
            var vlgKill = killfeed.AddComponent<VerticalLayoutGroup>();
            vlgKill.spacing = 6f;
            vlgKill.childControlWidth = true;
            vlgKill.childControlHeight = false;

            // Wire Serialized Properties
            var so = new SerializedObject(view);
            so.FindProperty("healthBar").objectReferenceValue = hpBar;
            so.FindProperty("healthText").objectReferenceValue = hpText;
            so.FindProperty("shieldBar").objectReferenceValue = shieldBar;
            so.FindProperty("shieldText").objectReferenceValue = shieldText;
            so.FindProperty("stanceIcon").objectReferenceValue = stance;
            so.FindProperty("weaponNameText").objectReferenceValue = weaponName;
            so.FindProperty("ammoCurrentText").objectReferenceValue = ammoCur;
            so.FindProperty("ammoReserveText").objectReferenceValue = ammoRes;
            so.FindProperty("grenadeCountText").objectReferenceValue = nadeCount;
            so.FindProperty("matchTimerText").objectReferenceValue = timer;
            so.FindProperty("scoreBar").objectReferenceValue = scoreBar;
            so.FindProperty("scoreTeamAText").objectReferenceValue = scoreA;
            so.FindProperty("scoreTeamBText").objectReferenceValue = scoreB;
            so.FindProperty("minimapContainer").objectReferenceValue = minimap.GetComponent<RectTransform>();
            so.FindProperty("killfeedContainer").objectReferenceValue = killfeed.GetComponent<RectTransform>();
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/HUDView.prefab");
        }

        private static void BuildLoadoutPrefab()
        {
            GameObject root = CreateRoot("LoadoutView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<LoadoutView>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Left: Weapon Preview & Stats Panel
            GameObject previewPanel = CreateImage("WeaponPreviewPanel", root.transform, PanelBg, new Vector2(0.28f, 0.5f), new Vector2(0.28f, 0.5f), Vector2.zero, new Vector2(580f, 740f));
            CreateBorder("PreviewBorder", previewPanel.transform);

            TMP_Text wName = CreateText("WeaponTitle", previewPanel.transform, "MK-18 CARBINE", TextWhite, 28, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -30f), new Vector2(-60f, 40f));
            TMP_Text wCat = CreateText("WeaponCategory", previewPanel.transform, "PRIMARY WEAPON // ASSAULT", AccentOrange, 16, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -70f), new Vector2(-60f, 25f));

            Image wImg = CreateImage("WeaponSilhouette", previewPanel.transform, new Color(0.14f, 0.17f, 0.22f, 1f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(460f, 200f)).GetComponent<Image>();

            // Stat Bars
            GameObject statsGroup = CreatePanel("StatsGroup", previewPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, new Vector2(-60f, 0f));
            var vlg = statsGroup.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            Slider dmg = CreateLabeledSlider("DamageBar", statsGroup.transform, "DAMAGE");
            Slider rof = CreateLabeledSlider("FireRateBar", statsGroup.transform, "FIRE RATE");
            Slider acc = CreateLabeledSlider("AccuracyBar", statsGroup.transform, "ACCURACY");
            Slider rng = CreateLabeledSlider("RangeBar", statsGroup.transform, "RANGE");
            Slider mob = CreateLabeledSlider("MobilityBar", statsGroup.transform, "MOBILITY");

            // Right: Equipment Grid
            GameObject gridPanel = CreateImage("EquipmentGridPanel", root.transform, PanelBg, new Vector2(0.72f, 0.5f), new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(620f, 740f));
            CreateBorder("GridBorder", gridPanel.transform);
            CreateText("GridTitle", gridPanel.transform, "ARMORY INVENTORY", TextWhite, 24, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -30f), new Vector2(-60f, 35f));

            GameObject gridScroll = CreatePanel("GridScrollContent", gridPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -15f), new Vector2(-60f, -180f));
            var glg = gridScroll.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(120f, 120f);
            glg.spacing = new Vector2(16f, 16f);

            for (int i = 0; i < 8; i++)
            {
                GameObject tile = CreateImage($"ItemTile_{i}", gridScroll.transform, new Color(0.12f, 0.14f, 0.18f, 1f), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 120f));
                CreateBorder("TileBorder", tile.transform, i == 0 ? AccentOrange : BorderColor);
            }

            Button equipBtn = CreateButton("EquipButton", gridPanel.transform, "EQUIP WEAPON", AccentOrange, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 50f), new Vector2(220f, 52f));
            Button backBtn = CreateButton("BackButton", gridPanel.transform, "BACK", PanelBg, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 50f), new Vector2(140f, 52f));

            // Wire Serialized Properties
            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("weaponPreviewImage").objectReferenceValue = wImg;
            so.FindProperty("weaponNameText").objectReferenceValue = wName;
            so.FindProperty("weaponCategoryText").objectReferenceValue = wCat;
            so.FindProperty("damageBar").objectReferenceValue = dmg;
            so.FindProperty("fireRateBar").objectReferenceValue = rof;
            so.FindProperty("accuracyBar").objectReferenceValue = acc;
            so.FindProperty("rangeBar").objectReferenceValue = rng;
            so.FindProperty("mobilityBar").objectReferenceValue = mob;
            so.FindProperty("equipmentGridContent").objectReferenceValue = gridScroll.transform;
            so.FindProperty("equipButton").objectReferenceValue = equipBtn;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/LoadoutView.prefab");
        }

        private static void BuildGameOverPrefab()
        {
            GameObject root = CreateRoot("GameOverView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<GameOverView>();

            CreateImage("Backdrop", root.transform, DarkBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Banner Title
            GameObject banner = CreateImage("ResultBanner", root.transform, PanelBg, new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(800f, 110f));
            CreateBorder("BannerBorder", banner.transform, AccentOrange);
            TMP_Text title = CreateText("Title", banner.transform, "VICTORY", AccentOrange, 48, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 60f));

            // Stats Card (500x380)
            GameObject statsCard = CreateImage("StatsCard", root.transform, PanelBg, new Vector2(0.32f, 0.45f), new Vector2(0.32f, 0.45f), Vector2.zero, new Vector2(460f, 400f));
            CreateBorder("StatsBorder", statsCard.transform);
            CreateText("StatsTitle", statsCard.transform, "COMBAT DEBRIEF", TextWhite, 20, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 30f));

            TMP_Text kills = CreateText("Kills", statsCard.transform, "KILLS: 18", TextWhite, 18, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -70f), new Vector2(-50f, 25f));
            TMP_Text deaths = CreateText("Deaths", statsCard.transform, "DEATHS: 4", TextWhite, 18, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -105f), new Vector2(-50f, 25f));
            TMP_Text assists = CreateText("Assists", statsCard.transform, "ASSISTS: 7", TextWhite, 18, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -140f), new Vector2(-50f, 25f));
            TMP_Text score = CreateText("Score", statsCard.transform, "SCORE: 3,420", AccentGold, 20, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -175f), new Vector2(-50f, 25f));
            TMP_Text hs = CreateText("Headshots", statsCard.transform, "HEADSHOT: 38%", TextMuted, 16, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -210f), new Vector2(-50f, 25f));

            TMP_Text xpText = CreateText("XPText", statsCard.transform, "+1,250 XP", AccentOrange, 22, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(25f, 80f), new Vector2(-50f, 30f));
            Slider xpBar = CreateSliderOnly("XPBar", statsCard.transform, AccentOrange, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(25f, 45f), new Vector2(-50f, 12f));
            TMP_Text lvl = CreateText("LvlText", statsCard.transform, "LEVEL 43", AccentGold, 16, TextAlignmentOptions.Right, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-25f, 80f), new Vector2(-50f, 30f));

            // MVP Podium Container (600x400)
            GameObject podium = CreateImage("PodiumContainer", root.transform, PanelBg, new Vector2(0.68f, 0.45f), new Vector2(0.68f, 0.45f), Vector2.zero, new Vector2(560f, 400f));
            CreateBorder("PodiumBorder", podium.transform);
            CreateText("PodiumTitle", podium.transform, "TOP OPERATORS", TextWhite, 20, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 30f));

            // Bottom Actions
            GameObject actions = CreatePanel("ActionButtons", root.transform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(500f, 60f));
            Button playAgain = CreateButton("PlayAgainButton", actions.transform, "PLAY AGAIN", AccentOrange, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(210f, 54f));
            Button returnLobby = CreateButton("ReturnLobbyButton", actions.transform, "RETURN TO LOBBY", PanelBg, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(210f, 54f));

            // Wire Serialized Properties
            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("resultTitleText").objectReferenceValue = title;
            so.FindProperty("killsText").objectReferenceValue = kills;
            so.FindProperty("deathsText").objectReferenceValue = deaths;
            so.FindProperty("assistsText").objectReferenceValue = assists;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("headshotPctText").objectReferenceValue = hs;
            so.FindProperty("xpGainedText").objectReferenceValue = xpText;
            so.FindProperty("xpProgressBar").objectReferenceValue = xpBar;
            so.FindProperty("levelText").objectReferenceValue = lvl;
            so.FindProperty("mvpPodiumContainer").objectReferenceValue = podium.transform;
            so.FindProperty("playAgainButton").objectReferenceValue = playAgain;
            so.FindProperty("returnToLobbyButton").objectReferenceValue = returnLobby;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/GameOverView.prefab");
        }

        // ========================================================================= Helpers
        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = CreatePanel(name, parent, anchorMin, anchorMax, pos, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreateBorder(string name, Transform parent, Color? color = null)
        {
            var border = CreatePanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = border.AddComponent<Image>();
            img.color = color ?? BorderColor;
            img.raycastTarget = false;
            return border;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, Color color, float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dim)
        {
            var go = CreatePanel(name, parent, anchorMin, anchorMax, pos, dim);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_defaultFont != null) tmp.font = _defaultFont;
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.characterSpacing = 2f;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color bgColor, Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pos = null, Vector2? dim = null)
        {
            var btnGo = CreateImage(name, parent, bgColor, anchorMin ?? Vector2.zero, anchorMax ?? Vector2.one, pos ?? Vector2.zero, dim ?? Vector2.zero);
            var btn = btnGo.AddComponent<Button>();

            // Add Tactical Button Effects
            var glowObj = CreateImage("HoverGlow", btnGo.transform, new Color(1f, 0.48f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            glowObj.GetComponent<Image>().raycastTarget = false;

            var fx = btnGo.AddComponent<RailboundButtonEffects>();
            var fxSo = new SerializedObject(fx);
            fxSo.FindProperty("glowBorderGraphic").objectReferenceValue = glowObj.GetComponent<Image>();
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            // Label
            CreateText("Label", btnGo.transform, label, TextWhite, 16, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return btn;
        }

        private static Slider CreateLabeledSlider(string name, Transform parent, string label)
        {
            GameObject row = CreatePanel(name, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 40f));
            CreateText("Label", row.transform, label, TextWhite, 15, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(200f, 30f));
            return CreateSliderOnly("Slider", row.transform, AccentOrange, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(280f, 12f));
        }

        private static Slider CreateSliderOnly(string name, Transform parent, Color fillColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject sliderGo = CreatePanel(name, parent, anchorMin, anchorMax, pos, size);
            var slider = sliderGo.AddComponent<Slider>();

            // Background track
            GameObject bg = CreateImage("Background", sliderGo.transform, new Color(0.12f, 0.15f, 0.18f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Fill Area & Fill
            GameObject fillArea = CreatePanel("Fill Area", sliderGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GameObject fill = CreateImage("Fill", fillArea.transform, fillColor, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = null;
            slider.interactable = true;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            return slider;
        }

        private static TMP_Dropdown CreateLabeledDropdown(string name, Transform parent, string label)
        {
            GameObject row = CreatePanel(name, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 40f));
            CreateText("Label", row.transform, label, TextWhite, 15, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(200f, 30f));
            return CreateDropdownOnly("Dropdown", row.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(280f, 36f));
        }

        private static TMP_Dropdown CreateDropdownOnly(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject dropGo = CreateImage(name, parent, PanelBg, anchorMin, anchorMax, pos, size);
            CreateBorder("DropBorder", dropGo.transform);
            var drop = dropGo.AddComponent<TMP_Dropdown>();
            var caption = CreateText("CaptionText", dropGo.transform, "DEFAULT", TextWhite, 14, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(15f, 0f), new Vector2(-30f, 0f));
            drop.captionText = caption;
            return drop;
        }

        private static Toggle CreateLabeledToggle(string name, Transform parent, string label)
        {
            GameObject row = CreatePanel(name, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 40f));
            CreateText("Label", row.transform, label, TextWhite, 15, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(200f, 30f));

            GameObject toggleGo = CreateImage("Toggle", row.transform, PanelBg, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-270f, 0f), new Vector2(30f, 30f));
            CreateBorder("ToggleBorder", toggleGo.transform);
            var toggle = toggleGo.AddComponent<Toggle>();

            GameObject checkmark = CreateImage("Checkmark", toggleGo.transform, AccentOrange, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero);
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = true;

            return toggle;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[RailboundUIBuilder] Saved: {path}");
        }
    }
}
