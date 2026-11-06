using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.UI
{
    /// <summary>
    /// Directs the lobby's 3D stage:
    ///  - frames the camera close on the operator (and pulls back smoothly when friends join the party),
    ///  - spawns one model per party member beside the local operator,
    ///  - keeps the background alive: gentle camera sway, flickering practical lights, a slow light sweep,
    ///    breathing haze and drifting dust motes.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyStageDirector : MonoBehaviour
    {
        [Header("Camera framing (solo / with friends)")]
        [SerializeField] private Vector3 soloPosition = new Vector3(0f, 1.0f, -3.45f);
        [SerializeField] private float soloFov = 34f;
        [SerializeField] private Vector3 partyPosition = new Vector3(0f, 1.18f, -3.9f);
        [SerializeField] private float partyFov = 37f;
        [SerializeField] private Vector3 lookTarget = new Vector3(0f, 0.92f, 0f);
        [SerializeField] private float framingSmoothTime = 0.9f;

        [Header("Party models")]
        [SerializeField] private float sideOffset = 1.3f;
        [SerializeField] private float depthOffset = 0.55f;
        [SerializeField] private float inwardYaw = 14f;

        [Header("Ambient motion")]
        [SerializeField] private bool ambientMotion = true;
        [SerializeField] private float swayPosition = 0.018f;     // metres
        [SerializeField] private float swayRotation = 0.35f;      // degrees
        [SerializeField] private float swaySpeed = 0.22f;
        [SerializeField] private Material dustMaterial;           // additive HDRP/Unlit soft dot; dust is skipped if missing

        private Camera cam;
        private Vector3 camPos, camPosVel;
        private float camFov, camFovVel;

        private class Spawned { public GameObject go; public int operatorIndex; public float born; public Vector3 pos; public float yaw; public float scale = 1f; }
        private readonly Dictionary<string, Spawned> spawned = new Dictionary<string, Spawned>();
        private Transform modelsRoot;

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
            OnPartyChanged();

            SetupAmbient();
        }

        private void OnDestroy()
        {
            if (LobbyParty.Instance != null) LobbyParty.Instance.Changed -= OnPartyChanged;
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
                Vector3 pos = new Vector3((left ? -1f : 1f) * sideOffset, 0f, depthOffset) + new Vector3(-0.18f, 0f, -0.05f);
                float yaw = 180f + (left ? -inwardYaw : inwardYaw);

                if (spawned.TryGetValue(m.id, out var existing))
                {
                    if (existing.operatorIndex == m.operatorIndex) { existing.pos = pos; existing.yaw = yaw; continue; }
                    if (existing.go != null) Destroy(existing.go);
                    spawned.Remove(m.id);
                }

                var op = preview != null ? preview.GetOperator(m.operatorIndex) : null;
                if (op == null || op.characterPrefab == null) continue;

                // match the size of the local operator model (the stage anchor may be scaled)
                float modelScale = 1f;
                if (preview != null)
                {
                    var anchor = preview.SpawnAnchor;
                    modelScale = anchor.childCount > 0 ? anchor.GetChild(0).lossyScale.x : anchor.lossyScale.x;
                    if (modelScale < 0.05f) modelScale = 1f;
                }

                var go = Instantiate(op.characterPrefab, modelsRoot);
                go.name = "Party_" + m.displayName;
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * 0.001f;   // pops in (see Update)
                LobbyCharacterPreviewManager.TintOperator(go, m.operatorIndex);

                LobbyActorRig.Attach(go, mirror: !left);     // own idle, grounding, look-at and emotes; neighbours mirror each other
                spawned[m.id] = new Spawned { go = go, operatorIndex = m.operatorIndex, born = Time.unscaledTime, pos = pos, yaw = yaw, scale = modelScale };
            }

            var gone = new List<string>();
            foreach (var kv in spawned) if (!keep.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { if (spawned[id].go != null) Destroy(spawned[id].go); spawned.Remove(id); }
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.75f, 0.5f), new Color(0.8f, 0.88f, 1f, 0.35f));
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

            // party models pop in
            foreach (var kv in spawned)
            {
                var s = kv.Value; if (s.go == null) continue;
                float k = Mathf.Clamp01((t - s.born) / 0.45f);
                float e = 1f - (1f - k) * (1f - k);
                s.go.transform.localScale = Vector3.one * (s.scale * Mathf.Lerp(0.92f, 1f, e));
                var lp = s.go.transform.localPosition;   // x/z only: the rig owns the height (keeps the feet on the floor)
                lp.x = Mathf.Lerp(lp.x, s.pos.x, Time.unscaledDeltaTime * 8f); lp.z = Mathf.Lerp(lp.z, s.pos.z, Time.unscaledDeltaTime * 8f);
                s.go.transform.localPosition = lp;
                s.go.SetActive(k > 0.02f);
            }

            UpdateCamera(t);
            if (ambientMotion) UpdateAmbient(t);
        }

        private void UpdateCamera(float t)
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; camPos = cam.transform.position; camFov = cam.fieldOfView; }

            bool party = LobbyParty.Instance.Count > 1;
            Vector3 targetPos = party ? partyPosition : soloPosition;
            float targetFov = party ? partyFov : soloFov;
            camPos = Vector3.SmoothDamp(camPos, targetPos, ref camPosVel, framingSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            camFov = Mathf.SmoothDamp(camFov, targetFov, ref camFovVel, framingSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 sway = Vector3.zero; Vector3 swayRot = Vector3.zero;
            if (ambientMotion)
            {
                float s = t * swaySpeed;
                sway = new Vector3(Mathf.PerlinNoise(s, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, s) - 0.5f, Mathf.PerlinNoise(s, 4.1f) - 0.5f) * 2f * swayPosition;
                swayRot = new Vector3(Mathf.PerlinNoise(2.2f, s) - 0.5f, Mathf.PerlinNoise(s, 9.5f) - 0.5f, 0f) * 2f * swayRotation;
            }

            cam.transform.position = camPos + sway;
            cam.transform.rotation = Quaternion.LookRotation(lookTarget - camPos) * Quaternion.Euler(swayRot);
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
