using System;
using System.Collections.Generic;
using JUTPS;
using JUTPS.JUInputSystem;
using NWH.VehiclePhysics2;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>One seat of a vehicle: where the occupant's pelvis sits (vehicle space) and whether it drives.</summary>
    [Serializable]
    public struct VehicleSeatDef
    {
        public string Name;
        public Vector3 Hips;
        public bool Driver;
    }

    /// <summary>Who sits where: the NetworkObjectId + 1 of the character in each seat (0 = empty).</summary>
    public struct VehicleSeats : INetworkSerializable, IEquatable<VehicleSeats>
    {
        public const int Max = 4;
        public ulong S0, S1, S2, S3;

        public ulong this[int i]
        {
            get { return i == 0 ? S0 : i == 1 ? S1 : i == 2 ? S2 : i == 3 ? S3 : 0; }
            set { if (i == 0) S0 = value; else if (i == 1) S1 = value; else if (i == 2) S2 = value; else if (i == 3) S3 = value; }
        }

        public int IndexOf(ulong key)
        {
            if (key == 0) return -1;
            for (int i = 0; i < Max; i++) if (this[i] == key) return i;
            return -1;
        }

        public int Count { get { int n = 0; for (int i = 0; i < Max; i++) if (this[i] != 0) n++; return n; } }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref S0); s.SerializeValue(ref S1); s.SerializeValue(ref S2); s.SerializeValue(ref S3);
        }

        public bool Equals(VehicleSeats o) { return S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3; }
        public override bool Equals(object obj) { return obj is VehicleSeats o && Equals(o); }
        public override int GetHashCode() { return (S0 ^ (S1 << 8) ^ (S2 << 16) ^ (S3 << 24)).GetHashCode(); }
    }

    /// <summary>What the simulating peer's driver is doing, for every other copy (wheels, steering wheel, engine sound, lights).</summary>
    public struct VehicleDriveState : INetworkSerializable, IEquatable<VehicleDriveState>
    {
        public float Steering, Throttle, Brakes, Handbrake, EngineAV;
        public int Lights;
        public byte Flags;   // 1 engine on, 2 boost, 4 horn

        public bool EngineOn => (Flags & 1) != 0;
        public bool Boost => (Flags & 2) != 0;
        public bool Horn => (Flags & 4) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Steering); s.SerializeValue(ref Throttle); s.SerializeValue(ref Brakes);
            s.SerializeValue(ref Handbrake); s.SerializeValue(ref EngineAV); s.SerializeValue(ref Lights); s.SerializeValue(ref Flags);
        }

        public bool Equals(VehicleDriveState o)
        {
            return Steering == o.Steering && Throttle == o.Throttle && Brakes == o.Brakes && Handbrake == o.Handbrake
                && EngineAV == o.EngineAV && Lights == o.Lights && Flags == o.Flags;
        }

        public override bool Equals(object obj) { return obj is VehicleDriveState o && Equals(o); }
        public override int GetHashCode() { return Steering.GetHashCode() ^ Throttle.GetHashCode() ^ Flags; }

        /// <summary>Different enough from <paramref name="o"/> to be worth sending.</summary>
        public bool Differs(VehicleDriveState o)
        {
            return Mathf.Abs(Steering - o.Steering) > 0.02f || Mathf.Abs(Throttle - o.Throttle) > 0.03f || Mathf.Abs(Brakes - o.Brakes) > 0.03f
                || Mathf.Abs(Handbrake - o.Handbrake) > 0.05f || Mathf.Abs(EngineAV - o.EngineAV) > 15f || Lights != o.Lights || Flags != o.Flags;
        }
    }

    /// <summary>
    /// A drivable NWH vehicle in a networked match.
    ///
    /// Physics authority follows the driver: an empty vehicle belongs to the server, which simulates it (it rolls, settles, gets
    /// pushed); when a player takes the driver's seat the server hands the NetworkObject to that player's client, which simulates it
    /// with no input lag, and every other copy follows through the owner-authoritative <see cref="ClientNetworkTransform"/> with a
    /// kinematic body. When the driver leaves (exit, death, disconnect) the server takes it back and keeps the vehicle's momentum.
    ///
    /// Seats are server-authoritative (<see cref="seats"/>): a client asks, the server checks (own character, alive, close, slow
    /// enough, seat free) and publishes. Every peer then places the occupants with <see cref="VehicleOccupant"/>.
    /// The simulating peer publishes the driver's input and engine state (<see cref="drive"/>) so remote copies steer their wheels,
    /// play the engine at the right revs and show the same lights.
    /// Without a network session (offline map) the same code runs locally.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    [DefaultExecutionOrder(50)]
    public class RailboundVehicle : NetworkBehaviour
    {
        public static readonly List<RailboundVehicle> All = new List<RailboundVehicle>();

        [Header("Vehicle")]
        [SerializeField] private string displayName = "VEHICLE";
        [SerializeField] private VehicleSeatDef[] seatDefs = new VehicleSeatDef[0];
        [Tooltip("The steering wheel the driver's hands hold (falls back to NWH's steering wheel).")]
        [SerializeField] private Transform steeringWheel;
        [SerializeField] private float steeringWheelRadius = 0.17f;
        [Tooltip("How far from the body a player can get in (metres).")]
        [SerializeField] private float enterRange = 2.6f;
        [Tooltip("Spawn weight in a match (higher = more common).")]
        [SerializeField] private float spawnWeight = 1f;

        [Header("Run-over")]
        [SerializeField] private float runOverMinSpeed = 6f;
        [SerializeField] private float runOverDamagePerMs = 7.5f;

        private const float MaxEnterSpeed = 9f;          // m/s: nobody jumps into a car going faster
        private const float ServerRangeSlack = 2.5f;     // the server's copy of a player lags a little behind its owner

        private readonly NetworkVariable<VehicleSeats> seats = new NetworkVariable<VehicleSeats>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<VehicleDriveState> drive = new NetworkVariable<VehicleDriveState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private VehicleSeats localSeats;   // offline
        private VehicleController vc;
        private Rigidbody body;
        private Collider[] bodyColliders = new Collider[0];
        [SerializeField, HideInInspector] private Bounds localBounds;   // vehicle space; baked into the prefab, refreshed in Awake
        private bool initialized;
        private float nextDrivePublish, nextWatch;
        private VehicleDriveState lastSent;
        private Vector3 lastVelocity, lastAngular;
        private readonly Dictionary<JUHealth, float> runOverCooldown = new Dictionary<JUHealth, float>();
        private readonly List<(Collider[] victim, float until)> ignoredVictims = new List<(Collider[], float)>();

        public static event Action<RailboundVehicle, string> Denied;   // local player was refused (reason)

        // ---------------------------------------------------------------- public state
        public string DisplayName => displayName;
        public int SeatCount => Mathf.Min(seatDefs != null ? seatDefs.Length : 0, VehicleSeats.Max);
        public VehicleSeatDef SeatDef(int i) => seatDefs[i];
        public VehicleController Controller => vc;
        public Rigidbody Body => body;
        public Bounds LocalBounds => localBounds;
        public float SpawnWeight => spawnWeight;
        public float EnterRange => enterRange;
        public Transform SteeringWheel => steeringWheel;
        public float SteeringWheelRadius => steeringWheelRadius;
        public IReadOnlyList<Collider> BodyColliders => bodyColliders;
        public VehicleSeats Seats => IsSpawned ? seats.Value : localSeats;
        public float SpeedKmh => body != null ? (IsSimulating ? body.velocity.magnitude : observedSpeed) * 3.6f : 0f;
        public int DriverSeat { get { for (int i = 0; i < SeatCount; i++) if (seatDefs[i].Driver) return i; return 0; } }
        public bool HasDriver => Seats[DriverSeat] != 0;

        private static bool NetActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        /// <summary>This peer runs the vehicle's physics (the server when empty, the driver's client when driven, offline always).</summary>
        public bool IsSimulating => IsSpawned ? IsOwner : !NetActive;
        private bool IsAuthority => IsSpawned ? IsServer : !NetActive;

        // remote copies have a kinematic body: their speed comes from how far the transform moved
        private float observedSpeed;
        private Vector3 observedVelocity, lastObservedPos;

        // ---------------------------------------------------------------- lifecycle
        private void Awake()
        {
            vc = GetComponent<VehicleController>();
            body = GetComponent<Rigidbody>();
            if (NetworkObject != null) NetworkObject.DontDestroyWithOwner = true;   // the driver leaving must not delete the car
            StripStandaloneParts(gameObject);
            if (vc != null)
            {
                vc.input.autoSetInput = false;
                vc.onVehicleInitialized.AddListener(OnVehicleInitialized);
            }
            if (steeringWheel == null && vc != null) steeringWheel = vc.steering.steeringWheel;
            CacheGripAxes();   // before NWH turns the wheel
            CacheColliders();
            lastObservedPos = transform.position;
        }

        private void Start() { if (!IsSpawned) VehicleLocalPlayer.Ensure(); }   // offline map

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public override void OnDestroy()
        {
            All.Remove(this);
            base.OnDestroy();
        }

#if UNITY_EDITOR
        /// <summary>Editor (prefab builder): bake the steering wheel, grip axes and body bounds into the prefab.</summary>
        public void BakeForPrefab(string name, VehicleSeatDef[] seatList, float weight, Transform wheel)
        {
            displayName = name;
            seatDefs = seatList;
            spawnWeight = weight;
            vc = GetComponent<VehicleController>();
            body = GetComponent<Rigidbody>();
            steeringWheel = wheel != null ? wheel : (vc != null ? vc.steering.steeringWheel : null);
            CacheColliders();
        }
#endif

        /// <summary>The NWH prefab is built for single player: its own cameras, audio listener, mirror cameras and reflection probe
        /// would take over the player's view (and cost a camera render each). The match camera is the player's.</summary>
        public static void StripStandaloneParts(GameObject root)
        {
            foreach (var cc in root.GetComponentsInChildren<NWH.Common.Cameras.CameraChanger>(true)) DestroyObj(cc.gameObject);
            foreach (var cam in root.GetComponentsInChildren<Camera>(true)) DestroyObj(cam.gameObject);
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true)) DestroyObj(al);
            foreach (var rp in root.GetComponentsInChildren<ReflectionProbe>(true)) DestroyObj(rp.gameObject);
            // mesh deformation is local to each machine (it would differ between players) and costly
            foreach (var dh in root.GetComponentsInChildren<NWH.VehiclePhysics2.Damage.DamageHandler>(true)) dh.enabled = false;
        }

        private static void DestroyObj(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o, true);
        }

        private void CacheColliders()
        {
            var list = new List<Collider>();
            bool any = false;
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                list.Add(c);
                if (c.GetComponentInParent<Rigidbody>() != body) continue;   // loose interior props (attachedRigidbody is null in the editor)
                // from the collider's own geometry: Collider.bounds is stale until the physics scene syncs (and in the editor)
                if (!ColliderLocalBox(c, out Bounds b)) continue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = transform.InverseTransformPoint(c.transform.TransformPoint(corner));
                    if (!any) { localBounds = new Bounds(local, Vector3.zero); any = true; } else localBounds.Encapsulate(local);
                }
            }
            // the wheels are ray-cast (no colliders) but stand out past the body on some vehicles: they belong to the footprint
            foreach (var w in GetComponentsInChildren<NWH.Common.Vehicles.WheelUAPI>(true))
            {
                Vector3 c = transform.InverseTransformPoint(w.transform.position);
                float r = Mathf.Max(0.25f, w.Radius), half = Mathf.Max(0.12f, w.Width * 0.5f);
                foreach (var corner in new[] { c + new Vector3(-half, -r * 2f, -r), c + new Vector3(half, 0f, r) })
                {
                    if (!any) { localBounds = new Bounds(corner, Vector3.zero); any = true; } else localBounds.Encapsulate(corner);
                }
            }
            bodyColliders = list.ToArray();
            if (!any) localBounds = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.6f, 4.4f));
        }

        private static bool ColliderLocalBox(Collider c, out Bounds b)
        {
            switch (c)
            {
                case BoxCollider box: b = new Bounds(box.center, box.size); return true;
                case MeshCollider mesh when mesh.sharedMesh != null: b = mesh.sharedMesh.bounds; return true;
                case SphereCollider sph: b = new Bounds(sph.center, Vector3.one * sph.radius * 2f); return true;
                case CapsuleCollider cap:
                    var size = Vector3.one * cap.radius * 2f;
                    size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f);
                    b = new Bounds(cap.center, size); return true;
                default: b = default; return false;
            }
        }

        private void OnVehicleInitialized()
        {
            initialized = true;
            ApplyAuthority();
            if (!HasDriver && IsSimulating && vc.powertrain.engine.IsRunning) vc.powertrain.engine.StopEngine();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) seats.Value = default;
            ApplyAuthority();
            VehicleLocalPlayer.Ensure();
        }

        public override void OnNetworkDespawn()
        {
            // everyone inside gets out where they are
            for (int i = 0; i < VehicleSeats.Max; i++)
            {
                var occ = OccupantComponent(Seats[i]);
                if (occ != null && occ.Vehicle == this) occ.Leave(false);
            }
        }

        public override void OnGainedOwnership() { ApplyAuthority(); }
        public override void OnLostOwnership() { ApplyAuthority(); }

        /// <summary>The simulating peer has a dynamic body; every other copy is kinematic and follows the network transform.</summary>
        private void ApplyAuthority()
        {
            if (body == null) return;
            bool sim = IsSimulating;
            bool wasKinematic = body.isKinematic;
            body.isKinematic = !sim;
            // taking over a moving vehicle (the driver got in or left, or disconnected): keep the motion it had on this screen
            if (sim && wasKinematic) body.velocity = Vector3.ClampMagnitude(observedVelocity, 70f);
            body.interpolation = sim ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (vc != null && initialized) vc.MultiplayerIsRemote = !sim;
            if (sim) lastSent = default;
        }

        // ---------------------------------------------------------------- occupants
        public static ulong KeyOf(GameObject character)
        {
            if (character == null) return 0;
            var no = character.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) return no.NetworkObjectId + 1;
            return (ulong)(uint)character.GetInstanceID() | (1UL << 40);   // offline: any stable non-zero key
        }

        public int SeatOf(GameObject character) => Seats.IndexOf(KeyOf(character));

        public static RailboundVehicle VehicleOf(GameObject character, out int seat)
        {
            ulong key = KeyOf(character);
            for (int i = 0; i < All.Count; i++)
            {
                int s = All[i].Seats.IndexOf(key);
                if (s >= 0) { seat = s; return All[i]; }
            }
            seat = -1;
            return null;
        }

        private static readonly Dictionary<ulong, GameObject> offlineKeys = new Dictionary<ulong, GameObject>();

        public static GameObject CharacterOf(ulong key)
        {
            if (key == 0) return null;
            if ((key & (1UL << 40)) != 0) return offlineKeys.TryGetValue(key, out var go) ? go : null;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue(key - 1, out var no) && no != null ? no.gameObject : null;
        }

        private static VehicleOccupant OccupantComponent(ulong key)
        {
            var go = CharacterOf(key);
            return go != null ? go.GetComponent<VehicleOccupant>() : null;
        }

        public GameObject Driver => CharacterOf(Seats[DriverSeat]);

        public Vector3 SeatHipsWorld(int seat) => transform.TransformPoint(seatDefs[Mathf.Clamp(seat, 0, SeatCount - 1)].Hips);

        /// <summary>Distance from a point to the vehicle's body (0 inside).</summary>
        public float DistanceTo(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 closest = new Vector3(Mathf.Clamp(local.x, localBounds.min.x, localBounds.max.x),
                                          Mathf.Clamp(local.y, localBounds.min.y, localBounds.max.y),
                                          Mathf.Clamp(local.z, localBounds.min.z, localBounds.max.z));
            return Vector3.Distance(transform.TransformPoint(closest), world);
        }

        public bool IsUpright => Vector3.Dot(transform.up, Vector3.up) > 0.45f;

        // the steering wheel's sideways and upward axes at rest, in its own space (NWH turns it about its local forward axis)
        private Vector3 gripAxisLocal = Vector3.right, gripUpLocal = Vector3.up;
        private bool gripAxesReady;

        private void CacheGripAxes()
        {
            if (steeringWheel == null) return;
            Quaternion wheelInVehicle = Quaternion.Inverse(transform.rotation) * steeringWheel.rotation;
            Vector3 axis = wheelInVehicle * Vector3.forward;
            Vector3 side = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            if (side.sqrMagnitude < 0.5f || up.sqrMagnitude < 0.5f) return;
            gripAxisLocal = Quaternion.Inverse(wheelInVehicle) * side;
            gripUpLocal = Quaternion.Inverse(wheelInVehicle) * up;
            gripAxesReady = true;
        }

        /// <summary>Where the driver's hands go: the left and right of the rim (turning with the wheel), and "down" along the rim plane.</summary>
        public bool WheelGrips(out Vector3 left, out Vector3 right, out Vector3 down)
        {
            left = right = down = Vector3.zero;
            if (steeringWheel == null || !gripAxesReady) return false;
            Vector3 side = steeringWheel.rotation * gripAxisLocal;
            Vector3 up = steeringWheel.rotation * gripUpLocal;
            Vector3 c = steeringWheel.position + up * (steeringWheelRadius * 0.25f);
            left = c - side * steeringWheelRadius;
            right = c + side * steeringWheelRadius;
            down = -up;
            return true;
        }

        /// <summary>The seat a player near this vehicle would get: the driver's seat when free, else the free seat nearest to them.</summary>
        public int PreferredFreeSeat(Vector3 from)
        {
            var s = Seats;
            if (s[DriverSeat] == 0) return DriverSeat;
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < SeatCount; i++)
            {
                if (s[i] != 0) continue;
                float d = Vector3.Distance(SeatHipsWorld(i), from);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ---------------------------------------------------------------- requests (any peer, for its own character)
        /// <summary>Asks to take <paramref name="seat"/> (-1 = best free seat). The server decides.</summary>
        public void RequestEnter(GameObject character, int seat = -1)
        {
            if (character == null) return;
            if (IsSpawned)
            {
                var no = character.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned) return;
                RequestSeatServerRpc(no, seat < 0 ? -2 : seat, Vector3.zero, Vector3.zero);
            }
            else if (!NetActive)
            {
                offlineKeys[KeyOf(character)] = character;
                string why = TrySeat(KeyOf(character), character, seat < 0 ? -2 : seat, Vector3.zero, Vector3.zero);
                if (why != null) Denied?.Invoke(this, why);
            }
        }

        /// <summary>Leaves the vehicle (the server keeps the momentum the driver had).</summary>
        public void RequestExit(GameObject character) { RequestMove(character, -1); }

        /// <summary>Moves to another seat (-1 = out).</summary>
        public void RequestMove(GameObject character, int seat)
        {
            if (character == null) return;
            Vector3 v = body != null && IsSimulating ? body.velocity : Vector3.zero;
            Vector3 w = body != null && IsSimulating ? body.angularVelocity : Vector3.zero;
            if (IsSpawned)
            {
                var no = character.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned) return;
                RequestSeatServerRpc(no, seat, v, w);
            }
            else if (!NetActive)
            {
                string why = TrySeat(KeyOf(character), character, seat, v, w);
                if (why != null) Denied?.Invoke(this, why);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSeatServerRpc(NetworkObjectReference who, int seat, Vector3 velocity, Vector3 angular, ServerRpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            string why;
            if (!who.TryGet(out NetworkObject character) || character.OwnerClientId != sender) why = "NOT YOUR CHARACTER";
            else why = TrySeat(character.NetworkObjectId + 1, character.gameObject, seat, velocity, angular);
            if (why != null)
            {
                Debug.Log($"[Vehicle] {name}: client {sender} was refused seat {seat}: {why}");
                DeniedClientRpc(why, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } });
            }
        }

        [ClientRpc]
        private void DeniedClientRpc(string why, ClientRpcParams p = default) { Denied?.Invoke(this, why); }

        /// <summary>
        /// Authority: seat <paramref name="key"/> (seat -2 = best free one, -1 = out). Returns null when done, else the reason it was not.
        /// </summary>
        private string TrySeat(ulong key, GameObject character, int seat, Vector3 velocity, Vector3 angular)
        {
            var s = Seats;
            int current = s.IndexOf(key);

            if (seat == -1)   // out
            {
                if (current < 0) return null;
                s[current] = 0;
                Publish(s);
                if (current == DriverSeat) ReturnToServer(velocity, angular);
                Debug.Log($"[Vehicle] {name}: {character.name} left seat {current}.");
                return null;
            }

            var health = character.GetComponent<JUHealth>();
            if (health != null && health.IsDead) return "DEAD";
            if (current < 0)
            {
                var other = VehicleOf(character, out _);
                if (other != null && other != this) return "ALREADY IN A VEHICLE";
                if (DistanceTo(character.transform.position) > enterRange + (IsSpawned ? ServerRangeSlack : 0.5f)) return "TOO FAR";
                float speed = body != null ? (IsSimulating ? body.velocity.magnitude : observedSpeed) : 0f;
                if (speed > MaxEnterSpeed) return "MOVING TOO FAST";
            }

            if (seat == -2) seat = PreferredFreeSeat(character.transform.position);
            if (seat < 0 || seat >= SeatCount) return "VEHICLE FULL";
            if (s[seat] != 0 && s[seat] != key) return "SEAT TAKEN";
            if (seat == current) return null;

            if (current >= 0) s[current] = 0;
            s[seat] = key;
            Publish(s);
            Debug.Log($"[Vehicle] {name}: {character.name} took seat {seat} ({seatDefs[seat].Name}){(current >= 0 ? " from seat " + current : "")}.");

            if (seat == DriverSeat) GiveTo(character);
            else if (current == DriverSeat) ReturnToServer(velocity, angular);
            return null;
        }

        private void Publish(VehicleSeats s)
        {
            if (IsSpawned) seats.Value = s; else localSeats = s;
        }

        /// <summary>The driver's client simulates the vehicle from now on.</summary>
        private void GiveTo(GameObject character)
        {
            if (!IsSpawned) return;
            var no = character.GetComponent<NetworkObject>();
            if (no == null) return;
            if (NetworkObject.OwnerClientId != no.OwnerClientId) NetworkObject.ChangeOwnership(no.OwnerClientId);
        }

        /// <summary>The server simulates the vehicle again, starting from the motion the driver's client reported.</summary>
        private void ReturnToServer(Vector3 velocity, Vector3 angular)
        {
            if (IsSpawned && NetworkObject.OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
            ApplyAuthority();
            if (body != null && !body.isKinematic)
            {
                body.velocity = Vector3.ClampMagnitude(velocity, 70f);
                body.angularVelocity = Vector3.ClampMagnitude(angular, 10f);
            }
        }

        // ---------------------------------------------------------------- per frame
        private void Update()
        {
            if (vc == null) return;

            // speed of a kinematic copy, for the HUD and the server's checks
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 moved = (transform.position - lastObservedPos) / dt;
            if (moved.sqrMagnitude > 90f * 90f) moved = observedVelocity;   // a teleport, not motion
            observedVelocity = Vector3.Lerp(observedVelocity, moved, Mathf.Clamp01(8f * dt));
            observedSpeed = observedVelocity.magnitude;
            lastObservedPos = transform.position;

            if (IsAuthority && Time.unscaledTime >= nextWatch) { nextWatch = Time.unscaledTime + 0.25f; WatchOccupants(); }

            // everyone in a seat is placed by its VehicleOccupant (also late joiners and characters that spawned after the vehicle)
            var s = Seats;
            for (int i = 0; i < SeatCount; i++)
            {
                if (s[i] == 0) continue;
                var go = CharacterOf(s[i]);
                if (go == null) continue;
                var occ = go.GetComponent<VehicleOccupant>();
                if (occ == null) occ = go.AddComponent<VehicleOccupant>();
                occ.Sit(this, i);
            }

            if (!initialized) return;
            if (IsSimulating) SimulateInput();
            else ApplyRemoteState(IsSpawned ? drive.Value : default);
            ExpireIgnoredVictims();
        }

        /// <summary>Authority: a seat whose character is gone or dead is freed, and the physics owner always matches the driver.</summary>
        private void WatchOccupants()
        {
            var s = Seats;
            bool changed = false;
            for (int i = 0; i < SeatCount; i++)
            {
                if (s[i] == 0) continue;
                var go = CharacterOf(s[i]);
                var h = go != null ? go.GetComponent<JUHealth>() : null;
                if (go == null || !go.activeInHierarchy || (h != null && h.IsDead))
                {
                    Debug.Log($"[Vehicle] {name}: seat {i} freed ({(go == null ? "character gone" : "character dead")}).");
                    s[i] = 0;
                    changed = true;
                    if (i == DriverSeat) ReturnToServer(IsSimulating ? lastVelocity : observedVelocity, IsSimulating ? lastAngular : Vector3.zero);
                }
            }
            if (changed) Publish(s);
            if (!IsSpawned) return;

            var driver = Driver;
            ulong want = driver != null && driver.TryGetComponent(out NetworkObject dno) ? dno.OwnerClientId : NetworkManager.ServerClientId;
            if (NetworkObject.OwnerClientId != want)
            {
                if (want == NetworkManager.ServerClientId) ReturnToServer(observedVelocity, Vector3.zero);
                else NetworkObject.ChangeOwnership(want);
            }
        }

        private void FixedUpdate()
        {
            if (body == null || !IsSimulating) return;
            lastVelocity = body.velocity;
            lastAngular = body.angularVelocity;
        }

        /// <summary>Simulating peer: the local driver's controls, or a parked vehicle that holds still; then publish them.</summary>
        private void SimulateInput()
        {
            var st = vc.input.states;
            var driver = Driver;
            bool localDriver = driver != null && VehicleOccupant.IsLocalCharacter(driver);
            var engine = vc.powertrain.engine;

            if (localDriver)
            {
                bool controls = !JUPauseGame.IsPaused && Cursor.lockState == CursorLockMode.Locked;
                float v = controls ? JUInput.GetAxis(JUInput.Axis.MoveVertical) : 0f;
                float h = controls ? JUInput.GetAxis(JUInput.Axis.MoveHorizontal) : 0f;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                st.throttle = Mathf.Clamp01(v);
                st.brakes = Mathf.Clamp01(-v);
                st.steering = Mathf.Clamp(h, -1f, 1f);
                st.handbrake = controls && JUInput.GetButton(JUInput.Buttons.JumpButton) ? 1f : 0f;
                if (VehicleTestAid.Throttle.HasValue) st.throttle = VehicleTestAid.Throttle.Value;
                if (VehicleTestAid.Brake.HasValue) st.brakes = VehicleTestAid.Brake.Value;
                if (VehicleTestAid.Steer.HasValue) st.steering = VehicleTestAid.Steer.Value;
                if (VehicleTestAid.Handbrake.HasValue) st.handbrake = VehicleTestAid.Handbrake.Value;
                st.boost = controls && JUInput.GetButton(JUInput.Buttons.RunButton);
                st.horn = controls && kb != null && kb.hKey.isPressed;
                st.flipOver = controls && kb != null && kb.rKey.isPressed;
                if (controls && kb != null && kb.lKey.wasPressedThisFrame) st.lowBeamLights = true;   // a toggle NWH consumes
                if (!engine.IsRunning && !engine.starterActive) engine.StartEngine();
            }
            else if (driver == null)
            {
                // parked (or the driver jumped out): no throttle, handbrake on once it has slowed down
                st.throttle = 0f; st.boost = false; st.horn = false; st.flipOver = false;
                st.steering = Mathf.MoveTowards(st.steering, 0f, Time.deltaTime);
                float speed = body.velocity.magnitude;
                st.brakes = speed > 1.5f ? 0.15f : 0f;
                st.handbrake = speed < 3f ? 1f : 0f;
                if (engine.IsRunning && speed < 1f) engine.StopEngine();
                if (vc.effectsManager.lightsManager.lowBeamLights.On) st.lowBeamLights = true;   // lights off when parked
            }
            vc.input.states = st;

            if (!IsSpawned) return;
            var now = new VehicleDriveState
            {
                Steering = st.steering, Throttle = st.throttle, Brakes = st.brakes, Handbrake = st.handbrake,
                EngineAV = engine.outputAngularVelocity,
                Lights = vc.effectsManager.lightsManager.GetIntState(),
                Flags = (byte)((engine.IsRunning ? 1 : 0) | (st.boost ? 2 : 0) | (st.horn ? 4 : 0))
            };
            // at most ~15 updates a second, and only when something visible changed
            if (Time.unscaledTime >= nextDrivePublish && now.Differs(lastSent))
            {
                nextDrivePublish = Time.unscaledTime + 0.066f;
                lastSent = now;
                drive.Value = now;
            }
        }

        /// <summary>Copies the simulating peer's state onto this kinematic copy (visuals and sound only; the transform comes from the network).</summary>
        private void ApplyRemoteState(VehicleDriveState d)
        {
            var st = vc.input.states;
            st.steering = Mathf.Lerp(st.steering, d.Steering, 12f * Time.deltaTime);
            st.throttle = d.Throttle; st.brakes = d.Brakes; st.handbrake = d.Handbrake;
            st.boost = d.Boost; st.horn = d.Horn; st.flipOver = false;
            vc.input.states = st;
            var engine = vc.powertrain.engine;
            if (d.EngineOn && !engine.IsRunning && !engine.starterActive) engine.StartEngine();
            else if (!d.EngineOn && engine.IsRunning) engine.StopEngine();
            if (d.EngineOn) engine.outputAngularVelocity = Mathf.Lerp(engine.outputAngularVelocity, d.EngineAV, 10f * Time.deltaTime);
            vc.effectsManager.lightsManager.SetStateFromInt(d.Lights);
        }

        // ---------------------------------------------------------------- running people over
        /// <summary>
        /// On the driver's machine (where the vehicle has real physics) a hit on a character hurts it through the normal damage path:
        /// <see cref="Railbound.BR.BRNetHealth"/> forwards it to the server with the driver as the attacker, and the squad filter
        /// still protects teammates. The body is then let through (a kinematic copy of a player would stop the car like a wall).
        /// </summary>
        private void OnCollisionEnter(Collision c)
        {
            if (!IsSimulating || c.collider == null) return;
            var victimHealth = c.collider.GetComponentInParent<JUHealth>();
            if (victimHealth == null) return;
            var victim = victimHealth.gameObject;
            if (SeatOf(victim) >= 0) return;

            var victimColliders = victim.GetComponentsInChildren<Collider>(false);
            SetIgnore(victimColliders, true);
            ignoredVictims.Add((victimColliders, Time.time + 1.5f));
            body.velocity = lastVelocity * 0.88f;
            body.angularVelocity = lastAngular;

            var driver = Driver;
            if (driver == null || !VehicleOccupant.IsLocalCharacter(driver) || victimHealth.IsDead) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < runOverMinSpeed) return;
            if (runOverCooldown.TryGetValue(victimHealth, out float until) && Time.time < until) return;
            runOverCooldown[victimHealth] = Time.time + 0.75f;

            float damage = Mathf.Clamp((speed - runOverMinSpeed * 0.66f) * runOverDamagePerMs, 5f, 250f);
            JUHealth.PendingAttacker = driver;
            victimHealth.DoDamage(damage, c.GetContact(0).point);
            JUHealth.PendingAttacker = null;
            Debug.Log($"[Vehicle] {name} ran over {victim.name} at {speed * 3.6f:F0} km/h: {damage:F0} damage.");
        }

        private void SetIgnore(Collider[] others, bool ignore)
        {
            if (others == null) return;
            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] == null) continue;
                for (int j = 0; j < bodyColliders.Length; j++)
                    if (bodyColliders[j] != null) Physics.IgnoreCollision(others[i], bodyColliders[j], ignore);
            }
        }

        private void ExpireIgnoredVictims()
        {
            for (int i = ignoredVictims.Count - 1; i >= 0; i--)
            {
                if (Time.time < ignoredVictims[i].until) continue;
                SetIgnore(ignoredVictims[i].victim, false);
                ignoredVictims.RemoveAt(i);
            }
        }

        /// <summary>Occupants never collide with the vehicle they sit in (or the car would be pushed by its own passengers).</summary>
        public void IgnoreOccupant(Collider[] occupantColliders, bool ignore) { SetIgnore(occupantColliders, ignore); }

        // ---------------------------------------------------------------- getting out
        /// <summary>A free standing spot next to <paramref name="seat"/>: its side first, then the other side, the back, the front, the roof.</summary>
        public Vector3 FindExitPoint(int seat, GameObject character)
        {
            float side = seat >= 0 && seat < SeatCount && seatDefs[seat].Hips.x > 0.05f ? 1f : -1f;
            float z = seat >= 0 && seat < SeatCount ? seatDefs[seat].Hips.z : localBounds.center.z;
            float halfW = Mathf.Max(Mathf.Abs(localBounds.min.x), Mathf.Abs(localBounds.max.x));
            var candidates = new[]
            {
                new Vector3(side * (halfW + 0.7f), 0f, z),
                new Vector3(side * (halfW + 0.7f), 0f, z - 0.9f),
                new Vector3(-side * (halfW + 0.7f), 0f, z),
                new Vector3(0f, 0f, localBounds.min.z - 1.0f),
                new Vector3(0f, 0f, localBounds.max.z + 1.0f),
            };
            var ignore = new HashSet<Collider>(bodyColliders);
            if (character != null) foreach (var c in character.GetComponentsInChildren<Collider>(true)) ignore.Add(c);

            foreach (var local in candidates)
            {
                Vector3 w = transform.TransformPoint(local);
                w.y = transform.position.y;
                if (!GroundBelow(w + Vector3.up * 3f, ignore, out Vector3 ground)) continue;
                if (Mathf.Abs(ground.y - transform.position.y) > 2.5f) continue;
                if (Blocked(ground, ignore)) continue;
                return ground + Vector3.up * 0.05f;
            }
            // nowhere beside it: on the roof
            return transform.TransformPoint(new Vector3(0f, localBounds.max.y + 0.15f, localBounds.center.z));
        }

        private static bool GroundBelow(Vector3 from, HashSet<Collider> ignore, out Vector3 point)
        {
            var hits = Physics.RaycastAll(from, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (ignore.Contains(h.collider)) continue;
                point = h.point;
                return Vector3.Angle(h.normal, Vector3.up) < 50f;
            }
            point = default;
            return false;
        }

        private static bool Blocked(Vector3 feet, HashSet<Collider> ignore)
        {
            var overlaps = Physics.OverlapCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.32f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var o in overlaps) if (!ignore.Contains(o)) return true;
            return false;
        }

        // ---------------------------------------------------------------- test aid
        /// <summary>Drivetrain and wheel state (gear, revs, steering, per wheel: grounded / drive / brake / slip / what the ray hits).</summary>
        public string DebugDrive()
        {
            if (vc == null || !initialized) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append($"gear={vc.powertrain.transmission.Gear} rpm={vc.powertrain.engine.OutputRPM:F0} steerAngle={vc.steering.angle:F1} hb={vc.input.states.handbrake:F1} br={vc.input.states.brakes:F1} com={body.centerOfMass:F2} mass={body.mass:F0}");
            foreach (var w in vc.powertrain.wheels)
            {
                var u = w.wheelUAPI;
                if (u == null) continue;
                sb.Append($" | {u.name}: g={(u.IsGrounded ? 1 : 0)} mt={u.MotorTorque:F0} bt={u.BrakeTorque:F0} sa={u.SteerAngle:F1} ls={u.LongitudinalSlip:F2} lat={u.LateralSlip:F2} hit={(u.HitCollider != null ? u.HitCollider.name : "-")}");
            }
            return sb.ToString();
        }

        public string DebugLine()
        {
            var s = Seats;
            return $"{name} pos={transform.position:F2} yaw={transform.eulerAngles.y:F0} up={Vector3.Dot(transform.up, Vector3.up):F2} speed={SpeedKmh:F0}km/h " +
                   $"owner={(IsSpawned ? NetworkObject.OwnerClientId.ToString() : "local")} sim={IsSimulating} kin={(body != null && body.isKinematic)} " +
                   $"seats=[{s.S0},{s.S1},{s.S2},{s.S3}] engine={(vc != null && vc.powertrain.engine.IsRunning)} thr={(vc != null ? vc.input.states.throttle : 0f):F2} steer={(vc != null ? vc.input.states.steering : 0f):F2}";
        }
    }
}
