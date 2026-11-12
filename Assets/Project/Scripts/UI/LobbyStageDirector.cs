using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.UI
{
    /// <summary>
    /// Directs the lobby's 3D stage:
    ///  - spawns one model per party member and arranges the squad on stage: the selected member stands in the
    ///    middle, enlarged and lit by a moving spotlight; the others step back to the sides, smaller and dimmer,
    ///  - glides everyone to their new place (and the spotlight after them) whenever the selection changes,
    ///  - keeps the background alive: gentle camera sway, flickering practical lights, a slow light sweep,
    ///    breathing haze and drifting dust motes.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyStageDirector : MonoBehaviour
    {
        [Header("Camera framing (solo / with friends)")]
        [SerializeField] private Vector3 soloPosition = new Vector3(0f, 1.0f, -3.45f);
        [SerializeField] private float soloFov = 34f;
        [SerializeField] private Vector3 partyPosition = new Vector3(0f, 1.2f, -4.4f);
        [SerializeField] private float partyFov = 37f;
        [SerializeField] private Vector3 lookTarget = new Vector3(0f, 0.98f, 0f);
        [SerializeField] private float framingSmoothTime = 0.9f;
        [Tooltip("Slides the whole stage sideways on screen (negative = to the right) to leave room for the left-hand HUD column.")]
        [SerializeField] private float stageShiftX = -0.4f;

        [Header("Customization close-up (one operator, centred)")]
        [SerializeField] private Vector3 customPosition = new Vector3(0f, 1.42f, -2.75f);
        [SerializeField] private Vector3 customLook = new Vector3(0f, 1.36f, -0.2f);
        [SerializeField] private float customFov = 30f;

        [Header("Squad layout")]
        [SerializeField] private Vector3 spotlightSlot = new Vector3(0f, 0f, -0.2f);
        [SerializeField] private float sideSpread = 1.3f;
        [SerializeField] private float sideDepth = 0.6f;
        [Tooltip("The fourth squad member stands behind and to the right of the third.")]
        [SerializeField] private float backSpread = 2.1f;
        [SerializeField] private float backDepth = 0.35f;
        [SerializeField] private float inwardYaw = 14f;
        [SerializeField] private float spotlightScale = 1.1f;
        [SerializeField] private float sideScale = 0.92f;
        [SerializeField, Range(0.2f, 1f)] private float sideBrightness = 0.92f;
        [Tooltip("Higher = snappier move between slots.")]
        [SerializeField] private float slotSharpness = 6.5f;

        [Header("Spotlight")]
        [SerializeField] private bool spotlight = true;
        [Tooltip("Kept low on purpose: the hangar rig already lights the stage; this only lifts whoever is selected.")]
        [SerializeField] private float spotlightIntensity = 9000f;     // candela
        [SerializeField] private float spotlightAngle = 32f;
        [SerializeField] private float fillIntensity = 55000f;          // candela
        [SerializeField] private Vector3 spotlightOffset = new Vector3(0.3f, 3.6f, -2.8f);
        [SerializeField] private float spotlightFollowTime = 0.38f;

        [Header("Ambient motion")]
        [SerializeField] private bool ambientMotion = true;
        [SerializeField] private float swayPosition = 0.018f;     // metres
        [SerializeField] private float swayRotation = 0.35f;      // degrees
        [SerializeField] private float swaySpeed = 0.22f;
        [SerializeField] private Material dustMaterial;           // additive HDRP/Unlit soft dot; dust is skipped if missing

        private static readonly Vector3 ModelOffset = new Vector3(-0.18f, 0f, -0.05f);   // cancels the FBX's +0.18m hip offset

        private Camera cam;
        private Vector3 camPos, camPosVel;
        private float camFov, camFovVel;
        private Vector3 lookPos, lookVel;
        private bool lookPlaced;

        private bool customizing;
        /// <summary>True while the customization screen is open: only the local operator is shown, in a close-up.</summary>
        public bool Customizing => customizing;

        public void SetCustomizing(bool on)
        {
            customizing = on;
            if (modelsRoot != null) modelsRoot.gameObject.SetActive(!on);   // the rest of the squad steps out of frame
        }

        private class Spawned
        {
            public GameObject go; public int operatorIndex; public int memberIndex; public float born; public float scale = 1f;
            public Vector3 pos; public float yaw; public float mul; public float brightness = 1f; public bool placed;
        }
        private readonly Dictionary<string, Spawned> spawned = new Dictionary<string, Spawned>();
        private Transform modelsRoot;

        // the local player's model lives under the preview manager's anchor, so the anchor is what moves
        private float baseModelScale = -1f;
        private Vector3 localPos; private float localYaw, localMul = 1f, localBrightness = 1f; private bool localPlaced;
        private GameObject localTintedModel; private float localTintedBrightness = 1f;

        private Light spotLight;
        private HDAdditionalLightData spotHd;
        private Vector3 spotVel;
        private float spotPulse;

        private class Flick { public HDAdditionalLightData hd; public Light light; public float baseIntensity; public float speed; public float depth; public float seed; public Quaternion baseRot; public float sweep; }
        private readonly List<Flick> flickers = new List<Flick>();
        private Fog hazeFog;
        private float hazeBase = 34f;

        private void Start()
        {
            cam = Camera.main;
            if (cam != null)
            {
                camPos = cam.transform.position;
                camFov = cam.fieldOfView;
            }
            modelsRoot = new GameObject("Party_Models").transform;
            modelsRoot.SetParent(transform, false);

            var party = LobbyParty.Instance;
            party.Changed += OnPartyChanged;
            party.SelectionChanged += OnSelectionChanged;
            OnPartyChanged();

            SetupAmbient();
            if (spotlight) CreateSpotlight();
        }

        private void OnDestroy()
        {
            var party = LobbyParty.InstanceIfExists;
            if (party != null) { party.Changed -= OnPartyChanged; party.SelectionChanged -= OnSelectionChanged; }
        }

        private void OnSelectionChanged() { spotPulse = 1f; }

        private float BaseModelScale()
        {
            if (baseModelScale > 0.05f) return baseModelScale;
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview == null) return 1f;
            float s = preview.SpawnAnchor.lossyScale.x;
            baseModelScale = s < 0.05f ? 1f : s;
            return baseModelScale;
        }

        // ------------------------------------------------------------------ slots
        /// <summary>
        /// Where a member stands. The selected one takes the spotlight in the middle; the next one in the party stands to its
        /// right, the previous one to its left, and with four players the one after the next steps further back on the right.
        /// </summary>
        private void SlotFor(int memberIndex, int count, out Vector3 pos, out float yaw, out float mul)
        {
            int sel = LobbyParty.Instance.SelectedIndex;
            int rel = count <= 0 ? 0 : ((memberIndex - sel) % count + count) % count;
            if (rel == 0) { pos = spotlightSlot; yaw = 0f; mul = spotlightScale; return; }
            if (rel == 1) { pos = new Vector3(sideSpread, 0f, sideDepth); yaw = inwardYaw; mul = sideScale; return; }
            if (count >= 4 && rel == 2) { pos = new Vector3(backSpread, 0f, backDepth); yaw = inwardYaw * 1.3f; mul = sideScale * 0.97f; return; }
            pos = new Vector3(-sideSpread, 0f, sideDepth); yaw = -inwardYaw; mul = sideScale;
        }

        // ------------------------------------------------------------------ party models
        private void OnPartyChanged()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            var members = LobbyParty.Instance.Members;
            var keep = new HashSet<string>();

            for (int i = 1; i < members.Count && i < LobbyParty.MaxMembers; i++)
            {
                var m = members[i];
                keep.Add(m.id);
                bool left = (i % 2) == 1;

                if (spawned.TryGetValue(m.id, out var existing))
                {
                    existing.memberIndex = i;
                    if (existing.operatorIndex == m.operatorIndex) continue;
                    if (existing.go != null) Destroy(existing.go);
                    spawned.Remove(m.id);
                }

                var op = preview != null ? preview.GetOperator(m.operatorIndex) : null;
                if (op == null || op.characterPrefab == null) continue;

                var go = Instantiate(op.characterPrefab, modelsRoot);
                go.name = "Party_" + m.displayName;
                go.transform.localScale = Vector3.one * 0.001f;   // pops in (see Update)
                LobbyCharacterPreviewManager.TintOperator(go, m.operatorIndex);

                LobbyActorRig.Attach(go, mirror: !left);     // own idle, grounding and look-at; neighbours mirror each other
                spawned[m.id] = new Spawned { go = go, operatorIndex = m.operatorIndex, memberIndex = i, born = Time.unscaledTime, scale = BaseModelScale() };
            }

            var gone = new List<string>();
            foreach (var kv in spawned) if (!keep.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { if (spawned[id].go != null) Destroy(spawned[id].go); spawned.Remove(id); }
        }

        /// <summary>World position just above a member's head (for nameplates). False if that member has no model yet.</summary>
        public bool TryGetHeadPoint(int memberIndex, out Vector3 point)
        {
            GameObject model = null; float scale = 1f;
            if (memberIndex == 0)
            {
                var preview = LobbyCharacterPreviewManager.Instance;
                model = preview != null ? preview.CurrentModel : null;
                scale = BaseModelScale() * localMul;
            }
            else
            {
                foreach (var kv in spawned) if (kv.Value.memberIndex == memberIndex) { model = kv.Value.go; scale = kv.Value.scale * kv.Value.mul; break; }
            }
            point = Vector3.zero;
            if (model == null) return false;
            var anim = model.GetComponent<Animator>();
            var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            point = head != null ? head.position + Vector3.up * 0.26f * scale : model.transform.position + Vector3.up * 1.95f * scale;
            return true;
        }

        // ------------------------------------------------------------------ spotlight
        private void CreateSpotlight()
        {
            var go = new GameObject("Lobby_Spotlight");
            go.transform.SetParent(transform, false);
            spotHd = go.AddHDLight(HDLightTypeAndShape.ConeSpot);
            spotLight = go.GetComponent<Light>();
            spotLight.color = new Color(1f, 0.96f, 0.88f);
            spotLight.spotAngle = spotlightAngle;
            spotHd.innerSpotPercent = 55f;
            spotHd.SetIntensity(spotlightIntensity, LightUnit.Candela);
            spotHd.EnableShadows(true);
            spotHd.volumetricDimmer = 1f;
            spotLight.transform.position = FocusPoint() + spotlightOffset;

            // wide, shadowless fill from the camera side: the hangar rig leaves the right edge dark, which is where the
            // fourth operator stands
            var fillGo = new GameObject("Lobby_StageFill");
            fillGo.transform.SetParent(transform, false);
            var fillHd = fillGo.AddHDLight(HDLightTypeAndShape.ConeSpot);
            var fill = fillGo.GetComponent<Light>();
            fill.color = new Color(0.86f, 0.92f, 1f);
            fill.spotAngle = 105f;
            fillHd.innerSpotPercent = 30f;
            fillHd.SetIntensity(fillIntensity, LightUnit.Candela);
            fillHd.EnableShadows(false);
            fillHd.volumetricDimmer = 0f;
            fill.transform.position = new Vector3(0.9f, 3.2f, -3.4f);
            fill.transform.rotation = Quaternion.LookRotation(new Vector3(2.0f, 1.0f, 0.4f) - fill.transform.position);
        }

        private Vector3 FocusPoint()
        {
            var party = LobbyParty.Instance;
            int sel = party.SelectedIndex;
            if (sel == 0)
            {
                var preview = LobbyCharacterPreviewManager.Instance;
                var m = preview != null ? preview.CurrentModel : null;
                if (m != null) return new Vector3(m.transform.position.x, 0f, m.transform.position.z);
                return spotlightSlot;
            }
            foreach (var kv in spawned)
                if (kv.Value.memberIndex == sel && kv.Value.go != null)
                    return new Vector3(kv.Value.go.transform.position.x, 0f, kv.Value.go.transform.position.z);
            return spotlightSlot;
        }

        private void UpdateSpotlight(float dt)
        {
            if (spotLight == null) return;
            Vector3 focus = FocusPoint();
            Vector3 target = focus + spotlightOffset;
            spotLight.transform.position = Vector3.SmoothDamp(spotLight.transform.position, target, ref spotVel, spotlightFollowTime, Mathf.Infinity, dt);
            Vector3 look = focus + Vector3.up * 1.0f * spotlightScale;
            spotLight.transform.rotation = Quaternion.LookRotation(look - spotLight.transform.position);

            spotPulse = Mathf.MoveTowards(spotPulse, 0f, dt * 1.6f);
            float pulse = spotPulse * spotPulse * (3f - 2f * spotPulse);     // eases the flash out
            spotHd.SetIntensity(spotlightIntensity * (1f + 0.35f * pulse), LightUnit.Candela);
        }

        // ------------------------------------------------------------------ ambience setup
        private void SetupAmbient()
        {
            var rigRoot = GameObject.Find("Lobby_AAA_Lighting");
            if (rigRoot != null)
            {
                foreach (Transform t in rigRoot.transform)
                {
                    var l = t.GetComponent<Light>(); var hd = t.GetComponent<HDAdditionalLightData>();
                    if (l == null || hd == null) continue;
                    var f = new Flick { hd = hd, light = l, baseIntensity = hd.intensity, seed = Random.value * 50f, baseRot = t.rotation };
                    if (t.name.StartsWith("Practical")) { f.speed = 7f; f.depth = 0.10f; }
                    else if (t.name.StartsWith("Rim")) { f.speed = 0.35f; f.depth = 0.06f; }
                    else if (t.name == "Wash_BackWall") { f.speed = 0.2f; f.depth = 0.05f; f.sweep = 12f; }
                    else continue;
                    flickers.Add(f);
                }
            }

            var vol = FindObjectOfType<Volume>();
            if (vol != null && vol.sharedProfile != null)
            {
                var inst = vol.profile;   // runtime copy: never edits the saved asset
                if (inst.TryGet(out hazeFog)) hazeBase = hazeFog.meanFreePath.value;
            }

            if (ambientMotion && dustMaterial != null) CreateDust();
        }

        private void CreateDust()
        {
            var go = new GameObject("Lobby_Dust");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 1.5f, 0.8f);
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMaterial; r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;

            var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.012f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.75f, 0.28f), new Color(0.8f, 0.88f, 1f, 0.18f));
            main.maxParticles = 260; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 22f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(7f, 3.2f, 6f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f); vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.05f); vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.25f; noise.scrollSpeed = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // ------------------------------------------------------------------ per frame
        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            HandleInput();
            UpdateSquad(t, dt);
            UpdateSpotlight(dt);
            UpdateCamera(t);
            if (ambientMotion) UpdateAmbient(t);
        }

        private static bool TypingInField()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            return sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;
        }

        private void HandleInput()
        {
            if (customizing || TypingInField()) return;
            var party = LobbyParty.Instance;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) party.SelectOffset(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) party.SelectOffset(-1);
        }

        private void UpdateSquad(float t, float dt)
        {
            var party = LobbyParty.Instance;
            int n = Mathf.Min(party.Count, LobbyParty.MaxMembers);
            float k = 1f - Mathf.Exp(-slotSharpness * dt);
            float baseScale = BaseModelScale();

            // --- party models: glide to their slot, scale up/down, brighten/dim
            foreach (var kv in spawned)
            {
                var s = kv.Value; if (s.go == null) continue;
                SlotFor(s.memberIndex, n, out var slotPos, out var slotYaw, out var slotMul);
                Vector3 target = slotPos + ModelOffset;
                float targetBrightness = s.memberIndex == party.SelectedIndex ? 1f : sideBrightness;
                if (!s.placed) { s.pos = target; s.yaw = slotYaw; s.mul = slotMul; s.brightness = targetBrightness; s.placed = true; }
                else
                {
                    s.pos = Vector3.Lerp(s.pos, target, k);
                    s.yaw = Mathf.Lerp(s.yaw, slotYaw, k);
                    s.mul = Mathf.Lerp(s.mul, slotMul, k);
                    float nb = Mathf.Lerp(s.brightness, targetBrightness, k);
                    if (Mathf.Abs(nb - s.brightness) > 0.002f) { s.brightness = nb; LobbyCharacterPreviewManager.TintOperator(s.go, s.operatorIndex, s.brightness); }
                }

                float pop = Mathf.Clamp01((t - s.born) / 0.45f);
                float e = 1f - (1f - pop) * (1f - pop);
                s.go.transform.localScale = Vector3.one * (s.scale * s.mul * Mathf.Lerp(0.92f, 1f, e));
                var lp = s.go.transform.localPosition;   // x/z only: the rig owns the height (keeps the feet on the floor)
                lp.x = s.pos.x; lp.z = s.pos.z;
                s.go.transform.localPosition = lp;
                s.go.transform.localRotation = Quaternion.Euler(0f, 180f + s.yaw, 0f);
                s.go.SetActive(pop > 0.02f);
            }

            // --- the local player: the preview manager owns the model, so move its anchor
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview == null) return;
            SlotFor(0, n, out var lSlot, out var lYaw, out var lMul);
            float lBright = party.SelectedIndex == 0 ? 1f : sideBrightness;
            if (!localPlaced) { localPos = lSlot; localYaw = lYaw; localMul = lMul; localBrightness = lBright; localPlaced = true; }
            else
            {
                localPos = Vector3.Lerp(localPos, lSlot, k);
                localYaw = Mathf.Lerp(localYaw, lYaw, k);
                localMul = Mathf.Lerp(localMul, lMul, k);
                localBrightness = Mathf.Lerp(localBrightness, lBright, k);
            }

            var anchor = preview.SpawnAnchor;
            anchor.localPosition = localPos;
            anchor.localScale = Vector3.one * (baseScale * localMul);
            var model = preview.CurrentModel;
            if (model != null)
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f + localYaw, 0f);
                // re-tint when the dimming moved, and whenever the operator model was respawned
                if (model != localTintedModel || Mathf.Abs(localBrightness - localTintedBrightness) > 0.002f)
                {
                    localTintedModel = model;
                    localTintedBrightness = localBrightness;
                    LobbyCharacterPreviewManager.TintOperator(model, preview.CurrentOperatorIndex, localBrightness, true);
                }
            }
        }

        private void UpdateCamera(float t)
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; camPos = cam.transform.position; camFov = cam.fieldOfView; }

            bool party = LobbyParty.Instance.Count > 1;
            Vector3 shift = customizing ? Vector3.zero : Vector3.right * stageShiftX;
            Vector3 targetPos = customizing ? customPosition : (party ? partyPosition : soloPosition) + shift;
            float targetFov = customizing ? customFov : (party ? partyFov : soloFov);
            Vector3 targetLook = customizing ? customLook : lookTarget + shift;
            if (!lookPlaced) { lookPos = targetLook; lookPlaced = true; }
            camPos = Vector3.SmoothDamp(camPos, targetPos, ref camPosVel, framingSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            camFov = Mathf.SmoothDamp(camFov, targetFov, ref camFovVel, framingSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            lookPos = Vector3.SmoothDamp(lookPos, targetLook, ref lookVel, framingSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 sway = Vector3.zero; Vector3 swayRot = Vector3.zero;
            if (ambientMotion)
            {
                float s = t * swaySpeed;
                sway = new Vector3(Mathf.PerlinNoise(s, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, s) - 0.5f, Mathf.PerlinNoise(s, 4.1f) - 0.5f) * 2f * swayPosition;
                swayRot = new Vector3(Mathf.PerlinNoise(2.2f, s) - 0.5f, Mathf.PerlinNoise(s, 9.5f) - 0.5f, 0f) * 2f * swayRotation;
            }

            cam.transform.position = camPos + sway;
            cam.transform.rotation = Quaternion.LookRotation(lookPos - camPos) * Quaternion.Euler(swayRot);
            cam.fieldOfView = camFov;
        }

        private void UpdateAmbient(float t)
        {
            foreach (var f in flickers)
            {
                if (f.hd == null) continue;
                float n = Mathf.PerlinNoise(f.seed + t * f.speed, 0.5f) - 0.5f;
                float mod = 1f + n * 2f * f.depth;
                // occasional brief dip, like a tired hangar fixture
                if (f.light.name.StartsWith("Practical") && Mathf.PerlinNoise(f.seed, t * 0.35f) > 0.86f) mod *= 0.55f;
                f.hd.intensity = f.baseIntensity * mod;
                if (f.sweep > 0f) f.light.transform.rotation = f.baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.25f + f.seed) * f.sweep, 0f);
            }
            if (hazeFog != null) hazeFog.meanFreePath.value = hazeBase + Mathf.Sin(t * 0.18f) * 5f;
        }
    }
}
