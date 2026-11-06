using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Brings a lobby operator model to life and keeps it grounded:
    ///  - plants the lowest foot on the stage floor (no more floating),
    ///  - calm, looping idle with an occasional alternate idle (no frozen end pose),
    ///  - a gentle head / eye follow toward the camera,
    ///  - emotes solved with arm IK on top of the idle (salute, wave, clap, flex, point, victory).
    /// Added at runtime to every operator model in the lobby (local player and party members).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class LobbyActorRig : MonoBehaviour
    {
        public const int EmoteCount = 6;
        public static readonly string[] EmoteNames = { "SALUTE", "WAVE", "CLAP", "FLEX", "POINT", "VICTORY" };
        private static readonly float[] EmoteLength = { 2.4f, 3.4f, 2.8f, 3.0f, 2.6f, 3.2f };

        public static LobbyActorRig Attach(GameObject model, bool mirror)
        {
            if (model == null) return null;
            var rig = model.GetComponent<LobbyActorRig>();
            if (rig == null) rig = model.AddComponent<LobbyActorRig>();
            rig.mirror = mirror;
            return rig;
        }

        private Animator anim;
        private bool mirror;
        private float floorY;
        private float rootOffset, rootOffsetVel;

        // bones
        private Transform head, chest;
        private Transform lUpper, rUpper, lHand, rHand;

        // emote state
        private int emote;            // 1..EmoteCount, 0 = none
        private float emoteStart;
        private float emoteWeight;

        // idle variation
        private float nextIdleSwitch;
        private bool altIdle;

        // look
        private float lookWeight;
        private Vector3 lookPoint;
        private float nextGlance;

        public bool IsEmoting => emote != 0;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>("UI/Lobby/AC_LobbyActor");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.enabled = true;
            if (anim.isHuman)
            {
                head = anim.GetBoneTransform(HumanBodyBones.Head);
                chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
                lUpper = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rUpper = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        private void Start()
        {
            floorY = FindFloorY();
            if (anim.runtimeAnimatorController != null)
            {
                anim.SetBool("Mirror", mirror);
                anim.Play("Idle_Neutral", 0, Random.value);          // desync neighbours
                nextIdleSwitch = Time.time + Random.Range(9f, 18f);
            }
            nextGlance = Time.time + Random.Range(2f, 5f);
        }

        private static float FindFloorY()
        {
            var floor = GameObject.Find("Floor_GEO");
            if (floor != null)
            {
                var r = floor.GetComponent<Renderer>();
                if (r != null) return r.bounds.max.y;
            }
            return 0f;
        }

        public void PlayEmote(int id)
        {
            if (id < 1 || id > EmoteCount) return;
            emote = id;
            emoteStart = Time.time;
        }

        private void Update()
        {
            // occasionally swap between the two idles with a soft crossfade
            if (anim.runtimeAnimatorController != null && !IsEmoting && Time.time >= nextIdleSwitch)
            {
                altIdle = !altIdle;
                anim.CrossFadeInFixedTime(altIdle ? "Idle_Normal" : "Idle_Neutral", 1.2f, 0, Random.value * 2f);
                nextIdleSwitch = Time.time + Random.Range(10f, 20f);
            }

            // emote envelope (ease in / hold / ease out)
            if (emote != 0)
            {
                float t = Time.time - emoteStart, len = EmoteLength[emote - 1];
                float inK = Mathf.Clamp01(t / 0.45f), outK = Mathf.Clamp01((len - t) / 0.55f);
                float k = Mathf.Min(inK, outK);
                emoteWeight = k * k * (3f - 2f * k);
                if (t >= len) { emote = 0; emoteWeight = 0f; }
            }
            else emoteWeight = Mathf.MoveTowards(emoteWeight, 0f, Time.deltaTime * 4f);
        }

        private void LateUpdate()
        {
            // keep the lowest sole on the floor: smooth so the idle's weight shift never makes the model bob
            if (!anim.isHuman) return;
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return;
            float lSole = lf.position.y - anim.leftFeetBottomHeight * transform.lossyScale.y;
            float rSole = rf.position.y - anim.rightFeetBottomHeight * transform.lossyScale.y;
            float lowest = Mathf.Min(lSole, rSole) - appliedOffset;       // sole height without our own correction
            float target = floorY - lowest;
            rootOffset = Mathf.SmoothDamp(rootOffset, target, ref rootOffsetVel, 0.18f);
            var p = transform.position; p.y += rootOffset - appliedOffset; transform.position = p;
            appliedOffset = rootOffset;
        }
        private float appliedOffset;

        private void OnAnimatorIK(int layerIndex)
        {
            if (!anim.isHuman) return;
            ApplyLook();
            if (emoteWeight > 0.001f && emote != 0) ApplyEmoteIK();
            else
            {
                anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f); anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f); anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
                anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f); anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            }
        }

        // ------------------------------------------------------------------ head / eyes follow the camera
        private void ApplyLook()
        {
            var cam = Camera.main;
            if (cam == null || head == null) return;
            if (Time.time >= nextGlance)
            {
                // now and then glance a little to the side, like someone waiting in a lobby
                lookPoint = cam.transform.position + cam.transform.right * Random.Range(-1.2f, 1.2f) + Vector3.up * Random.Range(-0.2f, 0.25f);
                nextGlance = Time.time + Random.Range(2.5f, 6f);
            }
            if (lookPoint == Vector3.zero) lookPoint = cam.transform.position;
            float target = emote == 6 ? 0.2f : 0.55f;
            lookWeight = Mathf.MoveTowards(lookWeight, target, Time.deltaTime * 0.8f);
            anim.SetLookAtWeight(lookWeight, 0.15f, 0.65f, 0.7f, 0.5f);
            anim.SetLookAtPosition(Vector3.Lerp(lookPoint, cam.transform.position, 0.5f));
        }

        // ------------------------------------------------------------------ emotes (arm IK on top of the idle)
        private void Goal(AvatarIKGoal g, AvatarIKHint h, Vector3 pos, Vector3 elbow, float w)
        {
            anim.SetIKPositionWeight(g, w); anim.SetIKPosition(g, pos);
            anim.SetIKHintPositionWeight(h, w); anim.SetIKHintPosition(h, elbow);
        }

        private void ApplyEmoteIK()
        {
            if (rUpper == null || lUpper == null || head == null) return;
            float t = Time.time - emoteStart;
            float w = emoteWeight;
            float s = transform.lossyScale.y;
            Vector3 fwd = transform.forward, up = Vector3.up, rt = transform.right;   // rt = the character's own right
            Vector3 H = head.position, SR = rUpper.position, SL = lUpper.position;
            Vector3 C = chest != null ? chest.position : (SR + SL) * 0.5f;

            switch (emote)
            {
                case 1: // SALUTE: right hand to the brow, elbow out, crisp
                {
                    Vector3 hand = H + fwd * 0.10f * s + up * 0.11f * s + rt * 0.17f * s;
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, hand, SR + rt * 0.42f * s + up * 0.02f * s - fwd * 0.05f * s, w);
                    anim.SetIKRotationWeight(AvatarIKGoal.RightHand, w * 0.7f);
                    anim.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(-rt + up * 0.4f, -fwd + up * 0.2f));
                    break;
                }
                case 2: // WAVE: right hand beside the head, forearm swings side to side
                {
                    float sway = Mathf.Sin(t * 7.5f) * 0.10f * s;
                    Vector3 hand = SR + rt * (0.26f * s + sway) + up * 0.40f * s + fwd * 0.12f * s;
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, hand, SR + rt * 0.42f * s + up * 0.02f * s - fwd * 0.05f * s, w);
                    break;
                }
                case 3: // CLAP: hands meet in front of the chest
                {
                    float open = 0.5f + 0.5f * Mathf.Sin(t * 10f);              // 0 = together, 1 = apart
                    float gap = Mathf.Lerp(0.02f, 0.20f, open) * s;
                    Vector3 mid = C + fwd * 0.32f * s - up * 0.02f * s;
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, mid + rt * gap, SR + rt * 0.30f * s - up * 0.10f * s, w);
                    Goal(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, mid - rt * gap, SL - rt * 0.30f * s - up * 0.10f * s, w);
                    break;
                }
                case 4: // FLEX: double bicep, elbows wide, a little pulse
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(t * 6f);
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, SR + rt * 0.17f * s + up * 0.25f * s * pulse + fwd * 0.05f * s, SR + rt * 0.62f * s - up * 0.02f * s, w);
                    Goal(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, SL - rt * 0.17f * s + up * 0.25f * s * pulse + fwd * 0.05f * s, SL - rt * 0.62f * s - up * 0.02f * s, w);
                    break;
                }
                case 5: // POINT: right arm out, slightly across the body, pointing into the distance
                {
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, SR + fwd * 0.58f * s + rt * 0.16f * s + up * 0.04f * s, SR + rt * 0.25f * s - up * 0.25f * s + fwd * 0.10f * s, w);
                    break;
                }
                case 6: // VICTORY: both fists overhead, a little bounce
                {
                    float bob = Mathf.Sin(t * 8f) * 0.03f * s;
                    Goal(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, SR + rt * 0.26f * s + up * (0.54f * s + bob) + fwd * 0.04f * s, SR + rt * 0.42f * s + up * 0.14f * s, w);
                    Goal(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, SL - rt * 0.26f * s + up * (0.54f * s + bob) + fwd * 0.04f * s, SL - rt * 0.42f * s + up * 0.14f * s, w);
                    break;
                }
            }
        }
    }
}
