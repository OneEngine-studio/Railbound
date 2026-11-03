using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Michsky.UI.Reach
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Reach UI/Localization/Localized Object")]
    public class LocalizedObject : MonoBehaviour
    {
        // Resources
        public LocalizationManager localizationManager;
        public LocalizationSettings localizationSettings;
        public TextMeshProUGUI textObj;
        public AudioSource audioObj;
        public Image imageObj;

        // Settings
        public int tableIndex = -1;
        public string localizationKey;
        public ObjectType objectType = ObjectType.TextMeshPro;
        public UpdateMode updateMode = UpdateMode.OnEnable;
        public bool rebuildLayoutOnUpdate;
        [SerializeField] private bool forceAddToManager = false;
#if UNITY_EDITOR
        public bool showOutputOnEditor = true;
#endif

        // Events
        public LanguageChangedEvent onLanguageChanged = new LanguageChangedEvent();

        [System.Serializable]
        public class LanguageChangedEvent : UnityEvent<string> { }

        // Helpers
        public bool isInitialized = false;

        public enum UpdateMode { OnEnable, OnDemand }
        public enum ObjectType { TextMeshPro, Custom, ComponentDriven, Audio, Image }

        void Awake()
        {
            if (localizationManager != null && localizationManager.UIManagerAsset.enableLocalization == false) { Destroy(this); return; }
            InitializeItem();
        }

        void OnEnable()
        {
            if (localizationManager == null) { Destroy(this); return; }
            if (!isInitialized || localizationManager == null || localizationManager.currentLanguageAsset == null) { return; }
            if (updateMode == UpdateMode.OnEnable) { UpdateItem(); }
        }

        public void InitializeItem()
        {
            if (isInitialized)
                return;

            if (localizationManager == null)
            {
                bool locManagerFound = false;

                foreach (LocalizationManager lm in Resources.FindObjectsOfTypeAll(typeof(LocalizationManager)) as LocalizationManager[])
                {
                    if (lm.gameObject.scene.name != null)
                    {
                        localizationManager = lm;
                        locManagerFound = true;
                        break;
                    }
                }

                if (locManagerFound == false)
                {
                    UIManager tempUIM = (UIManager)Resources.FindObjectsOfTypeAll(typeof(UIManager))[0];
                    if (tempUIM == null || tempUIM.enableLocalization == false) { return; }

                    GameObject newLM = new GameObject("Localization Manager [Auto Generated]");
                    localizationManager = newLM.AddComponent<LocalizationManager>();
                }
            }

            if (localizationManager != null && localizationManager.UIManagerAsset.enableLocalization == false) { Destroy(this); return; }
            if (localizationManager == null || localizationManager.UIManagerAsset == null || localizationManager.UIManagerAsset.enableLocalization == false) { return; }
            if (forceAddToManager && !localizationManager.localizedItems.Contains(this)) { localizationManager.localizedItems.Add(this); }

            if (objectType == ObjectType.TextMeshPro && textObj == null) { textObj = gameObject.GetComponent<TextMeshProUGUI>(); }
            else if (objectType == ObjectType.Audio && audioObj == null) { audioObj = gameObject.GetComponent<AudioSource>(); }
            else if (objectType == ObjectType.Image && imageObj == null) { imageObj = gameObject.GetComponent<Image>(); }

            isInitialized = true;
        }

        public void ReInitializeItem()
        {
            isInitialized = false;
            InitializeItem();
        }

        public void UpdateItem()
        {
            if (!isInitialized || localizationManager == null || localizationManager.currentLanguageAsset == null || localizationManager.currentLanguageAsset.tableList.Count == 0)
                return;

            if (tableIndex < 0 || tableIndex >= localizationManager.currentLanguageAsset.tableList.Count)
                return;

            var activeTable = localizationManager.currentLanguageAsset.tableList[tableIndex];

            if (objectType == ObjectType.TextMeshPro && textObj != null)
            {
                for (int i = 0; i < activeTable.tableContent.Count; i++)
                {
                    if (localizationKey == activeTable.tableContent[i].key)
                    {
                        if (string.IsNullOrEmpty(activeTable.tableContent[i].value))

