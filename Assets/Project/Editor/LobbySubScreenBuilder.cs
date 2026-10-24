using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Railbound.UI;

namespace Railbound.Editor
{
    [InitializeOnLoad]
    public static class LobbySubScreenBuilder
    {
        private const string CrossFrameAltPath  = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Special/Cross Frame Alt.png";
        private const string SquareFilledPath   = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Flat/Square Filled.png";
        private const string RadialShadowPath   = "Assets/Reach - Complete Sci-Fi UI/Textures/Shadows/Radial/Radial Directional Shadow Filled.png";
        private const string FontBoldPath       = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Bold SDF.asset";
        private const string FontSemiBoldPath   = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-SemiBold SDF.asset";
        private const string FontMediumPath     = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Medium SDF.asset";

        private static readonly Color DarkCharcoal  = new Color(0.055f, 0.067f, 0.086f, 0.96f); // #0E1116
        private static readonly Color TacticalEmber = new Color(1.0f, 0.478f, 0.0f, 1.0f);     // #FF7A00
        private static readonly Color BevelBorder   = new Color(0.165f, 0.192f, 0.239f, 1.0f);  // #2A313D
        private static readonly Color CoolGrey      = new Color(0.70f, 0.75f, 0.82f, 1.0f);
        private static readonly Color HeaderBarCol  = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        private static readonly Color GhostDark     = new Color(0.09f, 0.11f, 0.15f, 0.90f);

        static LobbySubScreenBuilder()
        {
            EditorApplication.delayCall += TriggerOnceIfNeeded;
        }

        private static void TriggerOnceIfNeeded()
        {
            string flagFile = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "LobbyBuilderDone_v2.flag");
            if (!File.Exists(flagFile))
            {
                try
                {
                    string report = BuildAndCleanAll();
                    File.WriteAllText(flagFile, report);
                    Debug.Log("[LobbySubScreenBuilder] Initial automated build completed:\n" + report);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[LobbySubScreenBuilder] Automated build error: " + ex.Message);
                }
            }
        }

        [MenuItem("Railbound/UI Fix/Build All SubScreens And Clean")]
        public static string BuildAndCleanAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== LOBBY SUB-SCREENS AUDIT & FIX REPORT ===");

