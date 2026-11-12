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
    /// - Plays idle animations.
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
        private LobbyActorRig currentRig;
        private float targetYRotation = 0f;
        private float currentYRotation = 0f;
        private bool isDragging = false;
        private Vector2 lastPointerPos;

        public event Action<OperatorDefinition> OnOperatorChanged;

        public OperatorDefinition CurrentOperator => (operators != null && operators.Count > currentOperatorIndex && currentOperatorIndex >= 0) 
            ? operators[currentOperatorIndex] : null;

        public int CurrentOperatorIndex => currentOperatorIndex;
        /// <summary>The local player's spawned model (null until the first operator is spawned).</summary>
        public GameObject CurrentModel => currentCharacterInstance;
        public Transform SpawnAnchor => characterSpawnAnchor != null ? characterSpawnAnchor : transform;
        public OperatorDefinition GetOperator(int index) => (operators != null && index >= 0 && index < operators.Count) ? operators[index] : null;
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
            currentRig = LobbyActorRig.Attach(currentCharacterInstance, mirror: false);   // idle, grounding, look-at

            ApplyOperatorVisualVariant(index);

            Debug.Log($"[LobbyPreview] Spawned Operator [{index}]: {op.displayName}");
        }

        private void ApplyOperatorVisualVariant(int index)
        {
            TintOperator(currentCharacterInstance, index);
        }

        /// <summary>How one operator dresses the shared MIL-R5 model: a tint per gear group, and which accessories are worn.</summary>
        private struct OperatorLook
        {
            public Color suit, plate, pouches, pads, cap, headset;
            public bool wearCap, wearHeadset;
        }

        // The tints multiply the model's own olive textures, so values are chosen to move the hue, not replace it.
        private static readonly OperatorLook[] Looks =
        {
            // 0: MIL-R5 GHOST - the model as authored
            new OperatorLook { suit = Color.white, plate = Color.white, pouches = Color.white, pads = Color.white, cap = Color.white, headset = Color.white, wearCap = true, wearHeadset = true },
            // 1: KRUGER-9 VANGUARD - steel grey-blue, heavy dark plate, bare head
            new OperatorLook { suit = new Color(0.70f, 0.80f, 1.05f), plate = new Color(0.42f, 0.46f, 0.54f), pouches = new Color(0.60f, 0.66f, 0.78f), pads = new Color(0.38f, 0.40f, 0.46f), cap = Color.white, headset = new Color(0.55f, 0.60f, 0.70f), wearCap = false, wearHeadset = true },
            // 2: SPECTRE-X RECON - near-black teal night kit, cap and headset
            new OperatorLook { suit = new Color(0.55f, 0.82f, 0.78f), plate = new Color(0.30f, 0.40f, 0.40f), pouches = new Color(0.40f, 0.55f, 0.55f), pads = new Color(0.28f, 0.34f, 0.34f), cap = new Color(0.30f, 0.46f, 0.46f), headset = new Color(0.35f, 0.45f, 0.45f), wearCap = true, wearHeadset = true },
            // 3: VALKYRIE-7 MEDIC - desert sand with a rust vest, no headset
            new OperatorLook { suit = new Color(1.55f, 1.25f, 0.80f), plate = new Color(1.50f, 0.85f, 0.65f), pouches = new Color(1.35f, 1.10f, 0.75f), pads = new Color(1.10f, 0.90f, 0.70f), cap = new Color(1.50f, 1.20f, 0.80f), headset = Color.white, wearCap = true, wearHeadset = false }
        };

        private enum GearGroup { Skin, Suit, Plate, Pouches, Pads, Cap, Headset }

        private static GearGroup GroupOf(string rendererName)
        {
            if (rendererName.Contains("Eye") || rendererName.Contains("Head") && !rendererName.Contains("Headphones")) return GearGroup.Skin;
            if (rendererName.Contains("Headphones")) return GearGroup.Headset;
            if (rendererName.Contains("Cap")) return GearGroup.Cap;
            if (rendererName.Contains("Plate")) return GearGroup.Plate;
            if (rendererName.Contains("Puches")) return GearGroup.Pouches;
            if (rendererName.Contains("Pads")) return GearGroup.Pads;
            return GearGroup.Suit;
        }

        /// <summary>
        /// Dresses a model in its operator's look (per-group tints, cap / headset on or off); also used for party members'
        /// models. brightness &lt; 1 dims models that are out of the spotlight.
        /// </summary>
        public static void TintOperator(GameObject model, int index, float brightness = 1f)
        {
            if (model == null) return;

            var look = Looks[Mathf.Abs(index) % Looks.Length];
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var propBlock = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var group = GroupOf(r.name);

                Color tint;
                switch (group)
                {
                    case GearGroup.Suit: tint = look.suit; break;
                    case GearGroup.Plate: tint = look.plate; break;
                    case GearGroup.Pouches: tint = look.pouches; break;
                    case GearGroup.Pads: tint = look.pads; break;
                    case GearGroup.Cap: tint = look.cap; r.enabled = look.wearCap; break;
                    case GearGroup.Headset: tint = look.headset; r.enabled = look.wearHeadset; break;
                    default: tint = Color.white; break;      // face and eyes keep their own colour
                }
                tint = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, 1f);

                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", tint);
                propBlock.SetColor("_Color", tint);
                r.SetPropertyBlock(propBlock);
            }
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
