using System.Text;
using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.PhysicsScripts;

namespace Railbound.BR
{
    /// <summary>
    /// Test aid, only active with the command-line flag <c>-netdebug</c>: every few seconds each peer logs the state of the
    /// replicated match objects (bots, loot, alive count), so the server's log and a client's log can be compared line by line.
    /// </summary>
    public class BRNetDebug : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (!string.Equals(a, "-netdebug", System.StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("[BRNetDebug]");
                DontDestroyOnLoad(go);
                go.AddComponent<BRNetDebug>();
                return;
            }
        }

        private float nextFxShot;
        private int fxShotNo;
        private void OnEnable() { BRMatchCoordinator.KillOccurred += LogKill; RemoteWeaponFx.ShotShownEvent += FxShot; }
        private void OnDisable() { BRMatchCoordinator.KillOccurred -= LogKill; RemoteWeaponFx.ShotShownEvent -= FxShot; }

        // -netshotfx: a screenshot right after a remote player's shot was shown on this peer (to see the muzzle flash and the pose)
        private void FxShot(JUTPS.WeaponSystem.Weapon w)
        {
            string dir = ArgValue("-netshot");
            if (!HasArg("-netshotfx") || string.IsNullOrEmpty(dir) || Time.unscaledTime < nextFxShot) return;
            nextFxShot = Time.unscaledTime + 4f;
            try { System.IO.Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "fx_" + (fxShotNo++) + ".png")); } catch { }
        }

        private static string ItemStates(JUTPS.InventorySystem.JUInventory inv)
        {
            if (inv == null) return "noinv";
            var sb = new StringBuilder();
            sb.Append(" R=").Append(inv.HoldableItemInUseInRightHand != null ? inv.HoldableItemInUseInRightHand.ItemName + "/" + inv.HoldableItemInUseInRightHand.gameObject.activeInHierarchy : "none");
            sb.Append(" L=").Append(inv.HoldableItemInUseInLeftHand != null ? inv.HoldableItemInUseInLeftHand.ItemName + "/" + inv.HoldableItemInUseInLeftHand.gameObject.activeInHierarchy : "none");
            sb.Append(" curR=").Append(inv.CurrentRightHandItemID).Append(" curL=").Append(inv.CurrentLeftHandItemID).Append(" activeRight:");
            if (inv.HoldableItensRightHand != null) foreach (var it in inv.HoldableItensRightHand) if (it != null && it.gameObject.activeInHierarchy) sb.Append(it.ItemName).Append('#').Append(it.ItemSwitchID).Append(',');
            sb.Append(" activeLeft:");
            if (inv.HoldableItensLeftHand != null) foreach (var it in inv.HoldableItensLeftHand) if (it != null && it.gameObject.activeInHierarchy) sb.Append(it.ItemName).Append('#').Append(it.ItemSwitchID).Append(',');
            return sb.ToString();
        }

        private static string LayerWeights(Animator a)
        {
            if (a == null) return "?";
            var sb = new StringBuilder();
            for (int i = 0; i < a.layerCount; i++) sb.Append(a.GetLayerWeight(i).ToString("F1")).Append(',');
            return sb.ToString();
        }

        private static string PuppetInfo(JUNetworkPlayer jp)
        {
            var c = jp.GetComponent<JUCharacterController>();
            if (c == null) return "";
            var h = c.GetComponent<JUTPS.InventorySystem.JUInventory>();
            return " puppet=" + c.IsRemotePuppet + " ctrl=" + c.enabled + " fire=" + c.FiringMode + " ikR=" + c.RightHandWeightIK.ToString("F2") + " ikL=" + c.LeftHandWeightIK.ToString("F2")
                + " item=" + (h != null ? h.CurrentRightHandItemID : -9) + " armed=" + c.IsItemEquiped + " look=" + c.LookAtPosition.ToString("F0")
                + " ikCalls=" + c.DebugIkCalls + " animEnabled=" + (c.GetComponent<Animator>() != null && c.GetComponent<Animator>().enabled) + " appliedR=" + (c.GetComponent<Animator>() != null ? c.GetComponent<Animator>().GetIKPositionWeight(AvatarIKGoal.RightHand).ToString("F2") : "?")
                + " layers=" + LayerWeights(c.GetComponent<Animator>()) + " " + c.PuppetDebug() + ItemStates(c.GetComponent<JUTPS.InventorySystem.JUInventory>());
        }

        // height of a transform above the ground (terrain or any collider) in metres
        private static float HeightAboveGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore)) return hit.distance - 1.0f;
            var t = Terrain.activeTerrain;
            return t != null ? p.y - t.SampleHeight(p) - t.transform.position.y : float.NaN;
        }
        private static void LogKill(BRKillInfo k)
        {
            var nm = NetworkManager.Singleton;
            string role = nm == null || !nm.IsListening ? "offline" : nm.IsServer ? "server" : "client";
            Debug.Log("[BRNetDebug] KILL(" + role + ") " + k.Killer + " -> " + k.Victim + " place=" + k.Placement + " alive=" + k.AliveAfter + (k.VictimIsLocal ? " [victim is me]" : "") + (k.KillerIsLocal ? " [killer is me]" : ""));
        }

        private float next;
        private int frames;
        private float lastLog;
        private bool experimentApplied;

        private static bool HasArg(string name)
        {
            foreach (string a in System.Environment.GetCommandLineArgs()) if (string.Equals(a, name, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgValue(string name)
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], name, System.StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        private float nextShot, nextFollow, nextKill, nextFire, nextWatch, nextJump;
        private float maxRemoteUp = -999f;
        private float lastGoodY = float.NaN, nextTrail;
        private bool fallLogged;
        private readonly System.Collections.Generic.Queue<string> trail = new System.Collections.Generic.Queue<string>();
        private int shotNo;

        // -netshot <dir>: save a screenshot every 10 s.  -netfollow: put the local player next to the nearest bot that holds a gun,
        // so a client's own view can be inspected (weapons, muzzle flashes, ragdolls of bots it did not simulate itself)
        // -nethudshot: a screenshot 0.35 s after each match banner or kill popup appears (into -netshot), to check the HUD visually
        private int killHits, seenBanners, seenPopups, hudShotNo;
        private float hudShotAt = -1f, nextHudShot;
        private void HudShots()
        {
            if (!HasArg("-nethudshot")) return;
            int b = Railbound.UI.MatchEventHUD.BannersShown, k = Railbound.UI.MatchEventHUD.KillPopupsShown;
            if ((b != seenBanners || k != seenPopups) && Time.unscaledTime >= nextHudShot) { hudShotAt = Time.unscaledTime + 0.35f; nextHudShot = Time.unscaledTime + 2.5f; }
            seenBanners = b; seenPopups = k;
            if (hudShotAt > 0f && Time.unscaledTime >= hudShotAt)
            {
                hudShotAt = -1f;
                string dir = ArgValue("-netshot");
                if (!string.IsNullOrEmpty(dir)) { try { System.IO.Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "hud_" + (hudShotNo++) + ".png")); } catch { } }
            }
        }

        // -netff: shoots the nearest squadmate twice (tests that friendly fire is blocked): once normally (the client's own filter must drop
        // it), once with the client's filter switched off like a modified client would do (the server must drop it)
        private float nextFf = -1f; private int ffStep;
        private void FriendlyFireTest()
        {
            if (!HasArg("-netff") || ffStep >= 2 || Time.realtimeSinceStartup < 45f || Time.unscaledTime < nextFf) return;
            var nm = NetworkManager.Singleton;
            var me = Railbound.BR.BRTeams.LocalPlayer;
            if (nm == null || !nm.IsClient || me == null || me.TeamId < 0) return;
            nextFf = Time.unscaledTime + 6f;
            JUNetworkPlayer mate = null;
            foreach (var np in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None)) if (np != me && np.TeamId == me.TeamId) { mate = np; break; }
            if (mate == null) { Debug.Log("[BRNetDebug] netff: no squadmate found (my squad " + me.TeamId + ")"); return; }
            var h = mate.GetComponent<JUHealth>();
            if (h == null) return;
            float before = h.Health;
            var savedFilter = JUHealth.DamageFilter;
            if (ffStep == 1) JUHealth.DamageFilter = null;   // the "modified client": only the server can stop it now
            JUHealth.PendingAttacker = me.gameObject;
            h.DoDamage(30f);
            JUHealth.PendingAttacker = null;
            JUHealth.DamageFilter = savedFilter;
            Debug.Log("[BRNetDebug] netff " + (ffStep == 0 ? "with the client filter" : "WITHOUT the client filter (server must block)") + ": squadmate " + mate.OwnerClientId + " health " + before + " -> " + h.Health
                + "  (distance " + Vector3.Distance(me.transform.position, mate.transform.position).ToString("0.0") + " m, squad " + me.TeamId + ")");
            ffStep++;
        }

        private void TestAids()
        {
            HudShots();
            FriendlyFireTest();
            string dir = ArgValue("-netshot");
            if (!string.IsNullOrEmpty(dir) && Time.unscaledTime >= nextShot && Time.realtimeSinceStartup > 12f)
            {
                nextShot = Time.unscaledTime + 10f;
                try { System.IO.Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shot_" + (shotNo++) + ".png")); }
                catch (System.Exception e) { Debug.Log("[BRNetDebug] screenshot failed: " + e.Message); }
            }

            var pc = JUGameManager.PlayerController;
            // -netkill: the local player "shoots" the nearest bot (damage with the player as the attacker, like a real bullet does),
            // to test that the server credits the kill to this player
            if (HasArg("-netkill") && pc != null && Time.unscaledTime >= nextKill && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && Time.realtimeSinceStartup > 30f)
            {
                nextKill = Time.unscaledTime + 1.5f;
                JUHealth bestH = null; float bestKD = float.MaxValue;
                foreach (var b in FindObjectsByType<BRNetBot>(FindObjectsSortMode.None))
                {
                    var h = b.GetComponent<JUHealth>();
                    if (h == null || h.IsDead) continue;
                    float d = (b.transform.position - pc.transform.position).sqrMagnitude;
                    if (d < bestKD) { bestKD = d; bestH = h; }
                }
                if (bestH != null)
                {
                    JUHealth.PendingAttacker = pc.gameObject;
                    // every other hit goes through the head body part (x5 damage), so the "HEADSHOT KILL" popup gets tested too
                    JUTPS.ArmorSystem.DamageableBodyPart head = null;
                    if ((killHits++ & 1) == 1)
                        foreach (var part in bestH.GetComponentsInChildren<JUTPS.ArmorSystem.DamageableBodyPart>()) if (part.IsHead) { head = part; break; }
                    if ((killHits & 1) == 0) Debug.Log("[BRNetDebug] netkill head hit on " + bestH.name + ": head part " + (head != null ? "found" : "NOT found") + " (" + bestH.GetComponentsInChildren<JUTPS.ArmorSystem.DamageableBodyPart>().Length + " parts)");
                    if (head != null) head.DoDamage(9f); else bestH.DoDamage(45f);
                    JUHealth.PendingAttacker = null;
                }
            }

            // -netfire: the local player fires its weapon (and reloads when empty), to test what other players see and hear of it
            if (HasArg("-netfire") && pc != null && Time.unscaledTime >= nextFire && Time.realtimeSinceStartup > 20f)
            {
                nextFire = Time.unscaledTime + 0.7f;
                var w = pc.WeaponInUseRightHand;
                if (((int)Time.realtimeSinceStartup) % 10 == 0) Debug.Log("[BRNetDebug] netfire: weapon=" + (w != null ? w.ItemName + " bullets=" + w.BulletsAmounts + "/" + w.TotalBullets + " canUse=" + w.CanUseItem : "none") + " dead=" + (pc.GetComponent<JUHealth>() != null && pc.GetComponent<JUHealth>().IsDead));
                if (w != null)
                {
                    if (w.BulletsAmounts <= 0) w.Reload();
                    else
                    {
                        // a real shot also puts the character into firing mode (weapon raised, spine turned to the target)
                        pc.FiringMode = true; pc.FiringModeIK = true; pc.CurrentTimeToDisableFireMode = 0;
                        w.SetWeaponOrientation(pc.transform.position + Vector3.up * 1.5f, pc.transform.forward); w.UseItem();
                    }
                }
            }
            // fall watchdog: the local player went far below the ground. Log the whole physical state once, with the last positions.
            if (pc != null)
            {
                var prb = pc.GetComponent<Rigidbody>();
                var pcol = pc.GetComponent<Collider>();
                if (Time.unscaledTime >= nextTrail)
                {
                    nextTrail = Time.unscaledTime + 0.5f;
                    trail.Enqueue("t=" + Time.realtimeSinceStartup.ToString("F1") + " y=" + pc.transform.position.y.ToString("F1") + " v=" + (prb != null ? prb.velocity.ToString("F1") : "-") + " grounded=" + pc.IsGrounded + " jumping=" + pc.IsJumping);
                    while (trail.Count > 12) trail.Dequeue();
                }
                float gy = HeightAboveGround(pc.transform.position);
                if (!fallLogged && (gy < -15f || pc.transform.position.y < -60f))
                {
                    fallLogged = true;
                    Debug.Log("[BRNetDebug] FALL DETECTED y=" + pc.transform.position.y.ToString("F1") + " pos=" + pc.transform.position.ToString("F1") + " rb(kinematic=" + (prb != null && prb.isKinematic) + " gravity=" + (prb != null && prb.useGravity) + " detect=" + (prb != null ? prb.collisionDetectionMode.ToString() : "-") + " constraints=" + (prb != null ? prb.constraints.ToString() : "-") + ")"
                        + " collider(enabled=" + (pcol != null && pcol.enabled) + " trigger=" + (pcol != null && pcol.isTrigger) + " type=" + (pcol != null ? pcol.GetType().Name : "-") + ") ctrl(enabled=" + pc.enabled + " puppet=" + pc.IsRemotePuppet + " dead=" + pc.IsDead + " ragdolled=" + pc.IsRagdolled + " rolling=" + pc.IsRolling + " driving=" + pc.IsDriving + ")"
                        + " terrain=" + (Terrain.activeTerrain != null ? Terrain.activeTerrain.name + " enabledCollider=" + (Terrain.activeTerrain.GetComponent<TerrainCollider>() != null && Terrain.activeTerrain.GetComponent<TerrainCollider>().enabled) : "none")
                        + " TRAIL: " + string.Join(" | ", trail));
                }
            }
            // -netjump: the local player jumps every 3 s (what do the other players see of a jump?)
            if (HasArg("-netjump") && pc != null && Time.unscaledTime >= nextJump && Time.realtimeSinceStartup > 20f) { nextJump = Time.unscaledTime + 3f; pc._Jump(); }
            // highest any OTHER player has been above the ground on this peer
            foreach (var jp in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
                if (!jp.IsOwner) { float u = HeightAboveGround(jp.transform.position); if (u > maxRemoteUp) maxRemoteUp = u; }
            // -netwatch: put the local player a few metres from ANOTHER human player, looking at it
            if (HasArg("-netwatch") && pc != null && Time.unscaledTime >= nextWatch && Time.realtimeSinceStartup > 25f)
            {
                nextWatch = Time.unscaledTime + 3f;
                foreach (var jp in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
                {
                    if (jp.IsOwner) continue;
                    Vector3 spot = jp.transform.position + jp.transform.forward * 5f + jp.transform.right * 1.5f;
                    var rb = pc.GetComponent<Rigidbody>();
                    pc.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(jp.transform.position - spot));
                    if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    break;
                }
            }
            if (HasArg("-netfollow") && pc != null && Time.unscaledTime >= nextFollow && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                nextFollow = Time.unscaledTime + 4f;
                BRNetBot best = null; float bestD = float.MaxValue;
                foreach (var b in FindObjectsByType<BRNetBot>(FindObjectsSortMode.None))
                {
                    var h = b.GetComponent<JUHealth>();
                    var ctrl = b.GetComponent<JUCharacterController>();
                    if (h == null || h.IsDead || ctrl == null || ctrl.WeaponInUseRightHand == null) continue;
                    float d = (b.transform.position - pc.transform.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = b; }
                }
                if (best != null && bestD > 14f * 14f)
                {
                    Vector3 target = best.transform.position - best.transform.forward * 8f;
                    var rb = pc.GetComponent<Rigidbody>();
                    pc.transform.SetPositionAndRotation(target + Vector3.up * 0.5f, Quaternion.LookRotation(best.transform.position - target));
                    if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    Debug.Log("[BRNetDebug] moved the local player next to " + best.name);
                }
            }
        }

        // CPU experiments for the dedicated server: switch parts of the bots off and compare the process CPU
        private void ApplyExperiment()
        {
            experimentApplied = true;
            var bots = FindObjectsByType<BRNetBot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (HasArg("-xnobots")) foreach (var b in bots) b.gameObject.SetActive(false);
            if (HasArg("-xnoanim")) foreach (var b in bots) { var a = b.GetComponent<Animator>(); if (a != null) a.enabled = false; }
            if (HasArg("-xnoctrl")) foreach (var b in bots) { var c = b.GetComponent<JUCharacterController>(); if (c != null) c.enabled = false; }
            if (HasArg("-xnophys")) foreach (var b in bots) { var r = b.GetComponent<Rigidbody>(); if (r != null) r.isKinematic = true; }
            Debug.Log("[BRNetDebug] experiment applied to " + bots.Length + " bots");
        }

        // ---- bot motion: how evenly the living bots move on THIS peer. Stutter shows up as speed changes from frame to frame
        // (rough = sum of |velocity change| * dt per metre travelled) and as frames in which a running bot did not move at all.
        private sealed class MotionTrack
        {
            public JUHealth Health; public Vector3 P, V; public bool HasP, HasV;
            // animation check: while the root moves, do the gameplay rig's leg and the visible skin's leg move too?
            public Animator Anim; public Transform RigLeg, SkinLeg; public AdvancedRagdollController Ragdoll;
            public Quaternion RigLegPrev, SkinLegPrev; public bool HasLegs;
            public int MovingFrames, AnimOff, RagdollOn, SpeedZero; public float RigLegDeg, SkinLegDeg;
        }

        private static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
            return null;
        }

        private static void TrackAnimation(BRNetBot b, MotionTrack s, float rootSpeed)
        {
            if (s.Anim == null)
            {
                s.Anim = b.GetComponent<Animator>();
                s.Ragdoll = b.GetComponent<AdvancedRagdollController>();
                if (s.Anim != null && s.Anim.isHuman) s.RigLeg = s.Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            }
            if (s.SkinLeg == null)
            {
                var skin = b.transform.Find(Project.Character.CharacterSkinApplier.SkinChildName);
                if (skin != null) s.SkinLeg = FindDeep(skin, "mixamorig:LeftUpLeg");
                s.HasLegs = false;
            }
            if (s.RigLeg == null) return;
            if (rootSpeed > 1f)
            {
                s.MovingFrames++;
                if (s.Anim != null && !s.Anim.enabled) s.AnimOff++;
                if (s.Ragdoll != null && s.Ragdoll.State != AdvancedRagdollController.RagdollState.Animated) s.RagdollOn++;
                if (s.Anim != null && s.Anim.enabled && s.Anim.GetFloat("Speed") < 0.05f) s.SpeedZero++;
                if (s.HasLegs)
                {
                    s.RigLegDeg += Quaternion.Angle(s.RigLegPrev, s.RigLeg.localRotation);
                    if (s.SkinLeg != null) s.SkinLegDeg += Quaternion.Angle(s.SkinLegPrev, s.SkinLeg.localRotation);
                }
            }
            s.RigLegPrev = s.RigLeg.localRotation;
            if (s.SkinLeg != null) s.SkinLegPrev = s.SkinLeg.localRotation;
            s.HasLegs = true;
        }

        // bots that moved in the last report window although their legs (rig or skin) hardly did: "sliding statues"
        private string AnimationReport()
        {
            int checkedBots = 0, sliding = 0;
            var list = new StringBuilder();
            foreach (var kv in motion)
            {
                var s = kv.Value;
                if (s.MovingFrames < 20) { Reset(s); continue; }
                checkedBots++;
                float rigPerFrame = s.RigLegDeg / s.MovingFrames, skinPerFrame = s.SkinLegDeg / s.MovingFrames;
                bool bad = rigPerFrame < 0.3f || (s.SkinLeg != null && skinPerFrame < 0.3f) || s.AnimOff > 0 || s.RagdollOn > 0;
                if (bad)
                {
                    sliding++;
                    if (list.Length < 600)
                        list.Append(' ').Append(kv.Key != null ? kv.Key.name : "?").Append("(frames=").Append(s.MovingFrames)
                            .Append(" rigLeg=").Append(rigPerFrame.ToString("F2")).Append(" skinLeg=").Append(s.SkinLeg != null ? skinPerFrame.ToString("F2") : "noskin")
                            .Append(" animOff=").Append(s.AnimOff).Append(" ragdoll=").Append(s.RagdollOn).Append(" speed0=").Append(s.SpeedZero)
                            .Append(" state=").Append(s.Ragdoll != null ? s.Ragdoll.State.ToString() : "-").Append(')');
                }
                Reset(s);
            }
            return " botAnim(checked=" + checkedBots + " sliding=" + sliding + list + ")";
        }

        private static void Reset(MotionTrack s) { s.MovingFrames = s.AnimOff = s.RagdollOn = s.SpeedZero = 0; s.RigLegDeg = s.SkinLegDeg = 0f; }
        private readonly System.Collections.Generic.Dictionary<BRNetBot, MotionTrack> motion = new System.Collections.Generic.Dictionary<BRNetBot, MotionTrack>();
        private BRNetBot[] motionBots;
        private float motionRefresh, motionDist, motionJerk, motionMaxStep;
        private int motionStalls, motionMovingFrames;

        private void TrackBotMotion()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (motionBots == null || Time.unscaledTime > motionRefresh)
            {
                motionBots = FindObjectsByType<BRNetBot>(FindObjectsSortMode.None);
                motionRefresh = Time.unscaledTime + 2f;
            }
            foreach (var b in motionBots)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                if (!motion.TryGetValue(b, out var s)) { s = new MotionTrack { Health = b.GetComponent<JUHealth>() }; motion[b] = s; }
                Vector3 p = b.transform.position;
                if (s.Health != null && s.Health.IsDead) { s.HasP = false; s.HasV = false; continue; }
                if (s.HasP)
                {
                    Vector3 step = p - s.P;
                    float d = step.magnitude;
                    Vector3 v = step / dt;
                    bool wasRunning = s.HasV && s.V.magnitude > 1f;
                    if (d > 0.0005f || wasRunning)
                    {
                        motionMovingFrames++;
                        motionDist += d;
                        if (s.HasV) motionJerk += (v - s.V).magnitude * dt;
                        if (d <= 0.0005f && wasRunning) motionStalls++;
                        if (d > motionMaxStep) motionMaxStep = d;
                    }
                    TrackAnimation(b, s, v.magnitude);
                    s.V = v;
                    s.HasV = true;
                }
                s.P = p;
                s.HasP = true;
            }
        }

        // every visible character mesh whose lowest point is more than 5 m above the ground below it (any model, alive or dead), with its path
        private static string FloatingModels()
        {
            var found = new StringBuilder();
            int n = 0;
            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                var b = smr.bounds;
                if (b.size.y < 0.3f) continue;
                // from the middle of the model (a ray from under the feet passed through whatever the character stands on)
                RaycastHit hit = default; bool any = false;
                foreach (var h0 in Physics.RaycastAll(b.center, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h0.collider.transform.root == smr.transform.root) continue;   // its own capsule and ragdoll
                    if (!any || h0.distance < hit.distance) { hit = h0; any = true; }
                }
                if (!any) continue;
                float gap = hit.distance - (b.center.y - b.min.y);
                if (gap < 5f) continue;
                n++;
                if (found.Length > 400) continue;
                Transform t = smr.transform; string path = t.name;
                for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; path = t.name + "/" + path; }
                var h = smr.GetComponentInParent<JUHealth>();
                found.Append(' ').Append(path).Append(" +").Append(gap.ToString("F0")).Append("m@").Append(b.center.ToString("F0"))
                     .Append(h != null ? (h.IsDead ? " dead" : " alive") : " nohealth");
            }
            return " floatingModels=" + n + found;
        }

        private string MotionReport()
        {
            string r = " botMotion(dist=" + motionDist.ToString("F0") + "m rough=" + (motionDist > 1f ? (motionJerk / motionDist).ToString("F3") : "-")
                + " stalls=" + motionStalls + "/" + motionMovingFrames + " maxStep=" + motionMaxStep.ToString("F2") + "m)";
            motionDist = motionJerk = motionMaxStep = 0f;
            motionStalls = motionMovingFrames = 0;
            return r;
        }

        // -netexit <seconds>: press the pause menu's EXIT after that many seconds (tests leaving a running match)
        private bool exitPressed;
        private void TestExit()
        {
            if (exitPressed || !float.TryParse(ArgValue("-netexit"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float at)) return;
            if (Time.realtimeSinceStartup < at) return;
            exitPressed = true;
            var bridge = FindFirstObjectByType<Michsky.UI.Reach.ReachPauseBridge>();
            var nm = NetworkManager.Singleton;
            Debug.Log("[BRNetDebug] netexit: pressing EXIT (bridge=" + (bridge != null) + " listening=" + (nm != null && nm.IsListening) + ")");
            if (bridge != null) bridge.ExitToMainMenu();
        }

        // -netrock <prototype>[,<prototype>...]: from t=40 s, every 15 s drop the local player onto a terrain rock of that prototype and
        // take a screenshot 4 s later (into -netshot): it must stand on the visible rock, not float above it or sink into it
        private int rockStep;
        private float nextRock = 40f, rockShotAt = -1f;
        private void TestRocks()
        {
            string list = ArgValue("-netrock");
            var pc = JUGameManager.PlayerController;
            if (string.IsNullOrEmpty(list) || pc == null) return;
            if (rockShotAt > 0f && Time.realtimeSinceStartup >= rockShotAt)
            {
                rockShotAt = -1f;
                string dir = ArgValue("-netshot");
                if (!string.IsNullOrEmpty(dir))
                {
                    try { System.IO.Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "rock_" + rockStep + ".png")); } catch { }
                }
                Physics.Raycast(pc.transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log("[BRNetDebug] netrock " + rockStep + ": player at " + pc.transform.position.ToString("F2") + " standing on " + (hit.collider != null ? hit.collider.name + "/" + hit.collider.transform.parent?.name : "nothing"));
                rockStep++;
            }
            string[] names = list.Split(',');
            if (rockStep >= names.Length || rockShotAt > 0f || Time.realtimeSinceStartup < nextRock) return;
            nextRock = Time.realtimeSinceStartup + 15f;
            string want = names[rockStep];
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData;
                int pi = System.Array.FindIndex(td.treePrototypes, p => p.prefab != null && p.prefab.name == want);
                if (pi < 0) continue;
                // the biggest instance of that rock on this terrain, so the test is not decided by a pebble
                TreeInstance best = default; bool found = false;
                foreach (var ti in td.treeInstances) if (ti.prototypeIndex == pi && (!found || ti.heightScale > best.heightScale)) { best = ti; found = true; }
                if (!found) continue;
                Vector3 p = t.transform.position + Vector3.Scale(best.position, td.size);
                var body = pc.GetComponent<Rigidbody>();
                pc.transform.position = p + Vector3.up * 12f;
                if (body != null) { body.position = pc.transform.position; body.velocity = Vector3.zero; }
                Debug.Log("[BRNetDebug] netrock " + rockStep + ": dropping onto " + want + " (scale " + best.widthScale.ToString("F2") + "/" + best.heightScale.ToString("F2") + ", prefab scale " + td.treePrototypes[pi].prefab.transform.localScale.y + ") at " + p.ToString("F1"));
                rockShotAt = Time.realtimeSinceStartup + 4f;
                return;
            }
            Debug.Log("[BRNetDebug] netrock: no instance of " + want);
            rockStep++;
        }

        private void Update()
        {
            frames++;
            TestExit();
            TestRocks();
            TrackBotMotion();
            TestAids();
            if (!experimentApplied && Time.realtimeSinceStartup > 20f) ApplyExperiment();
            if (Time.unscaledTime < next) return;
            float dt = Time.unscaledTime - lastLog; lastLog = Time.unscaledTime;
            float fpsNow = dt > 0.01f ? frames / dt : 0f; frames = 0;
            next = Time.unscaledTime + 5f;

            var nm = NetworkManager.Singleton;
            string role = nm == null || !nm.IsListening ? "offline" : nm.IsServer ? "server" : "client";
            var sb = new StringBuilder();
            sb.Append("[BRNetDebug] role=").Append(role).Append(" t=").Append(Time.realtimeSinceStartup.ToString("F0")).Append(" fps=").Append(fpsNow.ToString("F1"));

            var co = BRMatchCoordinator.Instance;
            if (co != null) sb.Append(" state=").Append(co.State).Append(" alive=").Append(co.AliveCount).Append('/').Append(co.TotalCount);
            var z = BRZoneManager.Instance;
            if (z != null) sb.Append(" zoneR=").Append(z.CurrentRadius.ToString("F0")).Append(" phase=").Append(z.CurrentPhaseIndex);

            var pc = JUGameManager.PlayerController;
            if (pc != null)
            {
                var ph = pc.GetComponent<JUHealth>();
                Vector3 pp = pc.transform.position;
                if (ph != null) sb.Append(" me=").Append(pc.name).Append('@').Append(pp.x.ToString("F0")).Append(',').Append(pp.z.ToString("F0")).Append(" hp").Append(ph.Health.ToString("F0"));
            }
            foreach (var jp in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
            {
                var jh = jp.GetComponent<JUHealth>();
                if (jh != null) sb.Append(" [player").Append(jp.OwnerClientId).Append(" hp").Append(jh.Health.ToString("F0")).Append(" y=").Append(jp.transform.position.y.ToString("F1")).Append(" up=").Append(HeightAboveGround(jp.transform.position).ToString("F1")).Append(jp.IsOwner ? " owner" : "").Append(PuppetInfo(jp)).Append("]");
            }

            var bots = FindObjectsByType<BRNetBot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(bots, (a, b) => string.CompareOrdinal(a.name, b.name));
            int active = 0, dead = 0, ragdolled = 0, armed = 0, deadNotRagdolled = 0, airborne = 0;
            var airList = new StringBuilder();
            var list = new StringBuilder();
            for (int i = 0; i < bots.Length; i++)
            {
                var b = bots[i];
                if (!b.gameObject.activeInHierarchy) continue;
                active++;
                var h = b.GetComponent<JUHealth>();
                if (h != null && h.IsDead)
                {
                    dead++;
                    var rg = b.GetComponent<AdvancedRagdollController>();
                    if (rg != null && rg.RagdollEnabled) ragdolled++; else deadNotRagdolled++;
                }
                var hw = b.HeldWeapon;
                if (hw != null && hw.gameObject.activeInHierarchy) armed++;
                if (h != null && !h.IsDead) { float hg = HeightAboveGround(b.transform.position); if (hg > 2.5f) { airborne++; if (airList.Length < 300) airList.Append(' ').Append(b.name.Length > 10 ? b.name.Substring(b.name.Length - 10) : b.name).Append(":+").Append(hg.ToString("F1")); } }
                if (list.Length < 700 && i % 3 == 0)
                {
                    Vector3 p = b.transform.position;
                    list.Append(' ').Append(b.name.Length > 12 ? b.name.Substring(b.name.Length - 12) : b.name)
                        .Append('@').Append(p.x.ToString("F0")).Append(',').Append(p.z.ToString("F0"))
                        .Append(" hp").Append(h != null ? h.Health.ToString("F0") : "?");
                }
            }
            sb.Append(MotionReport());
            sb.Append(AnimationReport());
            sb.Append(" hudEvents(banners=").Append(Railbound.UI.MatchEventHUD.BannersShown).Append(" killPopups=").Append(Railbound.UI.MatchEventHUD.KillPopupsShown).Append(')');
            var mm = FindFirstObjectByType<Railbound.UI.Map1MiniMapController>();
            var lp = Railbound.Gameplay.LocalPlayer.Transform;
            if (mm != null) sb.Append(" minimap(tracks=").Append(mm.TrackedPlayer != null ? mm.TrackedPlayer.name + (mm.TrackedPlayer.gameObject.activeInHierarchy ? "" : "/INACTIVE") : "none")
                .Append(" local=").Append(lp != null ? lp.name : "none").Append(" ok=").Append(mm.TrackedPlayer != null && mm.TrackedPlayer == lp).Append(')');
            sb.Append(FloatingModels());
            sb.Append(" rockColliders=").Append(Railbound.Gameplay.TerrainRockColliders.LastBuiltCount);
            sb.Append(" botTriggersForwarded=").Append(BRNetBot.TriggersForwarded).Append(" groundGuardFixes=").Append(BRNetBot.GroundGuardFixes)
              .Append(" lootMarkers=").Append(Railbound.UI.LootMarkersHUD.TrackedCount);
            sb.Append(" maxRemoteUp=").Append(maxRemoteUp.ToString("F1")).Append(" airborneBots=").Append(airborne).Append(airList).Append(" botsActive=").Append(active).Append(" botsDead=").Append(dead).Append(" ragdolled=").Append(ragdolled).Append(" deadNoRagdoll=").Append(deadNotRagdolled).Append(" armed=").Append(armed).Append(" |").Append(list);
            sb.Append(" shotFx=").Append(BRNetBot.ShotEffectsShown).Append(" PLAYERFX sent=").Append(PlayerCombatNet.SentShots).Append('/').Append(PlayerCombatNet.SentBullets).Append('/').Append(PlayerCombatNet.SentReloads).Append(" relayed=").Append(PlayerCombatNet.RelayedShots).Append(" received=").Append(PlayerCombatNet.ReceivedShots).Append('/').Append(PlayerCombatNet.ReceivedBullets).Append('/').Append(PlayerCombatNet.ReceivedReloads).Append(" remoteFx(shots/bullets/reloads)=").Append(RemoteWeaponFx.ShotsShown).Append('/').Append(RemoteWeaponFx.BulletsShown).Append('/').Append(RemoteWeaponFx.ReloadsShown).Append(" bulletVis=").Append(BRNetBot.BulletVisualsShown).Append(" | loot registered=").Append(BRLootSync.RegisteredCount).Append(" taken=").Append(BRLootSync.TakenCount);
            Debug.Log(sb.ToString());
        }
    }
}
