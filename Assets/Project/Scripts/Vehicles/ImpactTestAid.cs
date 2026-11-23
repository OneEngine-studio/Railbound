using System.Collections;
using System.IO;
using System.Text;
using JUTPS;
using JUTPS.PhysicsScripts;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Test aid, client only: <c>-impacttest DIR</c>. The local player takes the nearest vehicle and drives it into a living bot twice:
    /// first at about 30 km/h (a knockdown it should survive and get up from), then flat out (a hit that kills). The approach is chosen
    /// on open, fairly level ground so the car can reach the bot; a target that dies of something else first (the bots fight each other)
    /// is swapped for another. Only a real hit counts (the vehicle's own run-over event). A camera beside the bot saves a frame every
    /// 0.1 s, and the log follows the bot's body on this machine: how high and how far it flew, its ragdoll state, whether it got up
    /// again, and whether its copy follows the network again afterwards.
    /// </summary>
    public class ImpactTestAid : MonoBehaviour
    {
        private string dir;
        private GameObject ranOverVictim; private float ranOverSpeed, ranOverDamage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-impacttest" || Application.isBatchMode) continue;
                var go = new GameObject("[Test] Impact");
                DontDestroyOnLoad(go);
                go.AddComponent<ImpactTestAid>().dir = a[i + 1];
                return;
            }
        }

        private static GameObject Me => JUGameManager.PlayerController != null ? JUGameManager.PlayerController.gameObject : null;

        private void OnEnable() { RailboundVehicle.RanOver += OnRanOver; }
        private void OnDisable() { RailboundVehicle.RanOver -= OnRanOver; }
        private void OnRanOver(RailboundVehicle v, GameObject victim, float speed, float damage) { ranOverVictim = victim; ranOverSpeed = speed; ranOverDamage = damage; }

        private static float Ground(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<JUHealth>() == null && h.collider.attachedRigidbody == null) return h.point.y;
            var t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
        }

        // a heading from which the car can drive straight at the bot: level ground and nothing (but the bot) in the way
        private static bool ClearApproach(RailboundVehicle v, JUHealth bot, Vector3 dirFlat, float dist, out Vector3 start)
        {
            Vector3 bp = bot.transform.position;
            start = bp - dirFlat * dist;
            float prev = Ground(start);
            start.y = prev + 1.2f;
            for (float d = 3f; d <= dist + 0.01f; d += 3f)
            {
                Vector3 q = bp - dirFlat * (dist - d);
                float y = Ground(q);
                if (Mathf.Abs(y - prev) > 3f * 0.3f) return false;   // steeper than ~17 degrees between samples
                prev = y;
            }
            Vector3 from = start + Vector3.up * 0.6f;
            Vector3 to = bp + Vector3.up * 1.0f;
            foreach (var h in Physics.SphereCastAll(from, 1.1f, (to - from).normalized, Vector3.Distance(from, to) - 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(bot.transform) || h.collider.transform.IsChildOf(v.transform)) continue;
                if (h.normal.y > 0.75f) continue;   // the ground itself
                if (h.collider.GetComponentInParent<JUHealth>() != null) continue;
                return false;
            }
            return true;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float waited = 0f;
            while (waited < 240f && (Me == null || RailboundVehicle.All.Count == 0 || BR.BRMatchCoordinator.Instance == null || !BR.BRMatchCoordinator.Instance.IsMatchLive))
            { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(4f);
            var report = new StringBuilder();

            RailboundVehicle v = null; float bd = float.MaxValue;
            foreach (var c in RailboundVehicle.All) { if (c == null || c.HasDriver) continue; float d = Vector3.Distance(c.transform.position, Me.transform.position); if (d < bd) { bd = d; v = c; } }
            if (v == null) { Debug.Log("[ImpactTest] DONE (no vehicle)"); yield break; }
            var me = Me;
            Vector3 spot = v.FindExitPoint(v.DriverSeat, me);
            var nt0 = me.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt0 != null && nt0.IsSpawned && nt0.CanCommitToTransform) nt0.Teleport(spot, me.transform.rotation, me.transform.localScale); else me.transform.position = spot;
            yield return new WaitForSecondsRealtime(1.5f);
            v.RequestEnter(me);
            float te = Time.realtimeSinceStartup;
            while ((v.SeatOf(me) != v.DriverSeat || !v.IsSimulating) && Time.realtimeSinceStartup - te < 10f) yield return null;
            if (!v.IsSimulating) { Debug.Log("[ImpactTest] DONE (no wheel)"); yield break; }
            Debug.Log($"[ImpactTest] driving {v.DisplayName} ({v.Body.mass:0} kg)");
            var camRig = FindFirstObjectByType<JUTPS.CameraSystems.JUCameraController>();

            for (int run = 0; run < 2; run++)
            {
                float targetKmh = run == 0 ? 30f : 75f;
                float dist = run == 0 ? 30f : 60f;
                string line = null;
                for (int attempt = 0; attempt < 4 && line == null; attempt++)
                {
                    // the nearest living bot with an open approach
                    JUHealth bot = null; Vector3 start = Vector3.zero, dirFlat = Vector3.forward; bd = float.MaxValue;
                    foreach (var b in FindObjectsByType<BR.BRNetBot>(FindObjectsSortMode.None))
                    {
                        var h = b.GetComponent<JUHealth>();
                        if (h == null || h.IsDead || !b.gameObject.activeInHierarchy) continue;
                        float d = Vector3.Distance(b.transform.position, v.transform.position);
                        if (d >= bd) continue;
                        for (int k = 0; k < 16; k++)
                        {
                            Vector3 df = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                            if (!ClearApproach(v, h, df, dist, out var st)) continue;
                            bd = d; bot = h; start = st; dirFlat = df;
                            break;
                        }
                    }
                    if (bot == null) { line = $"run {run}: no living bot with an open approach"; break; }

                    v.Body.velocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
                    v.Body.position = start; v.Body.rotation = Quaternion.LookRotation(dirFlat, Vector3.up);
                    v.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dirFlat, Vector3.up));
                    VehicleTestAid.Throttle = 0f; VehicleTestAid.Brake = 1f; VehicleTestAid.Steer = 0f; VehicleTestAid.Handbrake = 0f;
                    yield return new WaitForSecondsRealtime(1f);

                    var ragdoll = bot.GetComponent<AdvancedRagdollController>();
                    var nt = bot.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                    Vector3 hitFrom = bot.transform.position;
                    float hipsStart = ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position.y : bot.transform.position.y + 1f;
                    float hipsMax = -999f; bool hit = false; float hitAt = -1f; int frame = 0; float closest = 999f;
                    string states = "", chase = "";
                    ranOverVictim = null;
                    VehicleTestAid.Handbrake = 0f; VehicleTestAid.Brake = 0f;
                    float t = 0f;
                    if (camRig != null) camRig.enabled = false;
                    var cam = Camera.main;
                    bool diedElsewhere = false;
                    while (t < 22f)
                    {
                        t += Time.deltaTime;
                        if (bot == null) break;
                        if (!hit)
                        {
                            if (ranOverVictim == bot.gameObject)
                            {
                                hit = true; hitAt = t; hitFrom = bot.transform.position;
                                Debug.Log($"[ImpactTest] run {run}: hit at {ranOverSpeed * 3.6f:0} km/h, {ranOverDamage:0} damage");
                            }
                            else if (bot.IsDead) { diedElsewhere = true; break; }
                            else
                            {
                                Vector3 to = Vector3.ProjectOnPlane(bot.transform.position - v.transform.position, Vector3.up);
                                float ang = Vector3.SignedAngle(v.transform.forward, to, Vector3.up);
                                VehicleTestAid.Steer = Mathf.Clamp(ang / 25f, -1f, 1f);
                                VehicleTestAid.Throttle = v.SpeedKmh < targetKmh ? 1f : 0f;
                                if (Mathf.Abs(t % 1f) < Time.deltaTime) chase += $" {t:0}s:{Vector3.Distance(v.transform.position, bot.transform.position):0.0}m@{v.SpeedKmh:0}";
                            }
                        }
                        else { VehicleTestAid.Throttle = 0f; VehicleTestAid.Brake = 1f; VehicleTestAid.Steer = 0f; }
                        closest = Mathf.Min(closest, Vector3.Distance(v.transform.position, bot.transform.position));
                        if (hit && ragdoll != null && ragdoll.Hips != null) hipsMax = Mathf.Max(hipsMax, ragdoll.Hips.position.y);
                        if (cam != null)
                        {
                            Vector3 focus = ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position : bot.transform.position;
                            Vector3 side = Vector3.Cross(Vector3.up, dirFlat);
                            cam.transform.position = (hit ? hitFrom : focus) + side * 9f + Vector3.up * 2.5f - dirFlat * 2f;
                            cam.transform.LookAt(focus);
                        }
                        if (hit && t - hitAt < 5f && frame < 60 && Time.frameCount % 3 == 0) ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"run{run}_{frame++:00}.png"));
                        if (hit && Mathf.Abs((t - hitAt) % 1f) < Time.deltaTime) states += $" +{t - hitAt:0}s:{(ragdoll != null ? ragdoll.State.ToString() : "-")}/{(bot.IsDead ? "dead" : "alive")}/nt={(nt != null && nt.enabled)}";
                        if (hit && t - hitAt > 9f) break;
                        yield return null;
                    }
                    if (camRig != null) camRig.enabled = true;
                    VehicleTestAid.Throttle = 0f; VehicleTestAid.Brake = 1f;
                    if (diedElsewhere) { Debug.Log($"[ImpactTest] run {run}: the target died of something else, another one"); continue; }
                    line = bot == null ? $"run {run}: bot object gone" :
                        $"run {run} at ~{targetKmh} km/h: hit={hit}{(hit ? $" at {ranOverSpeed * 3.6f:0} km/h ({ranOverDamage:0} damage)" : "")}, body thrown {(bot.transform.position - hitFrom).magnitude:0.0} m, hips rose {Mathf.Max(0f, hipsMax - hipsStart):0.00} m, " +
                        $"now {(bot.IsDead ? "dead" : "alive")} health {bot.Health:0} ragdoll {(ragdoll != null ? ragdoll.State.ToString() : "-")} following network {(nt != null && nt.enabled)} | closest {closest:0.0} m | chase (distance@km/h):{chase} | after the hit:{states}";
                }
                if (line == null) line = $"run {run}: every target died of something else first";
                Debug.Log("[ImpactTest] " + line);
                report.AppendLine(line);
                yield return new WaitForSecondsRealtime(2f);
            }
            VehicleTestAid.Throttle = VehicleTestAid.Brake = VehicleTestAid.Steer = VehicleTestAid.Handbrake = null;
            File.WriteAllText(Path.Combine(dir, "impact.txt"), report.ToString());
            Debug.Log("[ImpactTest] DONE");
        }
    }
}
