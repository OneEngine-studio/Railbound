using System.Collections;
using JUTPS.PhysicsScripts;
using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// What a networked body does when it is thrown or dies on a peer that does not simulate it (a bot or another player on a client),
    /// and the throw on the peer that does.
    ///
    /// Death. JU's own death (KillCharacter, on the simulating peer) ragdolls the body and stops it from getting up. A copy on another
    /// machine used to switch the ragdoll on and nothing else: after 3 s the ragdoll controller stood the corpse up again, its
    /// NetworkTransform fought the falling body, and a running enemy dropped on the spot. Here the body never gets up, stops following the
    /// network, keeps the speed it was moving at, gets a push from the killing shot when this machine fired it, settles with damped limbs,
    /// is frozen once EVERY bone has come to rest (not only the hips: arms used to freeze in the air), lies for a while and then sinks out
    /// of sight.
    ///
    /// Knockdown (a vehicle hit). <see cref="Knockdown"/> throws the body with the impact's velocity, on every peer, and leaves it lying;
    /// the simulating peer lets JU's ragdoll get up, the copies blend back up when it says so (<see cref="Recover"/>). If the hit (or the
    /// next bullet) kills, the knockdown turns into a death without losing the throw.
    /// </summary>
    public static class CorpseRagdoll
    {
        private const float MaxInheritSpeed = 7f;        // m/s a body may keep from its movement
        private const float MaxImpactSpeed = 22f;        // m/s a vehicle may throw a body
        private const float ShotPush = 2.6f;              // m/s the killing shot adds to the bone it hit
        private const float ShotPushBody = 0.9f;          // m/s it adds to the rest of the body
        private const float BoneAngularDrag = 2.5f;       // limbs swing and settle like a heavy body, not a rag (JU sets 0.5)
        private const float SettleSpeed = 0.12f;          // a bone counts as resting below this (m/s)
        private const float SettleSeconds = 1f;           // the whole body resting this long freezes it
        private const float MaxSimulatedSeconds = 9f;     // frozen after this at the latest
        private const float CorpseSeconds = 40f;          // a body lies this long once it has settled
        private const float SinkSeconds = 3f;             // then sinks into the ground over this long and is hidden
        private const float SinkDepth = 0.8f;

        /// <summary>Ragdolls a body that died on another machine. <paramref name="velocity"/> is how it was moving.</summary>
        public static void Begin(GameObject body, Vector3 velocity, bool hasShot, Vector3 shotPoint, Vector3 shotDirection)
        {
            var ragdoll = Prepare(body);
            if (ragdoll == null) return;
            ragdoll.TimeToGetUp = 900f;   // like JU's KillCharacter: a dead body never gets up
            var runner = Runner.On(body);
            // thrown a moment ago (a vehicle): it is already flying, keep that motion and only end it as a corpse
            if (runner.Thrown) { runner.BecomeCorpse(); return; }
            runner.PlayDeath(ragdoll, Vector3.ClampMagnitude(velocity, MaxInheritSpeed), hasShot, shotPoint, shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : Vector3.zero);
        }

        /// <summary>
        /// Throws a body (a vehicle hit it) with <paramref name="velocity"/>, the part of it struck spinning it over. On the simulating peer
        /// pass <paramref name="simulating"/>: JU then decides when it gets up. A copy lies until <see cref="Recover"/>.
        /// </summary>
        public static void Knockdown(GameObject body, Vector3 velocity, Vector3 point, bool simulating)
        {
            var ragdoll = Prepare(body, !simulating);
            if (ragdoll == null) return;
            if (simulating) ragdoll.TimeToGetUp = Mathf.Min(ragdoll.TimeToGetUp, 1.6f);   // a living body gets up a moment after it lands
            else ragdoll.TimeToGetUp = 900f;                                               // a copy waits for the simulating peer
            Runner.On(body).PlayThrow(ragdoll, Vector3.ClampMagnitude(velocity, MaxImpactSpeed), point);
        }

        /// <summary>The simulating peer's body is back on its feet: the copy gets up too and follows the network again.</summary>
        public static void Recover(GameObject body)
        {
            if (body == null) return;
            var runner = body.GetComponent<Runner>();
            if (runner != null && runner.Dead) return;
            var ragdoll = body.GetComponent<AdvancedRagdollController>();
            if (runner != null) runner.Stop();
            if (ragdoll == null || ragdoll.State == AdvancedRagdollController.RagdollState.Animated) { Follow(body, true); return; }
            ragdoll.TimeToGetUp = 3f;
            ragdoll.State = AdvancedRagdollController.RagdollState.WaitStablePosition;   // JU stores the pose and blends up to the animator
            Runner.On(body).FollowAfter(0.6f);
        }

        /// <summary>The server brought the body back (a new round): it follows the network and the animator again.</summary>
        public static void End(GameObject body)
        {
            if (body == null) return;
            var runner = body.GetComponent<Runner>();
            if (runner != null) { runner.Stop(); runner.Reset(); }
            var ragdoll = body.GetComponent<AdvancedRagdollController>();
            if (ragdoll != null && ragdoll.State != AdvancedRagdollController.RagdollState.Animated)
            {
                if (ragdoll.Hips != null && ragdoll.HipsParent != null) ragdoll.Hips.SetParent(ragdoll.HipsParent);
                ragdoll.SetActiveRagdoll(false);
                ragdoll.State = AdvancedRagdollController.RagdollState.Animated;
                ragdoll.TimeToGetUp = 3f;
            }
            Follow(body, true);
        }

        private static AdvancedRagdollController Prepare(GameObject body, bool stopFollowing = true)
        {
            if (body == null) return null;
            var ragdoll = body.GetComponent<AdvancedRagdollController>();
            if (ragdoll == null) return null;
            ragdoll.CancelInvoke();
            ragdoll.State = AdvancedRagdollController.RagdollState.Ragdolled;
            // the body now moves itself: the network's root position would only fight the ragdoll
            if (stopFollowing) Follow(body, false);
            return ragdoll;
        }

        private static void Follow(GameObject body, bool on)
        {
            var nt = body.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.enabled = on;
            if (on) { var rb = body.GetComponent<Rigidbody>(); if (rb != null && body.GetComponent<Unity.Netcode.NetworkObject>() is var no && no != null && !no.IsOwner) rb.isKinematic = true; }
        }

        private sealed class Runner : MonoBehaviour
        {
            private Coroutine routine;
            private AdvancedRagdollController ragdoll;
            private float thrownAt = -99f;
            private bool throwPending;   // the throw is waiting for the bones to switch to physics

            public bool Dead { get; private set; }
            public bool Thrown => Time.time - thrownAt < 4f;

            public static Runner On(GameObject body) => body.GetComponent<Runner>() ?? body.AddComponent<Runner>();

            public void Reset() { Dead = false; thrownAt = -99f; throwPending = false; }

            public void PlayDeath(AdvancedRagdollController r, Vector3 velocity, bool hasShot, Vector3 point, Vector3 dir)
            {
                Stop();
                ragdoll = r; Dead = true;
                routine = StartCoroutine(Death(velocity, hasShot, point, dir));
            }

            public void PlayThrow(AdvancedRagdollController r, Vector3 velocity, Vector3 point)
            {
                bool wasDead = Dead;
                Stop();
                ragdoll = r; thrownAt = Time.time;
                routine = StartCoroutine(Throw(velocity, point, wasDead));
            }

            public void BecomeCorpse()
            {
                if (Dead) return;
                Dead = true;
                if (ragdoll == null) ragdoll = GetComponent<AdvancedRagdollController>();
                // the kill usually arrives a frame after the hit, before the throw has reached the bones: let the throw run, it settles the corpse
                if (throwPending && routine != null) return;
                Stop();
                routine = StartCoroutine(SettleAndRest());
            }

            public void FollowAfter(float seconds) { Stop(); routine = StartCoroutine(FollowLater(seconds)); }

            public void Stop()
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                throwPending = false;
                if (ragdoll != null && ragdoll.RagdollBones != null && ragdoll.RagdollEnabled)
                    foreach (var b in ragdoll.RagdollBones) if (b != null) b.isKinematic = false;
            }

            private IEnumerator WaitForPhysicsBones()
            {
                // the ragdoll controller switches the bones to physics in its own Update
                float waited = 0f;
                while (ragdoll != null && !ragdoll.RagdollEnabled && waited < 1f) { waited += Time.deltaTime; yield return null; }
                yield return new WaitForFixedUpdate();
                if (ragdoll != null && ragdoll.RagdollBones != null)
                    foreach (var b in ragdoll.RagdollBones) if (b != null) { b.angularDrag = BoneAngularDrag; b.drag = 0.05f; }
            }

            private IEnumerator Death(Vector3 velocity, bool hasShot, Vector3 point, Vector3 dir)
            {
                yield return WaitForPhysicsBones();
                if (ragdoll == null || ragdoll.RagdollBones == null || ragdoll.RagdollBones.Length == 0) yield break;

                // keep the speed it was moving at, and push the bone the shot hit (and a little of the rest) along the bullet
                Rigidbody hitBone = hasShot && dir != Vector3.zero ? Nearest(point) : null;
                foreach (var b in ragdoll.RagdollBones)
                {
                    if (b == null || b.isKinematic) continue;
                    Vector3 v = velocity;
                    if (hitBone != null) v += dir * (b == hitBone ? ShotPush : ShotPushBody);
                    b.velocity = v;
                }
                yield return SettleAndRest();
            }

            private IEnumerator Throw(Vector3 velocity, Vector3 point, bool dead)
            {
                throwPending = true;
                yield return WaitForPhysicsBones();
                throwPending = false;
                if (ragdoll == null || ragdoll.RagdollBones == null || ragdoll.RagdollBones.Length == 0) yield break;
                // the whole body takes the vehicle's speed; the part struck takes a little more, which tips the body over the bonnet
                Rigidbody struck = Nearest(point);
                foreach (var b in ragdoll.RagdollBones)
                {
                    if (b == null || b.isKinematic) continue;
                    b.velocity = velocity * (b == struck ? 1.15f : 1f);
                    b.angularVelocity = Vector3.zero;
                }
                if (dead || Dead) yield return SettleAndRest();
            }

            private IEnumerator SettleAndRest()
            {
                if (ragdoll == null || ragdoll.RagdollBones == null) yield break;
                // let it fall and settle (every bone, so no limb freezes in the air), then freeze it where it lies
                float still = 0f, t = 0f;
                while (t < MaxSimulatedSeconds && still < SettleSeconds)
                {
                    yield return new WaitForFixedUpdate();
                    t += Time.fixedDeltaTime;
                    float fastest = 0f;
                    foreach (var b in ragdoll.RagdollBones) if (b != null && !b.isKinematic) fastest = Mathf.Max(fastest, b.velocity.magnitude);
                    still = fastest < SettleSpeed ? still + Time.fixedDeltaTime : 0f;
                }
                foreach (var b in ragdoll.RagdollBones)
                {
                    if (b == null) continue;
                    b.velocity = Vector3.zero; b.angularVelocity = Vector3.zero;
                    b.isKinematic = true;
                }

                // it lies there for a while, then sinks out of sight (no body popping out of existence)
                yield return new WaitForSeconds(CorpseSeconds);
                var hipsT = ragdoll.Hips;
                if (hipsT != null)
                {
                    foreach (var c in hipsT.GetComponentsInChildren<Collider>()) if (c != null) c.enabled = false;
                    Vector3 from = hipsT.position;
                    float s = 0f;
                    while (s < SinkSeconds && hipsT != null)
                    {
                        s += Time.deltaTime;
                        float k = s / SinkSeconds;
                        hipsT.position = from + Vector3.down * (SinkDepth * k * k);
                        yield return null;
                    }
                    foreach (var r in GetComponentsInChildren<Renderer>()) if (r != null) r.enabled = false;
                    if (hipsT != null) foreach (var r in hipsT.GetComponentsInChildren<Renderer>()) if (r != null) r.enabled = false;
                }
                routine = null;
            }

            private IEnumerator FollowLater(float seconds)
            {
                yield return new WaitForSeconds(seconds);
                Follow(gameObject, true);
                thrownAt = -99f;
                routine = null;
            }

            private Rigidbody Nearest(Vector3 point)
            {
                Rigidbody best = null; float bd = float.MaxValue;
                foreach (var b in ragdoll.RagdollBones)
                {
                    if (b == null) continue;
                    float d = (b.worldCenterOfMass - point).sqrMagnitude;
                    if (d < bd) { bd = d; best = b; }
                }
                return best;
            }
        }
    }
}
