using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JUTPS;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Test aid, client only: <c>-cartest DIR</c>. Drives one vehicle of each kind (the local player takes the wheel) over the same route:
    /// full throttle 8 s, a turn 3 s, full throttle 4 s. Every physics step it records speed, vertical speed and acceleration, how many
    /// wheels touch the ground, height above the terrain and body pitch / roll, and writes DIR/cars.csv plus a summary per vehicle:
    /// time airborne, longest flight, the mean vertical acceleration while airborne (gravity here is -15 m/s^2; a car that floats shows a
    /// smaller number), highest point above the ground and the largest tilt.
    /// </summary>
    public class CarPhysicsTestAid : MonoBehaviour
    {
        private string dir;
        private RailboundVehicle current;
        private readonly StringBuilder csv = new StringBuilder("car,t,kmh,vy,ay,wheelsDown,wheels,height,pitch,roll\n");
        private float t0, lastVy;
        private bool logging, firstSample;
        private class Stats { public int samples, airborne; public float flight, maxFlight, airAy, maxH, maxPitch, maxRoll, maxKmh; public int airAyN; }
        private Stats stats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-cartest" || Application.isBatchMode) continue;
                var go = new GameObject("[Test] Car physics");
                DontDestroyOnLoad(go);
                go.AddComponent<CarPhysicsTestAid>().dir = a[i + 1];
                return;
            }
        }

        private static GameObject Me => JUGameManager.PlayerController != null ? JUGameManager.PlayerController.gameObject : null;

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float waited = 0f;
            while (waited < 240f && (Me == null || RailboundVehicle.All.Count == 0 || BR.BRMatchCoordinator.Instance == null || !BR.BRMatchCoordinator.Instance.IsMatchLive))
            { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(4f);
            var summary = new StringBuilder();

            // one of each kind, nearest first
            var kinds = new Dictionary<string, RailboundVehicle>();
            foreach (var v in RailboundVehicle.All)
            {
                if (v == null || v.HasDriver) continue;
                string k = v.DisplayName;
                if (!kinds.TryGetValue(k, out var have) || Vector3.Distance(v.transform.position, Me.transform.position) < Vector3.Distance(have.transform.position, Me.transform.position)) kinds[k] = v;
            }

            foreach (var kv in kinds)
            {
                var v = kv.Value;
                var me = Me;
                if (me == null || v == null) continue;
                Vector3 spot = v.FindExitPoint(v.DriverSeat, me);
                var nt = me.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(spot, me.transform.rotation, me.transform.localScale); else me.transform.position = spot;
                var rb = me.GetComponent<Rigidbody>(); if (rb != null) { rb.position = spot; rb.velocity = Vector3.zero; }
                yield return new WaitForSecondsRealtime(1.5f);
                v.RequestEnter(me);
                float te = Time.realtimeSinceStartup;
                while ((v.SeatOf(me) != v.DriverSeat || !v.IsSimulating) && Time.realtimeSinceStartup - te < 10f) yield return null;
                if (!v.IsSimulating) { summary.AppendLine($"{kv.Key}: could not take the wheel"); continue; }
                yield return new WaitForSecondsRealtime(1.5f);

                current = v; stats = new Stats(); t0 = Time.time; firstSample = true; logging = true;
                VehicleTestAid.Throttle = 1f; VehicleTestAid.Steer = 0f; VehicleTestAid.Brake = 0f; VehicleTestAid.Handbrake = 0f;
                yield return new WaitForSecondsRealtime(4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, kv.Key.Replace(' ', '_') + "_a.png"));
                yield return new WaitForSecondsRealtime(4f);
                VehicleTestAid.Steer = 0.45f;
                yield return new WaitForSecondsRealtime(3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, kv.Key.Replace(' ', '_') + "_b.png"));
                VehicleTestAid.Steer = 0f;
                yield return new WaitForSecondsRealtime(4f);
                logging = false;
                VehicleTestAid.Throttle = 0f; VehicleTestAid.Brake = 1f;
                float tb = Time.realtimeSinceStartup;
                while (v.SpeedKmh > 3f && Time.realtimeSinceStartup - tb < 6f) yield return null;
                VehicleTestAid.Brake = 0f; VehicleTestAid.Handbrake = 1f;

                var s = stats;
                string line = $"{kv.Key}: max {s.maxKmh:0} km/h, airborne {100f * s.airborne / Mathf.Max(1, s.samples):0.0} % of the time, longest flight {s.maxFlight:0.00} s, " +
                              $"vertical acceleration in the air {(s.airAyN > 0 ? s.airAy / s.airAyN : 0f):0.0} m/s2 (gravity {Physics.gravity.y:0.0}), highest {s.maxH:0.00} m above the ground, " +
                              $"max pitch {s.maxPitch:0} deg, max roll {s.maxRoll:0} deg";
                summary.AppendLine(line);
                Debug.Log("[CarTest] " + line);
                v.RequestExit(me);
                float tx = Time.realtimeSinceStartup;
                while (v.SeatOf(me) >= 0 && Time.realtimeSinceStartup - tx < 6f) yield return null;
                VehicleTestAid.Throttle = VehicleTestAid.Brake = VehicleTestAid.Steer = VehicleTestAid.Handbrake = null;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            File.WriteAllText(Path.Combine(dir, "cars.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString());
            Debug.Log("[CarTest] DONE");
        }

        private void FixedUpdate()
        {
            if (!logging || current == null || current.Body == null || current.Controller == null) return;
            var body = current.Body;
            float vy = body.velocity.y;
            float ay = firstSample ? 0f : (vy - lastVy) / Time.fixedDeltaTime;
            firstSample = false; lastVy = vy;
            int down = 0, n = 0;
            var wheels = current.Controller.powertrain.wheels;
            for (int i = 0; i < wheels.Count; i++) { n++; if (wheels[i].wheelUAPI != null && wheels[i].wheelUAPI.IsGrounded) down++; }
            var terrain = Terrain.activeTerrain;
            float h = terrain != null ? body.position.y - (terrain.SampleHeight(body.position) + terrain.transform.position.y) : 0f;
            Vector3 e = body.rotation.eulerAngles;
            float pitch = Mathf.Abs(Mathf.DeltaAngle(0f, e.x)), roll = Mathf.Abs(Mathf.DeltaAngle(0f, e.z));
            float kmh = body.velocity.magnitude * 3.6f;
            csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:0.000},{2:0.0},{3:0.00},{4:0.0},{5},{6},{7:0.00},{8:0.0},{9:0.0}\n",
                current.DisplayName, Time.time - t0, kmh, vy, ay, down, n, h, pitch, roll);
            var s = stats;
            s.samples++;
            s.maxKmh = Mathf.Max(s.maxKmh, kmh);
            s.maxH = Mathf.Max(s.maxH, h);
            s.maxPitch = Mathf.Max(s.maxPitch, pitch); s.maxRoll = Mathf.Max(s.maxRoll, roll);
            if (down == 0)
            {
                s.airborne++;
                s.flight += Time.fixedDeltaTime;
                s.maxFlight = Mathf.Max(s.maxFlight, s.flight);
                if (s.flight > 0.05f) { s.airAy += ay; s.airAyN++; }
            }
            else s.flight = 0f;
        }
    }
}
