using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// AAA Full-Scene Volumetric World Scan FX (inspired by INab Studio World Scan FX).
    /// Projects an expanding, depth-reconstructed glowing holographic wavefront
    /// across all 3D geometry (terrain, buildings, stairs, crates, barrels, weapons, characters).
    /// Renders sharp neon geometry edge highlights, white-hot leading wavefront crest,
    /// and surface scanline ripples over a smooth, multi-second expansion.
    /// </summary>
    public class SonarPulseWaveEffect : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        private Vector3 _center;
        private float _currentRadius = 0f;
        private float _maxRadius = 140f;
        private float _duration = 3.8f; // Expands smoothly over 3.8 seconds
        private float _startTime;

        // Visual Colors (Matching user's screenshot - Neon Coral / Amber Glow)
        private Color _waveColor = new Color(2.8f, 0.72f, 0.48f, 1.0f);
        private Color _coreColor = new Color(3.8f, 3.2f, 2.7f, 1.0f);
        private Color _trailColor = new Color(1.8f, 0.45f, 0.28f, 0.45f);

        // Ground ripple ring reference
        private Transform _groundRingTr;
        private Material _groundRingMat;

        public static SonarPulseWaveEffect Create(Vector3 center, float maxRadius = 140f, float duration = 3.8f, Color? waveColor = null)
        {
            var go = new GameObject("WorldScanFX_VolumetricSphere");
            go.transform.position = center;
            var effect = go.AddComponent<SonarPulseWaveEffect>();
            effect._center = center;
            effect._maxRadius = maxRadius;
            effect._duration = duration;
            if (waveColor.HasValue)
            {
                effect._waveColor = waveColor.Value;
            }
            return effect;
        }

        private void Awake()
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Generate sphere primitive mesh
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _meshFilter.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempSphere);

            Shader shader = Shader.Find("Project/URP_WorldScanFX");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _material = new Material(shader);
            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            CreateGroundRippleRing();
        }

        private void CreateGroundRippleRing()
        {
            var ringGO = new GameObject("GroundScanRing");
            ringGO.transform.SetParent(transform, false);
            ringGO.transform.localPosition = Vector3.up * 0.12f;
            _groundRingTr = ringGO.transform;

            var mf = ringGO.AddComponent<MeshFilter>();
            var mr = ringGO.AddComponent<MeshRenderer>();

            Shader ringShader = Shader.Find("MadeByProfessorOakie/URP_SonarWaveRing");
            if (ringShader == null) ringShader = Shader.Find("Universal Render Pipeline/Unlit");

            _groundRingMat = new Material(ringShader);
            _groundRingMat.SetColor("_Color", new Color(1.0f, 0.5f, 0.35f, 0.85f));
            _groundRingMat.SetFloat("_Emission", 2.8f);

            Texture ringTex = Resources.Load<Texture>("SonarRingTex_wave");
            if (ringTex == null)
            {
#if UNITY_EDITOR
                ringTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/MadeByProfessorOakie/SimpleSonarShader/Ring Textures/SonarRingTex_wave.png");
#endif
            }
            if (ringTex != null) _groundRingMat.mainTexture = ringTex;

            mr.sharedMaterial = _groundRingMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Simple Quad flat on XZ
            Mesh ringMesh = new Mesh();
            ringMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3(-0.5f, 0f,  0.5f)
            };
            ringMesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            ringMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            ringMesh.RecalculateNormals();
            mf.sharedMesh = ringMesh;
        }

        private void Start()
        {
            _startTime = Time.time;
            transform.position = _center;
            transform.localScale = Vector3.zero;

            _material.SetVector("_ScanCenter", new Vector4(_center.x, _center.y, _center.z, 0f));
            _material.SetFloat("_MaxRadius", _maxRadius);
            _material.SetColor("_WaveColor", _waveColor);
            _material.SetColor("_CoreColor", _coreColor);
            _material.SetColor("_TrailColor", _trailColor);
            _material.SetFloat("_WaveWidth", 3.2f);
            _material.SetFloat("_TrailLength", 16.0f);
            _material.SetFloat("_EdgeDetectSensitivity", 2.5f);
        }

        private void Update()
        {
            float elapsed = Time.time - _startTime;
            float progress = Mathf.Clamp01(elapsed / _duration);

            // Buttery-smooth cubic easing for visual perfection across multi-second scan
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            _currentRadius = Mathf.Lerp(0f, _maxRadius, smoothProgress);

            // Scale sphere bounding volume tightly around wavefront
            float diameter = Mathf.Max(0.1f, (_currentRadius + 6.0f) * 2.0f);
            transform.localScale = Vector3.one * diameter;

            _material.SetFloat("_CurrentRadius", _currentRadius);

            // Update ground ripple ring scale & alpha
            if (_groundRingTr != null)
            {
                float ringDiam = Mathf.Max(0.1f, _currentRadius * 2.0f);
                _groundRingTr.localScale = new Vector3(ringDiam, 1f, ringDiam);

                if (_groundRingMat != null)
                {
                    float ringAlpha = (1f - smoothProgress) * 0.85f;
                    _groundRingMat.SetColor("_Color", new Color(1.0f, 0.48f, 0.32f, ringAlpha));
                }
            }

            if (progress >= 1.0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_groundRingMat != null) Destroy(_groundRingMat);
        }
    }
}
