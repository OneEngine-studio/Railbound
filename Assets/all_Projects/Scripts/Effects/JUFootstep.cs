using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JUTPS.FX
{
    [AddComponentMenu("JU TPS/FX/Footstep")]
    public class JUFootstep : MonoBehaviour
    {
        private Animator anim;

        [Header("FX Settings")]
        public AudioSource audioSource;
        public SurfaceAudiosWithFX[] FootstepAudioClips;
        public bool InvertX;

        [Range(0, 1)]
        public float MinTimeToPlayAudio = 0.3f;

        [Header("Surface Detection")]
        [Tooltip("Terrains are usually not tagged. When on, the terrain layer under the foot (grass, soil, pebbles...) decides which footstep set plays.")]
        public bool UseTerrainLayers = true;
        // A terrain layer (or an untagged prop) whose name contains one of these words plays the matching footstep set.
        // Kept as constants rather than inspector fields so a scene never keeps a stale copy of the list.
        private static readonly string[] StoneKeywords = { "pebble", "rock", "stone", "gravel", "cliff", "scree", "tidal", "boulder" };
        private static readonly string[] GrassKeywords = { "grass", "soil", "dirt", "mud", "moss", "leaf", "forest", "earth", "heather" };

        [Header("Loudness")]
        [Tooltip("Loudness scales with how fast the character moves: quiet when sneaking, full when sprinting.")]
        public bool ScaleWithSpeed = true;
        [Range(0f, 1f)] public float MinVolume = 0.3f;
        [Range(0f, 1f)] public float MaxVolume = 1f;
        [Tooltip("Horizontal speed (m/s) that counts as walking / full sprint.")]
        public float WalkSpeed = 1.5f;
        public float SprintSpeed = 6f;
        [Tooltip("Never play the same clip twice in a row on one foot.")]
        public bool AvoidRepeatingClips = true;

        [Tooltip("Log the detected surface, speed and clip of every step to the Console.")]
        public bool DebugLog = false;

        [Header("3D Audio")]
        [Tooltip("Footsteps come from the feet's position, so other characters are heard from where they are and fade with distance.")]
        public bool Use3DAudio = true;
        public float FullVolumeDistance = 4f;
        public float MaxHearDistance = 35f;

        [Header("Ground Check")]
        public LayerMask GroundLayers;

        [Range(0, 1)]
        public float CheckRadius = 0.1f;

        [Header("Ground Check Position Offset")]
        [Range(-0.2f, 0.2f)]
        public float UpOffset = -0.07f;

        [Range(-0.2f, 0.2f)]
        public float ForwardOffset = 0.07f;

        [Space]
        public Transform LeftFoot;
        public Transform RightFoot;

        private bool LeftFootsteped;
        private bool RightFootsteped;

        private float CurrentTimeToLeftFootstep;
        private float CurrentTimeToRightFootstep;

        private Rigidbody body;
        private AudioClip lastLeftClip, lastRightClip;
        private Vector3 lastPosition;
        private float measuredSpeed;

        void Start()
        {
            audioSource = audioSource == null ? GetComponent<AudioSource>() : audioSource;
            anim = GetComponent<Animator>();
            body = GetComponent<Rigidbody>();
            lastPosition = transform.position;

            if (Use3DAudio && audioSource != null)
            {
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = FullVolumeDistance;
                audioSource.maxDistance = MaxHearDistance;
                audioSource.spread = 120f;
                audioSource.dopplerLevel = 0f;
            }

            if (anim != null && (LeftFoot == null || RightFoot == null))
            {
                if (LeftFoot == null)
                    LeftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);

                if (RightFoot == null)
                    RightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            if (GroundLayers.value == 0)
            {
                GroundLayers = LayerMask.GetMask("Default");
            }
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (LeftFoot == null || RightFoot == null)
            {
                if (anim == null)
                    return;

                if (LeftFoot == null)
                    LeftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);

                if (RightFoot == null)
                    RightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            if (LeftFoot == null || RightFoot == null)
                return;

            MeasureSpeed();

            Vector3 LeftFootPosition = LeftFoot.position + transform.forward * ForwardOffset + transform.up * UpOffset;
            Vector3 RightFootPosition = RightFoot.position + transform.forward * ForwardOffset + transform.up * UpOffset;

            Collider[] LeftFootCheck = Physics.OverlapSphere(LeftFootPosition, CheckRadius, GroundLayers);
            Collider[] RightFootCheck = Physics.OverlapSphere(RightFootPosition, CheckRadius, GroundLayers);

            if (CurrentTimeToLeftFootstep < MinTimeToPlayAudio)
            {
                CurrentTimeToLeftFootstep += Time.deltaTime;
            }

            if (CurrentTimeToRightFootstep < MinTimeToPlayAudio)
            {
                CurrentTimeToRightFootstep += Time.deltaTime;
            }

            // Left Footstep
            if (LeftFootCheck.Length == 0)
            {
                LeftFootsteped = false;
            }
            else if (!LeftFootsteped && CurrentTimeToLeftFootstep > MinTimeToPlayAudio)
            {
                DoFootstep(LeftFoot, ResolveSurface(LeftFootCheck, LeftFoot.position));
                CurrentTimeToLeftFootstep = 0;
                LeftFootsteped = true;
            }

            // Right Footstep
            if (RightFootCheck.Length == 0)
            {
                RightFootsteped = false;
            }
            else if (!RightFootsteped && CurrentTimeToRightFootstep > MinTimeToPlayAudio)
            {
                DoFootstep(RightFoot, ResolveSurface(RightFootCheck, RightFoot.position));
                CurrentTimeToRightFootstep = 0;
                RightFootsteped = true;
            }
        }

        public virtual void DoFootstep(Transform Foot, string SurfaceTag = "Untagged")
        {
            if (Foot == null)
                return;

            // Ground Raycast to get ground angle
            RaycastHit hit;
            Physics.Raycast(Foot.position, -transform.up, out hit, 1, GroundLayers);

            // Play a footstep clip (loudness follows the speed, never the same clip twice) and spawn the surface decal, if any
            GameObject FootstepDecal = PlayFootstep(Foot, hit.point, SurfaceTag);

            // Finish current footstep if decal is null
            if (FootstepDecal == null)
                return;

            Transform Decal = FootstepDecal.transform;

            // Set footstep decal rotation
            Decal.rotation = Foot.rotation;

            // Align footstep decal to ground angle
            Decal.rotation = Quaternion.FromToRotation(Foot.up, hit.normal) * Foot.rotation;

            // Invert footstep decal direction
            Decal.rotation = Quaternion.LookRotation(-Decal.forward);

            // Fix footstep decal sides
            if (Foot == RightFoot)
            {
                Decal.localScale = new Vector3(
                    InvertX ? Decal.localScale.x : -Decal.localScale.x,
                    Decal.localScale.y,
                    Decal.localScale.z);
            }
            else
            {
                Decal.localScale = new Vector3(
                    InvertX ? -Decal.localScale.x : Decal.localScale.x,
                    Decal.localScale.y,
                    Decal.localScale.z);
            }

            // Draw a line in the upward direction of the footstep decal
            Debug.DrawRay(FootstepDecal.transform.position, FootstepDecal.transform.up * 2, Color.red, 1);
        }

        private void MeasureSpeed()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 flat;
            if (body != null && !body.isKinematic) flat = body.velocity;
            else flat = (transform.position - lastPosition) / dt;
            flat.y = 0f;
            measuredSpeed = Mathf.Lerp(measuredSpeed, flat.magnitude, 10f * dt);
            lastPosition = transform.position;
        }

        private float LoudnessForSpeed()
        {
            if (!ScaleWithSpeed) return 1f;
            float k = Mathf.InverseLerp(Mathf.Max(0.1f, WalkSpeed) * 0.5f, Mathf.Max(WalkSpeed + 0.1f, SprintSpeed), measuredSpeed);
            return Mathf.Lerp(MinVolume, MaxVolume, k);
        }

        private SurfaceAudiosWithFX FindSurface(string surfaceTag)
        {
            if (FootstepAudioClips == null || FootstepAudioClips.Length == 0) return null;
            for (int i = 0; i < FootstepAudioClips.Length; i++)
            {
                if (FootstepAudioClips[i].SurfaceTag == surfaceTag && FootstepAudioClips[i].AudioClips.Count > 0) return FootstepAudioClips[i];
            }
            return FootstepAudioClips[0];
        }

        private GameObject PlayFootstep(Transform Foot, Vector3 position, string surfaceTag)
        {
            if (audioSource == null || FootstepAudioClips == null || FootstepAudioClips.Length == 0) return null;
            SurfaceAudiosWithFX surface = FindSurface(surfaceTag);
            if (surface == null) return null;

            if (surface.AudioClips.Count > 0)
            {
                bool isRight = Foot == RightFoot;
                AudioClip previous = isRight ? lastRightClip : lastLeftClip;
                AudioClip chosen = surface.AudioClips[Random.Range(0, surface.AudioClips.Count)];
                if (AvoidRepeatingClips && surface.AudioClips.Count > 1)
                {
                    for (int tries = 0; tries < 4 && chosen == previous; tries++)
                        chosen = surface.AudioClips[Random.Range(0, surface.AudioClips.Count)];
                }
                if (isRight) lastRightClip = chosen; else lastLeftClip = chosen;

                if (chosen != null)
                {
                    if (DebugLog) Debug.Log("[Footstep] " + name + " " + (isRight ? "R" : "L") + " surface=" + surface.SurfaceTag + " (asked " + surfaceTag + ") speed=" + measuredSpeed.ToString("F1") + " vol=" + LoudnessForSpeed().ToString("F2") + " clip=" + chosen.name);
                    // the source follows the foot so 3D positioning and distance falloff are correct
                    if (Use3DAudio) audioSource.transform.position = position != Vector3.zero ? position : Foot.position;
                    audioSource.PlayOneShot(chosen, LoudnessForSpeed());
                }
            }

            if (surface.Effects.Count > 0)
            {
                GameObject obj = Instantiate(surface.Effects[Random.Range(0, surface.Effects.Count)], position, Quaternion.identity);
                obj.hideFlags = HideFlags.HideInHierarchy;
                Destroy(obj, 5f);
                return obj;
            }
            return null;
        }

        /// <summary>
        /// Surface under the foot: a tagged collider wins; otherwise a terrain's dominant layer decides (grass/soil -> Grass, pebbles/rock -> Stone).
        /// </summary>
        private string ResolveSurface(Collider[] hits, Vector3 footPosition)
        {
            string fallback = hits[0].tag;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].tag != "Untagged" && HasSurface(hits[i].tag)) return hits[i].tag;
            }
            if (!UseTerrainLayers) return fallback;

            // untagged props named after their material (rocks, boulders) still sound right
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] is TerrainCollider) continue;
                string byName = MapLayerName(hits[i].name);
                if (byName != null && HasSurface(byName)) return byName;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] is TerrainCollider && hits[i].TryGetComponent(out Terrain terrain))
                {
                    string layerName = DominantTerrainLayerName(terrain, footPosition);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        string mapped = MapLayerName(layerName);
                        if (mapped != null && HasSurface(mapped)) return mapped;
                    }
                    break;
                }
            }
            return fallback;
        }

        /// <summary>Footstep surface tag (Untagged / Stone / Grass / Tiles...) at a world position, the same way a footstep decides it.</summary>
        public string GetSurfaceTagAt(Vector3 footPosition)
        {
            Collider[] hits = Physics.OverlapSphere(footPosition, CheckRadius, GroundLayers);
            return hits.Length == 0 ? null : ResolveSurface(hits, footPosition);
        }

        private bool HasSurface(string surfaceTag)
        {
            if (FootstepAudioClips == null) return false;
            for (int i = 0; i < FootstepAudioClips.Length; i++)
                if (FootstepAudioClips[i].SurfaceTag == surfaceTag && FootstepAudioClips[i].AudioClips.Count > 0) return true;
            return false;
        }

        private string MapLayerName(string layerName)
        {
            string n = layerName.ToLowerInvariant();
            if (ContainsAny(n, StoneKeywords)) return "Stone";
            if (ContainsAny(n, GrassKeywords)) return "Grass";
            return null;
        }

        private static bool ContainsAny(string text, string[] keywords)
        {
            if (keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++)
                if (!string.IsNullOrEmpty(keywords[i]) && text.Contains(keywords[i].ToLowerInvariant())) return true;
            return false;
        }

        private static string DominantTerrainLayerName(Terrain terrain, Vector3 worldPosition)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.alphamapLayers == 0 || data.terrainLayers == null || data.terrainLayers.Length == 0) return null;

            Vector3 local = worldPosition - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);

            float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            float bestWeight = -1f;
            int count = Mathf.Min(weights.GetLength(2), data.terrainLayers.Length);
            for (int i = 0; i < count; i++)
            {
                if (weights[0, 0, i] > bestWeight) { bestWeight = weights[0, 0, i]; best = i; }
            }
            TerrainLayer layer = data.terrainLayers[best];
            return layer != null ? layer.name : null;
        }

