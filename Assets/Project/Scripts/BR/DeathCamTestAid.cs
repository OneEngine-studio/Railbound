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

        private string lastLoot = "";

        // the dropped weapon nearest the corpse: walk to it and take it the way the player does
        private string LootCheck(int n, GameObject corpse, JUCharacterController me, out IEnumerator routine)
        {
            routine = null;
            if (corpse == null || me == null) return $"[DeathCam] bot {n} loot: no corpse or player";
            // the weapon the server dropped for this body (not world loot that happens to lie near it, nor the hidden one in the dead hand)
            JUTPS.WeaponSystem.Weapon best = null; float bd = 4f;
            foreach (var w in FindObjectsByType<JUTPS.WeaponSystem.Weapon>(FindObjectsSortMode.None))
            {
                if (w == null || !w.gameObject.activeInHierarchy || !BRLootSync.IsWeaponDrop(w.gameObject)) continue;
                float d = Vector3.Distance(w.transform.position, corpse.transform.position);
                if (d < bd) { bd = d; best = w; }
            }
            if (best == null) return $"[DeathCam] bot {n} loot: NO dropped weapon within 4 m of the corpse (the server log says whether it carried one)";
            routine = TakeIt(n, best, me);
            return $"[DeathCam] bot {n} loot: {best.ItemName} lies {bd:0.0} m from the corpse";
        }

        private IEnumerator TakeIt(int n, JUTPS.WeaponSystem.Weapon w, JUCharacterController me)
        {
            var inv = me.GetComponent<JUTPS.InventorySystem.JUInventory>();
            string name = w.ItemName;
            // stand so the inventory's pickup sphere (radius 1 at CheckerOffset) is centred on the weapon, as when a player walks onto it
            Vector3 spot = w.transform.position - (inv != null ? me.transform.TransformVector(inv.CheckerOffset) : Vector3.zero);
            spot.y = w.transform.position.y + 0.05f;
            var nt = me.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(spot, me.transform.rotation, me.transform.localScale); else me.transform.position = spot;
            var rb = me.GetComponent<Rigidbody>(); if (rb != null) { rb.position = spot; rb.velocity = Vector3.zero; }
            yield return new WaitForSecondsRealtime(0.8f);
            bool offered = inv != null && inv.ItemToPickUp != null;
            string offeredName = offered ? inv.ItemToPickUp.ItemName + (inv.ItemToPickUp.gameObject == w.gameObject ? " (the drop)" : " (ANOTHER item: " + inv.ItemToPickUp.transform.root.name + ")") : "-";
            bool had = false;
            if (inv != null) foreach (var it in inv.HoldableItensRightHand) if (it != null && it.ItemName == name && it.Unlocked) had = true;
            if (offered) inv.PickUp();
            yield return new WaitForSecondsRealtime(0.5f);
            bool has = false;
            if (inv != null) foreach (var it in inv.HoldableItensRightHand) if (it != null && it.ItemName == name && it.Unlocked) has = true;
            lastLoot = $"[DeathCam] bot {n} loot: offered={offered} ({offeredName}) taken={(has ? (had ? "already had it" : "YES") : "NO")} pickupStillThere={(w != null && w.gameObject.activeInHierarchy)}";
            Debug.Log(lastLoot);
        }

        private static int CountOn(GameObject go)
        {
            int n = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false)) if (r.enabled) n++;
            return n;
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
                me = JUGameManager.PlayerController;   // the player may have been replaced (killed by a bot meanwhile)
                if (me == null) break;
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
                    cam = Camera.main;
                    if (cam == null || t == null) break;
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
                // the corpse, second by second
                var go = target != null ? target.gameObject : null;
                var hipsT = ragdoll != null ? ragdoll.Hips : null;
                for (int sec = 0; sec < 8; sec++)
                {
                    string st = go == null ? "object destroyed" :
                        $"active={go.activeInHierarchy} ragdoll={(ragdoll != null ? ragdoll.State.ToString() : "-")} renderers={CountOn(go)} hips={(hipsT != null ? (hipsT.gameObject.activeInHierarchy ? hipsT.position.ToString("F1") : "inactive") : "destroyed")} root={go.transform.position.ToString("F1")}";
                    Debug.Log($"[DeathCam] bot {n} +{4 + sec} s: {st}");
                    report.AppendLine($"[DeathCam] bot {n} +{4 + sec} s: {st}");
                    yield return new WaitForSecondsRealtime(1f);
                }
                if (target == null || t == null) { report.AppendLine($"[DeathCam] bot {n}: its object is gone 6 s after the kill"); Debug.Log("[DeathCam] bot gone"); continue; }

                // six seconds after the kill: still down?
                float hipsUp = ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position.y - t.position.y : -1f;
                var nt = target.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                string line = $"[DeathCam] bot {n} {target.name}: dead={target.IsDead} health={target.Health:0.0} ragdoll={(ragdoll != null ? ragdoll.State.ToString() : "none")} " +
                              $"hipsAboveRoot={hipsUp:0.00} m drift={(t.position - startPos).magnitude:0.00} m networkTransform={(nt != null ? nt.enabled.ToString() : "none")} " +
                              $"after={Time.realtimeSinceStartup - startSecs:0.0} s";
                Debug.Log(line);
                report.AppendLine(line);
                foreach (var w in FindObjectsByType<JUTPS.WeaponSystem.Weapon>(FindObjectsSortMode.None))
                {
                    if (w == null || go == null || Vector3.Distance(w.transform.position, go.transform.position) > 6f) continue;
                    int shown = 0; foreach (var r in w.GetComponentsInChildren<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) shown++;
                    float above = w.transform.position.y - WeaponDrop.OnGround(w.transform.position).y;
                    string what = BRLootSync.IsWeaponDrop(w.gameObject) ? "the drop" : w.GetComponentInParent<JUTPS.InventorySystem.JUInventory>() != null ? "in " + w.GetComponentInParent<JUTPS.InventorySystem.JUInventory>().name + "'s hands" : "world loot";
                    string wl = $"[DeathCam] bot {n} near: {w.ItemName} ({what}) {Vector3.Distance(w.transform.position, go.transform.position):0.0} m away, {above:0.00} m above the ground, renderers shown {shown}, active {w.gameObject.activeInHierarchy}";
                    Debug.Log(wl); report.AppendLine(wl);
                }
                // the corpse from above (how it lies), then: can its weapon be taken?
                if (go != null && cam != null && hipsT != null)
                {
                    // set every frame: the player camera's late update moves it back otherwise
                    for (int f = 0; f < 40; f++)
                    {
                        cam = Camera.main;
                        if (cam == null || hipsT == null) break;
                        cam.transform.position = hipsT.position + Vector3.up * 2.6f + side * 1.4f;
                        cam.transform.LookAt(hipsT.position);
                        if (f == 20) ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"corpse{n}_above.png"));
                        yield return null;
                    }
                }
                if (camRig != null) camRig.enabled = true;
                camRig = FindFirstObjectByType<JUTPS.CameraSystems.JUCameraController>();
                report.AppendLine(LootCheck(n, go, me, out var lootRoutine));
                if (lootRoutine != null) yield return lootRoutine;
                report.AppendLine(lastLoot);
                yield return new WaitForSecondsRealtime(1f);
            }

            // and the player's own death, as the player sees it (the server applies the damage)
            me = JUGameManager.PlayerController;
            var myHealth = me != null ? me.GetComponent<JUHealth>() : null;
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
