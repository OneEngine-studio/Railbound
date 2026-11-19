using System.Collections;
using System.Collections.Generic;
using System.IO;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.WeaponSystem;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Test aid, only with <c>-aimtest DIR</c>: the local player raises its sights and stands, walks and runs, once with JU's old
    /// sight camera (steady aim off) and once with the steady aim, and logs how much the view shakes in each phase:
    /// the high-frequency movement of the camera (what makes the picture shake), its biggest jump between two frames, and the
    /// rotation jitter. Screenshots of the walking phase go to DIR.
    /// </summary>
    [DefaultExecutionOrder(32000)]   // measure after every camera has been placed
    public class AimStabilityTest : MonoBehaviour
    {
        private string dir;
        private JUCharacterController pc;
        private TPSCameraController tps;
        private float move, run;
        private bool driving, measuring;
        private readonly List<float> lateral = new List<float>(), vertical = new List<float>(), jumps = new List<float>(), turns = new List<float>();
        private Vector3 smoothed, last;
        private Quaternion lastRot;
        private bool hasLast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-aimtest" || FindFirstObjectByType<AimStabilityTest>() != null) continue;
                var go = new GameObject("[Test] Aim Stability");
                DontDestroyOnLoad(go);
                go.AddComponent<AimStabilityTest>().dir = a[i + 1];
                return;
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            while (JUGameManager.PlayerController == null || FindFirstObjectByType<TPSCameraController>() == null) yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(8f);
            pc = JUGameManager.PlayerController;
            tps = FindFirstObjectByType<TPSCameraController>();

            // a weapon with sights in the hand
            var inv = pc.Inventory;
            Weapon gun = null;
            if (inv != null && inv.HoldableItensRightHand != null)
                foreach (var item in inv.HoldableItensRightHand)
                    if (item is Weapon w && w.AimMode != Weapon.WeaponAimMode.None) { gun = w; break; }
            if (gun == null) { Debug.LogError("[AimTest] no weapon with sights"); yield break; }
            gun.Unlocked = true; if (gun.ItemQuantity <= 0) gun.ItemQuantity = 1;
            if (pc.WeaponInUseRightHand != gun) pc.SwitchToItem(gun.ItemSwitchID);
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[AimTest] weapon {(pc.WeaponInUseRightHand != null ? pc.WeaponInUseRightHand.ItemName : "none")} aim mode {gun.AimMode}");

            pc.UseDefaultControllerInput = false;
            driving = true;
            foreach (bool steady in new[] { false, true })
            {
                tps.SteadyAim = steady;
                tps.NoMotionBlurInSights = steady;
                string tag = steady ? "STEADY" : "OLD";
                yield return Phase(tag, "stand", 0f, 0f, 2.5f, null);
                yield return Phase(tag, "walk", 1f, 0f, 5f, tag.ToLower() + "_walk");
                yield return Phase(tag, "run", 1f, 1f, 4f, tag.ToLower() + "_run");
                move = 0f; run = 0f;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            driving = false;
            pc.UseDefaultControllerInput = true;
            Debug.Log("[AimTest] DONE");
        }

        private IEnumerator Phase(string tag, string name, float forward, float running, float seconds, string shot)
        {
            move = forward; run = running;
            yield return new WaitForSecondsRealtime(1.2f);   // into the gait, sights settled
            lateral.Clear(); vertical.Clear(); jumps.Clear(); turns.Clear();
            hasLast = false;
            measuring = true;
            float t = 0f; int shotNo = 0;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                if (shot != null && shotNo < 3 && t > 1f + shotNo * 0.37f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shot}_{shotNo}.png"));
                    shotNo++;
                }
                yield return null;
            }
            measuring = false;
            Debug.Log($"[AimTest] {tag,-6} {name,-5} sights={tps.ScopeActive} frames={jumps.Count} shake lateral {Rms(lateral):F2} cm, vertical {Rms(vertical):F2} cm, " +
                      $"biggest jump {Max(jumps):F2} cm, rotation jitter {Rms(turns):F3} deg, speed {pc.GetComponent<Rigidbody>().velocity.magnitude:F1} m/s");
        }

        private void Update()
        {
            if (!driving || pc == null) return;
            pc.IsAiming = true;
            pc.FiringMode = true;
            pc.FiringModeIK = true;
            pc.CurrentTimeToDisableFireMode = 0f;
            pc.IsRunning = run > 0.5f;
            pc.SetMoveInput(0f, move);
            if (tps != null && tps.mCamera != null) pc.LookAtPosition = tps.mCamera.transform.position + tps.mCamera.transform.forward * 50f;
        }

        private void LateUpdate()
        {
            if (!measuring || tps == null || tps.mCamera == null) return;
            var cam = tps.mCamera.transform;
            Vector3 p = cam.position;
            if (!hasLast) { smoothed = last = p; lastRot = cam.rotation; hasLast = true; return; }
            // high-pass: the camera's movement around a 0.25 s average of itself (the walk itself is slow, the shake is fast)
            smoothed = Vector3.Lerp(smoothed, p, 1f - Mathf.Exp(-Time.deltaTime / 0.25f));
            Vector3 d = p - smoothed;
            lateral.Add(Vector3.Dot(d, cam.right) * 100f);
            vertical.Add(Vector3.Dot(d, cam.up) * 100f);
            // the step between frames, minus the steady movement of walking (forward speed)
            Vector3 step = p - last;
            Vector3 expected = cam.forward * Vector3.Dot(step, cam.forward);
            jumps.Add((step - expected).magnitude * 100f);
            turns.Add(Quaternion.Angle(lastRot, cam.rotation));
            last = p; lastRot = cam.rotation;
        }

        private static float Rms(List<float> v)
        {
            if (v.Count == 0) return 0f;
            double s = 0; foreach (var x in v) s += x * x;
            return (float)System.Math.Sqrt(s / v.Count);
        }

        private static float Max(List<float> v) { float m = 0f; foreach (var x in v) m = Mathf.Max(m, x); return m; }
    }
}
