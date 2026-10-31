using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Michsky.UI.Reach;
using JUTPS.JUInputSystem;
using Railbound.Gameplay;

namespace Railbound.UI
{
    [ExecuteAlways]
    public class KeyBindingSettingsManager : MonoBehaviour
    {
        public static KeyBindingSettingsManager Instance { get; private set; }

        [System.Serializable]
        public class KeyBindEntry
        {
            public string displayName;
            public string actionName;
            public int bindingIndex;
            public bool isSonarAction;
            public bool isHealAction;
            [HideInInspector] public ButtonManager buttonManager;
            [HideInInspector] public GameObject rowObject;
        }

        [Header("UI Hierarchy References")]
        [SerializeField] private Transform layoutGroupTransform;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bindSuccessClip;
        [SerializeField] private AudioClip bindCancelClip;

        private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;
        private KeyBindEntry _activeRebindEntry;
        private Coroutine _customRebindCoroutine;
        private JUTPSInputControlls _standaloneInputs;

        [SerializeField]
        public List<KeyBindEntry> bindEntries = new List<KeyBindEntry>(); // Populated via Inspector

        private void Awake()
        {
            if (Application.isPlaying) Instance = this;
        }

        private void Start() => InitializeKeyBindingUI();
        private void OnEnable() => UpdateAllButtonLabels();
        private void OnDisable() => CancelActiveRebinding();

        public JUInputManager GetJUInput() => FindObjectOfType<JUInputManager>() ?? JUInput.Instance();

        public JUTPSInputControlls GetInputActions()
        {
            var juInput = GetJUInput();
            if (juInput != null) return juInput.InputActions;

            if (_standaloneInputs == null)
            {
                _standaloneInputs = new JUTPSInputControlls();
                string saved = PlayerPrefs.GetString(JUInputManager.BINDING_OVERRIDES_KEY, string.Empty);
                if (!string.IsNullOrEmpty(saved)) _standaloneInputs.asset.LoadBindingOverridesFromJson(saved);
            }
            return _standaloneInputs;
        }

        public void InitializeKeyBindingUI()
        {
            if (layoutGroupTransform == null) return;

            List<GameObject> existingItems = new List<GameObject>();
            for (int i = 0; i < layoutGroupTransform.childCount; i++)
            {
                var child = layoutGroupTransform.GetChild(i);
                if (child.name.StartsWith("Settings Item (Binding)")) existingItems.Add(child.gameObject);
            }

            GameObject template = existingItems.Count > 0 ? existingItems[0] : null;
            if (template == null) return;

            for (int i = 0; i < bindEntries.Count; i++)
            {
                var entry = bindEntries[i];
                GameObject row = (i < existingItems.Count) ? existingItems[i] : Instantiate(template, layoutGroupTransform);
                
                SetupRowUI(row, entry.displayName, (btnMgr) => {
                    entry.buttonManager = btnMgr;
                    btnMgr.onClick.AddListener(() => OnBindingButtonClicked(entry));
                });
                
                entry.rowObject = row;
            }

            for (int i = bindEntries.Count; i < existingItems.Count; i++) existingItems[i].SetActive(false);

            SetupResetButton(template);
            UpdateAllButtonLabels();
        }

        private void SetupRowUI(GameObject row, string title, Action<ButtonManager> onButtonSetup)
        {
            row.SetActive(true);
            var textObj = row.transform.Find("Text");
            if (textObj != null)
            {
                if (textObj.TryGetComponent<LocalizedObject>(out var loc)) loc.enabled = false;
                if (textObj.TryGetComponent<TextMeshProUGUI>(out var tmp)) tmp.text = title.ToUpper();
            }

            row.transform.Find("Binding 2")?.gameObject.SetActive(false);

            var b1 = row.transform.Find("Binding 1");
            if (b1 != null)
            {
                if (b1.TryGetComponent<LocalizedObject>(out var locB)) locB.enabled = false;
                if (b1.TryGetComponent<ButtonManager>(out var btnMgr))
                {
                    btnMgr.useLocalization = false;
                    btnMgr.onClick.RemoveAllListeners();
                    onButtonSetup?.Invoke(btnMgr);
                }
            }
        }

        private void SetupResetButton(GameObject template)
        {
            if (layoutGroupTransform.Find("Reset Keybindings Button") != null) return;

            var resetRow = Instantiate(template, layoutGroupTransform);
            resetRow.name = "Reset Keybindings Button";
            resetRow.transform.SetAsLastSibling();

            SetupRowUI(resetRow, "<color=#E57373>RESET ALL KEY BINDINGS</color>", (btnMgr) => {
                btnMgr.SetText("RESTORE DEFAULTS");
                btnMgr.onClick.AddListener(ResetAllToDefaults);
            });
        }

