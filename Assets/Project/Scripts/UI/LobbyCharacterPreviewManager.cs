using System;
using System.Collections.Generic;
using Project.Character;
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
        [Tooltip("Model used in a match (id of the CharacterSkinCatalog).")] public string skinId = "player1";
        [Tooltip("Coins to unlock it in the store. The first operator is the starting one and is always owned.")] public int priceCoins = 0;
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

        private const string EquippedOperatorKey = "railbound.equipped.operator";
        private int equippedOperatorIndex;

        /// <summary>The operator the player will play in a match: the last OWNED one that was selected. (CurrentOperatorIndex is only what is on stage, it may be a locked one being previewed.)</summary>
        public int EquippedOperatorIndex => equippedOperatorIndex;

        public bool IsOperatorOwned(int index)
        {
            var op = GetOperator(index);
            return op != null && PlayerProgression.OwnsOperator(op.operatorId, index == 0);
        }

        /// <summary>The price shown: the server's price table once the profile was read (the server charges that), the catalog's before.</summary>
        public int PriceOf(int index) { var op = GetOperator(index); return op != null ? PlayerProgression.PriceOf(op.operatorId, Mathf.Max(0, op.priceCoins)) : 0; }

        /// <summary>
        /// Buys a locked operator and equips it when that worked. Signed in, the SERVER charges its own price and adds the operator; this only
        /// shows the outcome.
        /// </summary>
        public async System.Threading.Tasks.Task<BuyResult> BuyOperatorAsync(int index)
        {
            var op = GetOperator(index);
            if (op == null) return BuyResult.UnknownOperator;
            if (IsOperatorOwned(index)) return BuyResult.AlreadyOwned;
            var result = await PlayerProgression.BuyOperatorAsync(op.operatorId, Mathf.Max(0, op.priceCoins));
            if (result == BuyResult.Bought && this != null) SelectOperator(index);
            return result;
        }
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
            AppendCatalogOperators();
            RestoreEquippedOperator();
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

        // the operators of the skin catalog (real models) after the four starting ones; the model is the catalog's, not a tint of the first
        private void AppendCatalogOperators()
        {
            for (int i = 1; i < operators.Count && i <= 3; i++)
            {
                if (operators[i].priceCoins <= 0) operators[i].priceCoins = 1500;     // the look variants of the starting model are not free either
                if (string.IsNullOrEmpty(operators[i].skinId)) operators[i].skinId = CharacterSkinCatalog.DefaultSkinId;
            }
            var catalog = CharacterSkinCatalog.Instance;
            if (catalog == null) return;
            foreach (var e in catalog.entries)
            {
                if (e == null || !e.Usable || e.id == CharacterSkinCatalog.DefaultSkinId) continue;
                string opId = "op_" + e.id;
                if (operators.Exists(o => o.operatorId == opId)) continue;
                operators.Add(new OperatorDefinition { operatorId = opId, displayName = e.displayName, roleCategory = e.roleCategory, characterPrefab = e.prefab, skinId = e.id, priceCoins = e.priceCoins });
            }
        }

        private void RestoreEquippedOperator()
        {
            equippedOperatorIndex = 0;
            string saved = PlayerPrefs.GetString(EquippedOperatorKey, "");
            for (int i = 0; i < operators.Count; i++)
                if (!string.IsNullOrEmpty(saved) && operators[i].operatorId == saved && IsOperatorOwned(i)) { equippedOperatorIndex = i; break; }
            currentOperatorIndex = equippedOperatorIndex;
            CharacterSkins.SetEquipped(operators.Count > 0 ? operators[equippedOperatorIndex].skinId : CharacterSkinCatalog.DefaultSkinId);
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
            // any operator can be put on stage to look at; only an owned one becomes the one played in matches
            if (IsOperatorOwned(index))
            {
                equippedOperatorIndex = index;
                PlayerPrefs.SetString(EquippedOperatorKey, operators[index].operatorId);
                CharacterSkins.SetEquipped(operators[index].skinId);
                PlayerPrefs.Save();
            }
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
            TintOperator(currentCharacterInstance, index, 1f, true);
        }

        /// <summary>Re-dresses the local player's model after a customization choice changed.</summary>
        public void RefreshLook()
        {
            if (currentCharacterInstance != null) TintOperator(currentCharacterInstance, currentOperatorIndex, 1f, true);
        }

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
        /// models. brightness &lt; 1 dims models that are out of the spotlight. <paramref name="local"/> applies the local
        /// player's saved customization on top of the operator's authored look.
        /// </summary>
        public static void TintOperator(GameObject model, int index, float brightness = 1f, bool local = false)
        {
            if (model == null) return;

            var look = LobbyOperatorLooks.Resolve(index, local);
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
