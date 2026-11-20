using System.Collections;
using System.IO;
using System.Text;
using JUTPS;
using JUTPS.PhysicsScripts;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// Test aid, client only: <c>-deathcam DIR</c> watches bots die on this machine's copy of them, which is what a player sees after a kill.
    /// For three bots in turn it switches the player camera off, puts a camera 4 m from the bot, kills it the way a hit from the local player
    /// does (damage reported to the server, which decides), saves a frame every 0.1 s for 4 s, and 6 s after the death logs whether the body
    /// is still down (ragdoll state, hips height above the feet, position drift), so a corpse that gets up is caught.
    /// </summary>
    public class DeathCamTestAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-deathcam" || Application.isBatchMode) continue;
                var go = new GameObject("[Test] Death cam");
                DontDestroyOnLoad(go);
                go.AddComponent<DeathCamTestAid>().dir = a[i + 1];
                return;
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float waited = 0f;
            while (waited < 240f && (JUGameManager.PlayerController == null || BRMatchCoordinator.Instance == null || !BRMatchCoordinator.Instance.IsMatchLive))
            { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(5f);
            var me = JUGameManager.PlayerController;
            var cam = Camera.main;
            if (me == null || cam == null) { Debug.LogError("[DeathCam] no player or camera"); yield break; }
            var camRig = FindFirstObjectByType<JUTPS.CameraSystems.JUCameraController>();
            var report = new StringBuilder();

            for (int n = 0; n < 3; n++)
            {
                // the nearest living bot
                JUHealth target = null; float best = float.MaxValue;
                foreach (var b in FindObjectsByType<BRNetBot>(FindObjectsSortMode.None))
                {
                    var h = b.GetComponent<JUHealth>();
                    if (h == null || h.IsDead || !b.gameObject.activeInHierarchy) continue;
                    float d = (b.transform.position - me.transform.position).sqrMagnitude;
                    if (d < best) { best = d; target = h; }
                }
                if (target == null) break;

                if (camRig != null) camRig.enabled = false;
                Transform t = target.transform;
                Vector3 side = Quaternion.Euler(0f, 35f + n * 50f, 0f) * t.forward;
                float startSecs = Time.realtimeSinceStartup;
                Vector3 startPos = t.position;
                var ragdoll = target.GetComponent<AdvancedRagdollController>();

                // the killing hit, from the camera's side, the way the local player's bullet reports it
                for (int f = 0; f < 40; f++)
                {
                    Vector3 focus = (ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position : t.position + Vector3.up);
                    cam.transform.position = focus + side * 4f + Vector3.up * 0.6f;
                    cam.transform.LookAt(focus);
                    if (f == 3)
                    {
                        JUHealth.PendingAttacker = me.gameObject;
                        Vector3 hit = focus + Vector3.up * 0.25f;
                        target.DoDamage(target.Health + 5f, hit);
                        JUHealth.PendingAttacker = null;
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"death{n}_{f:00}.png"));
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                yield return new WaitForSecondsRealtime(2f);

                // six seconds after the kill: still down?
                float hipsUp = ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position.y - t.position.y : -1f;
                var nt = target.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                string line = $"[DeathCam] bot {n} {target.name}: dead={target.IsDead} health={target.Health:0.0} ragdoll={(ragdoll != null ? ragdoll.State.ToString() : "none")} " +
                              $"hipsAboveRoot={hipsUp:0.00} m drift={(t.position - startPos).magnitude:0.00} m networkTransform={(nt != null ? nt.enabled.ToString() : "none")} " +
                              $"after={Time.realtimeSinceStartup - startSecs:0.0} s";
                Debug.Log(line);
                report.AppendLine(line);
                if (camRig != null) camRig.enabled = true;
                yield return new WaitForSecondsRealtime(1f);
            }

            // and the player's own death, as the player sees it (the server applies the damage)
            var myHealth = me.GetComponent<JUHealth>();
            if (myHealth != null && !myHealth.IsDead)
            {
                myHealth.DoDamage(9999f);
                for (int f = 0; f < 48; f++)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"self_{f:00}.png"));
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                var myRagdoll = me.GetComponent<AdvancedRagdollController>();
                string self = $"[DeathCam] self: dead={myHealth.IsDead} ragdoll={(myRagdoll != null ? myRagdoll.State.ToString() : "none")} timeScale={Time.timeScale:0.00}";
                Debug.Log(self);
                report.AppendLine(self);
            }
            File.WriteAllText(Path.Combine(dir, "deaths.txt"), report.ToString());
            Debug.Log("[DeathCam] DONE");
        }
    }
}
