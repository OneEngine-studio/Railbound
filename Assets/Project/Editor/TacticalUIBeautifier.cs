using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;

namespace Railbound.Editor
{
    /// <summary>
    /// Master builder to completely redesign, polish, and populate all 6 target UI prefabs under Assets/Project/UI/:
    /// 1. ConfirmationModal.prefab
    /// 2. MatchmakingView.prefab
    /// 3. LoadoutView.prefab
    /// 4. PauseMenuModal.prefab
    /// 5. ScoreboardOverlay.prefab
    /// 6. LevelUpUnlockModal.prefab
    ///
    /// Follows Railbound's tactical sci-fi visual tokens:
    /// - Primary Accent: Ember Orange (#FF7A00 to #FF8C00)
    /// - Secondary Accent: Golden Yellow (#FFB800)
    /// - Backgrounds: Gunmetal Slate (#12151B to #161A22)
    /// - Borders: Crisp Bevel Slate (#2A313D)
    /// - Typography: High-contrast TextMeshPro Rajdhani / LiberationSans
    /// - Micro-interactions: RailboundButtonEffects with tactical audio
    /// </summary>
    public static class TacticalUIBeautifier
    {
        private static readonly Color DarkVignette = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        private static readonly Color PanelSlate   = new Color(0.07f, 0.08f, 0.11f, 0.98f);
        private static readonly Color CardSlate    = new Color(0.10f, 0.12f, 0.16f, 1.00f);
        private static readonly Color InputSlate   = new Color(0.04f, 0.05f, 0.07f, 1.00f);
        private static readonly Color EmberOrange  = new Color(1.00f, 0.48f, 0.00f, 1.00f); // #FF7A00
        private static readonly Color EmberGold    = new Color(1.00f, 0.72f, 0.00f, 1.00f); // #FFB800
        private static readonly Color CyanAccent   = new Color(0.20f, 0.70f, 1.00f, 1.00f);
        private static readonly Color TextWhite    = new Color(0.95f, 0.97f, 1.00f, 1.00f);
        private static readonly Color TextMuted    = new Color(0.56f, 0.61f, 0.68f, 1.00f);
        private static readonly Color BevelBorder  = new Color(0.165f, 0.192f, 0.239f, 1.00f); // #2A313D

        private static TMP_FontAsset _boldFont;
        private static TMP_FontAsset _medFont;
        private static AudioClip _selectSfx;
        private static AudioClip _deploySfx;
        private static AudioClip _openSfx;
        private static AudioClip _hoverSfx;

