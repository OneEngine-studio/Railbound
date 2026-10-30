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
    /// <summary>
    /// Manages the in-game Keyboard Settings panel in Reach UI, allowing players to customize all keybindings.
    /// Supports Unity's New Input System actions (WASD, Sprint, Jump, Shoot, Aim, etc.) and custom abilities (Sonar).
    /// </summary>
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
            public ButtonManager buttonManager;
            public GameObject rowObject;
        }

        [Header("UI Hierarchy References")]
        [SerializeField] private Transform layoutGroupTransform;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bindSuccessClip;
        [SerializeField] private AudioClip bindCancelClip;

        private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;
        private KeyBindEntry _activeRebindEntry;
        private Coroutine _sonarRebindCoroutine;
        private JUTPSInputControlls _standaloneInputs;

        // All customizable player actions
        [SerializeField]
        public List<KeyBindEntry> bindEntries = new List<KeyBindEntry>
        {
            new KeyBindEntry { displayName = "Move Forward", actionName = "Move", bindingIndex = 2 },
            new KeyBindEntry { displayName = "Move Backward", actionName = "Move", bindingIndex = 4 },
            new KeyBindEntry { displayName = "Move Left", actionName = "Move", bindingIndex = 6 },
            new KeyBindEntry { displayName = "Move Right", actionName = "Move", bindingIndex = 8 },
            new KeyBindEntry { displayName = "Sprint / Run", actionName = "Run", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Jump", actionName = "Jump", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Crouch", actionName = "Crouch", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Prone", actionName = "Prone", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Combat Roll", actionName = "Roll", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Shoot / Attack", actionName = "Fire", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Aim Down Sights", actionName = "Aim", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Reload Weapon", actionName = "Reload", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Interact", actionName = "Interact", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Pick Up Item", actionName = "Pickup", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Next Weapon", actionName = "Next", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Previous Weapon", actionName = "Previous", bindingIndex = 0 },
            new KeyBindEntry { displayName = "Open Inventory", actionName = "Open Inventory", bindingIndex = 1 },
            new KeyBindEntry { displayName = "Use Medkit / Heal", actionName = "Heal", bindingIndex = 0, isHealAction = true },
            new KeyBindEntry { displayName = "World Scan (Sonar)", actionName = "Sonar", bindingIndex = 0, isSonarAction = true }
        };

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            InitializeKeyBindingUI();
        }

        private void OnEnable()
        {
            UpdateAllButtonLabels();
        }

        private void OnDisable()
        {
            CancelActiveRebinding();
        }

        public JUInputManager GetJUInput()
        {
            return FindObjectOfType<JUInputManager>() ?? JUInput.Instance();
        }

        public JUTPSInputControlls GetInputActions()
        {
            var juInput = GetJUInput();
            if (juInput != null) return juInput.InputActions;

            if (_standaloneInputs == null)
            {
                _standaloneInputs = new JUTPSInputControlls();
                string saved = PlayerPrefs.GetString(JUInputManager.BINDING_OVERRIDES_KEY, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    _standaloneInputs.asset.LoadBindingOverridesFromJson(saved);
                }
            }
            return _standaloneInputs;
        }

        /// <summary>
        /// Locates or builds the keybinding UI elements inside the Controls layout group.
        /// </summary>
        public void InitializeKeyBindingUI()
        {
            if (layoutGroupTransform == null)
            {
                layoutGroupTransform = transform.Find("Content/List/Layout Group");
                if (layoutGroupTransform == null)
                {
                    var canvas = GameObject.Find("Canvas - Pause Menu");
                    if (canvas != null)
                    {
                        layoutGroupTransform = canvas.transform.Find("Pause Menu/Settings/Content/Panel Content/Panels/Controls/Content/List/Layout Group");
                    }
                }
            }

            if (layoutGroupTransform == null) return;

            // Find all existing Settings Item (Binding) objects in layout
            List<GameObject> existingItems = new List<GameObject>();
            for (int i = 0; i < layoutGroupTransform.childCount; i++)
            {
                var child = layoutGroupTransform.GetChild(i);
                if (child.name.StartsWith("Settings Item (Binding)"))
                {
                    existingItems.Add(child.gameObject);
                }
            }

            GameObject template = existingItems.Count > 0 ? existingItems[0] : null;

            // Setup or instantiate rows for each action
            for (int i = 0; i < bindEntries.Count; i++)
            {
                var entry = bindEntries[i];
                GameObject row;

                if (i < existingItems.Count)
                {
                    row = existingItems[i];
                }
                else if (template != null)
                {
                    row = Instantiate(template, layoutGroupTransform);
                    row.name = $"Settings Item (Binding) - {entry.displayName}";
                }
                else
                {
                    continue;
                }

                entry.rowObject = row;
                row.SetActive(true);

                // Set Title Text
                var textObj = row.transform.Find("Text");
                if (textObj != null)
                {
                    var loc = textObj.GetComponent<LocalizedObject>();
                    if (loc != null) loc.enabled = false;

                    var tmp = textObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = entry.displayName.ToUpper();
                    }
                }

                // Hide secondary binding button to maintain clean modern look
                var b2 = row.transform.Find("Binding 2");
                if (b2 != null) b2.gameObject.SetActive(false);

                // Setup primary binding button
                var b1 = row.transform.Find("Binding 1");
                if (b1 != null)
                {
                    var locB = b1.GetComponent<LocalizedObject>();
                    if (locB != null) locB.enabled = false;

                    var btnMgr = b1.GetComponent<ButtonManager>();
                    if (btnMgr != null)
                    {
                        entry.buttonManager = btnMgr;
                        btnMgr.useLocalization = false;
                        btnMgr.onClick.RemoveAllListeners();
                        var capturedEntry = entry;
                        btnMgr.onClick.AddListener(() => OnBindingButtonClicked(capturedEntry));
                    }
                }
            }

            // Remove any excess template items beyond our configured list
            for (int i = bindEntries.Count; i < existingItems.Count; i++)
            {
                existingItems[i].SetActive(false);
            }

            // Add or configure "Reset to Default" button at the bottom of the list
            SetupResetButton();

            // Refresh all button text with saved/current bindings
            UpdateAllButtonLabels();
        }

        private void SetupResetButton()
        {
            if (layoutGroupTransform == null) return;

            Transform existingReset = layoutGroupTransform.Find("Reset Keybindings Button");
            if (existingReset != null) return;

            var firstRow = layoutGroupTransform.Find("Settings Item (Binding)");
            if (firstRow != null)
            {
                var resetRow = Instantiate(firstRow.gameObject, layoutGroupTransform);
                resetRow.name = "Reset Keybindings Button";
                resetRow.transform.SetAsLastSibling();

                var textObj = resetRow.transform.Find("Text");
                if (textObj != null)
                {
                    var loc = textObj.GetComponent<LocalizedObject>();
                    if (loc != null) loc.enabled = false;

                    var tmp = textObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "<color=#E57373>RESET ALL KEY BINDINGS</color>";
                }

                var b2 = resetRow.transform.Find("Binding 2");
                if (b2 != null) b2.gameObject.SetActive(false);

                var b1 = resetRow.transform.Find("Binding 1");
                if (b1 != null)
                {
                    var locB = b1.GetComponent<LocalizedObject>();
                    if (locB != null) locB.enabled = false;

                    var btnMgr = b1.GetComponent<ButtonManager>();
                    if (btnMgr != null)
                    {
                        btnMgr.useLocalization = false;
                        btnMgr.SetText("RESTORE DEFAULTS");
                        btnMgr.onClick.RemoveAllListeners();
                        btnMgr.onClick.AddListener(ResetAllToDefaults);
                    }
                }
            }
        }

        /// <summary>
        /// Updates the text on all binding buttons to match current input overrides.
        /// </summary>
        public void UpdateAllButtonLabels()
        {
            var inputActions = GetInputActions();

            foreach (var entry in bindEntries)
            {
                if (entry.buttonManager == null) continue;

                if (entry.isSonarAction)
                {
                    string sonarKey = PlayerPrefs.GetString("Player_Sonar_Key", "I");
                    entry.buttonManager.SetText(FormatKeyDisplay(sonarKey));
                }
                else if (entry.isHealAction)
                {
                    string healKey = PlayerPrefs.GetString("Player_Heal_Key", "H");
                    entry.buttonManager.SetText(FormatKeyDisplay(healKey));
                }
                else if (inputActions != null)
                {
                    var action = inputActions.asset.FindAction(entry.actionName);
                    if (action != null && entry.bindingIndex >= 0 && entry.bindingIndex < action.bindings.Count)
                    {
                        string display = action.GetBindingDisplayString(entry.bindingIndex);
                        entry.buttonManager.SetText(FormatKeyDisplay(display));
                    }
                }
            }
        }

        private string FormatKeyDisplay(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "NONE";
            raw = raw.ToUpper();
            if (raw == "DELTA" || raw == "POSITION") return "MOUSE";
            return $"[ {raw} ]";
        }

        /// <summary>
        /// Initiates interactive rebinding for the given action entry.
        /// </summary>
        public void OnBindingButtonClicked(KeyBindEntry entry)
        {
            if (!Application.isPlaying) return;

            CancelActiveRebinding();

            _activeRebindEntry = entry;
            entry.buttonManager.SetText("<color=#FFD54F>PRESS ANY KEY...</color>");

            if (entry.isSonarAction)
            {
                _sonarRebindCoroutine = StartCoroutine(ListenForCustomKey(entry, "Player_Sonar_Key", KeyCode.I, code =>
                {
                    var playerSonar = FindObjectOfType<PlayerSonarSkill>();
                    if (playerSonar != null) playerSonar.triggerKey = code;
                }));
            }
            else if (entry.isHealAction)
            {
                _sonarRebindCoroutine = StartCoroutine(ListenForCustomKey(entry, "Player_Heal_Key", KeyCode.H, code =>
                {
                    var medkitSys = FindObjectOfType<PlayerMedkitSystem>();
                    if (medkitSys != null) medkitSys.SetHealKey(code);
                }));
            }
            else
            {
                var juInput = GetJUInput();
                var inputActions = GetInputActions();
                if (inputActions == null) return;

                var action = inputActions.asset.FindAction(entry.actionName);
                if (action == null) return;

                action.Disable();

                _rebindingOperation = action.PerformInteractiveRebinding(entry.bindingIndex)
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .OnMatchWaitForAnother(0.1f)
                    .OnComplete(op =>
                    {
                        action.Enable();
                        op.Dispose();
                        _rebindingOperation = null;
                        _activeRebindEntry = null;

                        juInput?.SaveCurrentBindingOverrides();
                        UpdateAllButtonLabels();
                        PlaySound(bindSuccessClip);
                    })
                    .OnCancel(op =>
                    {
                        action.Enable();
                        op.Dispose();
                        _rebindingOperation = null;
                        _activeRebindEntry = null;

                        UpdateAllButtonLabels();
                        PlaySound(bindCancelClip);
                    });

                _rebindingOperation.Start();
            }
        }

        private IEnumerator ListenForCustomKey(KeyBindEntry entry, string prefKey, KeyCode defaultKey, Action<KeyCode> onAssigned)
        {
            yield return null; // Wait 1 frame so click doesn't register

            bool done = false;
            while (!done)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        done = true;
                        PlaySound(bindCancelClip);
                        break;
                    }

                    foreach (var key in Keyboard.current.allKeys)
                    {
                        if (key.wasPressedThisFrame)
                        {
                            string keyName = key.name.ToUpper();
                            if (Enum.TryParse<KeyCode>(keyName, true, out var code) ||
                                Enum.TryParse<KeyCode>("Alpha" + keyName, true, out code))
                            {
                                PlayerPrefs.SetString(prefKey, code.ToString());
                                PlayerPrefs.Save();

                                onAssigned?.Invoke(code);

                                PlaySound(bindSuccessClip);
                                done = true;
                                break;
                            }
                        }
                    }
                }

                yield return null;
            }

            _sonarRebindCoroutine = null;
            _activeRebindEntry = null;
            UpdateAllButtonLabels();
        }

        public void CancelActiveRebinding()
        {
            if (_rebindingOperation != null)
            {
                _rebindingOperation.Cancel();
                _rebindingOperation = null;
            }

            if (_sonarRebindCoroutine != null)
            {
                StopCoroutine(_sonarRebindCoroutine);
                _sonarRebindCoroutine = null;
            }

            if (_activeRebindEntry != null)
            {
                _activeRebindEntry = null;
                UpdateAllButtonLabels();
            }
        }

        /// <summary>
        /// Resets all player key bindings back to factory defaults.
        /// </summary>
        public void ResetAllToDefaults()
        {
            CancelActiveRebinding();

            var juInput = GetJUInput();
            if (juInput != null)
            {
                juInput.ResetAllBindingOverrides();
            }

            PlayerPrefs.DeleteKey("Player_Sonar_Key");
            PlayerPrefs.DeleteKey("Player_Heal_Key");
            PlayerPrefs.Save();

            var playerSonar = FindObjectOfType<PlayerSonarSkill>();
            if (playerSonar != null) playerSonar.triggerKey = KeyCode.I;

            var medkitSys = FindObjectOfType<PlayerMedkitSystem>();
            if (medkitSys != null) medkitSys.SetHealKey(KeyCode.H);

            UpdateAllButtonLabels();
            PlaySound(bindSuccessClip);
            Debug.Log("[KeyBindingSettingsManager] All keybindings restored to defaults.");
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
