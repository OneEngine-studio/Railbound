using System.Collections;
using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Puts a character into a vehicle seat and takes it out again, on every peer (the owner, the server and every other client),
    /// driven by the vehicle's replicated seat list.
    ///
    ///  * Seated: the body has no physics of its own (kinematic, gravity off, no collision with that vehicle), JU is in its
    ///    driving state (<see cref="JUCharacterController.SeatedInExternalVehicle"/>: no locomotion, no rotation, arm layers off,
    ///    the driving camera), the weapon is put away and the animator plays the JU "Driving" pose (replicated by the network
    ///    animator). Each frame the root is placed so the pelvis sits exactly on the seat, so the pose fits any vehicle.
    ///  * The driver's hands hold the steering wheel (IK) and turn with it.
    ///  * Out: the owner places its character on a free spot beside the door and gets its controls back.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the network transform has moved the vehicle this frame
    [DisallowMultipleComponent]
    public class VehicleOccupant : MonoBehaviour
    {
        public RailboundVehicle Vehicle { get; private set; }
        public int Seat { get; private set; } = -1;
        public float SeatedSince { get; private set; }

        private JUCharacterController cc;
        private Rigidbody rb;
        private Animator anim;
        private JUHealth health;
        private Collider[] colliders = new Collider[0];
        private bool kinematicWas, gravityWas;
        private Vector3 hipsOffset = new Vector3(0f, 0.55f, 0f);   // pelvis relative to the root in the sitting pose (measured)
        private bool hipsMeasured;
        private float nextIgnoreRefresh;

        private void Awake()
        {
            cc = GetComponent<JUCharacterController>();
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
            health = GetComponent<JUHealth>();
        }

        public bool IsLocal => IsLocalCharacter(gameObject);
        public bool IsDriver => Vehicle != null && Seat >= 0 && Vehicle.SeatDef(Seat).Driver;

        /// <summary>The character this machine's player controls.</summary>
        public static bool IsLocalCharacter(GameObject go)
        {
            if (go == null) return false;
            var no = go.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) return no.IsOwner && go.GetComponent<JUNetworkPlayer>() != null;
            return JUGameManager.PlayerController != null && JUGameManager.PlayerController.gameObject == go;
        }

        // ---------------------------------------------------------------- in
        /// <summary>Called every frame by the vehicle for each character its seat list names.</summary>
        public void Sit(RailboundVehicle vehicle, int seat)
        {
            if (vehicle == null || (health != null && health.IsDead)) return;
            if (Vehicle == vehicle && Seat == seat) return;
            if (Vehicle != null && Vehicle != vehicle) Leave(false);

            bool first = Vehicle == null;
            Vehicle = vehicle;
            Seat = seat;
            if (first)
            {
                SeatedSince = Time.time;
                EnterPose();
                Debug.Log($"[Vehicle] {name} sits in {vehicle.name} seat {seat} ({vehicle.SeatDef(seat).Name}){(IsLocal ? " (local player)" : "")}.");
            }
            if (IsLocal) VehicleLocalPlayer.NotifySeated(this);
        }

        private void EnterPose()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            Vehicle.IgnoreOccupant(colliders, true);

            if (rb != null)
            {
                kinematicWas = rb.isKinematic; gravityWas = rb.useGravity;
                if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            if (cc != null)
            {
                cc.SeatedInExternalVehicle = true;
                cc.IsDriving = true;
                if (IsLocal) HoldStill();
            }
            hipsMeasured = false;
            Pin();
        }

        /// <summary>Owner: no locomotion, no weapon, the sitting pose.</summary>
        private void HoldStill()
        {
            if (cc == null) return;
            if (cc.IsItemEquiped) cc.SwitchToItem(-1);
            cc.DisableLocomotion();
            cc.IsJumping = false;
            cc.IsRunning = false;
            cc.IsCrouched = false;
            if (anim != null)
            {
                var p = cc.AnimatorParameters;
                anim.SetBool(p.Driving, true);
                anim.SetBool(p.Crouch, false);
                anim.SetBool(p.Running, false);
                anim.SetBool(p.FireMode, false);
                anim.SetBool(p.ItemEquiped, false);
                anim.SetFloat(p.Speed, 0f);
            }
        }

        // ---------------------------------------------------------------- seated
        private void Update()
        {
            if (Vehicle == null) { if (Seat >= 0) Leave(false); return; }
            if (health != null && health.IsDead) { Leave(false); return; }
            int now = Vehicle.SeatOf(gameObject);
            if (now < 0) { Leave(true); return; }
            Seat = now;

            // Unity forgets an ignored pair when either collider is switched off and on (JU toggles hitboxes and ragdoll parts)
            if (Time.time >= nextIgnoreRefresh)
            {
                nextIgnoreRefresh = Time.time + 0.5f;
                colliders = GetComponentsInChildren<Collider>(true);
                Vehicle.IgnoreOccupant(colliders, true);
            }

            if (cc != null)
            {
                cc.SeatedInExternalVehicle = true;
                cc.IsDriving = true;       // a remote copy (puppet) does not run JU's own driving check
                if (IsLocal) HoldStill();
            }
            Pin();
        }

        private void LateUpdate()
        {
            if (Vehicle == null) return;
            MeasureHips();
            Pin();
        }

        /// <summary>Where the pelvis is relative to the root in the current (animated) pose.</summary>
        private void MeasureHips()
        {
            if (anim == null || !anim.isHuman) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            Vector3 local = Quaternion.Inverse(transform.rotation) * (hips.position - transform.position);
            if (local.sqrMagnitude > 4f) return;   // a ragdoll, not a pose
            hipsOffset = hipsMeasured ? Vector3.Lerp(hipsOffset, local, 12f * Time.deltaTime) : local;
            hipsMeasured = true;
        }

        private void Pin()
        {
            if (Vehicle == null || Seat < 0) return;
            Quaternion rot = Vehicle.transform.rotation;
            Vector3 root = Vehicle.SeatHipsWorld(Seat) - rot * hipsOffset;
            transform.SetPositionAndRotation(root, rot);
            if (rb != null && rb.isKinematic) { rb.position = root; rb.rotation = rot; }
        }

        /// <summary>The driver's hands on the wheel; they follow it as it turns.</summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (Vehicle == null || anim == null || !IsDriver) return;
            if (!Vehicle.WheelGrips(out Vector3 left, out Vector3 right, out Vector3 down)) return;
            float w = Mathf.Clamp01((Time.time - SeatedSince - 0.25f) / 0.45f);
            Vector3 side = Vehicle.transform.right;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, left);
            anim.SetIKPosition(AvatarIKGoal.RightHand, right);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w * 0.8f);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w * 0.8f);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, left - side * 0.35f + down * 0.3f - Vehicle.transform.forward * 0.25f);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, right + side * 0.35f + down * 0.3f - Vehicle.transform.forward * 0.25f);
        }

        // ---------------------------------------------------------------- out
        /// <summary>Back on foot. <paramref name="placeOutside"/>: the owner moves its character beside the vehicle.</summary>
        public void Leave(bool placeOutside)
        {
            var vehicle = Vehicle;
            int seat = Seat;
            Vehicle = null;
            Seat = -1;
            bool dead = health != null && health.IsDead;
            bool local = IsLocal;

            if (cc != null)
            {
                cc.SeatedInExternalVehicle = false;
                cc.IsDriving = false;
            }

            if (local)
            {
                if (anim != null && cc != null) anim.SetBool(cc.AnimatorParameters.Driving, false);
                if (!dead && placeOutside && vehicle != null)
                {
                    Vector3 spot = vehicle.FindExitPoint(seat, gameObject);
                    Quaternion yaw = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);
                    Teleport(spot, yaw);
                }
                else if (!dead)
                {
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                }
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                if (cc != null && !dead)
                {
                    cc.DisableAllMove = false;
                    cc.enableMove();
                    cc.EnableMove();
                    cc.CanRotate = true;
                    cc.IsGrounded = true;
                    cc.ResetDefaultLayersWeight();
                }
                VehicleLocalPlayer.NotifyLeft(this, vehicle);
            }
            else if (rb != null)
            {
                rb.isKinematic = kinematicWas;
                rb.useGravity = gravityWas;
            }

            // collisions with that vehicle come back once the character is clear of it (a body that died inside keeps ignoring it)
            if (vehicle != null && !dead && isActiveAndEnabled) StartCoroutine(RestoreCollision(vehicle, colliders));
            if (vehicle != null) Debug.Log($"[Vehicle] {name} left {vehicle.name} seat {seat} at {transform.position:F2}{(dead ? " (dead)" : "")}.");
        }

        private IEnumerator RestoreCollision(RailboundVehicle vehicle, Collider[] cols)
        {
            yield return new WaitForSeconds(0.6f);
            if (vehicle != null && Vehicle != vehicle) vehicle.IgnoreOccupant(cols, false);
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(position, rotation, transform.localScale);
            else transform.SetPositionAndRotation(position, rotation);
            if (rb != null) { rb.position = position; rb.rotation = rotation; }
        }

        private void OnDisable()
        {
            if (Vehicle != null) Leave(false);
        }
    }
}