        [MenuItem("Railbound/Beautify All 6 Tactical UI Prefabs")]
        public static string BeautifyAllPrefabs()
        {
            LoadAssets();

            if (!Directory.Exists("Assets/Project/UI"))
                Directory.CreateDirectory("Assets/Project/UI");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BEAUTIFYING ALL 6 TACTICAL UI PREFABS ===");

            BuildConfirmationModal();
            sb.AppendLine("✅ [1/6] ConfirmationModal.prefab generated.");

            BuildMatchmakingView();
            sb.AppendLine("✅ [2/6] MatchmakingView.prefab generated.");

            BuildLoadoutView();
            sb.AppendLine("✅ [3/6] LoadoutView.prefab generated.");

            BuildPauseMenuModal();
            sb.AppendLine("✅ [4/6] PauseMenuModal.prefab generated.");

            BuildScoreboardOverlay();
            sb.AppendLine("✅ [5/6] ScoreboardOverlay.prefab generated.");

            BuildLevelUpUnlockModal();
            sb.AppendLine("✅ [6/6] LevelUpUnlockModal.prefab generated.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine("All 6 prefabs serialized successfully in Assets/Project/UI/!");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        private static void LoadAssets()
        {
            _boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Bold SDF.asset")
                     ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            _medFont  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-SemiBold SDF.asset")
                     ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            _selectSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Thin_Select_1.wav");
            _deploySfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Flangy_Thick_Generic_1.wav");
            _openSfx   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Movement_Open_1.wav");
            _hoverSfx  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Digital_Generic_1.wav");
        }

        // ================================================================== 1. ConfirmationModal
        public static void BuildConfirmationModal()
        {
            var root = CreateRoot("ConfirmationModal");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<ConfirmationModal>();

            CreateImage("Backdrop", root.transform, DarkVignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 320f));
            CreateBorder("Border", card.transform, BevelBorder);
            CreateImage("TopAccentRail", card.transform, EmberOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            // Warning Icon Badge
            var badge = CreateImage("WarningBadge", card.transform, new Color(1f, 0.48f, 0f, 0.15f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(48f, 48f));
            CreateBorder("BadgeBorder", badge.transform, EmberOrange);
            CreateText("WarningIcon", badge.transform, "!", EmberOrange, 28f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isBold: true);

            var title = CreateText("HeaderTitle", card.transform, "ACTION REQUIRED // CONFIRMATION", TextWhite, 18f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(500f, 26f), isBold: true);
            var msg = CreateText("MessageText", card.transform, "Are you sure you want to terminate the operation? Any unsaved match progress will be lost.", TextMuted, 13f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(460f, 60f));

            // Actions Row
            var cancelBtn = CreateTacticalButton("CancelButton", card.transform, "CANCEL", CardSlate, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 45f), new Vector2(200f, 46f), TextWhite, _selectSfx);
            var confirmBtn = CreateTacticalButton("ConfirmButton", card.transform, "CONFIRM ACTION", EmberOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 45f), new Vector2(200f, 46f), InputSlate, _deploySfx);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = msg;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/ConfirmationModal.prefab");
        }

        // ================================================================== 2. MatchmakingView
        public static void BuildMatchmakingView()
        {
            var root = CreateRoot("MatchmakingView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<MatchmakingView>();

            CreateImage("Backdrop", root.transform, DarkVignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Header Bar (1920x80)
            var header = CreateImage("HeaderBar", root.transform, PanelSlate, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 80f));
            CreateBorder("HeaderBorder", header.transform, BevelBorder);
            CreateImage("HeaderAccent", header.transform, EmberOrange, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f));

            var backBtn = CreateTacticalButton("BackButton", header.transform, "◄ DISPATCH HUB", CardSlate, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 42f), TextWhite, _selectSfx);
            var pName = CreateText("PlayerName", header.transform, "OPERATOR_01", TextWhite, 18f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(270f, 0f), new Vector2(200f, 26f), isBold: true);
            var pLvl = CreateText("PlayerLevel", header.transform, "LV.12", EmberGold, 14f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(430f, 0f), new Vector2(100f, 26f));
            var ping = CreateText("Ping", header.transform, "24 ms", CyanAccent, 14f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(100f, 26f));
            var region = CreateText("Region", header.transform, "ASIA PACIFIC // TOKYO", TextMuted, 14f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(180f, 26f));

            // Central Scanning Radar Hub (700 x 600)
            var hubCard = CreateImage("RadarHub", root.transform, PanelSlate, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(740f, 560f));
            CreateBorder("HubBorder", hubCard.transform, BevelBorder);
            CreateImage("HubAccent", hubCard.transform, EmberOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            // Radar Ring Outer
            var radarOuter = CreateImage("RadarOuterRing", hubCard.transform, new Color(0.12f, 0.15f, 0.20f, 0.6f), new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(220f, 220f));
            CreateBorder("RingBorder", radarOuter.transform, EmberOrange);

            var reticleObj = CreateImage("RadarReticle", radarOuter.transform, new Color(1f, 0.48f, 0f, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 180f));
            CreateBorder("CrossBorder", reticleObj.transform, EmberGold);
            var crossH = CreateImage("LineH", reticleObj.transform, EmberOrange, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 2f));
            var crossV = CreateImage("LineV", reticleObj.transform, EmberOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2f, 0f));

