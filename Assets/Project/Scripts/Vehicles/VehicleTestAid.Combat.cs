using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Test modes for vehicle combat and seat poses:
    ///  * <c>runover</c> + <c>target</c>: the driver steers at the other player, who stands 22 m in front of the car;
    ///  * <c>park</c> + <c>shoot</c>: one player sits in a parked car, the other shoots it with real bullets until it blows up
    ///    (run the server with <c>-vehicle-health n</c> for a short test);
    ///  * <c>seats</c>: every vehicle model, every seat, a close side-on screenshot of the pose.
    /// </summary>
    public partial class VehicleTestAid
    {
        private static GameObject OtherPlayer()
        {
            foreach (var p in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
                if (p != null && p.IsSpawned && !p.IsOwner) return p.gameObject;
            return null;
        }

        private static float HealthOf(GameObject go)
        {
            var h = go != null ? go.GetComponent<JUHealth>() : null;
            return h != null ? h.Health : -1f;
        }

        private void Teleport(Vector3 spot, Quaternion rot)
        {
            var me = Me;
            if (Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) spot = hit.point + Vector3.up * 0.1f;
            var nt = me.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(spot, rot, me.transform.localScale);
            else me.transform.SetPositionAndRotation(spot, rot);
            var rb = me.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = spot; rb.velocity = Vector3.zero; }
        }

        private IEnumerator TakeWheel(RailboundVehicle v)
        {
            WalkTo(v, v.DriverSeat);
            yield return new WaitForSecondsRealtime(1f);
            v.RequestEnter(Me, v.DriverSeat);
            yield return WaitSeat(v, v.DriverSeat);
            float t0 = Time.realtimeSinceStartup;
            while (!v.IsSimulating && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        }

        // ---------------------------------------------------------------- run-over
        private IEnumerator RunOver()
        {
            yield return WaitForPlayerAndVehicles();
            var v = Nearest(Me.transform.position, false);
            yield return TakeWheel(v);
            Debug.Log("[VehicleTest] runover: at the wheel of " + v.DebugLine());

            // the target walks in front of the car
            GameObject target = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                target = OtherPlayer();
                if (target != null && Vector3.Distance(target.transform.position, v.transform.position) < 40f) break;
                target = null;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (target == null) { Debug.Log("[VehicleTest] runover: no target came"); yield break; }
            yield return new WaitForSecondsRealtime(2f);

            float before = HealthOf(target), hitSpeed = 0f, hitDamage = 0f;
            bool hit = false;
            System.Action<RailboundVehicle, GameObject, float, float> onHit = (veh, victim, speed, dmg) =>
            {
                if (veh == v && victim == target) { hit = true; hitSpeed = speed; hitDamage = dmg; }
            };
            RailboundVehicle.RanOver += onHit;
            Debug.Log($"[VehicleTest] runover: driving at {target.name} {Vector3.Distance(target.transform.position, v.transform.position):F1} m away, its health {before:F0}");
            t0 = Time.realtimeSinceStartup;
            bool shot = false;
            while (!hit && Time.realtimeSinceStartup - t0 < 14f)
            {
                Vector3 to = target.transform.position - v.transform.position; to.y = 0f;
                float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(v.transform.forward, Vector3.up), to, Vector3.up);
                Steer = Mathf.Clamp(angle / 25f, -1f, 1f);
                Throttle = Mathf.Abs(angle) > 70f ? 0.4f : 1f;
                Brake = 0f; Handbrake = 0f;
                if (!shot && to.magnitude < 9f) { Shot("ro_impact"); shot = true; }
                yield return null;
            }
            RailboundVehicle.RanOver -= onHit;
            Throttle = 0f; Steer = 0f; Brake = 1f;
            yield return new WaitForSecondsRealtime(1.5f);   // the server's health arrives
            Brake = 0f; Handbrake = 1f;
            Debug.Log($"[VehicleTest] runover: hit={hit} at {hitSpeed * 3.6f:F0} km/h for {hitDamage:F0} damage; target health {before:F0} -> {HealthOf(target):F0}");
            Shot("ro_after");
            yield return new WaitForSecondsRealtime(4f);
            Debug.Log($"[VehicleTest] runover: target health later {HealthOf(target):F0}");
            Throttle = Brake = Steer = Handbrake = null;
            Debug.Log("[VehicleTest] DONE runover");
        }

        private IEnumerator Target()
        {
            yield return WaitForPlayerAndVehicles();
            RailboundVehicle v = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f)
            {
                foreach (var c in RailboundVehicle.All)
                    if (c != null && c.Driver != null && c.Driver != Me) { v = c; break; }
                if (v != null) break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (v == null) { Debug.Log("[VehicleTest] target: nobody drives"); yield break; }
            yield return new WaitForSecondsRealtime(1f);
            Vector3 fwd = Vector3.ProjectOnPlane(v.transform.forward, Vector3.up).normalized;
            Teleport(v.transform.position + fwd * 22f, Quaternion.LookRotation(-fwd));
            float start = HealthOf(Me);
            Debug.Log($"[VehicleTest] target: standing 22 m in front of {v.name}, health {start:F0}");
            t0 = Time.realtimeSinceStartup;
            float last = start;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                float h = HealthOf(Me);
                if (Mathf.Abs(h - last) > 0.5f) { Debug.Log($"[VehicleTest] target: health {last:F0} -> {h:F0}"); last = h; Shot("tg_hit"); }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Debug.Log($"[VehicleTest] target: health {start:F0} -> {HealthOf(Me):F0}, dead={(Me.GetComponent<JUHealth>() != null && Me.GetComponent<JUHealth>().IsDead)}");
            Debug.Log("[VehicleTest] DONE target");
        }

        // ---------------------------------------------------------------- destruction
        private IEnumerator Park()
        {
            yield return WaitForPlayerAndVehicles();
            var v = Nearest(Me.transform.position, false);
            yield return TakeWheel(v);
            Handbrake = 1f; Throttle = 0f;
            Debug.Log($"[VehicleTest] park: in {v.name}, health {HealthOf(Me):F0}, vehicle {v.Health:F0}/{v.MaxHealth:F0}");
            float t0 = Time.realtimeSinceStartup;
            float lastHp = v.Health;
            while (Time.realtimeSinceStartup - t0 < 120f && !v.IsWrecked)
            {
                if (Mathf.Abs(v.Health - lastHp) > 0.5f) { lastHp = v.Health; }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            var h = Me.GetComponent<JUHealth>();
            Debug.Log($"[VehicleTest] park: vehicle wrecked={v.IsWrecked} hp={v.Health:F0}; me health={HealthOf(Me):F0} dead={(h != null && h.IsDead)} seat={v.SeatOf(Me)}");
            Shot("pk_after");
            Throttle = Brake = Steer = Handbrake = null;
            Debug.Log("[VehicleTest] DONE park");
        }

        private IEnumerator Shoot()
        {
            yield return WaitForPlayerAndVehicles();
            RailboundVehicle v = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f)
            {
                foreach (var c in RailboundVehicle.All)
                    if (c != null && c.Driver != null && c.Driver != Me && c.SpeedKmh < 3f) { v = c; break; }
                if (v != null) break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (v == null) { Debug.Log("[VehicleTest] shoot: no occupied vehicle"); yield break; }
            yield return new WaitForSecondsRealtime(2f);

            Vector3 side = Vector3.ProjectOnPlane(v.transform.right, Vector3.up).normalized;
            Vector3 center = v.transform.TransformPoint(v.LocalBounds.center);
            Teleport(center + side * 12f, Quaternion.LookRotation(-side));
            yield return new WaitForSecondsRealtime(1.5f);
            var pc = JUGameManager.PlayerController;
            var victim = v.Driver;
            float victimBefore = HealthOf(victim);
            Debug.Log($"[VehicleTest] shoot: 12 m from {v.name} ({v.Health:F0}/{v.MaxHealth:F0}), its driver health {victimBefore:F0}, weapon {(pc.WeaponInUseRightHand != null ? pc.WeaponInUseRightHand.ItemName : "none")}");
            Shot("sh_aim");

            int shots = 0;
            float smokeShot = 0f;
            t0 = Time.realtimeSinceStartup;
            while (!v.IsWrecked && Time.realtimeSinceStartup - t0 < 60f)
            {
                var w = pc.WeaponInUseRightHand;
                if (w == null) { Debug.Log("[VehicleTest] shoot: no weapon in hand"); break; }
                center = v.transform.TransformPoint(v.LocalBounds.center);
                Vector3 dir = (center - (pc.transform.position + Vector3.up * 1.5f)).normalized;
                pc.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up));
                if (w.BulletsAmounts <= 0) { w.Reload(); yield return new WaitForSecondsRealtime(2f); continue; }
                pc.FiringMode = true; pc.FiringModeIK = true; pc.CurrentTimeToDisableFireMode = 0;
                pc.LookAtPosition = center;
                w.SetWeaponOrientation(pc.transform.position + Vector3.up * 1.5f, dir);
                w.UseItem();
                shots++;
                if (smokeShot == 0f && v.Health01 < 0.3f) { smokeShot = 1f; yield return new WaitForSecondsRealtime(1.2f); Shot("sh_smoking"); }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Debug.Log($"[VehicleTest] shoot: {shots} shots, vehicle wrecked={v.IsWrecked} hp={v.Health:F0}");
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("sh_explosion");
            yield return new WaitForSecondsRealtime(2.5f);
            var vh = victim != null ? victim.GetComponent<JUHealth>() : null;
            Debug.Log($"[VehicleTest] shoot: driver health {victimBefore:F0} -> {HealthOf(victim):F0} dead={(vh != null && vh.IsDead)}");
            Shot("sh_wreck");
            yield return new WaitForSecondsRealtime(4f);
            Shot("sh_wreck_burning");
            Debug.Log("[VehicleTest] DONE shoot");
        }

        // ---------------------------------------------------------------- seat poses
        private IEnumerator SeatTour()
        {
            yield return WaitForPlayerAndVehicles();
            var done = new HashSet<string>();
            int seatShot = 0;
            while (true)
            {
                RailboundVehicle v = null; float best = float.MaxValue;
                foreach (var c in RailboundVehicle.All)
                {
                    if (c == null || done.Contains(c.DisplayName) || c.HasDriver || c.IsWrecked || !c.IsUpright) continue;
                    float d = Vector3.Distance(c.transform.position, Me.transform.position);
                    if (d < best) { best = d; v = c; }
                }
                if (v == null) break;
                done.Add(v.DisplayName);
                Debug.Log($"[VehicleTest] seats: {v.DisplayName} ({v.name}), {v.SeatCount} seats");

                for (int seat = 0; seat < v.SeatCount; seat++)
                {
                    if (v.SeatOf(Me) < 0) { WalkTo(v, seat); yield return new WaitForSecondsRealtime(1f); v.RequestEnter(Me, seat); }
                    else v.RequestMove(Me, seat);
                    yield return WaitSeat(v, seat);
                    yield return new WaitForSecondsRealtime(1.6f);   // the sitting pose settles, the hands reach the wheel

                    var def = v.SeatDef(seat);
                    float sideSign = def.Hips.x >= 0f ? 1f : -1f;
                    var b = v.LocalBounds;
                    int s = seat;
                    // from outside the door at head height, looking at the seat (side windows are open glass)
                    VehicleLocalPlayer.TestCamera = () => (v.transform.TransformPoint(new Vector3(sideSign * (b.extents.x + 1.6f), def.Hips.y + 0.55f, def.Hips.z + 0.4f)),
                                                           v.SeatHipsWorld(s) + v.transform.up * 0.35f);
                    yield return new WaitForSecondsRealtime(0.8f);   // motion blur from the camera cut settles
                    Shot($"seat{seatShot++}_{v.DisplayName.Replace(' ', '_')}_{def.Name.Replace(' ', '_')}_side");
                    yield return new WaitForEndOfFrame();   // the capture happens at the end of this frame: keep the pose until then
                    yield return null;
                    // and from the front through the windscreen
                    VehicleLocalPlayer.TestCamera = () => (v.transform.TransformPoint(new Vector3(def.Hips.x * 0.6f, def.Hips.y + 1.0f, b.max.z + 1.6f)),
                                                           v.SeatHipsWorld(s) + v.transform.up * 0.45f);
                    yield return new WaitForSecondsRealtime(0.8f);
                    Shot($"seat{seatShot++}_{v.DisplayName.Replace(' ', '_')}_{def.Name.Replace(' ', '_')}_front");
                    yield return new WaitForEndOfFrame();
                    yield return null;
                    VehicleLocalPlayer.TestCamera = null;

                    var anim = Me.GetComponent<Animator>();
                    var hips = anim != null ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
                    var head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                    var lh = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                    string grips = "";
                    if (def.Driver && lh != null && v.WheelGrips(out Vector3 gl, out Vector3 gr, out _))
                    {
                        var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        grips = $" leftHand->grip {Vector3.Distance(lh.position, gl):F2} m, rightHand->grip {Vector3.Distance(rh.position, gr):F2} m";
                    }
                    var lf = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                    var rf = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightFoot) : null;
                    if (lf != null && rf != null)
                        Debug.Log($"[VehicleTest] seats: {v.DisplayName} {def.Name}: feet local L={v.transform.InverseTransformPoint(lf.position):F2} R={v.transform.InverseTransformPoint(rf.position):F2} (floor {def.FloorY:F2}, hips {def.Hips:F2})");
                    Debug.Log($"[VehicleTest] seats: {v.DisplayName} {def.Name}: hipsErr={(hips != null ? Vector3.Distance(hips.position, v.SeatHipsWorld(seat)) : -1f):F2} " +
                              $"head local={(head != null ? v.transform.InverseTransformPoint(head.position).ToString("F2") : "?")} roof={b.max.y:F2}{grips}");
                }
                v.RequestExit(Me);
                yield return WaitSeat(v, -1);
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Debug.Log("[VehicleTest] DONE seats");
        }
    }
}
