using System.Collections.Generic;
using JUTPS;
using JUTPS.CharacterBrain;
using JUTPS.WeaponSystem;
using Project.AI.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.RL
{
    /// <summary>
    /// The tactical brain the bots play matches with (the ML-Agents policy is only used while training). It sees with a field of
    /// view, needs a moment to react, remembers where enemies were, fights from cover (peek, burst, duck, reload hidden), pushes when
    /// it has the advantage, flanks a stalemate, falls back when hurt and searches where an enemy was last seen. It walks the
    /// NavMesh path properly, steps around what the NavMesh does not know (rocks, vehicles, people) and gets itself unstuck.
    /// Shots are only taken with the enemy in sight, the aim on him and a clear line of fire, in bursts that suit the weapon, with a
    /// human aim error that tightens while it tracks.
    /// </summary>
    public partial class JUCharacterRLAgent
    {
        public enum TacticalRole { Competitor, AreaGuard }

        [Header("Tactical brain")]
        [Tooltip("Matches use the tactical brain; the ML-Agents policy only drives the bot while training (-rltrain or a connected trainer).")]
        public bool UseTacticalBrain = true;
        public TacticalRole Tactics = TacticalRole.Competitor;
        [Tooltip("Area guard (extraction / DMZ style): the post it holds and how far it patrols and chases.")]
        public Vector3 GuardCenter;
        public float GuardRadius = 30f;
        [Tooltip("Half angle of the field of view (degrees). Inside NearAwareness metres a bot notices enemies all around.")]
        public float ViewHalfAngle = 75f;
        public float NearAwareness = 8f;

        public enum TacticState { Explore, Loot, Rotate, Investigate, Search, Engage, Cover, Push, Flank, Retreat, Guard }
        public TacticState State { get; private set; } = TacticState.Explore;
        public string TacticNote { get; private set; } = "";

        // ---- test counters (BRNetDebug, the bot test)
        public static int TacticalShots, TacticalStuckEvents, TacticalCoverTaken, TacticalFlanks, TacticalEngagements;
        /// <summary>The movement the brain asks for this frame (world direction, length 0..1), whichever brain drives the bot.</summary>
        public Vector3 IntendedMove => TacticalActive ? tMove : new Vector3(cachedMoveX, 0f, cachedMoveZ);
        public bool TacticalBrainActive => TacticalActive;
        public float StuckSeconds { get; private set; }
        public float MovingSeconds { get; private set; }

        private static bool? tacticalAllowed;
        private bool TacticalActive
        {
            get
            {
                if (!UseTacticalBrain) return false;
                if (!tacticalAllowed.HasValue)
                {
                    // -oldbots: the previous policy, for comparison tests
                    bool train = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => string.Equals(a, "-rltrain", System.StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-oldbots", System.StringComparison.OrdinalIgnoreCase));
                    bool trainer = false;
                    try { trainer = Unity.MLAgents.Academy.IsInitialized && Unity.MLAgents.Academy.Instance.IsCommunicatorOn; } catch { }
                    tacticalAllowed = !train && !trainer;
                }
                return tacticalAllowed.Value;
            }
        }

        // ================================================================== perception
        private class Known
        {
            public Transform T;
            public JUHealth Health;
            public JUCharacterBrain Brain;
            public Vector3 LastPos;
            public float LastSeen = -999f;     // when it was last visible
            public float Awareness;            // 0..1: 1 = noticed, may be shot at
            public bool Visible;
            public float HitMeAt = -999f;
        }

        private readonly List<Known> known = new List<Known>();
        private Known focus;                      // the enemy the bot fights
        private float perceptionTimer, decisionTimer, tacticTimer, stateSince;
        private float engagedSince = -1f, lastDamageDealtAt = -999f, lastHurtAt = -999f;
        private float focusTrackTime;            // how long the bot has been tracking its focus (aim settles)
        private bool tacticalHooked;

        // shared list of fighters, refreshed once a second for every bot
        private static readonly List<JUCharacterController> fighters = new List<JUCharacterController>();
        private static float fightersRefreshedAt = -999f;

        private static void RefreshFighters()
        {
            if (Time.time - fightersRefreshedAt < 1f) return;
            fightersRefreshedAt = Time.time;
            fighters.Clear();
            foreach (var c in Object.FindObjectsByType<JUCharacterController>(FindObjectsSortMode.None))
                if (c != null && c.isActiveAndEnabled) fighters.Add(c);
        }

        private Vector3 Eye => transform.position + Vector3.up * (characterBrain != null && characterBrain.IsCrouched ? 1.15f : 1.6f);

        private static Vector3 ChestOf(Known k)
        {
            if (k.Brain != null && k.Brain.HumanoidSpine != null) return k.Brain.HumanoidSpine.position;
            return k.T.position + Vector3.up * 1.2f;
        }

        private void HookTactical()
        {
            if (tacticalHooked || selfHealth == null) return;
            tacticalHooked = true;
            selfHealth.OnTakeDamage += (dmg, at) =>
            {
                lastHurtAt = Time.time;
                var attacker = selfHealth.LastAttacker;
                if (attacker == null || attacker == gameObject) return;
                var k = Remember(attacker.transform);
                if (k == null) return;
                k.HitMeAt = Time.time;
                k.Awareness = 1f;
                k.LastPos = attacker.transform.position;
                if (k.LastSeen < Time.time - 2f) k.LastSeen = Time.time - 0.5f;   // knows where it came from, not seen yet
            };
        }

        private Known Remember(Transform t)
        {
            if (t == null) return null;
            var root = t.GetComponentInParent<JUCharacterController>();
            if (root == null) return null;
            if (root.transform == transform || IsTeammate(root.gameObject)) return null;
            foreach (var k in known) if (k.T == root.transform) return k;
            var nk = new Known { T = root.transform, Health = root.GetComponent<JUHealth>(), Brain = root, LastPos = root.transform.position };
            known.Add(nk);
            return nk;
        }

        private bool IsTeammate(GameObject other)
        {
            try { return Railbound.BR.BRTeams.AreTeammates(gameObject, other); } catch { return false; }
        }

        private void Perceive()
        {
            RefreshFighters();
            Vector3 eye = Eye;
            Vector3 fwd = characterBrain != null && characterBrain.FiringMode ? (smoothedAimPosition - eye) : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            fwd.Normalize();
            float range = Mathf.Max(DetectionRadius, WeaponRange() * 1.1f);
            float reaction = runtimeProfile != null ? Mathf.Clamp(runtimeProfile.ReactionTime, 0.12f, 0.8f) : 0.3f;

            foreach (var c in fighters)
            {
                if (c == null || c.transform == transform) continue;
                var h = c.GetComponent<JUHealth>();
                if (h != null && h.IsDead) continue;
                Vector3 to = c.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist > range) continue;
                if (IsTeammate(c.gameObject)) continue;

                Known k = null;
                foreach (var e in known) if (e.T == c.transform) { k = e; break; }
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                bool inView = dist < NearAwareness || (flat.sqrMagnitude > 0.01f && Vector3.Angle(fwd, flat) <= ViewHalfAngle) || (k != null && Time.time - k.HitMeAt < 3f);
                if (!inView) { if (k != null) k.Visible = false; continue; }

                if (k == null) k = new Known { T = c.transform, Health = h, Brain = c, LastPos = c.transform.position };
                Vector3 chest = c.HumanoidSpine != null ? c.HumanoidSpine.position : c.transform.position + Vector3.up * 1.2f;
                bool seen = ClearLine(eye, chest, c.transform) || ClearLine(eye, chest + Vector3.up * 0.45f, c.transform);
                // crouched in tall grass or at long range a person is harder to notice
                float noticeRate = 1f / Mathf.Max(0.05f, reaction * (1f + dist / 60f) * (c.IsCrouched ? 1.6f : 1f));
                if (seen)
                {
                    if (!known.Contains(k)) known.Add(k);
                    k.Visible = true;
                    k.LastSeen = Time.time;
                    k.LastPos = c.transform.position;
                    k.Awareness = Mathf.Min(1f, k.Awareness + noticeRate * 0.15f);   // called every 0.15 s
                }
                else k.Visible = false;
            }

            // forget the dead and the long gone
            for (int i = known.Count - 1; i >= 0; i--)
            {
                var k = known[i];
                if (k.T == null || !k.T.gameObject.activeInHierarchy || (k.Health != null && k.Health.IsDead) || Time.time - k.LastSeen > 25f)
                {
                    if (focus == k) focus = null;
                    known.RemoveAt(i);
                    continue;
                }
                if (!k.Visible) k.Awareness = Mathf.Max(Time.time - k.LastSeen < 6f ? 1f : 0f, k.Awareness - 0.05f);
            }

            // the focus: noticed enemies, the closest and the most dangerous first, with some loyalty to the current one
            Known best = null;
            float bestScore = float.MaxValue;
            foreach (var k in known)
            {
                if (k.Awareness < 1f) continue;
                float d = Vector3.Distance(transform.position, k.LastPos);
                float score = d + (k.Visible ? 0f : 35f) + (Time.time - k.LastSeen) * 4f - (Time.time - k.HitMeAt < 4f ? 25f : 0f)
                              - (k.Health != null ? (100f - k.Health.Health) * 0.12f : 0f) - (k == focus ? 12f : 0f);
                if (score < bestScore) { bestScore = score; best = k; }
            }
            if (best != focus) { focus = best; focusTrackTime = 0f; }

            // the rest of the agent (rewards, debug) keeps reading these
            if (focus != null)
            {
                currentTarget = focus.T;
                currentTargetBrain = focus.Brain;
                currentTargetHealth = focus.Health;
                hasTargetLineOfSight = focus.Visible;
                lastKnownTargetPos = focus.LastPos;
                timeSinceTargetSeen = Time.time - focus.LastSeen;
            }
            else
            {
                currentTarget = null;
                hasTargetLineOfSight = false;
            }
        }

        /// <summary>True when nothing solid (not a character, not this bot) is between two points.</summary>
        private bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.05f) return true;
            var hits = Physics.RaycastAll(from, d / len, len, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var ht = hits[i].transform;
                if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
                if (target != null && (ht == target || ht.IsChildOf(target))) continue;
                if (ht.GetComponentInParent<JUCharacterController>() != null) continue;   // people do not stop sight (they are seen past)
                if (ht.GetComponentInParent<Weapon>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>True when the protected point is hidden from the threat by solid geometry (not people).</summary>
        private bool Blocked(Vector3 from, Vector3 to) => !ClearLine(from, to, null);

        // ================================================================== weapons
        private Weapon Gun => characterBrain != null ? characterBrain.WeaponInUseRightHand : null;

        private float WeaponRange()
        {
            var w = Gun;
            if (w == null) return 3f;
            switch (w.FireMode)
            {
                case Weapon.WeaponFireMode.Shotgun: return 28f;
                case Weapon.WeaponFireMode.BoltAction: return 230f;
                case Weapon.WeaponFireMode.SemiAuto: return w.BulletsPerMagazine >= 20 ? 110f : 50f;
                default: return w.BulletsPerMagazine >= 28 ? 90f : 60f;
            }
        }

        private float PreferredRange()
        {
            var w = Gun;
            float profile = runtimeProfile != null ? runtimeProfile.OptimalRange : OptimalCombatRange;
            if (w == null) return 2f;
            switch (w.FireMode)
            {
                case Weapon.WeaponFireMode.Shotgun: return Mathf.Min(profile, 9f);
                case Weapon.WeaponFireMode.BoltAction: return 70f;
                default: return Mathf.Clamp(profile * (w.BulletsPerMagazine >= 28 ? 1.6f : 1f), 10f, 40f);
            }
        }

        private bool HasAmmoInMag => Gun != null && Gun.BulletsAmounts > 0;
        private bool HasAnyAmmo => Gun != null && Gun.BulletsAmounts + Gun.TotalBullets > 0;

        /// <summary>Another weapon in the inventory that still has ammunition, or -1.</summary>
        private int OtherLoadedWeaponId()
        {
            var inv = characterBrain != null ? characterBrain.Inventory : null;
            if (inv == null || inv.HoldableItensRightHand == null) return -1;
            foreach (var item in inv.HoldableItensRightHand)
                if (item is Weapon w && w.Unlocked && w != Gun && w.BulletsAmounts + w.TotalBullets > 0) return w.ItemSwitchID;
            return -1;
        }

        // ================================================================== movement
        private Vector3[] tPath = new Vector3[0];
        private int tCorner;
        private Vector3 tPathDest = new Vector3(float.NaN, 0f, 0f);
        private float tRepathAt;
        private Vector3 stuckSample;
        private float stuckSampleAt, unstickUntil;
        private Vector3 unstickDir;
        private int stuckCount;
        private float stuckWindowStart;
        private bool goalInvalid;

        /// <summary>World direction (0..1 length) that walks the NavMesh towards <paramref name="dest"/>; arrived when within <paramref name="arrive"/> m.</summary>
        private Vector3 MoveTo(Vector3 dest, float arrive, out bool arrived)
        {
            Vector3 pos = transform.position;
            arrived = Flat(pos - dest).magnitude <= arrive;
            if (arrived) return Vector3.zero;

            if (float.IsNaN(tPathDest.x) || Flat(tPathDest - dest).magnitude > 2f || Time.time >= tRepathAt || tCorner >= tPath.Length)
                Repath(dest);

            // walk to the farthest corner the bot can reach in a straight line on the NavMesh (no corner cutting through walls)
            Vector3 target = dest;
            if (tPath.Length > 0)
            {
                while (tCorner < tPath.Length - 1 && Flat(pos - tPath[tCorner]).magnitude < 0.6f) tCorner++;
                if (NavMesh.SamplePosition(pos, out NavMeshHit here, 2f, NavMesh.AllAreas))
                    for (int look = 0; look < 2 && tCorner < tPath.Length - 1; look++)
                        if (!NavMesh.Raycast(here.position, tPath[tCorner + 1], out _, NavMesh.AllAreas)) tCorner++; else break;
                target = tPath[Mathf.Min(tCorner, tPath.Length - 1)];
            }
            Vector3 dir = Flat(target - pos);
            if (dir.sqrMagnitude < 0.0001f) dir = Flat(dest - pos);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            return Avoid(dir);
        }

        private void Repath(Vector3 dest)
        {
            tPathDest = dest;
            tRepathAt = Time.time + Random.Range(0.8f, 1.3f);
            tCorner = 0;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit a, 3f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(dest, out NavMeshHit b, 6f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 0)
                {
                    tPath = path.corners;
                    tCorner = tPath.Length > 1 ? 1 : 0;
                    return;
                }
            }
            tPath = new Vector3[0];
        }

        /// <summary>
        /// Steps around what is in the way and the NavMesh does not know: rocks, vehicles, crates, people. A sweep at knee and chest
        /// height a short way ahead; when it hits, the nearest clear heading that stays on the NavMesh wins.
        /// </summary>
        private Vector3 Avoid(Vector3 dir)
        {
            if (!SweepBlocked(dir, 1.5f)) return Separate(dir);
            float[] angles = { 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };
            // prefer the side the bot turned to last time (no left-right dithering)
            if (avoidSide < 0) System.Array.Reverse(angles);
            foreach (float a in angles)
            {
                Vector3 c = Quaternion.Euler(0f, a, 0f) * dir;
                if (SweepBlocked(c, 1.6f)) continue;
                Vector3 ahead = transform.position + c * 1.6f;
                if (!NavMesh.SamplePosition(ahead, out _, 1.2f, NavMesh.AllAreas)) continue;
                avoidSide = a > 0f ? 1 : -1;
                return Separate(c);
            }
            return Separate(dir * 0.3f);
        }

        private int avoidSide = 1;

        private bool SweepBlocked(Vector3 dir, float dist)
        {
            foreach (float h in new[] { 0.5f, 1.25f })
            {
                Vector3 o = transform.position + Vector3.up * h;
                var hits = Physics.SphereCastAll(o, 0.3f, dir, dist, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                {
                    var ht = hit.transform;
                    if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
                    if (hit.collider is TerrainCollider) continue;   // the ground itself: slopes are the NavMesh's business
                    if (ht.GetComponentInParent<Weapon>() != null) continue;
                    if (hit.distance <= 0f) continue;               // already touching: the stuck check handles that
                    if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f) continue;   // floors and steps
                    return true;
                }
            }
            return false;
        }

        /// <summary>Keeps a little room from other people (no queues, no pushing through teammates).</summary>
        private Vector3 Separate(Vector3 dir)
        {
            Vector3 push = Vector3.zero;
            foreach (var c in fighters)
            {
                if (c == null || c.transform == transform) continue;
                Vector3 d = Flat(transform.position - c.transform.position);
                float m = d.magnitude;
                if (m < 0.01f || m > 1.8f) continue;
                push += d / m * (1.8f - m);
            }
            Vector3 r = dir + push * 0.6f;
            return r.sqrMagnitude > 1f ? r.normalized : r;
        }

        /// <summary>Notices when a bot pushes but does not move, and walks it out (back off, step aside, new path; then a new goal).</summary>
        private Vector3 Unstick(Vector3 wanted)
        {
            Vector3 pos = transform.position;
            bool pushing = wanted.sqrMagnitude > 0.25f;
            if (pushing) MovingSeconds += Time.deltaTime;
            if (Time.time < unstickUntil) { StuckSeconds += Time.deltaTime; return unstickDir; }
            if (!pushing) { stuckSample = pos; stuckSampleAt = Time.time; return wanted; }
            if (Time.time - stuckSampleAt < 0.9f) return wanted;
            float moved = Flat(pos - stuckSample).magnitude;
            stuckSample = pos;
            stuckSampleAt = Time.time;
            if (moved > 0.35f) return wanted;

            // stuck: low obstacle -> hop over it; otherwise back off to the freer side and plan again
            TacticalStuckEvents++;
            if (Time.time - stuckWindowStart > 8f) { stuckWindowStart = Time.time; stuckCount = 0; }
            stuckCount++;
            if (stuckCount >= 3) { goalInvalid = true; stuckCount = 0; }
            Vector3 fwd = wanted.normalized;
            bool kneeBlocked = Physics.Raycast(pos + Vector3.up * 0.4f, fwd, 1f, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            bool chestFree = !Physics.Raycast(pos + Vector3.up * 1.5f, fwd, 1.2f, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            if (kneeBlocked && chestFree && characterBrain != null && characterBrain.IsGrounded) characterBrain._Jump();
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * (SweepBlocked(Vector3.Cross(Vector3.up, fwd), 1.2f) ? -1f : 1f);
            unstickDir = (side * 0.85f - fwd * 0.5f).normalized;
            unstickUntil = Time.time + Random.Range(0.5f, 0.9f);
            tRepathAt = 0f;
            return unstickDir;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ================================================================== cover
        private static readonly List<(Vector3 pos, float until, int owner)> claims = new List<(Vector3, float, int)>();
        private Vector3 coverPos;
        private bool hasCover;
        private float coverChosenAt;

        /// <summary>
        /// The best spot nearby to fight from: hidden from the threat when crouched, but with a line to it when standing (to peek), at a
        /// sensible range, not too far to run, and not taken by another bot. <paramref name="fullCover"/> wants a spot hidden even standing.
        /// </summary>
        private bool FindCover(Vector3 threat, bool fullCover, float minFromThreat, out Vector3 best)
        {
            best = default;
            float bestScore = float.MinValue;
            Vector3 threatEye = threat + Vector3.up * 1.5f;
            float pref = PreferredRange();
            int id = GetInstanceID();
            claims.RemoveAll(c => c.until < Time.time);
            float[] rings = { 3.5f, 6.5f, 10f, 14f };
            for (int r = 0; r < rings.Length; r++)
            {
                for (int i = 0; i < 10; i++)
                {
                    float a = (i / 10f + r * 0.05f) * Mathf.PI * 2f;
                    Vector3 c = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rings[r];
                    if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                    Vector3 p = hit.position;
                    float dThreat = Flat(p - threat).magnitude;
                    if (dThreat < minFromThreat) continue;
                    if (!Blocked(threatEye, p + Vector3.up * 0.95f)) continue;              // crouched: hidden
                    bool standingSeen = !Blocked(threatEye, p + Vector3.up * 1.6f);
                    Vector3 sideStep = Vector3.Cross(Vector3.up, Flat(threat - p).normalized) * 0.9f;
                    bool sidePeek = !Blocked(threatEye, p + sideStep + Vector3.up * 1.3f) || !Blocked(threatEye, p - sideStep + Vector3.up * 1.3f);
                    bool peekable = standingSeen || sidePeek;
                    if (fullCover && standingSeen) continue;
                    float score = 0f;
                    score += fullCover ? 4f : (peekable ? 6f : 1f);
                    score -= Flat(p - transform.position).magnitude * 0.35f;                 // short run
                    score -= Mathf.Abs(dThreat - pref) * (fullCover ? 0.02f : 0.08f);       // at a good range
                    foreach (var cl in claims) if (cl.owner != id && Flat(cl.pos - p).magnitude < 2.5f) score -= 8f;
                    // do not run across the enemy's face to get there
                    Vector3 toP = Flat(p - transform.position), toT = Flat(threat - transform.position);
                    if (toP.sqrMagnitude > 1f && toT.sqrMagnitude > 1f && Vector3.Angle(toP, toT) < 40f) score -= 3f;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
            }
            if (bestScore == float.MinValue) return false;
            claims.RemoveAll(c => c.owner == id);
            claims.Add((best, Time.time + 12f, id));
            return true;
        }

        // ================================================================== aim and fire
        private Vector3 tAimPoint;
        private Vector3 aimDir;
        private Vector2 aimNoiseSeed;
        private bool burstOn;
        private float burstUntil, burstPauseUntil, nextTapAt;
        private bool tapThisFrame;
        private int noShotFrames;
        private int lastMagCount = -1;
        private float wantFireSince = -1f;

        /// <summary>The point the bot wants to hit, with a human error that grows with range and movement and shrinks while it tracks.</summary>
        private Vector3 AimPointOn(Known k)
        {
            Vector3 chest = ChestOf(k);
            float accuracy = runtimeProfile != null ? runtimeProfile.AimAccuracy : 0.65f;
            float dist = Vector3.Distance(Eye, chest);
            // lead a moving target (the tracker knows its speed)
            float bulletSpeed = 160f;
            if (Gun != null && Gun.BulletPrefab != null)
            {
                var b = Gun.BulletPrefab.GetComponent<Bullet>();
                if (b != null && b.BulletVelocity > 10f) bulletSpeed = b.BulletVelocity;
            }
            Vector3 led = predictiveTracker.CalculateLeadAimPoint(Eye, bulletSpeed, Mathf.Lerp(0.6f, 1f, accuracy), runtimeProfile != null && runtimeProfile.HasSkill(AISkill.PredictiveLeadShot));
            if ((led - chest).sqrMagnitude > 16f || float.IsNaN(led.x)) led = chest;
            float settle = Mathf.Lerp(1.7f, 0.35f, Mathf.Clamp01(focusTrackTime / Mathf.Lerp(1.6f, 0.7f, accuracy)));
            float selfMoving = selfRb != null ? Mathf.Clamp01(Flat(selfRb.velocity).magnitude / 4f) : 0f;
            float spread = ((1f - accuracy) * 0.9f + dist * 0.01f) * settle * (1f + selfMoving * 0.7f);
            float t = Time.time * 1.7f;
            Vector3 toT = (led - Eye).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toT).normalized, up = Vector3.Cross(toT, right);
            float ex = (Mathf.PerlinNoise(aimNoiseSeed.x, t) - 0.5f) * 2f, ey = (Mathf.PerlinNoise(aimNoiseSeed.y, t + 3f) - 0.5f) * 2f;
            return led + (right * ex + up * ey * 0.8f) * spread;
        }

        /// <summary>Burst control for the weapon in hand: holds the trigger for automatic bursts, taps semi-automatics, waits out bolt actions.</summary>
        private bool TriggerWanted(bool canFire, out bool down)
        {
            down = false;
            var w = Gun;
            if (!canFire || w == null) { burstOn = false; return false; }
            float rate = Mathf.Max(0.05f, w.Fire_Rate);
            if (Time.time < burstPauseUntil) return false;
            switch (w.FireMode)
            {
                case Weapon.WeaponFireMode.Auto:
                    if (!burstOn)
                    {
                        burstOn = true;
                        float shots = Random.Range(3, 8);
                        burstUntil = Time.time + shots * rate;
                        down = true;
                    }
                    if (Time.time > burstUntil)
                    {
                        burstOn = false;
                        burstPauseUntil = Time.time + Random.Range(0.2f, 0.55f);
                        return false;
                    }
                    return true;
                default:
                    if (Time.time < nextTapAt) return false;
                    float gap = w.FireMode == Weapon.WeaponFireMode.BoltAction ? Random.Range(1.2f, 1.9f)
                              : w.FireMode == Weapon.WeaponFireMode.Shotgun ? Mathf.Max(rate, Random.Range(0.75f, 1.1f))
                              : Mathf.Max(rate, Random.Range(0.22f, 0.42f));
                    nextTapAt = Time.time + gap;
                    down = true;
                    return true;
            }
        }

        // ================================================================== the decision
        private Vector3 tMove;
        private bool tRun, tCrouch, tAim, tReload;
        private bool peekUp;
        private float peekUntil;
        private Vector3 searchPoint;
        private float searchUntil;
        private Vector3 flankPoint;

        private void SetState(TacticState s, string note = "")
        {
            if (s != State) { State = s; stateSince = Time.time; }
            TacticNote = note;
        }

        /// <summary>Runs the tactical brain for one frame and drives the character with it.</summary>
        private void TacticalTick()
        {
            HookTactical();
            if (aimNoiseSeed == Vector2.zero) aimNoiseSeed = new Vector2(Random.Range(0f, 100f), Random.Range(100f, 200f));

            perceptionTimer -= Time.deltaTime;
            if (perceptionTimer <= 0f) { perceptionTimer = 0.15f; Perceive(); }
            if (focus != null && focus.Visible) focusTrackTime += Time.deltaTime; else focusTrackTime = Mathf.Max(0f, focusTrackTime - Time.deltaTime * 2f);
            if (focus != null && focus.T != null) predictiveTracker.UpdateTargetKinematics(focus.T.position, Time.deltaTime);

            decisionTimer -= Time.deltaTime;
            if (decisionTimer <= 0f) { decisionTimer = 0.1f; Decide(); }

            // movement is steered every frame
            Vector3 move = Unstick(tMove);

            // aim: turn towards the wanted point no faster than a person turns
            Vector3 eye = Eye;
            Vector3 wantDir = tAimPoint - eye;
            if (wantDir.sqrMagnitude < 0.01f) wantDir = transform.forward;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = transform.forward;
            float accuracy = runtimeProfile != null ? runtimeProfile.AimAccuracy : 0.65f;
            float turn = Mathf.Lerp(150f, 340f, accuracy) * Mathf.Deg2Rad * Time.deltaTime;
            aimDir = Vector3.RotateTowards(aimDir, wantDir.normalized, turn, 0f).normalized;
            float aimDist = Mathf.Max(3f, wantDir.magnitude);
            smoothedAimPosition = eye + aimDir * aimDist;
            characterBrain.LookAtPosition = smoothedAimPosition;

            // fire: the enemy in sight, noticed, the aim on him and nothing in the way
            bool canFire = false;
            if (focus != null && focus.Visible && focus.Awareness >= 1f && tAim && HasAmmoInMag && !characterBrain.IsReloading && !tRun)
            {
                Vector3 chest = ChestOf(focus);
                float dist = Vector3.Distance(eye, chest);
                float tol = Mathf.Atan2(0.5f, dist) * Mathf.Rad2Deg + 1.2f;
                bool onTarget = Vector3.Angle(aimDir, chest - eye) <= tol + 2.5f;
                bool inRange = dist <= WeaponRange();
                Vector3 muzzle = Gun != null && Gun.Shoot_Position != null ? Gun.Shoot_Position.position : eye;
                bool lineClear = ClearLine(muzzle, chest, focus.T) && !TeammateInLine(muzzle, chest);
                canFire = onTarget && inRange && lineClear && characterBrain.FiringMode;
            }
            bool hold = TriggerWanted(canFire, out bool down);

            // drive the character
            characterBrain._Move(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.z, -1f, 1f), tRun && !tAim);
            if (tCrouch && !characterBrain.IsCrouched) characterBrain._Crouch();
            else if (!tCrouch && characterBrain.IsCrouched) characterBrain._GetUp();

            bool raise = tAim && Gun != null && !characterBrain.IsReloading;
            characterBrain.FiringMode = raise;
            characterBrain.FiringModeIK = raise;
            if (raise) characterBrain.DoLookAt(characterBrain.LookAtPosition, Mathf.Max(LookTargetSpeed, 10f));

            if (Role == CombatRole.MeleeRusher || (Gun == null && focus != null))
            {
                bool close = focus != null && Vector3.Distance(transform.position, focus.T.position) < 2.4f;
                if (close) characterBrain.DoLookAt(focus.T.position + Vector3.up, 12f);
                characterBrain.DefaultUseOfAllItems(false, false, false, false, false, close);
            }
            else if (tReload && Gun != null && Gun.BulletsAmounts < Gun.BulletsPerMagazine && Gun.TotalBullets > 0 && !characterBrain.IsReloading)
            {
                characterBrain.DefaultUseOfAllItems(false, false, true, false, false, false);
            }
            else if (hold)
            {
                characterBrain.DefaultUseOfAllItems(true, down, false, false, false, false);
                if (down) TacticalShots++;
                if (matchTracker != null && down) matchTracker.RecordShotFired();
            }
            else characterBrain.DefaultUseOfAllItems(false, false, false, false, false, false);

            // a gun that will not fire (jammed state, lost equip): put it away and draw again
            if (hold && Gun != null)
            {
                if (lastMagCount == Gun.BulletsAmounts) noShotFrames++; else noShotFrames = 0;
                lastMagCount = Gun.BulletsAmounts;
                if (wantFireSince < 0f) wantFireSince = Time.time;
                if (noShotFrames > 150 && Time.time - wantFireSince > 2.5f)
                {
                    noShotFrames = 0;
                    wantFireSince = -1f;
                    characterBrain.SwitchToItem(-1);
                    equipCheckTimer = 0.2f;
                }
            }
            else { wantFireSince = -1f; noShotFrames = 0; }
        }

        private bool TeammateInLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.5f) return false;
            foreach (var hit in Physics.SphereCastAll(from, 0.25f, d / len, len - 0.8f, VisibilityLayerMask, QueryTriggerInteraction.Ignore))
            {
                var c = hit.transform != null ? hit.transform.GetComponentInParent<JUCharacterController>() : null;
                if (c != null && c.transform != transform && IsTeammate(c.gameObject)) return true;
            }
            return false;
        }

        private void Decide()
        {
            tReload = false;
            Vector3 pos = transform.position;
            bool armed = Role == CombatRole.RangedTactical ? HasAnyRangedWeapon() : true;
            float hp = selfHealth != null ? selfHealth.Health / Mathf.Max(1f, selfHealth.MaxHealth) : 1f;
            float aggression = runtimeProfile != null ? runtimeProfile.Aggression : 0.6f;
            bool fighting = focus != null && focus.Awareness >= 1f && Time.time - focus.LastSeen < 3f;

            // out of ammunition in this gun: another loaded gun, else find ammo
            if (Gun != null && !HasAnyAmmo)
            {
                int other = OtherLoadedWeaponId();
                if (other >= 0) characterBrain.SwitchToItem(other);
            }
            // empty magazine: reload now; low magazine: reload when nobody is in sight
            if (Gun != null && Gun.TotalBullets > 0 && (Gun.BulletsAmounts == 0 || (Gun.BulletsAmounts < Gun.BulletsPerMagazine * 0.35f && !(focus != null && focus.Visible))))
                tReload = true;

            // 1. the storm
            if (TryGetZoneDestination(out Vector3 zoneDest, out bool zoneRun) && !(fighting && focus.Visible && Vector3.Distance(pos, focus.LastPos) < 22f))
            {
                SetState(TacticState.Rotate, "storm");
                tMove = MoveTo(zoneDest, 3f, out _);
                tRun = zoneRun && !(fighting && focus.Visible);
                tCrouch = false;
                AimWhileMoving(fighting);
                return;
            }

            // 2. no gun: get one (and stay away from enemies)
            if (Role == CombatRole.RangedTactical && !armed)
            {
                if (trackedLootValid && trackedLoot.IsAvailable())
                {
                    SetState(TacticState.Loot, "weapon");
                    tMove = MoveTo(trackedLoot.Position, 0.8f, out _);
                    tRun = Flat(trackedLoot.Position - pos).magnitude > 8f;
                }
                else Explore();
                tCrouch = false;
                tAim = false;
                tAimPoint = Eye + Flat(tMove.sqrMagnitude > 0.01f ? tMove : transform.forward).normalized * 10f;
                return;
            }

            // 3. a fight
            if (fighting && Role == CombatRole.RangedTactical && Gun != null)
            {
                Fight(hp, aggression);
                return;
            }
            if (fighting && (Role == CombatRole.MeleeRusher || Gun == null))
            {
                SetState(TacticState.Push, "melee");
                tMove = MoveTo(focus.LastPos, 1.2f, out _);
                tRun = Vector3.Distance(pos, focus.LastPos) > 4f;
                tCrouch = false;
                tAim = false;
                tAimPoint = ChestOf(focus);
                return;
            }
            engagedSince = -1f;
            hasCover = false;

            // 4. an enemy that slipped away: go and look where he was
            if (focus != null && Time.time - focus.LastSeen < 18f)
            {
                if (State != TacticState.Search) { searchPoint = focus.LastPos; searchUntil = Time.time + 14f; }
                SetState(TacticState.Search, "last seen");
                tMove = MoveTo(searchPoint, 2.5f, out bool there);
                tRun = false;
                tCrouch = false;
                tAim = true;
                tAimPoint = there ? Eye + Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.9f) * 70f, 0f) * Flat(searchPoint - pos + transform.forward).normalized * 15f : searchPoint + Vector3.up * 1.3f;
                if (there && Time.time > searchUntil) focus.LastSeen = -999f;
                return;
            }

            // 5. a gunshot or footsteps heard
            if (hasAcousticThreat && armed)
            {
                SetState(TacticState.Investigate, "heard");
                tMove = MoveTo(threatLocation, 4f, out _);
                tRun = Flat(threatLocation - pos).magnitude > 35f;
                tCrouch = false;
                tAim = !tRun;
                tAimPoint = threatLocation + Vector3.up * 1.3f;
                return;
            }

            // 6. loot worth taking (ammunition when low, a better gun)
            if (trackedLootValid && trackedLoot.IsAvailable() && !goalInvalid)
            {
                SetState(TacticState.Loot, "loot");
                tMove = MoveTo(trackedLoot.Position, 0.8f, out _);
                tRun = Flat(trackedLoot.Position - pos).magnitude > 10f;
                tCrouch = false;
                tAim = false;
                tAimPoint = Eye + Flat(tMove.sqrMagnitude > 0.01f ? tMove : transform.forward).normalized * 10f;
                return;
            }

            // 7. guards walk their post; everyone else explores
            if (Tactics == TacticalRole.AreaGuard) Guard();
            else Explore();
        }

        private void AimWhileMoving(bool fighting)
        {
            if (fighting && focus.Visible)
            {
                tAim = true;
                tAimPoint = AimPointOn(focus);
            }
            else
            {
                tAim = false;
                tAimPoint = Eye + Flat(tMove.sqrMagnitude > 0.01f ? tMove : transform.forward).normalized * 12f;
            }
        }

        private Vector3 exploreGoal;
        private bool hasExploreGoal;

        private void Explore()
        {
            SetState(TacticState.Explore, "explore");
            if (!hasExploreGoal || goalInvalid || Flat(exploreGoal - transform.position).magnitude < 3f)
            {
                goalInvalid = false;
                RefreshFrontierIfNeeded();
                if (hasFrontier) exploreGoal = frontierPoint;
                else
                {
                    Vector2 r = Random.insideUnitCircle * 40f;
                    exploreGoal = transform.position + new Vector3(r.x, 0f, r.y);
                }
                hasExploreGoal = true;
            }
            if (hasFrontier && Flat(frontierPoint - exploreGoal).magnitude > 1f) exploreGoal = frontierPoint;
            tMove = MoveTo(exploreGoal, 2.5f, out _);
            tRun = Flat(exploreGoal - transform.position).magnitude > 25f;
            tCrouch = false;
            tAim = false;
            tAimPoint = Eye + Flat(tMove.sqrMagnitude > 0.01f ? tMove : transform.forward).normalized * 12f;
        }

        private void Guard()
        {
            SetState(TacticState.Guard, "post");
            Vector3 center = GuardCenter == Vector3.zero ? spawnPosition : GuardCenter;
            if (!hasExploreGoal || goalInvalid || Flat(exploreGoal - transform.position).magnitude < 2f)
            {
                goalInvalid = false;
                Vector2 r = Random.insideUnitCircle * GuardRadius * 0.6f;
                exploreGoal = center + new Vector3(r.x, 0f, r.y);
                hasExploreGoal = true;
            }
            tMove = MoveTo(exploreGoal, 2f, out bool there) * 0.55f;   // a guard walks
            tRun = false;
            tCrouch = false;
            tAim = there;
            tAimPoint = Eye + Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.4f + GetInstanceID()) * 80f, 0f) * Flat(transform.forward).normalized * 15f;
        }

        // ------------------------------------------------------------------ fighting
        private void Fight(float hp, float aggression)
        {
            Vector3 pos = transform.position;
            Vector3 enemy = focus.LastPos;
            float dist = Flat(enemy - pos).magnitude;
            float pref = PreferredRange();
            float coverPriority = runtimeProfile != null ? runtimeProfile.CoverPriority : 0.55f;
            float flankAwareness = runtimeProfile != null ? runtimeProfile.FlankAwareness : 0.5f;
            if (engagedSince < 0f) { engagedSince = Time.time; TacticalEngagements++; tacticTimer = 0f; }
            bool damagedEnemyRecently = currentTargetHealth != null && Time.time - lastDamageDealtAt < 6f;
            if (currentTargetHealth != null && currentTargetHealth.Health < prevTargetHealth - 0.5f) lastDamageDealtAt = Time.time;

            // pick a tactic every few seconds (or when the current one ends)
            tacticTimer -= 0.1f;
            bool hurt = hp < 0.32f;
            bool enemyWeak = focus.Health != null && focus.Health.Health < 35f;
            bool enemyReloading = focus.Brain != null && focus.Brain.IsReloading;
            if (tacticTimer <= 0f || (hurt && State != TacticState.Retreat))
            {
                tacticTimer = Random.Range(3.5f, 6.5f);
                if (hurt && Time.time - lastHurtAt < 8f)
                {
                    hasCover = FindCover(enemy, true, Mathf.Max(dist, 12f), out coverPos);
                    SetState(TacticState.Retreat, "hurt");
                    if (hasCover) TacticalCoverTaken++;
                }
                else if ((enemyWeak || enemyReloading || dist < 7f || (aggression > 0.7f && hp > 0.6f)) && dist < pref * 2.2f)
                {
                    SetState(TacticState.Push, enemyWeak ? "enemy weak" : enemyReloading ? "enemy reloading" : "close");
                }
                else if (Time.time - engagedSince > 11f && !damagedEnemyRecently && flankAwareness > 0.45f && dist > 12f && TryFlankPoint(enemy, out flankPoint))
                {
                    SetState(TacticState.Flank, "stalemate");
                    TacticalFlanks++;
                }
                else
                {
                    bool keep = hasCover && State == TacticState.Cover && Time.time - coverChosenAt < 9f && Blocked(enemy + Vector3.up * 1.5f, coverPos + Vector3.up * 0.95f);
                    if (!keep)
                    {
                        hasCover = Random.value < Mathf.Lerp(0.55f, 0.95f, coverPriority) && FindCover(enemy, false, Mathf.Min(pref * 0.5f, 8f), out coverPos);
                        if (hasCover) { coverChosenAt = Time.time; TacticalCoverTaken++; }
                    }
                    SetState(hasCover ? TacticState.Cover : TacticState.Engage, hasCover ? "cover" : "strafe");
                }
            }

            Vector3 toEnemy = Flat(enemy - pos);
            Vector3 dirE = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dirE);
            float weave = Mathf.Sin(Time.time * 1.3f + GetInstanceID() * 0.37f);

            switch (State)
            {
                case TacticState.Retreat:
                case TacticState.Cover:
                {
                    if (!hasCover) { SetState(TacticState.Engage, "no cover"); goto default; }
                    tMove = MoveTo(coverPos, 0.7f, out bool inCover);
                    bool full = State == TacticState.Retreat;
                    tRun = !inCover && Flat(coverPos - pos).magnitude > 5f && !focus.Visible;
                    if (!inCover)
                    {
                        tCrouch = false;
                        AimWhileMoving(true);
                        if (!focus.Visible) { tAim = true; tAimPoint = enemy + Vector3.up * 1.3f; }
                        break;
                    }
                    // in cover: duck, then stand up to shoot a burst, then duck again; reload while down
                    tMove = Vector3.zero;
                    if (full || tReload || characterBrain.IsReloading)
                    {
                        tCrouch = true;
                        tAim = true;
                        tAimPoint = enemy + Vector3.up * 1.3f;
                        peekUp = false;
                        if (full && Time.time - stateSince > Random.Range(5f, 8f)) tacticTimer = 0f;
                        break;
                    }
                    if (Time.time > peekUntil)
                    {
                        peekUp = !peekUp;
                        peekUntil = Time.time + (peekUp ? Random.Range(1.1f, 2.4f) : Random.Range(0.7f, 1.6f));
                    }
                    tCrouch = !peekUp;
                    tAim = true;
                    tAimPoint = focus.Visible ? AimPointOn(focus) : enemy + Vector3.up * 1.3f;
                    // standing up shows nothing (bad peek spot): lean out to the side a little
                    if (peekUp && !focus.Visible && Time.time - focus.LastSeen > 1.2f) tMove = right * (Mathf.Sin(Time.time * 0.7f) > 0f ? 0.5f : -0.5f);
                    // the enemy can see the bot even when it is down: the cover is no good any more
                    if (!peekUp && focus.Visible && Time.time - stateSince > 1.5f) tacticTimer = 0f;
                    break;
                }
                case TacticState.Push:
                {
                    Vector3 dest = enemy - dirE * Mathf.Max(3f, pref * 0.45f) + right * weave * 3f;
                    tMove = MoveTo(dest, 1.5f, out _);
                    tRun = !focus.Visible && dist > 15f;
                    tCrouch = false;
                    AimWhileMoving(true);
                    if (!focus.Visible) { tAim = !tRun; tAimPoint = enemy + Vector3.up * 1.3f; }
                    if (dist < 2.5f) tMove = -dirE * 0.4f + right * weave * 0.6f;
                    break;
                }
                case TacticState.Flank:
                {
                    tMove = MoveTo(flankPoint, 2.5f, out bool there);
                    tRun = !focus.Visible;
                    tCrouch = false;
                    AimWhileMoving(true);
                    if (!focus.Visible) { tAim = false; }
                    if (there || Time.time - stateSince > 14f) tacticTimer = 0f;
                    break;
                }
                default:   // Engage: strafe at a good range, crouch now and then when standing still
                {
                    SetState(TacticState.Engage, TacticNote);
                    Vector3 rangeFix = dist > pref * 1.25f ? dirE : dist < pref * 0.6f ? -dirE : Vector3.zero;
                    Vector3 wish = (rangeFix * 0.8f + right * weave * 0.75f);
                    tMove = wish.sqrMagnitude > 0.01f ? Avoid(wish.normalized) * Mathf.Min(1f, wish.magnitude) : Vector3.zero;
                    tRun = false;
                    tCrouch = false;
                    AimWhileMoving(true);
                    if (!focus.Visible) { tAim = true; tAimPoint = enemy + Vector3.up * 1.3f; tMove = MoveTo(enemy, pref * 0.7f, out _); }
                    break;
                }
            }
            prevTargetHealth = currentTargetHealth != null ? currentTargetHealth.Health : prevTargetHealth;
        }

        /// <summary>A point beside the enemy (70 to 100 degrees round from where the bot is now), at the bot's range, on the NavMesh.</summary>
        private bool TryFlankPoint(Vector3 enemy, out Vector3 point)
        {
            point = default;
            Vector3 fromEnemy = Flat(transform.position - enemy);
            float r = Mathf.Clamp(fromEnemy.magnitude, 10f, PreferredRange() * 1.2f);
            Vector3 baseDir = fromEnemy.sqrMagnitude > 0.01f ? fromEnemy.normalized : Vector3.forward;
            foreach (float a in new[] { 85f, -85f, 70f, -70f, 100f, -100f })
            {
                Vector3 c = enemy + Quaternion.Euler(0f, a * (avoidSide >= 0 ? 1f : -1f), 0f) * baseDir * r;
                if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                point = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>Where to go for the storm (none when the bot is safe), as the existing zone steering decides it.</summary>
        private bool TryGetZoneDestination(out Vector3 dest, out bool run)
        {
            dest = default;
            run = false;
            if (!Railbound.BR.BRZoneManager.TryGetSafeCircle(out Vector3 center, out float radius, out bool shrinking)) return false;
            Vector3 pos = transform.position;
            Vector2 offset = new Vector2(pos.x - center.x, pos.z - center.z);
            float dist = offset.magnitude;
            var zone = Railbound.BR.BRZoneManager.Instance;
            float opening = zone != null ? Mathf.Max(1f, zone.InitialRadius) : Mathf.Max(1f, radius);
            float margin = shrinking ? 14f : 6f;
            bool outside = dist > radius - margin;
            bool lateGame = radius < opening * 0.45f && dist > radius * 0.55f;
            if (!outside && !lateGame) return false;
            Vector2 side = dist > 0.5f ? offset / dist : Vector2.right;
            float aim = Mathf.Max(0f, radius * 0.45f);
            dest = new Vector3(center.x + side.x * aim, pos.y, center.z + side.y * aim);
            run = dist > radius - margin || dist > radius * 0.8f;
            return true;
        }
    }
}
