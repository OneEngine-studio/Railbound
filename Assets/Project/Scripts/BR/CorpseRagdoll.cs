using System.Collections;
using JUTPS.PhysicsScripts;
using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// How a networked body dies on a peer that does not simulate it (a bot or another player on a client).
    ///
    /// JU's own death (KillCharacter, on the server and the owner) ragdolls the body and stops it from getting up. A copy on another
    /// machine used to switch the ragdoll on and nothing else, which looked broken in three ways:
    ///  * after 3 s the ragdoll controller decided the body was resting and blended it back to the animator: the corpse stood up, and
    ///    fell again when the animator's dying state arrived;
    ///  * the copy's NetworkTransform kept writing the server's root position while the ragdoll moved the body, so it jittered;
    ///  * the ragdoll started from rest (the copy's rigidbody is kinematic), so a running enemy dropped on the spot.
    ///
    /// Here the body never gets up, stops following the network, keeps the speed it was moving at, gets a push from the killing shot
    /// when this machine fired it (at the bone it hit, along the bullet), and is frozen once it has settled so it does not twitch.
    /// </summary>
    public static class CorpseRagdoll
    {
        private const float MaxInheritSpeed = 7f;        // m/s a body may keep from its movement
        private const float ShotPush = 2.6f;              // m/s the killing shot adds to the bone it hit
        private const float ShotPushBody = 0.9f;          // m/s it adds to the rest of the body
        private const float SettleSpeed = 0.08f;          // the hips count as resting below this (m/s)
        private const float SettleSeconds = 1.2f;         // resting this long freezes the body
        private const float MaxSimulatedSeconds = 8f;     // frozen after this at the latest
        private const float CorpseSeconds = 40f;          // a body lies this long once it has settled
        private const float SinkSeconds = 3f;             // then sinks into the ground over this long and is hidden
        private const float SinkDepth = 0.8f;

        /// <summary>Ragdolls a body that died on another machine. <paramref name="velocity"/> is how it was moving.</summary>
        public static void Begin(GameObject body, Vector3 velocity, bool hasShot, Vector3 shotPoint, Vector3 shotDirection)
        {
            if (body == null) return;
            var ragdoll = body.GetComponent<AdvancedRagdollController>();
            if (ragdoll == null) return;
            ragdoll.TimeToGetUp = 900f;   // like JU's KillCharacter: a dead body never gets up
            ragdoll.CancelInvoke();
            ragdoll.State = AdvancedRagdollController.RagdollState.Ragdolled;

            // the body now moves itself: the server's root position would only fight the ragdoll
            var nt = body.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.enabled = false;

            var runner = body.GetComponent<Runner>() ?? body.AddComponent<Runner>();
            runner.Play(ragdoll, Vector3.ClampMagnitude(velocity, MaxInheritSpeed), hasShot, shotPoint, shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : Vector3.zero);
        }

        /// <summary>The server brought the body back (a new round): it follows the network and the animator again.</summary>
        public static void End(GameObject body)
        {
            if (body == null) return;
            var runner = body.GetComponent<Runner>();
            if (runner != null) runner.Stop();
            var ragdoll = body.GetComponent<AdvancedRagdollController>();
            if (ragdoll != null && ragdoll.State != AdvancedRagdollController.RagdollState.Animated)
            {
                if (ragdoll.Hips != null && ragdoll.HipsParent != null) ragdoll.Hips.SetParent(ragdoll.HipsParent);
                ragdoll.SetActiveRagdoll(false);
                ragdoll.State = AdvancedRagdollController.RagdollState.Animated;
                ragdoll.TimeToGetUp = 3f;
            }
            var nt = body.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.enabled = true;
            var rb = body.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;   // a copy is moved by its NetworkTransform
        }

        private sealed class Runner : MonoBehaviour
        {
            private Coroutine routine;
            private AdvancedRagdollController ragdoll;

            public void Play(AdvancedRagdollController r, Vector3 velocity, bool hasShot, Vector3 point, Vector3 dir)
            {
                Stop();
                ragdoll = r;
                routine = StartCoroutine(Run(velocity, hasShot, point, dir));
            }

            public void Stop()
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                if (ragdoll != null && ragdoll.RagdollBones != null)
                    foreach (var b in ragdoll.RagdollBones) if (b != null && ragdoll.RagdollEnabled) b.isKinematic = false;
            }

            private IEnumerator Run(Vector3 velocity, bool hasShot, Vector3 point, Vector3 dir)
            {
                // the ragdoll controller switches the bones to physics in its own Update
                float waited = 0f;
                while (ragdoll != null && !ragdoll.RagdollEnabled && waited < 1f) { waited += Time.deltaTime; yield return null; }
                if (ragdoll == null || ragdoll.RagdollBones == null || ragdoll.RagdollBones.Length == 0) yield break;
                yield return new WaitForFixedUpdate();

                // keep the speed it was moving at, and push the bone the shot hit (and a little of the rest) along the bullet
                Rigidbody hitBone = null;
                if (hasShot && dir != Vector3.zero)
                {
                    float best = float.MaxValue;
                    foreach (var b in ragdoll.RagdollBones)
                    {
                        if (b == null) continue;
                        float d = (b.worldCenterOfMass - point).sqrMagnitude;
                        if (d < best) { best = d; hitBone = b; }
                    }
                }
                foreach (var b in ragdoll.RagdollBones)
                {
                    if (b == null || b.isKinematic) continue;
                    Vector3 v = velocity;
                    if (hitBone != null) v += dir * (b == hitBone ? ShotPush : ShotPushBody);
                    b.velocity = v;
                }

                // let it fall and settle, then freeze it where it lies
                var hips = ragdoll.Hips != null && ragdoll.Hips.TryGetComponent(out Rigidbody hipsBody) ? hipsBody : ragdoll.RagdollBones[0];
                float still = 0f, t = 0f;
                while (t < MaxSimulatedSeconds && still < SettleSeconds)
                {
                    yield return new WaitForFixedUpdate();
                    t += Time.fixedDeltaTime;
                    if (hips == null) yield break;
                    still = hips.velocity.magnitude < SettleSpeed ? still + Time.fixedDeltaTime : 0f;
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
        }
    }
}
