using System;
using System.Collections.Generic;
using UnityEngine;

namespace Railbound.UI
{
    [System.Serializable]
    public class OperatorDefinition
    {
        public string operatorId;
        public string displayName;
        public string roleCategory; // Assault, Recon, Vanguard, Support
        public GameObject characterPrefab;
        [TextArea(2, 3)] public string activeSkillName;
        [TextArea(2, 3)] public string activeSkillDesc;
        [TextArea(2, 3)] public string passiveSkillName;
        [TextArea(2, 3)] public string passiveSkillDesc;
        [TextArea(2, 3)] public string ultimateSkillName;
        [TextArea(2, 3)] public string ultimateSkillDesc;
    }

    /// <summary>
    /// Manages the 3D character preview in the middle of GameLobby:
    /// - Instantiates and switches operator models.
    /// - Plays idle animations and emotes.
    /// - Handles smooth 360-degree drag rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyCharacterPreviewManager : MonoBehaviour
    {
        public static LobbyCharacterPreviewManager Instance { get; private set; }

        [Header("Spawn Platform & Transform")]
        [SerializeField] private Transform characterSpawnAnchor;
        [SerializeField] private float rotationSpeed = 200f;
        [SerializeField] private float rotationSmoothDamp = 8f;

        [Header("Operator Rosters")]
        [SerializeField] private List<OperatorDefinition> operators = new List<OperatorDefinition>();
        [SerializeField] private int currentOperatorIndex = 0;

        private GameObject currentCharacterInstance;
        private Animator currentAnimator;
        private float targetYRotation = 0f;
        private float currentYRotation = 0f;
        private bool isDragging = false;
        private Vector2 lastPointerPos;

        public event Action<OperatorDefinition> OnOperatorChanged;
        public event Action<int> OnEmoteTriggered;

        public OperatorDefinition CurrentOperator => (operators != null && operators.Count > currentOperatorIndex && currentOperatorIndex >= 0) 
            ? operators[currentOperatorIndex] : null;

        public int CurrentOperatorIndex => currentOperatorIndex;
        public int TotalOperators => operators != null ? operators.Count : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (characterSpawnAnchor == null)
            {
                characterSpawnAnchor = transform;
            }
            EnsureDefaultOperators();
            SpawnOperator(currentOperatorIndex);
        }

        private void EnsureDefaultOperators()
        {
            if (operators == null) operators = new List<OperatorDefinition>();
            GameObject basePrefab = (operators.Count > 0 && operators[0].characterPrefab != null)
                ? operators[0].characterPrefab
                : null;

            if (operators.Count == 1 && basePrefab != null)
            {
                operators[0].displayName = "MIL-R5 GHOST";
                operators[0].roleCategory = "ASSAULT / SPEC-OPS";
                operators[0].activeSkillName = "TACTICAL CLOAK";
                operators[0].activeSkillDesc = "Engages optical thermoptic camouflage for 6s, masking thermal and radar signatures.";
                operators[0].passiveSkillName = "SILENT STEP";
                operators[0].passiveSkillDesc = "Reduces sprint footstep audio by 60% and highlights nearby enemy recon traps.";
                operators[0].ultimateSkillName = "EMP OVERDRIVE";
                operators[0].ultimateSkillDesc = "Detonates a directional electromagnetic pulse disabling electronics and shields in a 25m radius.";

                operators.Add(new OperatorDefinition
                {
                    operatorId = "op_vanguard",
                    displayName = "KRUGER-9 VANGUARD",
                    roleCategory = "HEAVY / BREACHER",
                    characterPrefab = basePrefab,
                    activeSkillName = "BALLISTIC BARRICADE",
                    activeSkillDesc = "Deploys a reinforced energy-absorbent cover wall capable of blocking heavy caliber fire.",
                    passiveSkillName = "FLAK PLATING",
                    passiveSkillDesc = "Reduces incoming explosive and shrapnel damage by 35% while fortified.",
                    ultimateSkillName = "SHOCKWAVE MORTAR",
                    ultimateSkillDesc = "Launches a high-yield concussive barrage that breaches cover and staggers enemy squads."
                });

                operators.Add(new OperatorDefinition
                {
                    operatorId = "op_recon",
                    displayName = "SPECTRE-X RECON",
                    roleCategory = "RECON / MARKSMAN",
                    characterPrefab = basePrefab,
                    activeSkillName = "SONAR PULSE DRONE",
                    activeSkillDesc = "Deploys an autonomous micro-UAV that scans and tags hostile positions through walls.",
                    passiveSkillName = "EAGLE EYE",
                    passiveSkillDesc = "Aiming down sights automatically ranges targets and marks snipers for your squad.",
                    ultimateSkillName = "ORBITAL TARGET LINK",
                    ultimateSkillDesc = "Links squad HUDs to satellite telemetry, revealing real-time enemy movement vectors for 10s."
                });

                operators.Add(new OperatorDefinition
                {
                    operatorId = "op_support",
                    displayName = "VALKYRIE-7 MEDIC",
                    roleCategory = "SUPPORT / FIELD MEDIC",
                    characterPrefab = basePrefab,
                    activeSkillName = "NANO-STIM DART",
                    activeSkillDesc = "Fires a ranged regenerative nano-injector that rapidly restores ally health and stamina.",
                    passiveSkillName = "COMBAT TRIAGE",
                    passiveSkillDesc = "Revives downed squadmates 40% faster and grants a temporary overshield upon revival.",
                    ultimateSkillName = "AEGIS DOME FIELD",
                    ultimateSkillDesc = "Projects an impenetrable 360-degree medical sanctuary dome with rapid squad regeneration."
                });
            }
        }

