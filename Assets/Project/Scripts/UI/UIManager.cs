using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Railbound.UI
{
    public enum ScreenType
    {
        None,
        LoginMenu,
        GameLobby,
        Matchmaking,
        Loadout,
        HUD,
        GameOver
    }

    public enum ModalType
    {
        None,
        Settings,
        GuestName,
        Confirmation,
        PauseMenu,
        LevelUpUnlock
    }

    /// <summary>
    /// Centralized singleton UI Manager for Railbound.
    /// Manages layered Canvases, screen history stack, modal queues, and async scene navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<UIManager>();
                    if (_instance == null)
                    {
                        var prefab =
#if UNITY_EDITOR
                            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/UIManager.prefab");
#else
                            Resources.Load<GameObject>("UIManager");
#endif
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "UIManager";
                            _instance = go.GetComponent<UIManager>();
                        }
                        else
                        {
                            var go = new GameObject("UIManager");
                            _instance = go.AddComponent<UIManager>();
                        }
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Layer Containers")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Transform backgroundLayer;
        [SerializeField] private Transform screenLayer;
        [SerializeField] private Transform hudLayer;
        [SerializeField] private Transform modalLayer;
        [SerializeField] private Transform chatLayer;
        [SerializeField] private Transform loadingLayer;

        [Header("Modal Backdrop")]
        [SerializeField] private GameObject modalBackdrop;

        [Header("Prefabs (Assets/Project/UI/)")]
        [SerializeField] private GameObject settingsViewPrefab;
        [SerializeField] private GameObject matchmakingViewPrefab;
        [SerializeField] private GameObject hudViewPrefab;
        [SerializeField] private GameObject loadoutViewPrefab;
        [SerializeField] private GameObject gameOverViewPrefab;
        [SerializeField] private GameObject pauseMenuModalPrefab;
        [SerializeField] private GameObject scoreboardOverlayPrefab;
        [SerializeField] private GameObject confirmationModalPrefab;
        [SerializeField] private GameObject guestNamePopupPrefab;
        [SerializeField] private GameObject globalChatDrawerPrefab;
        [SerializeField] private GameObject levelUpUnlockModalPrefab;
        [SerializeField] private GameObject emberOverlayPrefab;

        [Header("Loading Overlay")]
        [SerializeField] private Slider loadingBar;
        [SerializeField] private TMP_Text loadingStatusText;

        // Cached active instances
        private SettingsView _settingsView;
        private MatchmakingView _matchmakingView;
        private HUDView _hudView;
        private LoadoutView _loadoutView;
        private GameOverView _gameOverView;
        private PauseMenuModal _pauseMenuModal;
        private ScoreboardOverlay _scoreboardOverlay;
        private ConfirmationModal _confirmationModal;
        private GuestNameModalController _guestNameModal;
        private GlobalChatDrawer _globalChatDrawer;
        private LevelUpUnlockModal _levelUpModal;
        private UIEmberOverlay _emberOverlay;

        // Stacks & State
        private ScreenType _currentScreen = ScreenType.None;
        private readonly Stack<ScreenType> _screenHistory = new Stack<ScreenType>();
        private readonly Stack<ModalType> _activeModals = new Stack<ModalType>();

        public ScreenType CurrentScreen => _currentScreen;
        public ModalType ActiveModal => _activeModals.Count > 0 ? _activeModals.Peek() : ModalType.None;
        public bool IsAnyModalOpen => _activeModals.Count > 0;

        public event Action<ScreenType> OnScreenChanged;
        public event Action<ModalType> OnModalOpened;
        public event Action<ModalType> OnModalClosed;
        public event Action<float> OnLoadingProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureCanvasAndLayers();
            AutoLoadPrefabsIfNull();
            SetupModalBackdrop();
            SetupLoadingOverlay();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
            }
        }

        #region Layer Setup & Hierarchy
        public void EnsureCanvasAndLayers()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
                if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.sortingOrder = 100;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            if (backgroundLayer == null) backgroundLayer = GetOrCreateLayer("BackgroundLayer", 0);
            if (screenLayer == null) screenLayer = GetOrCreateLayer("ScreenLayer", 10);
            if (hudLayer == null) hudLayer = GetOrCreateLayer("HUDLayer", 20);
            if (modalLayer == null) modalLayer = GetOrCreateLayer("ModalLayer", 30);
            if (chatLayer == null) chatLayer = GetOrCreateLayer("ChatLayer", 40);
            if (loadingLayer == null) loadingLayer = GetOrCreateLayer("LoadingLayer", 50);
        }

        private RectTransform GetOrCreateLayer(string layerName, int siblingIndex)
        {
            Transform existing = transform.Find(layerName);
            if (existing != null)
            {
                var rt = existing.GetComponent<RectTransform>();
                rt.SetSiblingIndex(siblingIndex);
                return rt;
            }

            GameObject go = new GameObject(layerName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetSiblingIndex(siblingIndex);
            return rect;
        }

        private void SetupModalBackdrop()
        {
            if (modalBackdrop == null && modalLayer != null)
            {
                Transform existing = modalLayer.Find("ModalBackdrop");
                if (existing != null)
                {
                    modalBackdrop = existing.gameObject;
                }
                else
                {
                    var backdropGo = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
                    backdropGo.transform.SetParent(modalLayer, false);
                    var rt = backdropGo.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.SetAsFirstSibling();

                    var img = backdropGo.GetComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0.65f);

                    var btn = backdropGo.GetComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(OnBackdropClicked);

                    modalBackdrop = backdropGo;
                }
            }

            UpdateModalBackdropState();
        }

        private void OnBackdropClicked()
        {
            if (_activeModals.Count > 0)
            {
                ModalType top = _activeModals.Peek();
                if (top == ModalType.Settings || top == ModalType.GuestName || top == ModalType.Confirmation)
                {
                    CloseModal(top);
                }
            }
        }

        private void UpdateModalBackdropState()
        {
            if (modalBackdrop != null && modalLayer != null)
            {
                modalBackdrop.SetActive(_activeModals.Count > 0);
                if (_activeModals.Count > 0)
                {
                    modalBackdrop.transform.SetSiblingIndex(Mathf.Max(0, modalLayer.childCount - _activeModals.Count - 1));
                }
            }
        }

        private void SetupLoadingOverlay()
        {
            if (loadingLayer == null) return;

            var cg = loadingLayer.GetComponent<CanvasGroup>();
            if (cg == null) cg = loadingLayer.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            loadingLayer.gameObject.SetActive(false);

            if (loadingBar == null && loadingLayer.Find("LoadingBar") == null)
            {
                var bgObj = new GameObject("LoadingBackground", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(loadingLayer, false);
                var bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                bgObj.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.98f);

                var titleObj = new GameObject("LoadingTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(loadingLayer, false);
                var titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(0f, 60f);
                titleRt.sizeDelta = new Vector2(500f, 40f);
                var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
                titleTmp.text = "DEPLOYING OPERATOR...";
                titleTmp.fontSize = 22;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.color = new Color(1f, 0.42f, 0f, 1f);

                var statusObj = new GameObject("LoadingStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
                statusObj.transform.SetParent(loadingLayer, false);
                var statusRt = statusObj.GetComponent<RectTransform>();
                statusRt.anchorMin = new Vector2(0.5f, 0.5f);
                statusRt.anchorMax = new Vector2(0.5f, 0.5f);
                statusRt.anchoredPosition = new Vector2(0f, 20f);
                statusRt.sizeDelta = new Vector2(600f, 30f);
                loadingStatusText = statusObj.GetComponent<TextMeshProUGUI>();
                loadingStatusText.text = "SYNCHRONIZING TACTICAL DATA...";
                loadingStatusText.fontSize = 13;
                loadingStatusText.alignment = TextAlignmentOptions.Center;
                loadingStatusText.color = new Color(0.7f, 0.75f, 0.8f, 1f);

                var sliderObj = new GameObject("LoadingBar", typeof(RectTransform), typeof(Slider));
                sliderObj.transform.SetParent(loadingLayer, false);
                var sliderRt = sliderObj.GetComponent<RectTransform>();
                sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
                sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
                sliderRt.anchoredPosition = new Vector2(0f, -30f);
                sliderRt.sizeDelta = new Vector2(600f, 8f);

                loadingBar = sliderObj.GetComponent<Slider>();
                loadingBar.minValue = 0f;
                loadingBar.maxValue = 1f;
                loadingBar.value = 0f;
                loadingBar.interactable = false;

                var sliderBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                sliderBg.transform.SetParent(sliderObj.transform, false);
                var sbgRt = sliderBg.GetComponent<RectTransform>();
                sbgRt.anchorMin = Vector2.zero;
                sbgRt.anchorMax = Vector2.one;
                sbgRt.offsetMin = Vector2.zero;
                sbgRt.offsetMax = Vector2.zero;
                sliderBg.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

                var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(sliderObj.transform, false);
                var faRt = fillArea.GetComponent<RectTransform>();
                faRt.anchorMin = Vector2.zero;
                faRt.anchorMax = Vector2.one;
                faRt.offsetMin = Vector2.zero;
                faRt.offsetMax = Vector2.zero;

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                fill.GetComponent<Image>().color = new Color(1f, 0.42f, 0f, 1f);

                loadingBar.targetGraphic = fill.GetComponent<Image>();
                loadingBar.fillRect = fillRt;
            }
        }
        #endregion

        #region Screen & History Stack
        public void OpenScreen(ScreenType type, bool addToHistory = true)
        {
            if (_currentScreen == type && type != ScreenType.None) return;

            if (_currentScreen != ScreenType.None)
            {
                if (addToHistory)
                {
                    _screenHistory.Push(_currentScreen);
                }
                HideScreenView(_currentScreen);
            }

            _currentScreen = type;
            ShowScreenView(type);
            OnScreenChanged?.Invoke(type);
        }

        public void CloseCurrentScreen()
        {
            if (_currentScreen != ScreenType.None)
            {
                HideScreenView(_currentScreen);
                _currentScreen = ScreenType.None;
            }
        }

        public void GoBack()
        {
            if (_screenHistory.Count > 0)
            {
                ScreenType prev = _screenHistory.Pop();
                OpenScreen(prev, addToHistory: false);
            }
        }

        private void ShowScreenView(ScreenType type)
        {
            switch (type)
            {
                case ScreenType.Matchmaking:
                    var mm = GetOrCreate(ref _matchmakingView, ref matchmakingViewPrefab, "Assets/Project/UI/MatchmakingView.prefab", screenLayer);
                    if (mm != null)
                    {
                        mm.OnBackClicked -= GoBack;
                        mm.OnBackClicked += GoBack;
                        mm.Show();
                    }
                    break;
                case ScreenType.Loadout:
                    var loadout = GetOrCreate(ref _loadoutView, ref loadoutViewPrefab, "Assets/Project/UI/LoadoutView.prefab", screenLayer);
                    if (loadout != null)
                    {
                        loadout.OnBackClicked -= GoBack;
                        loadout.OnBackClicked += GoBack;
                        loadout.Show();
                    }
                    break;
                case ScreenType.GameOver:
                    GetOrCreate(ref _gameOverView, ref gameOverViewPrefab, "Assets/Project/UI/GameOverView.prefab", screenLayer)?.Show(true);
                    break;
                case ScreenType.HUD:
                    var hud = GetOrCreate(ref _hudView, ref hudViewPrefab, "Assets/Project/UI/HUDView.prefab", hudLayer);
                    if (hud != null) hud.gameObject.SetActive(true);
                    break;
            }
        }

        private void HideScreenView(ScreenType type)
        {
            switch (type)
            {
                case ScreenType.Matchmaking:
                    if (_matchmakingView != null) _matchmakingView.Hide();
                    break;
                case ScreenType.Loadout:
                    if (_loadoutView != null) _loadoutView.Hide();
                    break;
                case ScreenType.GameOver:
                    if (_gameOverView != null) _gameOverView.Hide();
                    break;
                case ScreenType.HUD:
                    if (_hudView != null) _hudView.gameObject.SetActive(false);
                    break;
            }
        }
        #endregion

        #region Modal Queue & Management
        public void OpenModal(ModalType type)
        {
            EnsureCanvasAndLayers();

            switch (type)
            {
                case ModalType.Settings:
                    var settings = GetOrCreate(ref _settingsView, ref settingsViewPrefab, "Assets/Project/UI/SettingsView.prefab", modalLayer);
                    if (settings != null)
                    {
                        settings.transform.SetAsLastSibling();
                        EnsureCanvasGroupInteractive(settings.gameObject);
                        settings.Show();
                    }
                    break;

                case ModalType.GuestName:
                    var guestModal = GetOrCreate(ref _guestNameModal, ref guestNamePopupPrefab, "Assets/Project/UI/GuestNamePopup.prefab", modalLayer);
                    if (guestModal != null)
                    {
                        guestModal.transform.SetAsLastSibling();
                        EnsureCanvasGroupInteractive(guestModal.gameObject);
                        guestModal.Show();
                    }
                    break;

                case ModalType.PauseMenu:
                    var pauseMenu = GetOrCreate(ref _pauseMenuModal, ref pauseMenuModalPrefab, "Assets/Project/UI/PauseMenuModal.prefab", modalLayer);
                    if (pauseMenu != null)
                    {
                        pauseMenu.transform.SetAsLastSibling();
                        EnsureCanvasGroupInteractive(pauseMenu.gameObject);
                        pauseMenu.Show();
                    }
                    break;

                case ModalType.Confirmation:
                    var confirm = GetOrCreate(ref _confirmationModal, ref confirmationModalPrefab, "Assets/Project/UI/ConfirmationModal.prefab", modalLayer);
                    if (confirm != null)
                    {
                        confirm.transform.SetAsLastSibling();
                        EnsureCanvasGroupInteractive(confirm.gameObject);
                    }
                    break;

                case ModalType.LevelUpUnlock:
                    var levelUp = GetOrCreate(ref _levelUpModal, ref levelUpUnlockModalPrefab, "Assets/Project/UI/LevelUpUnlockModal.prefab", modalLayer);
                    if (levelUp != null)
                    {
                        levelUp.transform.SetAsLastSibling();
                        EnsureCanvasGroupInteractive(levelUp.gameObject);
                    }
                    break;
            }

            if (!_activeModals.Contains(type))
            {
                _activeModals.Push(type);
            }
            UpdateModalBackdropState();
            OnModalOpened?.Invoke(type);
        }

        public void CloseModal(ModalType type)
        {
            switch (type)
            {
                case ModalType.Settings:
                    if (_settingsView != null) _settingsView.Hide();
                    break;
                case ModalType.GuestName:
                    if (_guestNameModal != null) _guestNameModal.Hide();
                    break;
                case ModalType.PauseMenu:
                    if (_pauseMenuModal != null) _pauseMenuModal.Hide();
                    break;
                case ModalType.Confirmation:
                    if (_confirmationModal != null) _confirmationModal.Hide();
                    break;
                case ModalType.LevelUpUnlock:
                    if (_levelUpModal != null) _levelUpModal.Hide();
                    break;
            }

            if (_activeModals.Count > 0 && _activeModals.Peek() == type)
            {
                _activeModals.Pop();
            }
            else
            {
                var temp = new Stack<ModalType>();
                while (_activeModals.Count > 0)
                {
                    var item = _activeModals.Pop();
                    if (item != type) temp.Push(item);
                }
                while (temp.Count > 0) _activeModals.Push(temp.Pop());
            }

            UpdateModalBackdropState();
            OnModalClosed?.Invoke(type);
        }

        private void EnsureCanvasGroupInteractive(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }
        }

        public void HandleEscape()
        {
            if (_activeModals.Count > 0)
            {
                ModalType top = _activeModals.Peek();
                CloseModal(top);
                return;
            }

            if (_globalChatDrawer != null && _globalChatDrawer.gameObject.activeSelf)
            {
                HideChat();
                return;
            }

            if (_currentScreen == ScreenType.Matchmaking || _currentScreen == ScreenType.Loadout)
            {
                GoBack();
                return;
            }

            if (SceneManager.GetActiveScene().name.Contains("MAP") || _currentScreen == ScreenType.HUD)
            {
                TogglePauseMenu();
            }
        }
        #endregion

        #region Public Helpers
        public void ShowSettings() => OpenModal(ModalType.Settings);
        public void HideSettings() => CloseModal(ModalType.Settings);

        public void ShowConfirmation(string title, string message, Action onConfirm, Action onCancel = null)
        {
            EnsureCanvasAndLayers();
            var modal = GetOrCreate(ref _confirmationModal, ref confirmationModalPrefab, "Assets/Project/UI/ConfirmationModal.prefab", modalLayer);
            if (modal != null)
            {
                modal.transform.SetAsLastSibling();
                EnsureCanvasGroupInteractive(modal.gameObject);
                if (!_activeModals.Contains(ModalType.Confirmation))
                    _activeModals.Push(ModalType.Confirmation);
                UpdateModalBackdropState();
                modal.Show(title, message, () =>
                {
                    onConfirm?.Invoke();
                    CloseModal(ModalType.Confirmation);
                }, () =>
                {
                    onCancel?.Invoke();
                    CloseModal(ModalType.Confirmation);
                });
            }
        }

        public void ShowPauseMenu() => OpenModal(ModalType.PauseMenu);
        public void HidePauseMenu() => CloseModal(ModalType.PauseMenu);
        public void TogglePauseMenu()
        {
            if (_activeModals.Contains(ModalType.PauseMenu)) HidePauseMenu();
            else ShowPauseMenu();
        }

        public void ShowScoreboard(bool show)
        {
            EnsureCanvasAndLayers();
            var sb = GetOrCreate(ref _scoreboardOverlay, ref scoreboardOverlayPrefab, "Assets/Project/UI/ScoreboardOverlay.prefab", hudLayer);
            if (sb != null)
            {
                if (show) sb.Show();
                else sb.Hide();
            }
        }

        public void ShowChat()
        {
            EnsureCanvasAndLayers();
            var chat = GetOrCreate(ref _globalChatDrawer, ref globalChatDrawerPrefab, "Assets/Project/UI/GlobalChatDrawer.prefab", chatLayer);
            if (chat != null) chat.Open();
        }

        public void HideChat()
        {
            if (_globalChatDrawer != null) _globalChatDrawer.Close();
        }

        public void ToggleChat()
        {
            EnsureCanvasAndLayers();
            var chat = GetOrCreate(ref _globalChatDrawer, ref globalChatDrawerPrefab, "Assets/Project/UI/GlobalChatDrawer.prefab", chatLayer);
            if (chat != null) chat.Toggle();
        }

        public void ShowLevelUp(int level, string rewardName, string category = "NEW WEAPON UNLOCKED", Sprite icon = null)
        {
            EnsureCanvasAndLayers();
            var modal = GetOrCreate(ref _levelUpModal, ref levelUpUnlockModalPrefab, "Assets/Project/UI/LevelUpUnlockModal.prefab", modalLayer);
            if (modal != null)
            {
                modal.transform.SetAsLastSibling();
                EnsureCanvasGroupInteractive(modal.gameObject);
                if (!_activeModals.Contains(ModalType.LevelUpUnlock))
                    _activeModals.Push(ModalType.LevelUpUnlock);
                UpdateModalBackdropState();
                modal.Show(level, rewardName, category, icon);
            }
        }

        public void ShowGuestLogin() => OpenModal(ModalType.GuestName);

        public void ShowGameOver(bool isVictory)
        {
            OpenScreen(ScreenType.GameOver);
            if (_gameOverView != null) _gameOverView.Show(isVictory);
        }
        #endregion

        #region Scene Navigation Integration
        public void LoadSceneAsync(string sceneName, Action onComplete = null)
        {
            StartCoroutine(LoadSceneRoutine(sceneName, onComplete));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, Action onComplete)
        {
            if (loadingLayer != null)
            {
                loadingLayer.gameObject.SetActive(true);
                var cg = loadingLayer.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                }
            }

            if (loadingStatusText != null)
                loadingStatusText.text = $"CONNECTING TO {sceneName.ToUpper()}...";

            if (loadingBar != null)
                loadingBar.value = 0f;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float fakeProgress = 0f;
            while (fakeProgress < 0.9f || op.progress < 0.9f)
            {
                fakeProgress = Mathf.MoveTowards(fakeProgress, Mathf.Clamp01(op.progress / 0.9f), Time.unscaledDeltaTime * 2f);
                if (loadingBar != null) loadingBar.value = fakeProgress;
                OnLoadingProgress?.Invoke(fakeProgress);
                yield return null;
            }

            if (loadingBar != null) loadingBar.value = 1f;
            OnLoadingProgress?.Invoke(1f);

            yield return new WaitForSecondsRealtime(0.2f);
            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                yield return null;
            }

            onComplete?.Invoke();

            if (loadingLayer != null)
            {
                var cg = loadingLayer.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float fade = 1f;
                    while (fade > 0f)
                    {
                        fade -= Time.unscaledDeltaTime * 4f;
                        cg.alpha = Mathf.Clamp01(fade);
                        yield return null;
                    }
                    cg.blocksRaycasts = false;
                }
                loadingLayer.gameObject.SetActive(false);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _activeModals.Clear();
            UpdateModalBackdropState();

            if (scene.name.Equals("LoginMenu", StringComparison.OrdinalIgnoreCase))
            {
                _currentScreen = ScreenType.LoginMenu;
            }
            else if (scene.name.Equals("GameLobby", StringComparison.OrdinalIgnoreCase))
            {
                _currentScreen = ScreenType.GameLobby;
            }
            else if (scene.name.Contains("MAP") || scene.name.Contains("Game"))
            {
                _currentScreen = ScreenType.HUD;
            }
        }
        #endregion

        #region Prefab Instantiation & Cache
        public void AutoLoadPrefabsIfNull()
        {
#if UNITY_EDITOR
            if (settingsViewPrefab == null)
                settingsViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/SettingsView.prefab");
            if (matchmakingViewPrefab == null)
                matchmakingViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/MatchmakingView.prefab");
            if (hudViewPrefab == null)
                hudViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/HUDView.prefab");
            if (loadoutViewPrefab == null)
                loadoutViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/LoadoutView.prefab");
            if (gameOverViewPrefab == null)
                gameOverViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GameOverView.prefab");
            if (pauseMenuModalPrefab == null)
                pauseMenuModalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/PauseMenuModal.prefab");
            if (scoreboardOverlayPrefab == null)
                scoreboardOverlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/ScoreboardOverlay.prefab");
            if (confirmationModalPrefab == null)
                confirmationModalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/ConfirmationModal.prefab");
            if (guestNamePopupPrefab == null)
                guestNamePopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GuestNamePopup.prefab");
            if (globalChatDrawerPrefab == null)
                globalChatDrawerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GlobalChatDrawer.prefab");
            if (levelUpUnlockModalPrefab == null)
                levelUpUnlockModalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/LevelUpUnlockModal.prefab");
            if (emberOverlayPrefab == null)
                emberOverlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/UIEmberOverlay.prefab");
#endif
        }

        private T GetOrCreate<T>(ref T cachedInstance, ref GameObject prefabSlot, string fallbackPath, Transform parent) where T : Component
        {
            if (cachedInstance != null)
            {
                cachedInstance.gameObject.SetActive(true);
                return cachedInstance;
            }

            if (parent == null)
            {
                EnsureCanvasAndLayers();
                if (typeof(T) == typeof(GuestNameModalController) || typeof(T) == typeof(SettingsView) || typeof(T) == typeof(PauseMenuModal) || typeof(T) == typeof(ConfirmationModal) || typeof(T) == typeof(LevelUpUnlockModal))
                    parent = modalLayer;
                else if (typeof(T) == typeof(HUDView) || typeof(T) == typeof(ScoreboardOverlay))
                    parent = hudLayer;
                else if (typeof(T) == typeof(GlobalChatDrawer))
                    parent = chatLayer;
                else
                    parent = screenLayer;
            }

            if (parent != null)
            {
                cachedInstance = parent.GetComponentInChildren<T>(true);
                if (cachedInstance != null)
                {
                    cachedInstance.gameObject.SetActive(true);
                    return cachedInstance;
                }
            }

            if (prefabSlot == null)
            {
#if UNITY_EDITOR
                prefabSlot = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPath);
#endif
            }

            if (prefabSlot == null && typeof(T) == typeof(GuestNameModalController) && parent != null)
            {
                cachedInstance = CreateFallbackGuestModal(parent) as T;
                return cachedInstance;
            }

            if (prefabSlot != null && parent != null)
            {
                GameObject go = Instantiate(prefabSlot, parent);
                go.name = prefabSlot.name;
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                }
                cachedInstance = go.GetComponent<T>();
                return cachedInstance;
            }

            Debug.LogWarning($"[UIManager] Cannot instantiate {typeof(T).Name}: Prefab or Parent is null. (Parent: {parent != null}, Prefab: {prefabSlot != null})");
            return null;
        }

        private GuestNameModalController CreateFallbackGuestModal(Transform parent)
        {
            var go = new GameObject("GuestNamePopup", typeof(RectTransform), typeof(CanvasGroup), typeof(UIScreenTransition), typeof(GuestNameModalController));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(go.transform, false);
            var bdRt = bd.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var card = new GameObject("CenterCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(go.transform, false);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(500f, 320f);
            card.GetComponent<Image>().color = new Color(0.082f, 0.094f, 0.122f, 0.98f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(card.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(440f, 30f);
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.text = "DEPLOY AS GUEST";
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.42f, 0f, 1f);

            var subGo = new GameObject("SubHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(card.transform, false);
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -68f);
            subRt.sizeDelta = new Vector2(440f, 20f);
            var subText = subGo.GetComponent<TextMeshProUGUI>();
            subText.text = "ENTER OPERATOR CALLSIGN (3-16 CHARACTERS)";
            subText.fontSize = 11;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.56f, 0.61f, 0.68f, 1f);

            var warnGo = new GameObject("WarningText", typeof(RectTransform), typeof(TextMeshProUGUI));
            warnGo.transform.SetParent(card.transform, false);
            var warnRt = warnGo.GetComponent<RectTransform>();
            warnRt.anchorMin = new Vector2(0.5f, 1f);
            warnRt.anchorMax = new Vector2(0.5f, 1f);
            warnRt.anchoredPosition = new Vector2(0f, -92f);
            warnRt.sizeDelta = new Vector2(440f, 20f);
            var warnText = warnGo.GetComponent<TextMeshProUGUI>();
            warnText.text = "Callsign must be 3-16 alphanumeric characters.";
            warnText.fontSize = 12;
            warnText.alignment = TextAlignmentOptions.Center;
            warnText.color = new Color(1f, 0.35f, 0.35f, 1f);
            warnGo.SetActive(false);

            var inputGo = new GameObject("CallsignInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(card.transform, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.5f, 0.5f);
            inputRt.anchorMax = new Vector2(0.5f, 0.5f);
            inputRt.anchoredPosition = new Vector2(0f, 10f);
            inputRt.sizeDelta = new Vector2(420f, 48f);
            inputGo.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 1f);

            var inputField = inputGo.GetComponent<TMP_InputField>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(inputGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 0f);
            textRt.offsetMax = new Vector2(-16f, 0f);
            var textTmp = textGo.GetComponent<TextMeshProUGUI>();
            textTmp.fontSize = 14;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.Left;

            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            phGo.transform.SetParent(inputGo.transform, false);
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(16f, 0f);
            phRt.offsetMax = new Vector2(-16f, 0f);
            var phTmp = phGo.GetComponent<TextMeshProUGUI>();
            phTmp.text = "Callsign_01";
            phTmp.fontSize = 14;
            phTmp.color = new Color(0.56f, 0.61f, 0.68f, 0.7f);
            phTmp.alignment = TextAlignmentOptions.Left;

            inputField.textViewport = textRt;
            inputField.textComponent = textTmp;
            inputField.placeholder = phTmp;

            var cancelGo = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RailboundButtonEffects));
            cancelGo.transform.SetParent(card.transform, false);
            var cancelRt = cancelGo.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.5f, 0f);
            cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.anchoredPosition = new Vector2(-110f, 45f);
            cancelRt.sizeDelta = new Vector2(190f, 44f);
            cancelGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            var cancelBtn = cancelGo.GetComponent<Button>();

            var cancelTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            cancelTextGo.transform.SetParent(cancelGo.transform, false);
            var cancelTextRt = cancelTextGo.GetComponent<RectTransform>();
            cancelTextRt.anchorMin = Vector2.zero;
            cancelTextRt.anchorMax = Vector2.one;
            cancelTextRt.offsetMin = Vector2.zero;
            cancelTextRt.offsetMax = Vector2.zero;
            var cancelTextTmp = cancelTextGo.GetComponent<TextMeshProUGUI>();
            cancelTextTmp.text = "CANCEL";
            cancelTextTmp.fontSize = 13;
            cancelTextTmp.fontStyle = FontStyles.Bold;
            cancelTextTmp.alignment = TextAlignmentOptions.Center;
            cancelTextTmp.color = Color.white;

            var confirmGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RailboundButtonEffects));
            confirmGo.transform.SetParent(card.transform, false);
            var confirmRt = confirmGo.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.5f, 0f);
            confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(110f, 45f);
            confirmRt.sizeDelta = new Vector2(190f, 44f);
            confirmGo.GetComponent<Image>().color = new Color(1f, 0.42f, 0f, 1f);
            var confirmBtn = confirmGo.GetComponent<Button>();

            var confirmTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            confirmTextGo.transform.SetParent(confirmGo.transform, false);
            var confirmTextRt = confirmTextGo.GetComponent<RectTransform>();
            confirmTextRt.anchorMin = Vector2.zero;
            confirmTextRt.anchorMax = Vector2.one;
            confirmTextRt.offsetMin = Vector2.zero;
            confirmTextRt.offsetMax = Vector2.zero;
            var confirmTextTmp = confirmTextGo.GetComponent<TextMeshProUGUI>();
            confirmTextTmp.text = "CONFIRM & DEPLOY";
            confirmTextTmp.fontSize = 13;
            confirmTextTmp.fontStyle = FontStyles.Bold;
            confirmTextTmp.alignment = TextAlignmentOptions.Center;
            confirmTextTmp.color = new Color(0.04f, 0.05f, 0.06f, 1f);

            var controller = go.GetComponent<GuestNameModalController>();
            var transition = go.GetComponent<UIScreenTransition>();

#if UNITY_EDITOR
            var so = new SerializedObject(controller);
            so.FindProperty("callsignInput").objectReferenceValue = inputField;
            so.FindProperty("warningText").objectReferenceValue = warnText;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("transition").objectReferenceValue = transition;
            so.FindProperty("lobbySceneName").stringValue = "GameLobby";
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return controller;
        }
        #endregion
    }
}
