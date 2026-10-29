using System.Collections;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Spawns and manages through-wall holographic X-Ray silhouettes on enemies
    /// when revealed by the Sonar Scan. Allows seeing enemies through walls,
    /// obstacles, buildings, and terrain like AAA tactical games.
    /// </summary>
    public class EnemyThroughWallSilhouette : MonoBehaviour
    {
        [Header("XRay Settings")]
        public Color silhouetteColor = new Color(1.0f, 0.12f, 0.22f, 1.0f); // Hostile Neon Red
        public Color rimColor = new Color(1.0f, 0.45f, 0.55f, 1.0f);
        public float emissionIntensity = 3.6f;

        private Material _xrayMaterial;
        private readonly List<GameObject> _overlayObjects = new List<GameObject>();
        private readonly List<Material> _clonedMaterials = new List<Material>();

        private float _endTime;
        private float _fadeDuration = 0.8f;
        private Coroutine _activeRoutine;
        private JUHealth _health;

        public static void RevealEnemy(GameObject enemy, float duration = 10f, Color? customColor = null)
        {
            if (enemy == null) return;

            var silhouette = enemy.GetComponent<EnemyThroughWallSilhouette>();
            if (silhouette == null)
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

            Shader shader = Shader.Find("Railbound/URP_EnemyThroughWallXRay");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _xrayMaterial = new Material(shader)
            {
                name = "EnemyThroughWall_RuntimeMat"
            };
            _xrayMaterial.SetColor("_Color", silhouetteColor);
            _xrayMaterial.SetColor("_RimColor", rimColor);
            _xrayMaterial.SetFloat("_EmissionIntensity", emissionIntensity);
        }

        public void StartReveal(float duration)
        {
            _endTime = Time.time + duration;

            if (_overlayObjects.Count == 0)
            {
                BuildOverlays();
            }

            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(RevealRoutine(duration));
        }

        private void BuildOverlays()
        {
            CleanupOverlays();

            // 1. Skinned Mesh Renderers (Body, Arms, Torso, Head)
            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.name.Contains("XRay")) continue;

                var overlayGO = new GameObject($"XRay_{smr.name}");
                overlayGO.transform.SetParent(smr.transform, false);
                overlayGO.transform.localPosition = Vector3.zero;
                overlayGO.transform.localRotation = Quaternion.identity;
                overlayGO.transform.localScale = Vector3.one;

                var cloneSMR = overlayGO.AddComponent<SkinnedMeshRenderer>();
                cloneSMR.sharedMesh = smr.sharedMesh;
                cloneSMR.bones = smr.bones;
                cloneSMR.rootBone = smr.rootBone;
                cloneSMR.updateWhenOffscreen = true;
                cloneSMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloneSMR.receiveShadows = false;

                int subMeshCount = Mathf.Max(1, smr.sharedMesh.subMeshCount);
                Material[] mats = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                {
                    var matInstance = new Material(_xrayMaterial);
                    _clonedMaterials.Add(matInstance);
                    mats[i] = matInstance;
                }
                cloneSMR.materials = mats;

                _overlayObjects.Add(overlayGO);
            }

            // 2. Mesh Renderers (Weapons, Guns, Props)
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                if (mr == null) continue;
                if (mr.name.Contains("XRay") || mr.name.Contains("Particle") || mr.name.Contains("Trail")) continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var overlayGO = new GameObject($"XRay_{mr.name}");
                overlayGO.transform.SetParent(mr.transform, false);
                overlayGO.transform.localPosition = Vector3.zero;
                overlayGO.transform.localRotation = Quaternion.identity;
                overlayGO.transform.localScale = Vector3.one;

                var cloneMF = overlayGO.AddComponent<MeshFilter>();
                cloneMF.sharedMesh = mf.sharedMesh;

                var cloneMR = overlayGO.AddComponent<MeshRenderer>();
                cloneMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloneMR.receiveShadows = false;

                int subMeshCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
                Material[] mats = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                {
                    var matInstance = new Material(_xrayMaterial);
                    _clonedMaterials.Add(matInstance);
                    mats[i] = matInstance;
                }
                cloneMR.materials = mats;

                _overlayObjects.Add(overlayGO);
            }
        }

        private IEnumerator RevealRoutine(float duration)
        {
            // Rapid fade in
            float fadeInTime = 0.15f;
            float t = 0f;
            while (t < fadeInTime)
            {
                t += Time.deltaTime;
                SetAlphaMultiplier(Mathf.Clamp01(t / fadeInTime));
                yield return null;
            }
            SetAlphaMultiplier(1f);

            // Wait until near end
            while (Time.time < _endTime - _fadeDuration)
            {
                if (_health != null && _health.IsDead)
                {
                    // If enemy dies while revealed, dissolve immediately
                    break;
                }
                yield return null;
            }

            // Smooth fade out
            t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(t / _fadeDuration);
                SetAlphaMultiplier(alpha);
                yield return null;
            }

            CleanupOverlays();
            _activeRoutine = null;
        }

        private void SetAlphaMultiplier(float alpha)
        {
            Color curCol = silhouetteColor;
            curCol.a = alpha;
            foreach (var m in _clonedMaterials)
            {
                if (m != null)
                {
                    m.SetColor("_Color", curCol);
                    m.SetFloat("_EmissionIntensity", emissionIntensity * alpha);
                }
            }
        }

        private void CleanupOverlays()
        {
            foreach (var go in _overlayObjects)
            {
                if (go != null) Destroy(go);
            }
            _overlayObjects.Clear();

            foreach (var m in _clonedMaterials)
            {
                if (m != null) Destroy(m);
            }
            _clonedMaterials.Clear();
        }

        private void OnDestroy()
        {
            CleanupOverlays();
            if (_xrayMaterial != null) Destroy(_xrayMaterial);
        }
    }
}

