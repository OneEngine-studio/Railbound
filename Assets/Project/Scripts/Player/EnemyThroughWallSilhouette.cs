using System.Collections;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Spawns and manages through-wall holographic X-Ray silhouettes on enemies
    /// when revealed by the Sonar Scan.
    /// </summary>
    [RequireComponent(typeof(JUHealth))]
    public class EnemyThroughWallSilhouette : MonoBehaviour
    {
        [Header("XRay Settings")]
        public Color silhouetteColor = new Color(1.0f, 0.12f, 0.22f, 1.0f);
        public Color rimColor = new Color(1.0f, 0.45f, 0.55f, 1.0f);
        public float emissionIntensity = 3.6f;

        private Material _xrayMaterial;
        private readonly List<GameObject> _overlayObjects = new List<GameObject>();
        private readonly List<Renderer> _overlayRenderers = new List<Renderer>();
        private MaterialPropertyBlock _propertyBlock;

        private float _endTime;
        private float _fadeDuration = 0.8f;
        private Coroutine _activeRoutine;
        private JUHealth _health;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int RimColorProperty = Shader.PropertyToID("_RimColor");
        private static readonly int EmissionProperty = Shader.PropertyToID("_EmissionIntensity");

        public static void RevealEnemy(GameObject enemy, float duration = 10f, Color? customColor = null)
        {
            if (enemy == null) return;

            if (!enemy.TryGetComponent<EnemyThroughWallSilhouette>(out var silhouette))
            {
                silhouette = enemy.AddComponent<EnemyThroughWallSilhouette>();
            }

            if (customColor.HasValue)
            {
                silhouette.silhouetteColor = customColor.Value;
            }

            silhouette.StartReveal(duration);
        }

        private void Awake()
        {
            _health = GetComponent<JUHealth>();
            _propertyBlock = new MaterialPropertyBlock();

            Shader shader = Shader.Find("Railbound/URP_EnemyThroughWallXRay");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _xrayMaterial = new Material(shader)
            {
                name = "EnemyThroughWall_RuntimeMat"
            };
        }

        public void StartReveal(float duration)
        {
            _endTime = Time.time + duration;

            if (_overlayObjects.Count == 0)
            {
                BuildOverlays();
            }

            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(RevealRoutine());
        }

        private void BuildOverlays()
        {
            CleanupOverlays();

            // Process Skinned Mesh Renderers
            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
            {
                if (smr == null || smr.sharedMesh == null || smr.name.Contains("XRay")) continue;

                var overlayGO = CreateOverlayObject(smr.name, smr.transform);
                var cloneSMR = overlayGO.AddComponent<SkinnedMeshRenderer>();
                
                cloneSMR.sharedMesh = smr.sharedMesh;
                cloneSMR.bones = smr.bones;
                cloneSMR.rootBone = smr.rootBone;
                cloneSMR.updateWhenOffscreen = true;
                
                SetupRenderer(cloneSMR, smr.sharedMesh.subMeshCount);
            }

            // Process Mesh Renderers
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                if (mr == null || mr.name.Contains("XRay") || mr.name.Contains("Particle")) continue;
                if (!mr.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;

                var overlayGO = CreateOverlayObject(mr.name, mr.transform);
                
                var cloneMF = overlayGO.AddComponent<MeshFilter>();
                cloneMF.sharedMesh = mf.sharedMesh;

                var cloneMR = overlayGO.AddComponent<MeshRenderer>();
                SetupRenderer(cloneMR, mf.sharedMesh.subMeshCount);
            }
        }

        private GameObject CreateOverlayObject(string originalName, Transform parent)
        {
            var overlayGO = new GameObject($"XRay_{originalName}");
            overlayGO.transform.SetParent(parent, false);
            overlayGO.transform.localPosition = Vector3.zero;
            overlayGO.transform.localRotation = Quaternion.identity;
            overlayGO.transform.localScale = Vector3.one;
            _overlayObjects.Add(overlayGO);
            return overlayGO;
        }

        private void SetupRenderer(Renderer renderer, int subMeshCount)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            int count = Mathf.Max(1, subMeshCount);
            Material[] mats = new Material[count];
            for (int i = 0; i < count; i++) mats[i] = _xrayMaterial;
            
            renderer.materials = mats;
            _overlayRenderers.Add(renderer);
        }

        private IEnumerator RevealRoutine()
        {
            float fadeInTime = 0.15f;
            float t = 0f;

            while (t < fadeInTime)
            {
                t += Time.deltaTime;
                SetAlphaMultiplier(Mathf.Clamp01(t / fadeInTime));
                yield return null;
            }
            SetAlphaMultiplier(1f);

            while (Time.time < _endTime - _fadeDuration)
            {
                if (_health != null && _health.IsDead) break;
                yield return null;
            }

            t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                SetAlphaMultiplier(1f - Mathf.Clamp01(t / _fadeDuration));
                yield return null;
            }

            CleanupOverlays();
            _activeRoutine = null;
        }

        private void SetAlphaMultiplier(float alpha)
        {
            Color curCol = silhouetteColor;
            curCol.a = alpha;

            _propertyBlock.SetColor(ColorProperty, curCol);
            _propertyBlock.SetColor(RimColorProperty, rimColor);
            _propertyBlock.SetFloat(EmissionProperty, emissionIntensity * alpha);

            foreach (var r in _overlayRenderers)
            {
                if (r != null) r.SetPropertyBlock(_propertyBlock);
            }
        }

        private void CleanupOverlays()
        {
            foreach (var go in _overlayObjects)
            {
                if (go != null) Destroy(go);
            }
            _overlayObjects.Clear();
            _overlayRenderers.Clear();
        }

        private void OnDestroy()
        {
            CleanupOverlays();
            if (_xrayMaterial != null) Destroy(_xrayMaterial);
        }
    }
}