#if UNITY_EDITOR
        private static AudioClip clip;

        [ContextMenu("Load Default Footstep Audios", false, 100)]
        public void LoadDefaultFootstepInInspector()
        {
            LoadDefaultFootstepAudios(this);
        }

        public static void LoadDefaultFootstepAudios(
            JUFootstep footsteper,
            string path = "Assets/Julhiecio TPS Controller/Audio/Footstep/")
        {
            if (footsteper == null)
            {
                Debug.LogError("Unable to load default footstep audios because the footstep component is missing.");
                return;
            }

            if (!System.IO.Directory.Exists(path))
            {
                Debug.LogError("Unable to load default footstep audios as the indicated path does not exist.");
                return;
            }

            var footstepClips = new List<SurfaceAudiosWithFX>();

            // Add empty cases
            for (int i = 0; i < 4; i++)
            {
                footstepClips.Add(new SurfaceAudiosWithFX());

                for (int x = 0; x < 4; x++)
                {
                    footstepClips[i].AudioClips.Add(clip);
                }
            }

            footsteper.FootstepAudioClips = footstepClips.ToArray();

            // Load footstep audios
            footsteper.FootstepAudioClips[0].SurfaceTag = "Untagged";
            for (int i = 0; i < 4; i++)
            {
                string audioClipPath = path + "Concrete/Footstep on Concrete 0" + (i + 1) + " OGG.ogg";
                if (!System.IO.File.Exists(audioClipPath))
                {
                    Debug.LogWarning("Unable to load default audio: " + audioClipPath);
                }

                footsteper.FootstepAudioClips[0].AudioClips[i] =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);
            }

            footsteper.FootstepAudioClips[1].SurfaceTag = "Stone";
            for (int i = 0; i < 4; i++)
            {
                string audioClipPath = path + "Stones/Footsteps-on-stone0" + (i + 1) + ".ogg";
                if (!System.IO.File.Exists(audioClipPath))
                {
                    Debug.LogWarning("Unable to load default audio: " + audioClipPath);
                }

                footsteper.FootstepAudioClips[1].AudioClips[i] =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);
            }

            footsteper.FootstepAudioClips[2].SurfaceTag = "Grass";
            for (int i = 0; i < 4; i++)
            {
                string audioClipPath = path + "Grass/Footsteps-on-grass0" + (i + 1) + ".ogg";
                if (!System.IO.File.Exists(audioClipPath))
                {
                    Debug.LogWarning("Unable to load default audio: " + audioClipPath);
                }

                footsteper.FootstepAudioClips[2].AudioClips[i] =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);
            }

            footsteper.FootstepAudioClips[3].SurfaceTag = "Tiles";
            for (int i = 0; i < 4; i++)
            {
                string audioClipPath = path + "Tiles/Footstep-on-tiles0" + (i + 1) + ".ogg";
                if (!System.IO.File.Exists(audioClipPath))
                {
                    Debug.LogWarning("Unable to load default audio: " + audioClipPath);
                }

                footsteper.FootstepAudioClips[3].AudioClips[i] =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);
            }
        }
