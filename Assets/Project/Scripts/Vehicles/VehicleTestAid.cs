using System.Collections;
using System.IO;
using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Test aid, only with <c>-netvehicle MODE</c> (or <c>-vehlog</c> for the log alone):
    ///  * every peer (also the server) logs each vehicle and each occupant once a second, so logs can be compared for sync;
    ///  * <c>drive</c>: the local player walks up to the nearest vehicle, takes the wheel, drives a route (throttle, turn, brake,
    ///    handbrake), switches seats, gets out, and takes screenshots on the way (<c>-vehshots DIR</c>);
    ///  * <c>ride</c>: waits for another player to sit at the wheel of a vehicle, joins as a passenger, rides, gets out.
    /// </summary>
    public partial class VehicleTestAid : MonoBehaviour
    {
        // test overrides for the local driver's controls (null = the real input)
        public static float? Throttle, Brake, Steer, Handbrake;

        private string mode, shots;
        private int shotNo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string m = Arg("-netvehicle");
            bool log = m != null || Has("-vehlog");
            if (!log || FindFirstObjectByType<VehicleTestAid>() != null) return;
            var go = new GameObject("[VehicleTestAid]");
            DontDestroyOnLoad(go);
            var aid = go.AddComponent<VehicleTestAid>();
            aid.mode = m;
            aid.shots = Arg("-vehshots");
        }

        private IEnumerator Start()
        {
            StartCoroutine(LogLoop());
            if (mode == "drive") yield return Drive();
            else if (mode == "ride") yield return Ride();
            else if (mode == "runover") yield return RunOver();
            else if (mode == "target") yield return Target();
            else if (mode == "park") yield return Park();
            else if (mode == "shoot") yield return Shoot();
            else if (mode == "seats") yield return SeatTour();
        }

        private IEnumerator LogLoop()
        {
            int lastFrame = Time.frameCount; float lastReal = Time.realtimeSinceStartup;
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                float span = Mathf.Max(0.001f, Time.realtimeSinceStartup - lastReal);
                Debug.Log($"[VehicleNet] fps={(Time.frameCount - lastFrame) / span:F1} timeScale={Time.timeScale:F2} dt={Time.deltaTime:F3} udt={Time.unscaledDeltaTime:F3} gameTime={Time.time:F1} vehicles={RailboundVehicle.All.Count}");
                lastFrame = Time.frameCount; lastReal = Time.realtimeSinceStartup;
                foreach (var v in RailboundVehicle.All)
                {
                    if (v == null) continue;
                    Debug.Log("[VehicleNet] t=" + Time.realtimeSinceStartup.ToString("F1") + " " + v.DebugLine());
                    if (v.IsSimulating && v.HasDriver) Debug.Log("[VehicleNet]   drive " + v.DebugDrive());
                    var s = v.Seats;
                    for (int i = 0; i < v.SeatCount; i++)
                    {
                        var go = RailboundVehicle.CharacterOf(s[i]);
                        if (go == null) continue;
                        var anim = go.GetComponent<Animator>();
                        var hips = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
                        float err = hips != null ? Vector3.Distance(hips.position, v.SeatHipsWorld(i)) : -1f;
                        var cc = go.GetComponent<JUCharacterController>();
                        bool drivingAnim = anim != null && cc != null && anim.GetBool(cc.AnimatorParameters.Driving);
                        Debug.Log($"[VehicleNet]   seat {i} {go.name} root={go.transform.position:F2} hipsErr={err:F2} drivingAnim={drivingAnim} isDriving={(cc != null && cc.IsDriving)} kin={(go.GetComponent<Rigidbody>() != null && go.GetComponent<Rigidbody>().isKinematic)}");
                    }
                }
            }
        }

        private static GameObject Me => JUGameManager.PlayerController != null ? JUGameManager.PlayerController.gameObject : null;

        private IEnumerator WaitForPlayerAndVehicles()
        {
            while (Me == null || RailboundVehicle.All.Count == 0) yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);
        }

        private static RailboundVehicle Nearest(Vector3 p, bool withOtherDriver)
        {
            RailboundVehicle best = null; float bd = float.MaxValue;
            foreach (var v in RailboundVehicle.All)
            {
                if (v == null) continue;
                if (withOtherDriver && (v.Driver == null || v.Driver == Me)) continue;
                float d = Vector3.Distance(v.transform.position, p);
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        private void WalkTo(RailboundVehicle v, int seat)
        {
            var me = Me;
            Vector3 spot = v.FindExitPoint(seat, me);
            var nt = me.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(spot, me.transform.rotation, me.transform.localScale);
            else me.transform.position = spot;
            var rb = me.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.position = spot; rb.velocity = Vector3.zero; }
            Debug.Log($"[VehicleTest] walked to {v.name} seat {seat} side at {spot:F2} (distance to body {v.DistanceTo(spot):F2}).");
        }

        private IEnumerator Drive()
        {
            yield return WaitForPlayerAndVehicles();
            var v = Nearest(Me.transform.position, false);
            Debug.Log("[VehicleTest] drive: target " + v.name + " " + v.DebugLine());
            WalkTo(v, v.DriverSeat);
            yield return new WaitForSecondsRealtime(1.5f);
            Shot("0_beside");
            Debug.Log("[VehicleTest] prompt offers " + (VehicleLocalPlayer.Offered != null ? VehicleLocalPlayer.Offered.name : "nothing"));
            v.RequestEnter(Me);
            float t0 = Time.realtimeSinceStartup;
            while ((v.SeatOf(Me) != v.DriverSeat || !v.IsSimulating) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Debug.Log($"[VehicleTest] seated={v.SeatOf(Me)} simulating={v.IsSimulating} after {Time.realtimeSinceStartup - t0:F2}s");
            yield return new WaitForSecondsRealtime(2f);
            Shot("1_seated");
            // seats: move to the passenger seat and back
            v.RequestMove(Me, 1);
            yield return WaitSeat(v, 1);
            Debug.Log($"[VehicleTest] after move to seat 1: seat={v.SeatOf(Me)} simulating={v.IsSimulating} owner={(v.IsSpawned ? v.OwnerClientId : 0)}");
            Shot("6_passenger_seat");
            v.RequestMove(Me, v.DriverSeat);
            yield return WaitSeat(v, v.DriverSeat);
            while (!v.IsSimulating) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log($"[VehicleTest] back at the wheel: seat={v.SeatOf(Me)} simulating={v.IsSimulating}");

            Vector3 start = v.transform.position;
            Throttle = 1f; Steer = 0f;
            yield return Hold(4f, "2_accelerate");
            Steer = 0.6f;
            yield return Hold(3f, "3_turn");
            Steer = -0.4f;
            yield return Hold(2f, "4_turn_back");
            Throttle = 0f; Steer = 0f; Brake = 1f;
            yield return Hold(0.6f, "5_brake");
            float tb = Time.realtimeSinceStartup;
            while (v.SpeedKmh > 2f && Time.realtimeSinceStartup - tb < 5f) yield return null;   // stop (held brake would reverse)
            Brake = 0f; Handbrake = 1f;
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[VehicleTest] drove {Vector3.Distance(start, v.transform.position):F1} m; {v.DebugLine()}");


            // a second leg for the other client to ride along
            Handbrake = 0f; Throttle = 0f;
            yield return new WaitForSecondsRealtime(12f);   // the rider gets in
            Throttle = 0.8f; Steer = 0.2f;
            yield return Hold(5f, "7_with_passenger");
            Throttle = 0f; Brake = 1f; Steer = 0f;
            yield return new WaitForSecondsRealtime(0.6f);
            tb = Time.realtimeSinceStartup;
            while (v.SpeedKmh > 2f && Time.realtimeSinceStartup - tb < 5f) yield return null;
            Brake = 0f; Handbrake = 1f;

            v.RequestExit(Me);
            yield return WaitSeat(v, -1);
            yield return new WaitForSecondsRealtime(1f);
            Throttle = Brake = Steer = Handbrake = null;
            var rbMe = Me.GetComponent<Rigidbody>();
            Debug.Log($"[VehicleTest] out: seat={v.SeatOf(Me)} pos={Me.transform.position:F2} distToBody={v.DistanceTo(Me.transform.position):F2} kin={(rbMe != null && rbMe.isKinematic)} owner={(v.IsSpawned ? v.OwnerClientId : 0)} {v.DebugLine()}");
            Shot("8_out");
            yield return new WaitForSecondsRealtime(3f);
            Debug.Log("[VehicleTest] DONE drive");
        }

        private IEnumerator Ride()
        {
            yield return WaitForPlayerAndVehicles();
            // wait until another player has driven a vehicle somewhere and parked it with only the driver inside for 4 s
            RailboundVehicle v = null;
            Vector3 firstSeen = Vector3.zero;
            float stableSince = -1f, t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 120f)
            {
                var c = Nearest(Me.transform.position, true);
                if (c != v) { v = c; firstSeen = c != null ? c.transform.position : Vector3.zero; stableSince = -1f; }
                bool parked = v != null && v.Seats.Count == 1 && v.SeatOf(v.Driver) == v.DriverSeat && v.SpeedKmh < 3f
                              && Vector3.Distance(firstSeen, v.transform.position) > 8f;
                if (!parked) stableSince = -1f;
                else if (stableSince < 0f) stableSince = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - stableSince > 4f) break;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (v == null || stableSince < 0f) { Debug.Log("[VehicleTest] ride: no driven vehicle found"); yield break; }
            Debug.Log("[VehicleTest] ride: joining " + v.DebugLine());
            Shot("r0_watch_driver");
            WalkTo(v, 1);
            yield return new WaitForSecondsRealtime(1f);
            v.RequestEnter(Me, 1);
            t0 = Time.realtimeSinceStartup;
            while (v.SeatOf(Me) < 0 && Time.realtimeSinceStartup - t0 < 8f) yield return null;
            Debug.Log($"[VehicleTest] ride: seat={v.SeatOf(Me)} after {Time.realtimeSinceStartup - t0:F2}s; {v.DebugLine()}");
            yield return Hold(8f, "r1_riding");
            Shot("r2_riding_end");
            yield return new WaitForSecondsRealtime(2f);
            v.RequestExit(Me);
            yield return WaitSeat(v, -1);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[VehicleTest] ride out: seat={v.SeatOf(Me)} pos={Me.transform.position:F2} distToBody={v.DistanceTo(Me.transform.position):F2}");
            Shot("r3_out");
            Debug.Log("[VehicleTest] DONE ride");
        }

        private IEnumerator WaitSeat(RailboundVehicle v, int seat)
        {
            float t0 = Time.realtimeSinceStartup;
            while (v.SeatOf(Me) != seat && Time.realtimeSinceStartup - t0 < 8f) yield return null;
            Debug.Log($"[VehicleTest] seat {v.SeatOf(Me)} (wanted {seat}) after {Time.realtimeSinceStartup - t0:F2}s");
        }

        private IEnumerator Hold(float seconds, string shot)
        {
            float t = 0f; bool took = false;
            while (t < seconds)
            {
                if (!took && t > seconds * 0.6f) { Shot(shot); took = true; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shots)) return;
            try { Directory.CreateDirectory(shots); ScreenCapture.CaptureScreenshot(Path.Combine(shots, (shotNo++).ToString("00") + "_" + name + ".png")); } catch { }
        }

        private static bool Has(string a)
        {
            foreach (string s in System.Environment.GetCommandLineArgs()) if (string.Equals(s, a, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Arg(string a)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], a, System.StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