            // Status & Timers
            var statusText = CreateText("SearchingStatus", hubCard.transform, "SEARCHING FOR OPERATORS...", TextWhite, 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(500f, 30f), isBold: true);
            var timerText = CreateText("ElapsedTimer", hubCard.transform, "00:24", EmberOrange, 38f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f), Vector2.zero, new Vector2(300f, 44f), isBold: true);

            // Game Mode Card Preview
            var modeCard = CreateImage("ModePreviewCard", hubCard.transform, CardSlate, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(560f, 64f));
            CreateBorder("ModeBorder", modeCard.transform, BevelBorder);
            CreateText("ModeTitle", modeCard.transform, "SELECTED OPERATION: CONVOY RAID // 4v4", EmberGold, 14f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(300f, 24f), isBold: true);
            CreateText("MapInfo", modeCard.transform, "MAP: IRON CANYON", TextMuted, 13f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-100f, 0f), new Vector2(180f, 24f));

            // Bottom Cancel Button
            var cancelBtn = CreateTacticalButton("CancelMatchmakingButton", root.transform, "CANCEL MATCHMAKING", new Color(0.20f, 0.08f, 0.10f), new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(320f, 50f), new Color(1f, 0.45f, 0.45f), _selectSfx);

            // Hidden Rosters container for full matchmaker compatibility
            var alphaRoster = CreatePanel("AlphaRoster", root.transform, new Vector2(0.15f, 0.5f), new Vector2(0.15f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
            alphaRoster.SetActive(false);
            var bravoRoster = CreatePanel("BravoRoster", root.transform, new Vector2(0.85f, 0.5f), new Vector2(0.85f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
            bravoRoster.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("cancelMatchmakingButton").objectReferenceValue = cancelBtn;
            so.FindProperty("playerNameText").objectReferenceValue = pName;
            so.FindProperty("playerLevelText").objectReferenceValue = pLvl;
            so.FindProperty("pingText").objectReferenceValue = ping;
            so.FindProperty("regionText").objectReferenceValue = region;
            so.FindProperty("radarReticle").objectReferenceValue = reticleObj.GetComponent<RectTransform>();
            so.FindProperty("searchStatusText").objectReferenceValue = statusText;
            so.FindProperty("elapsedTimeText").objectReferenceValue = timerText;
            so.FindProperty("teamAlphaRosterContainer").objectReferenceValue = alphaRoster.transform;
            so.FindProperty("teamBravoRosterContainer").objectReferenceValue = bravoRoster.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/MatchmakingView.prefab");
        }

        // ================================================================== 3. LoadoutView
        public static void BuildLoadoutView()
        {
            var root = CreateRoot("LoadoutView");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var view = root.AddComponent<LoadoutView>();

            CreateImage("Backdrop", root.transform, DarkVignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Header (1920 x 80)
            var header = CreateImage("HeaderBar", root.transform, PanelSlate, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 80f));
            CreateBorder("HeaderBorder", header.transform, BevelBorder);
            CreateImage("HeaderAccent", header.transform, EmberOrange, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f));

            var backBtn = CreateTacticalButton("BackButton", header.transform, "◄ BACK TO LOBBY", CardSlate, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(170f, 42f), TextWhite, _selectSfx);
            CreateText("HeaderTitle", header.transform, "ARSENAL // TACTICAL LOADOUT", TextWhite, 22f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(400f, 30f), isBold: true);

            // Slot Selectors on top header right
            var slotBar = CreatePanel("SlotBar", header.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-360f, 0f), new Vector2(680f, 44f));
            var primBtn = CreateTacticalButton("PrimarySlotButton", slotBar.transform, "PRIMARY", EmberOrange, new Vector2(0.18f, 0.5f), new Vector2(0.18f, 0.5f), Vector2.zero, new Vector2(150f, 40f), InputSlate, _selectSfx);
            var secBtn = CreateTacticalButton("SecondarySlotButton", slotBar.transform, "SIDEARM", CardSlate, new Vector2(0.50f, 0.5f), new Vector2(0.50f, 0.5f), Vector2.zero, new Vector2(150f, 40f), TextWhite, _selectSfx);
            var tactBtn = CreateTacticalButton("TacticalSlotButton", slotBar.transform, "GADGETS", CardSlate, new Vector2(0.82f, 0.5f), new Vector2(0.82f, 0.5f), Vector2.zero, new Vector2(150f, 40f), TextWhite, _selectSfx);

            // Left Column: Weapon Grid / Cards (540 x 860)
            var leftCol = CreateImage("WeaponGridPanel", root.transform, PanelSlate, new Vector2(0.23f, 0.46f), new Vector2(0.23f, 0.46f), Vector2.zero, new Vector2(500f, 800f));
            CreateBorder("LeftBorder", leftCol.transform, BevelBorder);
            CreateText("GridHeader", leftCol.transform, "OPERATOR WEAPONS // SELECT", TextWhite, 16f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -25f), new Vector2(-50f, 30f), isBold: true);

            var gridScroll = CreatePanel("EquipmentGridContent", leftCol.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(-40f, -80f));
            var vlg = gridScroll.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Populate 4 detailed Weapon Cards
            string[] wNames = { "MK-18 CARBINE", "SCAR-H HEAVY", "VECTOR-45 SMG", "M870 BREACHER" };
            string[] wTiers = { "TIER IV // LEGENDARY", "TIER III // RARE", "TIER II // MILITARY", "TIER I // STANDARD" };
            Color[] tierColors = { EmberOrange, EmberGold, CyanAccent, TextMuted };

            for (int i = 0; i < 4; i++)
            {
                var card = CreateImage($"WeaponCard_{i}", gridScroll.transform, CardSlate, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 110f));
                card.AddComponent<LayoutElement>().minHeight = 110f;
                CreateBorder("CardBorder", card.transform, i == 0 ? EmberOrange : BevelBorder);
                CreateImage("TierStripe", card.transform, tierColors[i], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), new Vector2(6f, 0f));
                CreateText("WName", card.transform, wNames[i], TextWhite, 16f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -22f), new Vector2(-40f, 24f), isBold: true);
                CreateText("WTier", card.transform, wTiers[i], tierColors[i], 11f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -44f), new Vector2(-40f, 20f));
                CreateText("WDesc", card.transform, "CALIBER: 5.56x45mm NATO • RATE: 750 RPM", TextMuted, 10f, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(25f, 20f), new Vector2(-40f, 20f));
                var btn = card.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var eff = card.AddComponent<RailboundButtonEffects>();
                eff.SetAudioClips(_selectSfx, _hoverSfx);
            }

            // Right Column: Weapon Detail Inspector (900 x 800)
            var rightCol = CreateImage("WeaponDetailInspector", root.transform, PanelSlate, new Vector2(0.68f, 0.46f), new Vector2(0.68f, 0.46f), Vector2.zero, new Vector2(900f, 800f));
            CreateBorder("RightBorder", rightCol.transform, BevelBorder);
            CreateImage("RightAccent", rightCol.transform, EmberOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            var wTitle = CreateText("WeaponTitle", rightCol.transform, "MK-18 CARBINE", TextWhite, 32f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, -40f), new Vector2(400f, 36f), isBold: true);
            var wCategory = CreateText("WeaponCategory", rightCol.transform, "PRIMARY WEAPON // ASSAULT RIFLE", EmberOrange, 15f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, -72f), new Vector2(400f, 24f));

            // Weapon Preview Frame
            var previewFrame = CreateImage("WeaponPreviewFrame", rightCol.transform, InputSlate, new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(820f, 240f));
            CreateBorder("FrameBorder", previewFrame.transform, BevelBorder);
            var wImg = CreateImage("WeaponPreviewImage", previewFrame.transform, new Color(1f, 0.48f, 0f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 180f)).GetComponent<Image>();
            CreateText("Holotag", previewFrame.transform, "3D TACTICAL PROJECTION // MK-18", new Color(0.2f, 0.7f, 1f, 0.7f), 10f, TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-120f, 15f), new Vector2(220f, 20f));

            // Stat Bars (Fire Rate, Damage, Accuracy, Range, Mobility)
            var statsPanel = CreatePanel("StatBarsGroup", rightCol.transform, new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(820f, 220f));
            var dmg = CreateTacticalStatBar("DamageBar", statsPanel.transform, "DAMAGE", 0.72f, EmberOrange, 80f);
            var rof = CreateTacticalStatBar("FireRateBar", statsPanel.transform, "FIRE RATE", 0.65f, EmberGold, 40f);
            var acc = CreateTacticalStatBar("AccuracyBar", statsPanel.transform, "ACCURACY", 0.80f, CyanAccent, 0f);
            var rng = CreateTacticalStatBar("RangeBar", statsPanel.transform, "RANGE", 0.60f, EmberOrange, -40f);
            var mob = CreateTacticalStatBar("MobilityBar", statsPanel.transform, "MOBILITY", 0.75f, EmberGold, -80f);

            // Action Buttons Row
            var customizeBtn = CreateTacticalButton("CustomizeButton", rightCol.transform, "CUSTOMIZE ATTACHMENTS", CardSlate, new Vector2(0.35f, 0.08f), new Vector2(0.35f, 0.08f), Vector2.zero, new Vector2(280f, 52f), TextWhite, _selectSfx);
            var equipBtn = CreateTacticalButton("EquipButton", rightCol.transform, "EQUIP WEAPON", EmberOrange, new Vector2(0.75f, 0.08f), new Vector2(0.75f, 0.08f), Vector2.zero, new Vector2(280f, 52f), InputSlate, _deploySfx);

            var so = new SerializedObject(view);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("primarySlotButton").objectReferenceValue = primBtn;
            so.FindProperty("secondarySlotButton").objectReferenceValue = secBtn;
            so.FindProperty("tacticalSlotButton").objectReferenceValue = tactBtn;
            so.FindProperty("weaponPreviewImage").objectReferenceValue = wImg;
            so.FindProperty("weaponNameText").objectReferenceValue = wTitle;
            so.FindProperty("weaponCategoryText").objectReferenceValue = wCategory;
            so.FindProperty("damageBar").objectReferenceValue = dmg;
            so.FindProperty("fireRateBar").objectReferenceValue = rof;
            so.FindProperty("accuracyBar").objectReferenceValue = acc;
            so.FindProperty("rangeBar").objectReferenceValue = rng;
            so.FindProperty("mobilityBar").objectReferenceValue = mob;
            so.FindProperty("equipmentGridContent").objectReferenceValue = gridScroll.transform;
            so.FindProperty("equipButton").objectReferenceValue = equipBtn;
            so.FindProperty("customizeButton").objectReferenceValue = customizeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/LoadoutView.prefab");
        }

        private static Slider CreateTacticalStatBar(string name, Transform parent, string label, float initialValue, Color fillColor, float yPos)
        {
            var row = CreatePanel(name + "_Row", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), new Vector2(800f, 30f));
            CreateText("Label", row.transform, label, TextMuted, 12f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(130f, 22f), isBold: true);

            var sliderObj = CreatePanel("Slider", row.transform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), Vector2.zero, new Vector2(500f, 10f));
            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;
            slider.interactable = false;

            var bg = CreateImage("Background", sliderObj.transform, InputSlate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateBorder("BarBorder", bg.transform, BevelBorder);

            var fillArea = CreatePanel("Fill Area", sliderObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = CreateImage("Fill", fillArea.transform, fillColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill.GetComponent<RectTransform>();

            CreateText("ValText", row.transform, $"{(int)(initialValue * 100)}%", TextWhite, 12f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(40f, 22f));

            return slider;
        }

        // ================================================================== 4. PauseMenuModal
        public static void BuildPauseMenuModal()
        {
            var root = CreateRoot("PauseMenuModal");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<PauseMenuModal>();

            CreateImage("FrostedBackdrop", root.transform, DarkVignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 520f));
            CreateBorder("Border", card.transform, BevelBorder);
            CreateImage("TopAccentRail", card.transform, EmberOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            CreateText("Header", card.transform, "TACTICAL PAUSE // SYSTEM MENU", TextWhite, 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(440f, 30f), isBold: true);
            CreateText("SubHeader", card.transform, "MISSION IN PROGRESS • COMBAT ACTIVE", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(440f, 20f));

            var stack = CreatePanel("ButtonStack", card.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(420f, 340f));

            var resumeBtn = CreateTacticalButton("ResumeButton", stack.transform, "RESUME MISSION", EmberOrange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(380f, 52f), InputSlate, _selectSfx);
            var settingsBtn = CreateTacticalButton("SettingsButton", stack.transform, "SETTINGS", CardSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 35f), new Vector2(380f, 48f), TextWhite, _openSfx);
            var restartBtn = CreateTacticalButton("RestartButton", stack.transform, "REDEPLOY / SUICIDE", CardSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(380f, 48f), TextWhite, _selectSfx);
            var leaveBtn = CreateTacticalButton("LeaveMatchButton", stack.transform, "ABANDON OPERATION", new Color(0.24f, 0.08f, 0.10f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(380f, 48f), new Color(1f, 0.45f, 0.45f), _deploySfx);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("restartButton").objectReferenceValue = restartBtn;
            so.FindProperty("leaveMatchButton").objectReferenceValue = leaveBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/PauseMenuModal.prefab");
        }

        // ================================================================== 5. ScoreboardOverlay
        public static void BuildScoreboardOverlay()
        {
            var root = CreateRoot("ScoreboardOverlay");
            var cg = root.AddComponent<CanvasGroup>();
            var controller = root.AddComponent<ScoreboardOverlay>();

            CreateImage("Backdrop", root.transform, new Color(0.03f, 0.04f, 0.06f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var board = CreateImage("BoardCard", root.transform, PanelSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 720f));
            CreateBorder("BoardBorder", board.transform, BevelBorder);
            CreateImage("TopAccentRail", board.transform, EmberOrange, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            // Header Row
            CreateText("HeaderTitle", board.transform, "RAILBOUND // OPERATION COMBAT RECORD", TextWhite, 18f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, -35f), new Vector2(450f, 30f), isBold: true);
            var mode = CreateText("ModeText", board.transform, "CONVOY RAID // IRON CANYON", EmberGold, 13f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, -60f), new Vector2(400f, 20f));
            var timer = CreateText("TimerText", board.transform, "TIME REMAINING: 07:45", TextMuted, 13f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260f, -35f), new Vector2(400f, 30f));

            var alphaScore = CreateText("AlphaScore", board.transform, "ALPHA SQUAD: 38", CyanAccent, 16f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, -95f), new Vector2(300f, 26f), isBold: true);
            var bravoScore = CreateText("BravoScore", board.transform, "BRAVO SQUAD: 32", EmberOrange, 16f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260f, -95f), new Vector2(300f, 26f), isBold: true);

            // Two Column Split
            var alphaRoster = CreatePanel("AlphaRoster", board.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(25f, -80f), new Vector2(-50f, -230f));
            CreateScoreboardColumnHeader(alphaRoster.transform, "TEAM ALPHA // DEFENDERS", CyanAccent);
            PopulateScoreboardRows(alphaRoster.transform, 4, "Operative_A", isAlpha: true);

            var bravoRoster = CreatePanel("BravoRoster", board.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(25f, -80f), new Vector2(-50f, -230f));
            CreateScoreboardColumnHeader(bravoRoster.transform, "TEAM BRAVO // RAIDERS", EmberOrange);
            PopulateScoreboardRows(bravoRoster.transform, 4, "Operative_B", isAlpha: false);

            CreateText("FooterHint", board.transform, "HOLD [TAB] TO DISPLAY COMBAT RECORD • RELEASE TO DISMISS", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(700f, 20f));

            var so = new SerializedObject(controller);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("matchModeText").objectReferenceValue = mode;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("alphaScoreText").objectReferenceValue = alphaScore;
            so.FindProperty("bravoScoreText").objectReferenceValue = bravoScore;
            so.FindProperty("alphaRosterContainer").objectReferenceValue = alphaRoster.transform;
            so.FindProperty("bravoRosterContainer").objectReferenceValue = bravoRoster.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/ScoreboardOverlay.prefab");
        }

        private static void CreateScoreboardColumnHeader(Transform parent, string title, Color teamColor)
        {
            var header = CreateImage("HeaderRow", parent, CardSlate, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, 36f));
            CreateBorder("HBorder", header.transform, BevelBorder);
            CreateText("TeamTitle", header.transform, title, teamColor, 13f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(180f, 24f), isBold: true);

            CreateText("Role", header.transform, "ROLE", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.38f, 0.5f), new Vector2(0.38f, 0.5f), Vector2.zero, new Vector2(70f, 24f));
            CreateText("Kills", header.transform, "K", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.50f, 0.5f), new Vector2(0.50f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
            CreateText("Deaths", header.transform, "D", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.60f, 0.5f), new Vector2(0.60f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
            CreateText("Assists", header.transform, "A", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.70f, 0.5f), new Vector2(0.70f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
            CreateText("Damage", header.transform, "DMG", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.82f, 0.5f), new Vector2(0.82f, 0.5f), Vector2.zero, new Vector2(60f, 24f));
            CreateText("Ping", header.transform, "PING", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.93f, 0.5f), new Vector2(0.93f, 0.5f), Vector2.zero, new Vector2(50f, 24f));
        }

        private static void PopulateScoreboardRows(Transform parent, int count, string prefix, bool isAlpha)
        {
            string[] roles = { "ASSAULT", "SNIPER", "HEAVY", "SUPPORT" };
            for (int i = 0; i < count; i++)
            {
                float y = -66f - (i * 44f);
                bool isLocalPlayer = isAlpha && i == 0;
                Color rowBg = isLocalPlayer ? new Color(1f, 0.48f, 0f, 0.12f) : (i % 2 == 0 ? CardSlate : InputSlate);
                var row = CreateImage($"Row_{i}", parent, rowBg, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(0f, 40f));
                CreateBorder("RowBorder", row.transform, isLocalPlayer ? EmberOrange : BevelBorder);

                string name = isLocalPlayer ? "YOU [Spectre_07]" : $"{prefix}_{i + 1}";
                CreateText("Name", row.transform, name, isLocalPlayer ? EmberOrange : TextWhite, 12f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(160f, 24f), isBold: isLocalPlayer);
                CreateText("Role", row.transform, roles[i % roles.Length], TextMuted, 10f, TextAlignmentOptions.Center, new Vector2(0.38f, 0.5f), new Vector2(0.38f, 0.5f), Vector2.zero, new Vector2(70f, 24f));
                CreateText("Kills", row.transform, $"{14 - i * 3}", TextWhite, 11f, TextAlignmentOptions.Center, new Vector2(0.50f, 0.5f), new Vector2(0.50f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
                CreateText("Deaths", row.transform, $"{2 + i * 2}", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.60f, 0.5f), new Vector2(0.60f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
                CreateText("Assists", row.transform, $"{5 + i}", TextMuted, 11f, TextAlignmentOptions.Center, new Vector2(0.70f, 0.5f), new Vector2(0.70f, 0.5f), Vector2.zero, new Vector2(40f, 24f));
                CreateText("Damage", row.transform, $"{2450 - i * 420}", EmberGold, 11f, TextAlignmentOptions.Center, new Vector2(0.82f, 0.5f), new Vector2(0.82f, 0.5f), Vector2.zero, new Vector2(60f, 24f));
                CreateText("Ping", row.transform, $"{22 + i * 6}ms", new Color(0.25f, 0.85f, 0.45f), 11f, TextAlignmentOptions.Center, new Vector2(0.93f, 0.5f), new Vector2(0.93f, 0.5f), Vector2.zero, new Vector2(50f, 24f));
            }
        }

        // ================================================================== 6. LevelUpUnlockModal
        public static void BuildLevelUpUnlockModal()
        {
            var root = CreateRoot("LevelUpUnlockModal");
            var cg = root.AddComponent<CanvasGroup>();
            var transition = root.AddComponent<UIScreenTransition>();
            var controller = root.AddComponent<LevelUpUnlockModal>();

            CreateImage("Backdrop", root.transform, DarkVignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = CreateImage("CenterCard", root.transform, PanelSlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 540f));
            CreateBorder("Border", card.transform, BevelBorder);
            CreateImage("TopAccentRail", card.transform, EmberGold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 4f));

            // Level Badge
            var badge = CreateImage("LevelBadge", card.transform, CardSlate, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(64f, 64f));
            CreateBorder("BadgeBorder", badge.transform, EmberGold);
            CreateText("BadgeText", badge.transform, "12", EmberGold, 26f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isBold: true);

            var title = CreateText("LevelTitleText", card.transform, "LEVEL UP // OPERATOR PROMOTED", TextWhite, 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(500f, 28f), isBold: true);
            var category = CreateText("RewardCategoryText", card.transform, "TACTICAL REWARD UNLOCKED // WEAPON BLUEPRINT", EmberOrange, 12f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(500f, 22f));

            // Center Showcase Card
            var showcase = CreateImage("ShowcaseCard", card.transform, InputSlate, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(480f, 220f));
            CreateBorder("ShowcaseBorder", showcase.transform, EmberOrange);

            var rName = CreateText("RewardNameText", showcase.transform, "MAG-PULSE RIFLE MK.II", TextWhite, 18f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 28f), isBold: true);
            var rIcon = CreateImage("RewardIconImage", showcase.transform, new Color(1f, 0.48f, 0f, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 100f)).GetComponent<Image>();
            CreateText("RarityBadge", showcase.transform, "TIER IV // LEGENDARY PROTOTYPE", EmberGold, 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 20f), isBold: true);

            // Bottom CTA Button
            var claimBtn = CreateTacticalButton("ClaimButton", card.transform, "CLAIM & CONTINUE", EmberOrange, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(340f, 50f), InputSlate, _deploySfx);

            var so = new SerializedObject(controller);
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("levelTitleText").objectReferenceValue = title;
            so.FindProperty("rewardCategoryText").objectReferenceValue = category;
            so.FindProperty("rewardNameText").objectReferenceValue = rName;
            so.FindProperty("rewardIconImage").objectReferenceValue = rIcon;
            so.FindProperty("claimButton").objectReferenceValue = claimBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, "Assets/Project/UI/LevelUpUnlockModal.prefab");
        }

        // ================================================================== Utilities
        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private static void CreateBorder(string name, Transform parent, Color borderColor)
        {
            var border = CreateImage(name, parent, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.GetComponent<Image>().raycastTarget = false;
            var outline = border.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, Color color, float fontSize, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, bool isBold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (isBold && _boldFont != null) tmp.font = _boldFont;
            else if (_medFont != null) tmp.font = _medFont;

            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateTacticalButton(string name, Transform parent, string label, Color bgCol, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color textCol, AudioClip clickClip)
        {
            var go = CreateImage(name, parent, bgCol, anchorMin, anchorMax, anchoredPos, sizeDelta);
            CreateBorder("Border", go.transform, BevelBorder);

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            CreateText("Label", go.transform, label, textCol, 14f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isBold: true);

            var effects = go.AddComponent<RailboundButtonEffects>();
            effects.SetAudioClips(clickClip, _hoverSfx);

            return btn;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
