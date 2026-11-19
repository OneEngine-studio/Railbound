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
    /// sight camera and weapon (OLD), with the steady sight camera only (CAMERA, the first fix) and with the steady weapon too
    /// (WEAPON), and logs how much the view shakes in each phase (the high-frequency movement of the camera, its jumps between
    /// frames, the rotation jitter) and how much the weapon swings around on the screen, in pixels. The same is measured with the
    /// weapon up from the hip (third person fire mode). Screenshots go to DIR.
    /// </summary>
    [DefaultExecutionOrder(32000)]   // measure after every camera has been placed
    public class AimStabilityTest : MonoBehaviour
    {
        private string dir;
        private JUCharacterController pc;
        private TPSCameraController tps;
        private float move, run;
        private bool driving, measuring;
        private readonly List<float> lateral = new List<float>(), vertical = new List<float>(), jumps = new List<float>(), turns = new List<float>(), bodyVertical = new List<float>(), gunVertical = new List<float>(), gunScreen = new List<float>(), gunScreenJumps = new List<float>();
        private float bodySmoothY, gunSmoothY;
        private Vector2 gunScreenSmooth, gunScreenLast;
        private bool aimDown = true;
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
            // the loadout is applied at spawn and can switch weapons too: keep asking until the weapon is really in the hand
            for (int i = 0; i < 30 && !(pc.WeaponInUseRightHand != null && pc.IsItemEquiped); i++)
            {
                pc.SwitchToItem(gun.ItemSwitchID);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[AimTest] weapon {(pc.WeaponInUseRightHand != null ? pc.WeaponInUseRightHand.ItemName : "none")} aim mode {gun.AimMode}");

            pc.UseDefaultControllerInput = false;
            driving = true;
            float bodySmoothing = tps.AimBodyHeightSmoothing, bobKeep = tps.AimBobKeep;
            Vector3 startPos = pc.transform.position; Quaternion startRot = pc.transform.rotation;
            foreach (int mode in new[] { 0, 1, 2 })
            {
                // every mode walks and runs the same path: back to the same start, facing the same way
                move = 0f; run = 0f;
                var nt = pc.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(startPos, startRot, pc.transform.localScale);
                else pc.transform.SetPositionAndRotation(startPos, startRot);
                var body = pc.GetComponent<Rigidbody>();
                if (body != null) { body.position = startPos; body.velocity = Vector3.zero; }
                yield return new WaitForSecondsRealtime(1.5f);
                // OLD: JU's camera and weapon. CAMERA: the first fix (steady sight camera, the weapon still on the spine).
                // WEAPON: the steady weapon, with the sight camera following it closely.
                bool steady = mode > 0;
                tps.SteadyAim = steady;
                tps.NoMotionBlurInSights = steady;
                tps.AimBodyHeightSmoothing = steady ? bodySmoothing : 0f;
                tps.AimBobKeep = mode == 1 ? 0.12f : bobKeep;
                pc.SteadyWeapon = mode == 2;
                string tag = mode == 0 ? "OLD" : mode == 1 ? "CAMERA" : "WEAPON";
                aimDown = true;
                yield return Phase(tag, "stand", 0f, 0f, 2.5f, null);
                yield return Phase(tag, "walk", 1f, 0f, 5f, tag.ToLower() + "_walk");
                yield return Phase(tag, "run", 1f, 1f, 4f, tag.ToLower() + "_run");
                aimDown = false;   // the weapon up from the hip (third person fire mode)
                yield return Phase(tag, "hipwalk", 1f, 0f, 4f, tag.ToLower() + "_hip");
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
            for (int i = 0; i < 20 && aimDown && !tps.ScopeActive; i++)
            {
                if (pc.WeaponInUseRightHand == null || !pc.IsItemEquiped)
                    foreach (var item in pc.Inventory.HoldableItensRightHand) if (item is Weapon w && w.AimMode != Weapon.WeaponAimMode.None) { pc.SwitchToItem(w.ItemSwitchID); break; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            lateral.Clear(); vertical.Clear(); jumps.Clear(); turns.Clear(); bodyVertical.Clear(); gunVertical.Clear(); gunScreen.Clear(); gunScreenJumps.Clear();
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
            Debug.Log($"[AimTest] {tag,-6} {name,-7} sights={tps.ScopeActive} frames={jumps.Count} WEAPON ON SCREEN sway {Rms(gunScreen):F1} px, jump p95 {P95(gunScreenJumps):F1} px, biggest {Max(gunScreenJumps):F1} px | " +
                      $"view shake lateral {Rms(lateral):F2} cm, vertical {Rms(vertical):F2} cm, " +
                      $"jump p95 {P95(jumps):F2} cm, biggest {Max(jumps):F2} cm, rotation jitter {Rms(turns):F3} deg | body vertical {Rms(bodyVertical):F2} cm, weapon vs body vertical {Rms(gunVertical):F2} cm, speed {pc.GetComponent<Rigidbody>().velocity.magnitude:F1} m/s");
        }

        private void Update()
        {
            if (!driving || pc == null) return;
            pc.IsAiming = aimDown;
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
            float by = pc.transform.position.y;
            float gy = pc.WeaponInUseRightHand != null ? pc.WeaponInUseRightHand.transform.position.y - by : 0f;
            // the weapon on the screen (pixels of a 1080 p picture): where it is drawn, around a 0.25 s average of itself
            Vector2 gs = Vector2.zero;
            if (pc.WeaponInUseRightHand != null)
            {
                Vector3 v = tps.mCamera.WorldToViewportPoint(pc.WeaponInUseRightHand.transform.position);
                gs = new Vector2(v.x * 1080f * tps.mCamera.aspect, v.y * 1080f);
            }
            if (!hasLast) { smoothed = last = p; lastRot = cam.rotation; hasLast = true; bodySmoothY = by; gunSmoothY = gy; gunScreenSmooth = gunScreenLast = gs; return; }
            gunScreenSmooth = Vector2.Lerp(gunScreenSmooth, gs, 1f - Mathf.Exp(-Time.deltaTime / 0.25f));
            gunScreen.Add((gs - gunScreenSmooth).magnitude);
            gunScreenJumps.Add((gs - gunScreenLast).magnitude);
            gunScreenLast = gs;
            float kk = 1f - Mathf.Exp(-Time.deltaTime / 0.25f);
            bodySmoothY = Mathf.Lerp(bodySmoothY, by, kk); gunSmoothY = Mathf.Lerp(gunSmoothY, gy, kk);
            bodyVertical.Add((by - bodySmoothY) * 100f); gunVertical.Add((gy - gunSmoothY) * 100f);
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

        private static float P95(List<float> v)
        {
            if (v.Count == 0) return 0f;
            var c = new List<float>(v); c.Sort();
            return c[Mathf.Clamp((int)(c.Count * 0.95f), 0, c.Count - 1)];
        }

        private static float Max(List<float> v) { float m = 0f; foreach (var x in v) m = Mathf.Max(m, x); return m; }
    }
}
