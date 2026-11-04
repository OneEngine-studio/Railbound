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

        private bool TryGetActiveTable(out LocalizationLanguage.TableList activeTable)
        {
            activeTable = null;

            if (!isInitialized || localizationManager == null || localizationManager.currentLanguageAsset == null)
                return false;

            if (tableIndex < 0 || tableIndex >= localizationManager.currentLanguageAsset.tableList.Count)
                return false;

            activeTable = localizationManager.currentLanguageAsset.tableList[tableIndex];
            return activeTable != null;
        }

        public void UpdateItem()
        {
            if (!TryGetActiveTable(out var activeTable))
                return;

            if (objectType == ObjectType.TextMeshPro && textObj != null)
            {
                for (int i = 0; i < activeTable.tableContent.Count; i++)
                {
                    if (localizationKey == activeTable.tableContent[i].key)
                    {
                        if (string.IsNullOrEmpty(activeTable.tableContent[i].value))
                        {
                            if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The specified key '" + localizationKey + "' could not be found or the output value is empty."); }
                            break;
                        }

                        textObj.text = activeTable.tableContent[i].value;
                        onLanguageChanged.Invoke(activeTable.tableContent[i].value);
                        break;
                    }
                }
            }

            else if (objectType == ObjectType.Audio && audioObj != null)
            {
                for (int i = 0; i < activeTable.tableContent.Count; i++)
                {
                    if (localizationKey == activeTable.tableContent[i].key)
                    {
                        if (activeTable.tableContent[i].audioValue == null)
                        {
                            if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The specified key '" + localizationKey + "' could not be found or the output value is empty."); }
                            break;
                        }

                        audioObj.clip = activeTable.tableContent[i].audioValue;
                        break;
                    }
                }
            }

            else if (objectType == ObjectType.Image && imageObj != null)
            {
                for (int i = 0; i < activeTable.tableContent.Count; i++)
                {
                    if (localizationKey == activeTable.tableContent[i].key)
                    {
                        if (activeTable.tableContent[i].spriteValue == null)
                        {
                            if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The specified key '" + localizationKey + "' could not be found or the output value is empty."); }
                            break;
                        }

                        imageObj.sprite = activeTable.tableContent[i].spriteValue;
                        break;
                    }
                }
            }

            else if (objectType == ObjectType.Custom || objectType == ObjectType.ComponentDriven)
            {
                for (int i = 0; i < activeTable.tableContent.Count; i++)
                {
                    if (localizationKey == activeTable.tableContent[i].key)
                    {
                        if (string.IsNullOrEmpty(activeTable.tableContent[i].value))
                            break;

                        onLanguageChanged.Invoke(activeTable.tableContent[i].value);
                        break;
                    }
                }
            }

            if (rebuildLayoutOnUpdate && gameObject.activeInHierarchy)
            {
                StartCoroutine(RebuildLayout());
            }
        }

        public bool CheckLocalizationStatus()
        {
            if (!isInitialized) { InitializeItem(); }
            if (localizationManager == null || localizationManager.UIManagerAsset == null || localizationManager.UIManagerAsset.enableLocalization == false) { return false; }
            else { return true; }
        }

        public string GetKeyOutput(string key)
        {
            string keyValue = null;
            bool keyFound = false;

            if (localizationManager != null && localizationManager.currentLanguageAsset == null) { localizationManager.InitializeLanguage(); }
            if (!TryGetActiveTable(out var activeTable))
                return LocalizationSettings.notInitializedText;

            for (int i = 0; i < activeTable.tableContent.Count; i++)
            {
                if (key == activeTable.tableContent[i].key)
                {
                    keyValue = activeTable.tableContent[i].value;
                    keyFound = true;
                    break;
                }
            }

            if (keyFound && string.IsNullOrEmpty(keyValue))
            {
                if (LocalizationManager.enableLogs == true) { Debug.Log("<b>[Localized Object]</b> The output value for '" + key + "' is empty in " + localizationManager.currentLanguageAsset.languageID + ": " + key); }
                return "EMPTY_KEY_IN_" + localizationManager.currentLanguageAsset.languageID + ": " + key;
            }

            else if (!keyFound)
            {
                if (LocalizationManager.enableLogs == true) { Debug.Log("<b>[Localized Object]</b> The specified key '" + key + "' could not be found in " + localizationManager.currentLanguageAsset.languageID + ": " + key); }
                return "MISSING_KEY_IN_" + localizationManager.currentLanguageAsset.languageID + ": " + key;
            }

            return keyValue;
        }

        public AudioClip GetKeyOutputAudio(string key)
        {
            AudioClip keyValue = null;
            bool keyFound = false;

            if (localizationManager != null && localizationManager.currentLanguageAsset == null) { localizationManager.InitializeLanguage(); }
            if (!TryGetActiveTable(out var activeTable))
                return null;

            for (int i = 0; i < activeTable.tableContent.Count; i++)
            {
                if (key == activeTable.tableContent[i].key)
                {
                    keyValue = activeTable.tableContent[i].audioValue;
                    keyFound = true;
                    break;
                }
            }

            if (keyFound && keyValue == null)
            {
                if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The output value for '" + key + "' is empty in " + localizationManager.currentLanguageAsset.languageName + ": " + key); }
                return null;
            }

            else if (!keyFound)
            {
                if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The specified key '" + key + "' could not be found in " + localizationManager.currentLanguageAsset.languageName + ": " + key); }
                return null;
            }

            return keyValue;
        }

        public Sprite GetKeyOutputSprite(string key)
        {
            Sprite keyValue = null;
            bool keyFound = false;

            if (localizationManager != null && localizationManager.currentLanguageAsset == null) { localizationManager.InitializeLanguage(); }
            if (!TryGetActiveTable(out var activeTable))
                return null;

            for (int i = 0; i < activeTable.tableContent.Count; i++)
            {
                if (key == activeTable.tableContent[i].key)
                {
                    keyValue = activeTable.tableContent[i].spriteValue;
                    keyFound = true;
                    break;
                }
            }

            if (keyFound && keyValue == null)
            {
                if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The output value for '" + key + "' is empty in " + localizationManager.currentLanguageAsset.languageName + ": " + key); }
                return null;
            }

            else if (!keyFound)
            {
                if (LocalizationManager.enableLogs) { Debug.Log("<b>[Localized Object]</b> The specified key '" + key + "' could not be found in " + localizationManager.currentLanguageAsset.languageName + ": " + key); }
                return null;
            }

            return keyValue;
        }

        public static string GetKeyOutput(string tableID, string tableKey)
        {
            UIManager tempUIM = (UIManager)Resources.FindObjectsOfTypeAll(typeof(UIManager))[0];

            if (tempUIM == null || !tempUIM.enableLocalization)
                return null;

            int tableIndex = -1;

            for (int i = 0; i < tempUIM.currentLanguage.tableList.Count; i++)
            {
                if (tempUIM.currentLanguage.tableList[i].table.tableID == tableID)
                {
                    tableIndex = i;
                    break;
                }
            }

            if (tableIndex == -1) { return null; }
            else
            {
                for (int i = 0; i < tempUIM.currentLanguage.tableList[tableIndex].tableContent.Count; i++)
                {
                    if (tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].key == tableKey)
                    {
                        return tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].value;
                    }
                }
            }

            return null;
        }

        public static AudioClip GetKeyOutputAudio(string tableID, string tableKey)
        {
            UIManager tempUIM = (UIManager)Resources.FindObjectsOfTypeAll(typeof(UIManager))[0];

            if (tempUIM == null || !tempUIM.enableLocalization)
                return null;

            int tableIndex = -1;

            for (int i = 0; i < tempUIM.currentLanguage.tableList.Count; i++)
            {
                if (tempUIM.currentLanguage.tableList[i].table.tableID == tableID)
                {
                    tableIndex = i;
                    break;
                }
            }

            if (tableIndex == -1) { return null; }
            else
            {
                for (int i = 0; i < tempUIM.currentLanguage.tableList[tableIndex].tableContent.Count; i++)
                {
                    if (tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].key == tableKey)
                    {
                        return tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].audioValue;
                    }
                }
            }

            return null;
        }

        public static Sprite GetKeyOutputSprite(string tableID, string tableKey)
        {
            UIManager tempUIM = (UIManager)Resources.FindObjectsOfTypeAll(typeof(UIManager))[0];

            if (tempUIM == null || !tempUIM.enableLocalization)
                return null;

            int tableIndex = -1;

            for (int i = 0; i < tempUIM.currentLanguage.tableList.Count; i++)
            {
                if (tempUIM.currentLanguage.tableList[i].table.tableID == tableID)
                {
                    tableIndex = i;
                    break;
                }
            }

            if (tableIndex == -1) { return null; }
            else
            {
                for (int i = 0; i < tempUIM.currentLanguage.tableList[tableIndex].tableContent.Count; i++)
                {
                    if (tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].key == tableKey)
                    {
                        return tempUIM.currentLanguage.tableList[tableIndex].tableContent[i].spriteValue;
                    }
                }
            }

            return null;
        }

        IEnumerator RebuildLayout()
        {
            yield return new WaitForSecondsRealtime(0.025f);

            if (transform.parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
            }
            else
            {
                var rect = GetComponent<RectTransform>();
                if (rect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }
}