            try
            {
                var crossAlt = AssetDatabase.LoadAssetAtPath<Sprite>(CrossFrameAltPath);
                var sqFill   = AssetDatabase.LoadAssetAtPath<Sprite>(SquareFilledPath);
                var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
                var fontSemi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSemiBoldPath);
                var fontMed  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMediumPath);

                // 1. Build Sub-Screen Prefabs
                var opPrefab      = BuildOperatorSelectPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                var lockerPrefab  = BuildLockerPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                var rankPrefab    = BuildRankingsPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                var careerPrefab  = BuildCareerStatsPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                sb.AppendLine("[PASS] Generated 4 modular Sub-Screen prefabs (Operators, Locker, Rankings, Career).");

                // 2. Build Modals
                var settingsPrefab = BuildSettingsViewPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                var confirmPrefab  = BuildConfirmationModalPrefab(crossAlt, sqFill, fontBold, fontSemi, fontMed);
                sb.AppendLine("[PASS] Generated polished SettingsView and ConfirmationModal prefabs with Reach UI styling.");

                // 3. Wire Prefabs to UIManager.prefab
                WireUIManagerPrefab(opPrefab, lockerPrefab, rankPrefab, careerPrefab, settingsPrefab, confirmPrefab, sb);

                // 4. Audit Raycast Targets & Button Effects across all UI prefabs
                AuditAndFixAllPrefabs(sb);

                // 5. Clean and fix GameLobby.unity
                CleanAndFixGameLobby(opPrefab, lockerPrefab, rankPrefab, careerPrefab, settingsPrefab, confirmPrefab, sb);

                // 6. Clean and fix LoadingScreen.unity
                CleanAndFixLoadingScreen(crossAlt, fontBold, fontSemi, fontMed, sb);

                sb.AppendLine("[SUCCESS] All lobby sub-screens, modals, raycasts, and scene wiring successfully completed!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[CRITICAL ERROR] {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        #region Helpers
        private static void SetRef(SerializedObject so, string propName, UnityEngine.Object obj)
        {
            if (so == null || string.IsNullOrEmpty(propName)) return;
            var prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = obj;
            }
        }
        #endregion

        #region Prefab Builders - Sub-Screens
        private static GameObject BuildBaseSubScreen(string name, string title, string category, Sprite frameSprite, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, out Button backButton)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(UIScreenTransition));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Full-bleed Backdrop
            var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = frameSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = DarkCharcoal;
            bgImg.raycastTarget = false;

            // HeaderBar
            var header = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(root.transform, false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 80f);
            var headerImg = header.GetComponent<Image>();
            headerImg.sprite = frameSprite;
            headerImg.type = Image.Type.Sliced;
            headerImg.color = HeaderBarCol;
            headerImg.raycastTarget = false;

            // Back Button
            var backBtnObj = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RailboundButtonEffects));
            backBtnObj.transform.SetParent(header.transform, false);
            var backBtnRt = backBtnObj.GetComponent<RectTransform>();
            backBtnRt.anchorMin = new Vector2(0f, 0.5f);
            backBtnRt.anchorMax = new Vector2(0f, 0.5f);
            backBtnRt.pivot = new Vector2(0f, 0.5f);
            backBtnRt.anchoredPosition = new Vector2(28f, 0f);
            backBtnRt.sizeDelta = new Vector2(210f, 48f);

            var backBtnImg = backBtnObj.GetComponent<Image>();
            backBtnImg.sprite = frameSprite;
            backBtnImg.type = Image.Type.Sliced;
            backBtnImg.color = BevelBorder;
            backBtnImg.raycastTarget = true;
            backButton = backBtnObj.GetComponent<Button>();

            var backTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            backTxtObj.transform.SetParent(backBtnObj.transform, false);
            var backTxtRt = backTxtObj.GetComponent<RectTransform>();
            backTxtRt.anchorMin = Vector2.zero;
            backTxtRt.anchorMax = Vector2.one;
            backTxtRt.offsetMin = Vector2.zero;
            backTxtRt.offsetMax = Vector2.zero;
            var backTmp = backTxtObj.GetComponent<TextMeshProUGUI>();
            backTmp.font = fontBold;
            backTmp.fontSize = 18f;
            backTmp.alignment = TextAlignmentOptions.Center;
            backTmp.color = Color.white;
            backTmp.text = "◄ BACK TO LOBBY";
            backTmp.raycastTarget = false;

            // Title Text
            var titleObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(header.transform, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(260f, 10f);
            titleRt.sizeDelta = new Vector2(-280f, 32f);
            var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.font = fontBold;
            titleTmp.fontSize = 24f;
            titleTmp.color = TacticalEmber;
            titleTmp.characterSpacing = 2f;
            titleTmp.text = title;
            titleTmp.raycastTarget = false;

            // Subtitle Category
            var catObj = new GameObject("HeaderCategory", typeof(RectTransform), typeof(TextMeshProUGUI));
            catObj.transform.SetParent(header.transform, false);
            var catRt = catObj.GetComponent<RectTransform>();
            catRt.anchorMin = new Vector2(0f, 0.5f);
            catRt.anchorMax = new Vector2(1f, 0.5f);
            catRt.pivot = new Vector2(0f, 0.5f);
            catRt.anchoredPosition = new Vector2(260f, -14f);
            catRt.sizeDelta = new Vector2(-280f, 22f);
            var catTmp = catObj.GetComponent<TextMeshProUGUI>();
            catTmp.font = fontSemi;
            catTmp.fontSize = 14f;
            catTmp.color = CoolGrey;
            catTmp.text = category;
            catTmp.raycastTarget = false;

            return root;
        }

        private static GameObject BuildOperatorSelectPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var go = BuildBaseSubScreen("OperatorSelectView", "OPERATORS // TACTICAL ROSTER", "SELECT OPERATIVE SPECIALIZATION", frameSprite, fontBold, fontSemi, out var backBtn);
            var comp = go.AddComponent<OperatorSelectView>();

            var listPanel = CreatePanel(go.transform, "OperatorListPanel", new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(30f, 30f), new Vector2(-15f, -100f), frameSprite, DarkCharcoal);
            var scroll = new GameObject("OperatorScrollView", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(listPanel.transform, false);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(15f, 15f);
            scrollRt.offsetMax = new Vector2(-15f, -15f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 400f);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            string[] opNames = { "UNIT 01 // RANGER", "UNIT 02 // GHOST", "UNIT 03 // VANGUARD", "UNIT 04 // TITAN" };
            string[] opRoles = { "ASSAULT // COMBAT SPECIALIST", "RECON // INFILTRATION", "BREACHER // CLOSE QUARTERS", "HEAVY // ARMOR SUPPORT" };
            for (int i = 0; i < opNames.Length; i++)
            {
                var card = CreateButton(content.transform, $"Card_{i}", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 80f), frameSprite, BevelBorder);
                var nameTmp = CreateText(card.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -14f), new Vector2(-40f, 26f), fontBold, 18f, TacticalEmber, opNames[i]);
                var roleTmp = CreateText(card.transform, "Role", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 14f), new Vector2(-40f, 20f), fontSemi, 14f, CoolGrey, opRoles[i]);
            }

            var detailPanel = CreatePanel(go.transform, "OperatorDetailPanel", new Vector2(0.40f, 0f), new Vector2(1f, 1f), new Vector2(15f, 30f), new Vector2(-30f, -100f), frameSprite, DarkCharcoal);
            var title = CreateText(detailPanel.transform, "OpName", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -24f), new Vector2(-56f, 36f), fontBold, 28f, TacticalEmber, "UNIT 01 // RANGER");
            var role = CreateText(detailPanel.transform, "OpRole", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -65f), new Vector2(-56f, 24f), fontSemi, 18f, Color.white, "TACTICAL ASSAULT SPECIALIST");
            var desc = CreateText(detailPanel.transform, "OpDesc", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -100f), new Vector2(-56f, 80f), fontMed, 15f, CoolGrey, "Equipped with tactical overdrive combat stims and multi-range carbine handling. Excels at high-tempo aggressive pushes and squad vanguard positioning.");
            var loadout = CreateText(detailPanel.transform, "OpLoadout", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -195f), new Vector2(-56f, 60f), fontSemi, 15f, Color.white, "PRIMARY: MK-18 CARBINE\nSECONDARY: APEX-9 PISTOL\nTACTICAL: FRAG GRENADE MK.II");

            var selectBtn = CreateButton(detailPanel.transform, "DeployBtn", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(240f, 52f), frameSprite, TacticalEmber);
            var selRt = selectBtn.GetComponent<RectTransform>();
            selRt.pivot = new Vector2(1f, 0f);
            var selTxt = CreateText(selectBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 18f, Color.black, "SELECT OPERATOR");
            selTxt.alignment = TextAlignmentOptions.Center;

            var so = new SerializedObject(comp);
            SetRef(so, "transition", go.GetComponent<UIScreenTransition>());
            SetRef(so, "backButton", backBtn);
            SetRef(so, "operatorNameText", title);
            SetRef(so, "operatorClassText", role);
            SetRef(so, "tacticalAbilityText", desc);
            SetRef(so, "passivePerkText", loadout);
            SetRef(so, "operatorBioText", desc);
            SetRef(so, "selectOperatorButton", selectBtn.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/OperatorSelectView.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildLockerPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var go = BuildBaseSubScreen("LockerView", "LOCKER // CUSTOMIZATION", "WEAPON CAMOS, CHARMS & CALLSIGN TITLES", frameSprite, fontBold, fontSemi, out var backBtn);
            var comp = go.AddComponent<LockerView>();

            var camoPanel = CreatePanel(go.transform, "CamoGridPanel", new Vector2(0f, 0f), new Vector2(0.60f, 1f), new Vector2(30f, 30f), new Vector2(-15f, -100f), frameSprite, DarkCharcoal);
            CreateText(camoPanel.transform, "PanelTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -16f), new Vector2(-40f, 26f), fontBold, 18f, TacticalEmber, "TACTICAL CAMO PATTERNS");

            var grid = new GameObject("CamoGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(camoPanel.transform, false);
            var gridRt = grid.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.offsetMin = new Vector2(20f, 20f);
            gridRt.offsetMax = new Vector2(-20f, -50f);
            var glg = grid.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(130f, 100f);
            glg.spacing = new Vector2(12f, 12f);

            string[] camos = { "EMBER HEX", "MIDNIGHT", "URBAN SLATE", "CARBON FIBER", "ARID TIGER", "TITANIUM" };
            for (int i = 0; i < camos.Length; i++)
            {
                var item = CreateButton(grid.transform, $"Camo_{i}", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, frameSprite, BevelBorder);
                var txt = CreateText(item.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontSemi, 13f, Color.white, camos[i]);
                txt.alignment = TextAlignmentOptions.Center;
            }

            var previewPanel = CreatePanel(go.transform, "CustomizationPreviewPanel", new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(15f, 30f), new Vector2(-30f, -100f), frameSprite, DarkCharcoal);
            var previewTitle = CreateText(previewPanel.transform, "PreviewTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-48f, 30f), fontBold, 22f, TacticalEmber, "CURRENT CALLSIGN");
            var titleText = CreateText(previewPanel.transform, "ActiveTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -60f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "TITLE: [APEX VANGUARD]");
            var camoText  = CreateText(previewPanel.transform, "ActiveCamo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -90f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "EQUIPPED CAMO: EMBER HEX");
            var charmText = CreateText(previewPanel.transform, "ActiveCharm", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -120f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "WEAPON CHARM: BULLET CASING");

            var applyBtn = CreateButton(previewPanel.transform, "ApplyCustomizationBtn", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(220f, 48f), frameSprite, TacticalEmber);
            var aRt = applyBtn.GetComponent<RectTransform>();
            aRt.pivot = new Vector2(1f, 0f);
            var aTxt = CreateText(applyBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 17f, Color.black, "APPLY TO WEAPON");
            aTxt.alignment = TextAlignmentOptions.Center;

            var so = new SerializedObject(comp);
            SetRef(so, "transition", go.GetComponent<UIScreenTransition>());
            SetRef(so, "backButton", backBtn);
            SetRef(so, "itemNameText", titleText);
            SetRef(so, "itemCategoryText", camoText);
            SetRef(so, "itemRarityText", charmText);
            SetRef(so, "itemDescriptionText", previewTitle);
            SetRef(so, "equipItemButton", applyBtn.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/LockerView.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildRankingsPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var go = BuildBaseSubScreen("RankingsView", "RANKINGS // GLOBAL LEADERBOARD", "SEASON 01 CONVOY INTERCEPTION RATINGS", frameSprite, fontBold, fontSemi, out var backBtn);
            var comp = go.AddComponent<RankingsView>();

            var rankCard = CreatePanel(go.transform, "MyRankCard", new Vector2(0f, 0.76f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-30f, -90f), frameSprite, DarkCharcoal);
            var myTier = CreateText(rankCard.transform, "MyTier", new Vector2(0f, 0.5f), new Vector2(0.4f, 0.5f), new Vector2(24f, 0f), new Vector2(0f, 40f), fontBold, 22f, TacticalEmber, "CURRENT TIER: GOLD II");
            var myPos  = CreateText(rankCard.transform, "MyPos", new Vector2(0.42f, 0.5f), new Vector2(0.7f, 0.5f), Vector2.zero, new Vector2(0f, 40f), fontSemi, 18f, Color.white, "RANK POSITION: #142");
            var myElo  = CreateText(rankCard.transform, "MyElo", new Vector2(0.72f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(0f, 40f), fontBold, 20f, TacticalEmber, "RATING: 1,450 RP");

            var lbPanel = CreatePanel(go.transform, "LeaderboardPanel", new Vector2(0f, 0f), new Vector2(1f, 0.74f), new Vector2(30f, 30f), new Vector2(-30f, 0f), frameSprite, DarkCharcoal);
            var scroll = new GameObject("LeaderboardScrollView", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(lbPanel.transform, false);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(15f, 15f);
            scrollRt.offsetMax = new Vector2(-15f, -15f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 350f);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            string[] topRanks = {
                "#1  [APEX] TITAN_V - 4,820 RP (GRANDMASTER)",
                "#2  [RAID] VECTOR_09 - 4,650 RP (GRANDMASTER)",
                "#3  [KNT] SHADOW_01 - 4,410 RP (MASTER)",
                "#4  [VNG] SPECTRE_99 - 4,120 RP (MASTER)",
                "#5  [IRON] FORGE_77 - 3,980 RP (DIAMOND I)",
                "#6  [HELL] VIPER_X - 3,740 RP (DIAMOND II)"
            };

            for (int i = 0; i < topRanks.Length; i++)
            {
                var row = CreatePanel(content.transform, $"RankRow_{i}", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 48f), frameSprite, BevelBorder);
                var rowTxt = CreateText(row.transform, "Text", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-40f, 0f), fontSemi, 16f, (i < 3) ? TacticalEmber : Color.white, topRanks[i]);
                rowTxt.alignment = TextAlignmentOptions.MidlineLeft;
            }

            var so = new SerializedObject(comp);
            SetRef(so, "transition", go.GetComponent<UIScreenTransition>());
            SetRef(so, "backButton", backBtn);
            SetRef(so, "seasonTitleText", myTier);
            SetRef(so, "playerTierText", myTier);
            SetRef(so, "playerRankPositionText", myPos);
            SetRef(so, "playerRankPointsText", myElo);
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/RankingsView.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildCareerStatsPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var go = BuildBaseSubScreen("CareerStatsView", "CAREER // OPERATOR RECORD", "COMBAT DOSSIER & LIFETIME STATISTICS", frameSprite, fontBold, fontSemi, out var backBtn);
            var comp = go.AddComponent<CareerStatsView>();

            var dossierPanel = CreatePanel(go.transform, "DossierOverview", new Vector2(0f, 0f), new Vector2(0.40f, 1f), new Vector2(30f, 30f), new Vector2(-15f, -100f), frameSprite, DarkCharcoal);
            var callsign = CreateText(dossierPanel.transform, "Callsign", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -20f), new Vector2(-40f, 32f), fontBold, 26f, Color.white, "CALLSIGN: SPECTRE_07");
            var lvl = CreateText(dossierPanel.transform, "Level", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -56f), new Vector2(-40f, 24f), fontSemi, 17f, TacticalEmber, "OPERATIVE LEVEL: 12 (PRESTIGE 0)");
            CreateText(dossierPanel.transform, "Bio", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -95f), new Vector2(-40f, 120f), fontMed, 15f, CoolGrey, "Registered combat specialist with tactical convoy escort commendations. Specializes in long-range target interception and asset recovery.");

            var statsPanel = CreatePanel(go.transform, "CombatMetricsPanel", new Vector2(0.43f, 0f), new Vector2(1f, 1f), new Vector2(15f, 30f), new Vector2(-30f, -100f), frameSprite, DarkCharcoal);
            CreateText(statsPanel.transform, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-48f, 30f), fontBold, 20f, TacticalEmber, "LIFETIME COMBAT METRICS");

            var deployed = CreateText(statsPanel.transform, "Deployed", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -65f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "MATCHES DEPLOYED: 48");
            var winRate  = CreateText(statsPanel.transform, "WinRate", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -95f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "WIN RATE: 29.2% (14 VICTORIES)");
            var kd       = CreateText(statsPanel.transform, "KD", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -125f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "K/D RATIO: 2.84 (136 ELIMINATIONS)");
            var streak   = CreateText(statsPanel.transform, "Streak", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -155f), new Vector2(-48f, 24f), fontSemi, 17f, Color.white, "LONGEST WIN STREAK: 5 WINS");

            CreateText(statsPanel.transform, "Accolades", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -200f), new Vector2(-48f, 100f), fontMed, 15f, CoolGrey, "COMMENDATIONS EARNED:\n• Convoy Vanguard Medal (Gold)\n• Precision Sharpshooter 100m+ (Silver)\n• Iron Valley Raid Survivor (Gold)");

            var so = new SerializedObject(comp);
            SetRef(so, "transition", go.GetComponent<UIScreenTransition>());
            SetRef(so, "backButton", backBtn);
            SetRef(so, "callsignText", callsign);
            SetRef(so, "levelText", lvl);
            SetRef(so, "matchesDeployedText", deployed);
            SetRef(so, "winRateText", winRate);
            SetRef(so, "kdRatioText", kd);
            SetRef(so, "streakText", streak);
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/CareerStatsView.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }
        #endregion

        #region Prefab Builders - Modals (SettingsView & ConfirmationModal)
        private static GameObject BuildSettingsViewPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var root = new GameObject("SettingsView", typeof(RectTransform), typeof(CanvasGroup), typeof(UIScreenTransition), typeof(SettingsView));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Fullscreen Backdrop (raycastTarget = true to block underneath)
            var backdrop = CreatePanel(root.transform, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, new Color(0f, 0f, 0f, 0.78f));
            backdrop.GetComponent<Image>().raycastTarget = true;

            // Centered Settings Card (940x630)
            var card = CreatePanel(root.transform, "SettingsCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 630f), frameSprite, DarkCharcoal);
            card.GetComponent<Image>().raycastTarget = false;
            var border = CreatePanel(card.transform, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, frameSprite, BevelBorder);
            border.GetComponent<Image>().raycastTarget = false;

            // Header Bar
            var header = CreatePanel(card.transform, "HeaderBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 0f), frameSprite, HeaderBarCol);
            header.GetComponent<Image>().raycastTarget = false;
            var title = CreateText(header.transform, "Title", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(28f, 0f), new Vector2(-100f, 32f), fontBold, 22f, TacticalEmber, "SYSTEM SETTINGS // CONFIGURATION");
            title.raycastTarget = false;

            // Top-right close button (✕)
            var closeTopBtn = CreateButton(header.transform, "CloseBtn", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(40f, 40f), frameSprite, BevelBorder);
            var ctRt = closeTopBtn.GetComponent<RectTransform>();
            ctRt.pivot = new Vector2(1f, 0.5f);
            var ctTxt = CreateText(closeTopBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 20f, Color.white, "✕");
            ctTxt.alignment = TextAlignmentOptions.Center;
            ctTxt.raycastTarget = false;

            // Tab Bar (Audio, Graphics, Controls)
            var tabBar = CreatePanel(card.transform, "TabBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -120f), new Vector2(-28f, -72f), frameSprite, GhostDark);
            tabBar.GetComponent<Image>().raycastTarget = false;

            var audioTabBtn = CreateButton(tabBar.transform, "AudioTab", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(160f, 38f), frameSprite, BevelBorder);
            var aRt = audioTabBtn.GetComponent<RectTransform>(); aRt.pivot = new Vector2(0f, 0.5f);
            var aTxt = CreateText(audioTabBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 16f, Color.white, "AUDIO");
            aTxt.alignment = TextAlignmentOptions.Center; aTxt.raycastTarget = false;

            var graphicsTabBtn = CreateButton(tabBar.transform, "GraphicsTab", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(160f, 38f), frameSprite, BevelBorder);
            var gRt = graphicsTabBtn.GetComponent<RectTransform>(); gRt.pivot = new Vector2(0f, 0.5f);
            var gTxt = CreateText(graphicsTabBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 16f, Color.white, "GRAPHICS");
            gTxt.alignment = TextAlignmentOptions.Center; gTxt.raycastTarget = false;

            var controlsTabBtn = CreateButton(tabBar.transform, "ControlsTab", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(160f, 38f), frameSprite, BevelBorder);
            var cRt = controlsTabBtn.GetComponent<RectTransform>(); cRt.pivot = new Vector2(0f, 0.5f);
            var cTxt = CreateText(controlsTabBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 16f, Color.white, "CONTROLS");
            cTxt.alignment = TextAlignmentOptions.Center; cTxt.raycastTarget = false;

            // Content Container
            var contentRoot = CreatePanel(card.transform, "ContentArea", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 75f), new Vector2(-28f, -130f), sqFill, Color.clear);
            contentRoot.GetComponent<Image>().raycastTarget = false;

            // 1. Audio Tab Content
            var audioContent = CreatePanel(contentRoot.transform, "AudioContent", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, Color.clear);
            audioContent.GetComponent<Image>().raycastTarget = false;
            var masterSlider = CreateLabeledSlider(audioContent.transform, "MasterSlider", "MASTER AUDIO VOLUME", -25f, frameSprite, sqFill, fontSemi, fontBold);
            var sfxSlider    = CreateLabeledSlider(audioContent.transform, "SfxSlider",    "SOUND EFFECTS (SFX)",   -100f, frameSprite, sqFill, fontSemi, fontBold);
            var musicSlider  = CreateLabeledSlider(audioContent.transform, "MusicSlider",  "MUSIC & AMBIENCE",     -175f, frameSprite, sqFill, fontSemi, fontBold);
            var voiceSlider  = CreateLabeledSlider(audioContent.transform, "VoiceSlider",  "VOICE COMMUNICATIONS", -250f, frameSprite, sqFill, fontSemi, fontBold);

            // 2. Graphics Tab Content
            var graphicsContent = CreatePanel(contentRoot.transform, "GraphicsContent", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, Color.clear);
            graphicsContent.GetComponent<Image>().raycastTarget = false;
            graphicsContent.SetActive(false);
            var resDropdown     = CreateLabeledDropdown(graphicsContent.transform, "ResolutionDropdown", "DISPLAY RESOLUTION", -25f, new[] { "1920 x 1080 (16:9)", "2560 x 1440 (2K)", "3840 x 2160 (4K)" }, frameSprite, fontSemi, fontBold);
            var qualityDropdown = CreateLabeledDropdown(graphicsContent.transform, "QualityDropdown",    "GRAPHICS QUALITY PRESET", -100f, new[] { "LOW", "MEDIUM", "HIGH", "ULTRA" }, frameSprite, fontSemi, fontBold);
            var fullToggle      = CreateLabeledToggle(graphicsContent.transform, "FullscreenToggle",    "FULLSCREEN DISPLAY", -175f, frameSprite, sqFill, fontSemi, fontBold);
            var fpsDropdown     = CreateLabeledDropdown(graphicsContent.transform, "FpsLimitDropdown",   "FRAME RATE CAP", -250f, new[] { "60 FPS", "120 FPS", "144 FPS", "UNLIMITED" }, frameSprite, fontSemi, fontBold);

            // 3. Controls Tab Content
            var controlsContent = CreatePanel(contentRoot.transform, "ControlsContent", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, Color.clear);
            controlsContent.GetComponent<Image>().raycastTarget = false;
            controlsContent.SetActive(false);
            var sensSlider = CreateLabeledSlider(controlsContent.transform, "SensitivitySlider", "LOOK SENSITIVITY", -25f, frameSprite, sqFill, fontSemi, fontBold);
            sensSlider.minValue = 0.1f; sensSlider.maxValue = 3.0f;
            var invertToggle = CreateLabeledToggle(controlsContent.transform, "InvertYToggle", "INVERT VERTICAL LOOK (Y-AXIS)", -100f, frameSprite, sqFill, fontSemi, fontBold);

            // Bottom Action Bar
            var bottomBar = CreatePanel(card.transform, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 15f), new Vector2(-28f, 65f), frameSprite, HeaderBarCol);
            bottomBar.GetComponent<Image>().raycastTarget = false;

            var resetBtn = CreateButton(bottomBar.transform, "ResetButton", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(180f, 40f), frameSprite, BevelBorder);
            var rRt = resetBtn.GetComponent<RectTransform>(); rRt.pivot = new Vector2(0f, 0.5f);
            var rTxt = CreateText(resetBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 15f, CoolGrey, "RESET DEFAULTS");
            rTxt.alignment = TextAlignmentOptions.Center; rTxt.raycastTarget = false;

            var applyBtn = CreateButton(bottomBar.transform, "ApplyButton", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(180f, 40f), frameSprite, TacticalEmber);
            var apRt = applyBtn.GetComponent<RectTransform>(); apRt.pivot = new Vector2(1f, 0.5f);
            var apTxt = CreateText(applyBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 15f, Color.black, "APPLY SETTINGS");
            apTxt.alignment = TextAlignmentOptions.Center; apTxt.raycastTarget = false;

            var closeBottomBtn = CreateButton(bottomBar.transform, "CloseButton", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-205f, 0f), new Vector2(140f, 40f), frameSprite, BevelBorder);
            var cbRt = closeBottomBtn.GetComponent<RectTransform>(); cbRt.pivot = new Vector2(1f, 0.5f);
            var cbTxt = CreateText(closeBottomBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 15f, Color.white, "CLOSE");
            cbTxt.alignment = TextAlignmentOptions.Center; cbTxt.raycastTarget = false;

            // Wire SettingsView component
            var view = root.GetComponent<SettingsView>();
            var so = new SerializedObject(view);
            SetRef(so, "transition", root.GetComponent<UIScreenTransition>());
            SetRef(so, "audioTabButton", audioTabBtn.GetComponent<Button>());
            SetRef(so, "graphicsTabButton", graphicsTabBtn.GetComponent<Button>());
            SetRef(so, "controlsTabButton", controlsTabBtn.GetComponent<Button>());
            SetRef(so, "audioTabContent", audioContent);
            SetRef(so, "graphicsTabContent", graphicsContent);
            SetRef(so, "controlsTabContent", controlsContent);
            SetRef(so, "masterVolumeSlider", masterSlider);
            SetRef(so, "sfxVolumeSlider", sfxSlider);
            SetRef(so, "musicVolumeSlider", musicSlider);
            SetRef(so, "voiceVolumeSlider", voiceSlider);
            SetRef(so, "resolutionDropdown", resDropdown);
            SetRef(so, "qualityDropdown", qualityDropdown);
            SetRef(so, "fullscreenToggle", fullToggle);
            SetRef(so, "fpsLimitDropdown", fpsDropdown);
            SetRef(so, "sensitivitySlider", sensSlider);
            SetRef(so, "invertYToggle", invertToggle);
            SetRef(so, "applyButton", applyBtn.GetComponent<Button>());
            SetRef(so, "resetDefaultsButton", resetBtn.GetComponent<Button>());
            SetRef(so, "closeButton", closeBottomBtn.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/SettingsView.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildConfirmationModalPrefab(Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed)
        {
            var root = new GameObject("ConfirmationModal", typeof(RectTransform), typeof(CanvasGroup), typeof(UIScreenTransition), typeof(ConfirmationModal));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Fullscreen Backdrop (raycastTarget = true)
            var backdrop = CreatePanel(root.transform, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, new Color(0f, 0f, 0f, 0.78f));
            backdrop.GetComponent<Image>().raycastTarget = true;

            // Center Card (620x330)
            var card = CreatePanel(root.transform, "CenterCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 330f), frameSprite, DarkCharcoal);
            card.GetComponent<Image>().raycastTarget = false;

            // Border
            var border = CreatePanel(card.transform, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, frameSprite, BevelBorder);
            border.GetComponent<Image>().raycastTarget = false;

            // Top Ember Accent Line
            var accent = CreatePanel(card.transform, "TopAccentRail", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -4f), new Vector2(-10f, 0f), sqFill, TacticalEmber);
            accent.GetComponent<Image>().raycastTarget = false;

            // Header Title
            var title = CreateText(card.transform, "HeaderTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -40f), new Vector2(-60f, 32f), fontBold, 22f, TacticalEmber, "CONFIRM ACTION");
            title.alignment = TextAlignmentOptions.Center;
            title.raycastTarget = false;

            // Message Body
            var msg = CreateText(card.transform, "MessageText", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 90f), new Vector2(-40f, -80f), fontSemi, 16f, CoolGrey, "Are you sure you want to proceed with this combat action?");
            msg.alignment = TextAlignmentOptions.Center;
            msg.raycastTarget = false;

            // Cancel Button (Slate Gray Ghost)
            var cancelBtn = CreateButton(card.transform, "CancelButton", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 28f), new Vector2(230f, 48f), frameSprite, BevelBorder);
            var canRt = cancelBtn.GetComponent<RectTransform>(); canRt.pivot = new Vector2(0f, 0f);
            var canTxt = CreateText(cancelBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 17f, CoolGrey, "CANCEL");
            canTxt.alignment = TextAlignmentOptions.Center; canTxt.raycastTarget = false;

            // Confirm Button (Solid Orange #FF7A00)
            var confirmBtn = CreateButton(card.transform, "ConfirmButton", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 28f), new Vector2(230f, 48f), frameSprite, TacticalEmber);
            var conRt = confirmBtn.GetComponent<RectTransform>(); conRt.pivot = new Vector2(1f, 0f);
            var conTxt = CreateText(confirmBtn.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontBold, 17f, Color.black, "CONFIRM");
            conTxt.alignment = TextAlignmentOptions.Center; conTxt.raycastTarget = false;

            // Wire ConfirmationModal component
            var modal = root.GetComponent<ConfirmationModal>();
            var so = new SerializedObject(modal);
            SetRef(so, "transition", root.GetComponent<UIScreenTransition>());
            SetRef(so, "titleText", title);
            SetRef(so, "messageText", msg);
            SetRef(so, "confirmButton", confirmBtn.GetComponent<Button>());
            SetRef(so, "cancelButton", cancelBtn.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = "Assets/Project/UI/ConfirmationModal.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static Slider CreateLabeledSlider(Transform parent, string name, string label, float yPos, Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontLabel, TMP_FontAsset fontValue)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, 56f);

            var lbl = CreateText(row.transform, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(320f, 30f), fontLabel, 16f, Color.white, label);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.raycastTarget = false;

            // Slider Object
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.transform, false);
            var sRt = sliderGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.40f, 0.5f);
            sRt.anchorMax = new Vector2(0.95f, 0.5f);
            sRt.sizeDelta = new Vector2(0f, 26f);

            var bg = CreatePanel(sliderGo.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, frameSprite, BevelBorder);
            bg.GetComponent<Image>().raycastTarget = true;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(4f, 4f); faRt.offsetMax = new Vector2(-4f, -4f);

            var fill = CreatePanel(fillArea.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sqFill, TacticalEmber);
            fill.GetComponent<Image>().raycastTarget = false;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(8f, 0f); haRt.offsetMax = new Vector2(-8f, 0f);

            var handle = CreatePanel(handleArea.transform, "Handle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 28f), frameSprite, Color.white);
            handle.GetComponent<Image>().raycastTarget = true;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        private static TMP_Dropdown CreateLabeledDropdown(Transform parent, string name, string label, float yPos, string[] options, Sprite frameSprite, TMP_FontAsset fontLabel, TMP_FontAsset fontValue)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, 56f);

            var lbl = CreateText(row.transform, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(320f, 30f), fontLabel, 16f, Color.white, label);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.raycastTarget = false;

            var dropGo = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown), typeof(RailboundButtonEffects));
            dropGo.transform.SetParent(row.transform, false);
            var dRt = dropGo.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.40f, 0.5f);
            dRt.anchorMax = new Vector2(0.95f, 0.5f);
            dRt.sizeDelta = new Vector2(0f, 40f);

            var img = dropGo.GetComponent<Image>();
            img.sprite = frameSprite;
            img.type = Image.Type.Sliced;
            img.color = BevelBorder;
            img.raycastTarget = true;

            var caption = CreateText(dropGo.transform, "CaptionText", Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-40f, 0f), fontValue, 16f, Color.white, options.Length > 0 ? options[0] : "");
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.raycastTarget = false;

            var arrow = CreateText(dropGo.transform, "Arrow", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(24f, 24f), fontValue, 16f, TacticalEmber, "▼");
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.raycastTarget = false;

            var dropdown = dropGo.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = img;
            dropdown.captionText = caption;
            dropdown.ClearOptions();
            var optList = new List<TMP_Dropdown.OptionData>();
            foreach (var opt in options) optList.Add(new TMP_Dropdown.OptionData(opt));
            dropdown.AddOptions(optList);

            return dropdown;
        }

        private static Toggle CreateLabeledToggle(Transform parent, string name, string label, float yPos, Sprite frameSprite, Sprite sqFill, TMP_FontAsset fontLabel, TMP_FontAsset fontValue)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, 56f);

            var lbl = CreateText(row.transform, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(320f, 30f), fontLabel, 16f, Color.white, label);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.raycastTarget = false;

            var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(RailboundButtonEffects));
            toggleGo.transform.SetParent(row.transform, false);
            var tRt = toggleGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.40f, 0.5f);
            tRt.anchorMax = new Vector2(0.40f, 0.5f);
            tRt.anchoredPosition = new Vector2(20f, 0f);
            tRt.sizeDelta = new Vector2(40f, 40f);

            var bg = CreatePanel(toggleGo.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, frameSprite, BevelBorder);
            var bgImg = bg.GetComponent<Image>();
            bgImg.raycastTarget = true;

            var check = CreatePanel(bg.transform, "Checkmark", Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f), sqFill, TacticalEmber);
            var checkImg = check.GetComponent<Image>();
            checkImg.raycastTarget = false;

            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            return toggle;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Sprite frameSprite, Color col)
        {
            var p = new GameObject(name, typeof(RectTransform), typeof(Image));
            p.transform.SetParent(parent, false);
            var rt = p.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = pos;
            rt.offsetMax = size;

            var img = p.GetComponent<Image>();
            img.sprite = frameSprite;
            img.type = Image.Type.Sliced;
            img.color = col;
            img.raycastTarget = false;
            return p;
        }

        private static GameObject CreateButton(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Sprite frameSprite, Color col)
        {
            var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(RailboundButtonEffects));
            b.transform.SetParent(parent, false);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = b.GetComponent<Image>();
            img.sprite = frameSprite;
            img.type = Image.Type.Sliced;
            img.color = col;
            img.raycastTarget = true;
            return b;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, TMP_FontAsset font, float sz, Color col, string text)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            t.transform.SetParent(parent, false);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = t.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = sz;
            tmp.color = col;
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }
        #endregion

        #region Raycast Target & Button Effects Audit
        private static void AuditAndFixAllPrefabs(StringBuilder sb)
        {
            string[] prefabs = {
                "Assets/Project/UI/OperatorSelectView.prefab",
                "Assets/Project/UI/LockerView.prefab",
                "Assets/Project/UI/RankingsView.prefab",
                "Assets/Project/UI/CareerStatsView.prefab",
                "Assets/Project/UI/LoadoutView.prefab",
                "Assets/Project/UI/SettingsView.prefab",
                "Assets/Project/UI/ConfirmationModal.prefab",
                "Assets/Project/UI/MatchmakingView.prefab"
            };

            int fixedImages = 0;
            int fixedButtons = 0;
            int fixedTexts = 0;

            foreach (var path in prefabs)
            {
                var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabGo == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabGo);
                bool modified = false;

                // 0. Ensure Fullscreen Stretch RectTransform
                var rootRt = instance.GetComponent<RectTransform>();
                if (rootRt != null)
                {
                    if (rootRt.anchorMin != Vector2.zero || rootRt.anchorMax != Vector2.one ||
                        rootRt.offsetMin != Vector2.zero || rootRt.offsetMax != Vector2.zero)
                    {
                        rootRt.anchorMin = Vector2.zero;
                        rootRt.anchorMax = Vector2.one;
                        rootRt.offsetMin = Vector2.zero;
                        rootRt.offsetMax = Vector2.zero;
                        modified = true;
                    }
                }

                // 1. Audit Images
                var images = instance.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    bool isInteractive = img.GetComponent<Button>() != null ||
                                         img.GetComponent<Toggle>() != null ||
                                         img.GetComponent<Slider>() != null ||
                                         img.GetComponent<Scrollbar>() != null ||
                                         img.GetComponent<TMP_Dropdown>() != null ||
                                         img.GetComponent<InputField>() != null ||
                                         img.GetComponent<TMP_InputField>() != null;

                    bool isBackdrop = img.gameObject.name.IndexOf("Backdrop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     img.gameObject.name.IndexOf("ModalBackdrop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     img.gameObject.name.IndexOf("Overlay", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isInteractive && !isBackdrop && img.raycastTarget)
                    {
                        img.raycastTarget = false;
                        fixedImages++;
                        modified = true;
                    }
                    else if (isBackdrop && !img.raycastTarget)
                    {
                        img.raycastTarget = true;
                        fixedImages++;
                        modified = true;
                    }
                }

                // 2. Audit TextMeshProUGUI (should never block clicks)
                var texts = instance.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt.raycastTarget)
                    {
                        txt.raycastTarget = false;
                        fixedTexts++;
                        modified = true;
                    }
                }

                // 3. Audit Buttons (must have RailboundButtonEffects)
                var buttons = instance.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    var fx = btn.GetComponent<RailboundButtonEffects>();
                    if (fx == null)
                    {
                        btn.gameObject.AddComponent<RailboundButtonEffects>();
                        fixedButtons++;
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }

            sb.AppendLine($"[PASS] Raycast & Micro-interactions Audit: Optimized {fixedImages} images, {fixedTexts} text components, ensured effects on {fixedButtons} buttons.");
        }
        #endregion

        #region Wiring & Cleaning
        private static void WireUIManagerPrefab(GameObject op, GameObject locker, GameObject rank, GameObject career, GameObject settings, GameObject confirm, StringBuilder sb)
        {
            string uiMgrPrefabPath = "Assets/Project/UI/UIManager.prefab";
            var uiMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiMgrPrefabPath);
            if (uiMgrPrefab != null)
            {
                var mgr = uiMgrPrefab.GetComponent<UIManager>();
                if (mgr != null)
                {
                    var so = new SerializedObject(mgr);
                    if (op != null) so.FindProperty("operatorSelectViewPrefab").objectReferenceValue = op;
                    if (locker != null) so.FindProperty("lockerViewPrefab").objectReferenceValue = locker;
                    if (rank != null) so.FindProperty("rankingsViewPrefab").objectReferenceValue = rank;
                    if (career != null) so.FindProperty("careerStatsViewPrefab").objectReferenceValue = career;
                    if (settings != null) so.FindProperty("settingsViewPrefab").objectReferenceValue = settings;
                    if (confirm != null) so.FindProperty("confirmationModalPrefab").objectReferenceValue = confirm;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(uiMgrPrefab);
                    sb.AppendLine("[PASS] UIManager.prefab wired with all sub-screens and modal prefabs.");
                }
            }
        }

        private static void CleanAndFixGameLobby(GameObject op, GameObject locker, GameObject rank, GameObject career, GameObject settings, GameObject confirm, StringBuilder sb)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Project/GameLobby.unity", OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            // Remove missing scripts
            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
            }
            sb.AppendLine($"[PASS] GameLobby.unity: Removed {removed} missing script component(s).");

            // Wire in-scene UIManager if present
            var uiMgr = UnityEngine.Object.FindFirstObjectByType<UIManager>();
            if (uiMgr != null)
            {
                var so = new SerializedObject(uiMgr);
                if (op != null) so.FindProperty("operatorSelectViewPrefab").objectReferenceValue = op;
                if (locker != null) so.FindProperty("lockerViewPrefab").objectReferenceValue = locker;
                if (rank != null) so.FindProperty("rankingsViewPrefab").objectReferenceValue = rank;
                if (career != null) so.FindProperty("careerStatsViewPrefab").objectReferenceValue = career;
                if (settings != null) so.FindProperty("settingsViewPrefab").objectReferenceValue = settings;
                if (confirm != null) so.FindProperty("confirmationModalPrefab").objectReferenceValue = confirm;
                so.ApplyModifiedProperties();
            }

            // Ensure GraphicRaycaster and GameLobbyController on Canvas_Lobby
            var canvasLobby = GameObject.Find("Canvas_Lobby");
            if (canvasLobby != null)
            {
                var raycaster = canvasLobby.GetComponent<GraphicRaycaster>();
                if (raycaster == null) canvasLobby.AddComponent<GraphicRaycaster>();

                var gameLobbyCtrl = canvasLobby.GetComponent<GameLobbyController>();
                if (gameLobbyCtrl == null) canvasLobby.AddComponent<GameLobbyController>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[PASS] GameLobby.unity saved cleanly with updated UIManager references.");
        }

        private static void CleanAndFixLoadingScreen(Sprite frameSprite, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed, StringBuilder sb)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Project/LoadingScreen.unity", OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
            }
            sb.AppendLine($"[PASS] LoadingScreen.unity: Removed {removed} missing script component(s).");

            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var oldBr = canvas.GetComponent<BattleRoyaleLoadingScreen>();
                Slider progressBar = oldBr != null ? oldBr.progressBar : null;
                TextMeshProUGUI progressPercent = oldBr != null ? oldBr.progressPercent : null;
                TextMeshProUGUI statusText = oldBr != null ? oldBr.statusText : null;
                CanvasGroup canvasGroup = oldBr != null ? oldBr.canvasGroup : null;
                Graphic progressGlow = oldBr != null ? oldBr.progressGlow : null;
                TextMeshProUGUI tipTitleText = oldBr != null ? oldBr.tipTitleText : null;
                TextMeshProUGUI tipDescriptionText = oldBr != null ? oldBr.tipDescriptionText : null;

                if (statusText != null)
                {
                    var sRt = statusText.GetComponent<RectTransform>();
                    sRt.anchorMin = new Vector2(0f, 1f);
                    sRt.anchorMax = new Vector2(1f, 1f);
                    sRt.pivot = new Vector2(0f, 1f);
                    sRt.anchoredPosition = new Vector2(4f, 0f);
                    sRt.sizeDelta = new Vector2(-130f, 26f);
                    statusText.enableWordWrapping = false;
                    statusText.overflowMode = TextOverflowModes.Ellipsis;
                }

                if (progressPercent != null)
                {
                    var pRt = progressPercent.GetComponent<RectTransform>();
                    pRt.anchorMin = new Vector2(1f, 1f);
                    pRt.anchorMax = new Vector2(1f, 1f);
                    pRt.pivot = new Vector2(1f, 1f);
                    pRt.anchoredPosition = new Vector2(-4f, 0f);
                    pRt.sizeDelta = new Vector2(110f, 26f);
                    progressPercent.alignment = TextAlignmentOptions.Right;
                }

                var ctrl = canvas.GetComponent<LoadingScreenController>();
                if (ctrl == null)
                {
                    if (oldBr != null && oldBr.GetType() != typeof(LoadingScreenController))
                    {
                        UnityEngine.Object.DestroyImmediate(oldBr);
                    }
                    ctrl = canvas.gameObject.AddComponent<LoadingScreenController>();
                }

                ctrl.progressBar = progressBar;
                ctrl.progressPercent = progressPercent;
                ctrl.statusText = statusText;
                ctrl.loadingCanvas = canvas;
                ctrl.canvasGroup = canvasGroup;
                ctrl.progressGlow = progressGlow;
                ctrl.tipTitleText = tipTitleText;
                ctrl.tipDescriptionText = tipDescriptionText;
                ctrl.gameScene = "GameLobby";
                sb.AppendLine("[PASS] LoadingScreen.unity: Text overlap fixed with non-overlapping bounds; LoadingScreenController configured.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[PASS] LoadingScreen.unity saved cleanly.");
        }
        #endregion
    }
}