        private void Update()
        {
            // Smoothly damp rotation to target
            currentYRotation = Mathf.Lerp(currentYRotation, targetYRotation, Time.deltaTime * rotationSmoothDamp);
            if (characterSpawnAnchor != null)
            {
                characterSpawnAnchor.localEulerAngles = new Vector3(0f, currentYRotation, 0f);
            }
        }

        public void NextOperator()
        {
            if (operators == null || operators.Count <= 1) return;
            int nextIdx = (currentOperatorIndex + 1) % operators.Count;
            SelectOperator(nextIdx);
        }

        public void PreviousOperator()
        {
            if (operators == null || operators.Count <= 1) return;
            int prevIdx = (currentOperatorIndex - 1 + operators.Count) % operators.Count;
            SelectOperator(prevIdx);
        }

        public void SelectOperator(int index)
        {
            if (operators == null || index < 0 || index >= operators.Count) return;
            currentOperatorIndex = index;
            SpawnOperator(currentOperatorIndex);
            // Subtle presentation turn-in when switching operators
            currentYRotation = targetYRotation - 22f;
            OnOperatorChanged?.Invoke(CurrentOperator);
        }

        private void SpawnOperator(int index)
        {
            if (currentCharacterInstance != null)
            {
                Destroy(currentCharacterInstance);
            }

            // Remove any editor-baked preview models so only one character exists at runtime
            if (characterSpawnAnchor != null)
            {
                for (int i = characterSpawnAnchor.childCount - 1; i >= 0; i--)
                {
                    var child = characterSpawnAnchor.GetChild(i).gameObject;
                    if (child != currentCharacterInstance) Destroy(child);
                }
            }

            var op = CurrentOperator;
            if (op == null || op.characterPrefab == null)
            {
                Debug.LogWarning("[LobbyPreview] Operator prefab missing for index " + index);
                return;
            }

            currentCharacterInstance = Instantiate(op.characterPrefab, characterSpawnAnchor);
            // Offset (-0.18f, 0f, -0.05f) cancels the FBX's +0.18m hip offset so the character is centered on the anchor
            currentCharacterInstance.transform.localPosition = new Vector3(-0.18f, 0f, -0.05f);
            // Models face +Z; the preview camera looks down +Z, so turn the model 180° to face it
            currentCharacterInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            currentCharacterInstance.transform.localScale = Vector3.one;

            currentAnimator = currentCharacterInstance.GetComponent<Animator>();
            if (currentAnimator != null)
            {
                currentAnimator.enabled = true;
                currentAnimator.applyRootMotion = false;
            }

            ApplyOperatorVisualVariant(index);

            Debug.Log($"[LobbyPreview] Spawned Operator [{index}]: {op.displayName}");
        }

        private void ApplyOperatorVisualVariant(int index)
        {
            if (currentCharacterInstance == null) return;

            // Subtle tactical uniform tint variation per operator class so switching operators is visually distinct
            Color[] classTints = new Color[]
            {
                new Color(1.00f, 1.00f, 1.00f, 1f), // 0: MIL-R5 GHOST (Standard Spec-Ops Olive/Black)
                new Color(0.82f, 0.88f, 0.96f, 1f), // 1: KRUGER-9 VANGUARD (Urban Steel Blue-Grey)
                new Color(0.76f, 0.92f, 0.84f, 1f), // 2: SPECTRE-X RECON (Night-Ops Cyber Teal)
                new Color(0.96f, 0.88f, 0.80f, 1f)  // 3: VALKYRIE-7 MEDIC (Desert Sand / Amber)
            };

            Color tint = classTints[Mathf.Abs(index) % classTints.Length];
            var renderers = currentCharacterInstance.GetComponentsInChildren<Renderer>(true);
            var propBlock = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", tint);
                propBlock.SetColor("_Color", tint);
                r.SetPropertyBlock(propBlock);
            }
        }

        public void PlayEmote(int emoteId)
        {
            if (currentAnimator == null)
            {
                if (currentCharacterInstance != null)
                    currentAnimator = currentCharacterInstance.GetComponent<Animator>();
            }

            if (currentAnimator != null)
            {
                currentAnimator.applyRootMotion = false;
                currentAnimator.SetInteger("EmoteID", emoteId);
                currentAnimator.SetTrigger("PlayEmote");

                // Also crossfade directly to state name to guarantee immediate transition
                string stateName = emoteId switch
                {
                    1 => "Emote_Salute",
                    2 => "Emote_Wave",
                    3 => "Emote_Flex",
                    4 => "Emote_Victory",
                    _ => "Military_Idle"
                };
                currentAnimator.CrossFadeInFixedTime(stateName, 0.2f, 0, 0f);
                Debug.Log($"[LobbyPreview] Triggered Emote ID: {emoteId} ({stateName})");
            }

            OnEmoteTriggered?.Invoke(emoteId);
        }

        // Pointer Drag rotation handlers
        public void OnBeginDrag(Vector2 pointerPos)
        {
            isDragging = true;
            lastPointerPos = pointerPos;
        }

        public void OnDrag(Vector2 pointerPos)
        {
            if (!isDragging) return;
            float deltaX = pointerPos.x - lastPointerPos.x;
            targetYRotation -= deltaX * (rotationSpeed / 100f);
            lastPointerPos = pointerPos;
        }

        public void OnEndDrag()
        {
            isDragging = false;
        }
    }
}
