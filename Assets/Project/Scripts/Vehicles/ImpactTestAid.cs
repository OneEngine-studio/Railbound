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
    /// first at about 30 km/h (a knockdown it should survive and get up from), then flat out (a hit that kills). A camera beside the bot
    /// saves a frame every 0.1 s, and the log follows the bot's body on this machine: how high and how far it flew, its ragdoll state,
    /// whether it got up again, and whether its copy follows the network again afterwards.
    /// </summary>
    public class ImpactTestAid : MonoBehaviour
    {
        private string dir;

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
            var camRig = FindFirstObjectByType<JUTPS.CameraSystems.JUCameraController>();

            for (int run = 0; run < 2; run++)
            {
                float targetKmh = run == 0 ? 30f : 75f;
                // the nearest living bot
                JUHealth bot = null; bd = float.MaxValue;
                foreach (var b in FindObjectsByType<BR.BRNetBot>(FindObjectsSortMode.None))
                {
                    var h = b.GetComponent<JUHealth>();
                    if (h == null || h.IsDead || !b.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(b.transform.position, v.transform.position);
                    if (d < bd) { bd = d; bot = h; }
                }
                if (bot == null) break;

                // the car 30 m away (more for the fast run), facing the bot
                Vector3 bp = bot.transform.position;
                Vector3 dirFlat = Vector3.ProjectOnPlane(bp - v.transform.position, Vector3.up);
                if (dirFlat.sqrMagnitude < 1f) dirFlat = Vector3.forward;
                dirFlat.Normalize();
                Vector3 start = bp - dirFlat * (run == 0 ? 30f : 60f);
                var terrain = Terrain.activeTerrain;
                if (terrain != null) start.y = terrain.SampleHeight(start) + terrain.transform.position.y + 1.2f;
                v.Body.velocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
                v.Body.position = start; v.Body.rotation = Quaternion.LookRotation(dirFlat, Vector3.up);
                v.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dirFlat, Vector3.up));
                yield return new WaitForSecondsRealtime(1f);

                var ragdoll = bot.GetComponent<AdvancedRagdollController>();
                var nt = bot.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                Vector3 hitFrom = bot.transform.position;
                float hipsMax = -999f, startY = bot.transform.position.y; bool hit = false; float hitAt = -1f; int frame = 0;
                string states = ""; float closest = 999f; v.TakeContacts();
                VehicleTestAid.Handbrake = 0f; VehicleTestAid.Brake = 0f;
                float t = 0f;
                if (camRig != null) camRig.enabled = false;
                var cam = Camera.main;
                while (t < 14f)
                {
                    t += Time.deltaTime;
                    // drive at the bot, holding the target speed
                    if (!hit && bot != null)
                    {
                        Vector3 to = Vector3.ProjectOnPlane(bot.transform.position - v.transform.position, Vector3.up);
                        float ang = Vector3.SignedAngle(v.transform.forward, to, Vector3.up);
                        VehicleTestAid.Steer = Mathf.Clamp(ang / 25f, -1f, 1f);
                        VehicleTestAid.Throttle = v.SpeedKmh < targetKmh ? 1f : 0f;
                        if (ragdoll != null && ragdoll.State != AdvancedRagdollController.RagdollState.Animated) { hit = true; hitAt = t; hitFrom = bot.transform.position; Debug.Log($"[ImpactTest] run {run}: hit at {v.SpeedKmh:0} km/h"); }
                    }
                    else { VehicleTestAid.Throttle = 0f; VehicleTestAid.Brake = 1f; VehicleTestAid.Steer = 0f; }
                    if (bot == null) break;
                    closest = Mathf.Min(closest, Vector3.Distance(v.transform.position, bot.transform.position));
                    if (ragdoll != null && ragdoll.Hips != null) hipsMax = Mathf.Max(hipsMax, ragdoll.Hips.position.y);
                    // a camera beside the meeting point
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
                string line = bot == null ? $"run {run}: bot object gone" :
                    $"run {run} at ~{targetKmh} km/h: hit={hit} thrown {(bot.transform.position - hitFrom).magnitude:0.0} m, flew {hipsMax - startY - 1f:0.00} m above standing hips, " +
                    $"now {(bot.IsDead ? "dead" : "alive")} health {bot.Health:0} ragdoll {(ragdoll != null ? ragdoll.State.ToString() : "-")} following network {(nt != null && nt.enabled)} | closest {closest:0.0} m, touched: {v.TakeContacts()} | {states}";
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