#endif

        void OnDrawGizmos()
        {
            if (LeftFoot == null || RightFoot == null)
            {
                if (anim == null)
                    anim = GetComponent<Animator>();

                if (anim == null)
                    return;

                if (LeftFoot == null)
                    LeftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);

                if (RightFoot == null)
                    RightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            if (LeftFoot == null || RightFoot == null)
                return;

            Color CollisionColor = Color.green;
            CollisionColor.a = 0.4f;

            Color NoCollisionColor = Color.red;
            NoCollisionColor.a = 0.2f;

            Vector3 LeftFootPosition = LeftFoot.position + transform.forward * ForwardOffset + transform.up * UpOffset;
            Vector3 RightFootPosition = RightFoot.position + transform.forward * ForwardOffset + transform.up * UpOffset;

            if (LeftFootsteped)
            {
                Gizmos.color = CollisionColor;
            }
            else
            {
                Gizmos.color = NoCollisionColor;
            }

            Gizmos.DrawSphere(LeftFootPosition, CheckRadius);
            Gizmos.DrawWireSphere(LeftFootPosition, CheckRadius);

            if (RightFootsteped)
            {
                Gizmos.color = CollisionColor;
            }
            else
            {
                Gizmos.color = NoCollisionColor;
            }

            Gizmos.DrawSphere(RightFootPosition, CheckRadius);
            Gizmos.DrawWireSphere(RightFootPosition, CheckRadius);
        }
    }
}
