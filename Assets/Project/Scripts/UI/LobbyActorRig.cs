using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Brings a lobby operator model to life and keeps it grounded:
    ///  - plants the lowest foot on the stage floor (no more floating),
    ///  - calm, looping idle with an occasional alternate idle (no frozen end pose),
    ///  - a gentle head / eye follow toward the camera.
    /// Added at runtime to every operator model in the lobby (local player and party members).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class LobbyActorRig : MonoBehaviour
    {
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

        private Transform head;

        // idle variation
        private float nextIdleSwitch;
        private bool altIdle;

        // look
        private float lookWeight;
        private Vector3 lookPoint;
        private float nextGlance;

        /// <summary>Receiver for the "move" animation event baked into the JU TPS idle clips (nothing to do in the lobby).</summary>
        public void move() { }

        private void Awake()
        {
            anim = GetComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>("UI/Lobby/AC_LobbyActor");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.enabled = true;
            if (anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
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

        private void Update()
        {
            // occasionally swap between the two idles with a soft crossfade
            if (anim.runtimeAnimatorController != null && Time.time >= nextIdleSwitch)
            {
                altIdle = !altIdle;
                anim.CrossFadeInFixedTime(altIdle ? "Idle_Normal" : "Idle_Neutral", 1.2f, 0, Random.value * 2f);
                nextIdleSwitch = Time.time + Random.Range(10f, 20f);
            }
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
            lookWeight = Mathf.MoveTowards(lookWeight, 0.55f, Time.deltaTime * 0.8f);
            anim.SetLookAtWeight(lookWeight, 0.15f, 0.65f, 0.7f, 0.5f);
            anim.SetLookAtPosition(Vector3.Lerp(lookPoint, cam.transform.position, 0.5f));
        }
    }
}