        public void UpdateAllButtonLabels()
        {
            var inputActions = GetInputActions();
            foreach (var entry in bindEntries)
            {
                if (entry.buttonManager == null) continue;

                if (entry.isSonarAction) entry.buttonManager.SetText(FormatKeyDisplay(PlayerPrefs.GetString("Player_Sonar_Key", "I")));
                else if (entry.isHealAction) entry.buttonManager.SetText(FormatKeyDisplay(PlayerPrefs.GetString("Player_Heal_Key", "H")));
                else if (inputActions != null)
                {
                    var action = inputActions.asset.FindAction(entry.actionName);
                    if (action != null && entry.bindingIndex >= 0 && entry.bindingIndex < action.bindings.Count)
                    {
                        entry.buttonManager.SetText(FormatKeyDisplay(action.GetBindingDisplayString(entry.bindingIndex)));
                    }
                }
            }
        }

        private string FormatKeyDisplay(string raw) => string.IsNullOrEmpty(raw) ? "NONE" : (raw.ToUpper() == "DELTA" || raw.ToUpper() == "POSITION") ? "MOUSE" : $"[ {raw.ToUpper()} ]";

        public void OnBindingButtonClicked(KeyBindEntry entry)
        {
            if (!Application.isPlaying) return;
            CancelActiveRebinding();

            _activeRebindEntry = entry;
            entry.buttonManager.SetText("<color=#FFD54F>PRESS ANY KEY...</color>");

            if (entry.isSonarAction || entry.isHealAction)
            {
                string prefKey = entry.isSonarAction ? "Player_Sonar_Key" : "Player_Heal_Key";
                KeyCode defaultKey = entry.isSonarAction ? KeyCode.I : KeyCode.H;
                
                _customRebindCoroutine = StartCoroutine(ListenForCustomKey(entry, prefKey, defaultKey, code => {
                    if (entry.isSonarAction && FindObjectOfType<PlayerSonarSkill>() is PlayerSonarSkill sonar) sonar.triggerKey = code;
                    else if (entry.isHealAction && FindObjectOfType<PlayerMedkitSystem>() is PlayerMedkitSystem medkit) medkit.SetHealKey(code);
                }));
            }
            else
            {
                StartStandardRebinding(entry);
            }
        }

        private void StartStandardRebinding(KeyBindEntry entry)
        {
            var inputActions = GetInputActions();
            var action = inputActions?.asset.FindAction(entry.actionName);
            if (action == null) return;

            action.Disable();
            _rebindingOperation = action.PerformInteractiveRebinding(entry.bindingIndex)
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => { CleanupRebind(action, op, true); })
                .OnCancel(op => { CleanupRebind(action, op, false); })
                .Start();
        }

        private void CleanupRebind(InputAction action, InputActionRebindingExtensions.RebindingOperation op, bool success)
        {
            action.Enable();
            op.Dispose();
            _rebindingOperation = null;
            _activeRebindEntry = null;
            
            if (success) GetJUInput()?.SaveCurrentBindingOverrides();
            UpdateAllButtonLabels();
            PlaySound(success ? bindSuccessClip : bindCancelClip);
        }

        private IEnumerator ListenForCustomKey(KeyBindEntry entry, string prefKey, KeyCode defaultKey, Action<KeyCode> onAssigned)
        {
            yield return null;
            while (true)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { PlaySound(bindCancelClip); break; }

                foreach (var key in Keyboard.current.allKeys)
                {
                    if (key.wasPressedThisFrame && (Enum.TryParse(key.name.ToUpper(), true, out KeyCode code) || Enum.TryParse("Alpha" + key.name.ToUpper(), true, out code)))
                    {
                        PlayerPrefs.SetString(prefKey, code.ToString());
                        onAssigned?.Invoke(code);
                        PlaySound(bindSuccessClip);
                        goto EndLoop;
                    }
                }
                yield return null;
            }
            EndLoop:
            _customRebindCoroutine = null;
            _activeRebindEntry = null;
            UpdateAllButtonLabels();
        }

        public void CancelActiveRebinding()
        {
            _rebindingOperation?.Cancel();
            if (_customRebindCoroutine != null) StopCoroutine(_customRebindCoroutine);
            _activeRebindEntry = null;
            UpdateAllButtonLabels();
        }

        public void ResetAllToDefaults()
        {
            CancelActiveRebinding();
            GetJUInput()?.ResetAllBindingOverrides();
            
            PlayerPrefs.DeleteKey("Player_Sonar_Key");
            PlayerPrefs.DeleteKey("Player_Heal_Key");
            
            if (FindObjectOfType<PlayerSonarSkill>() is PlayerSonarSkill sonar) sonar.triggerKey = KeyCode.I;
            if (FindObjectOfType<PlayerMedkitSystem>() is PlayerMedkitSystem medkit) medkit.SetHealKey(KeyCode.H);

            UpdateAllButtonLabels();
            PlaySound(bindSuccessClip);
        }

        private void PlaySound(AudioClip clip) { if (audioSource != null && clip != null) audioSource.PlayOneShot(clip); }
    }
}
