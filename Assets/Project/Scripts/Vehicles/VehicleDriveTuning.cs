using NWH.Common.CoM;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Drive physics on top of the NWH vehicles (their prefabs are not edited by hand), measured with <c>-cartest</c>:
    ///  * Bumps launched the cars: the sports car's short, stiff suspension (0.2 m of travel, equal bump and rebound damping) threw it
    ///    up at 5 m/s from a small bump at 88 km/h, 0.8 s in the air and 2.8 m high. Longer travel and a rebound much stronger than the
    ///    bump keep the wheels on the ground and soak the bump up instead of springing the body off it.
    ///  * In the air the vehicles spun like toys (the monster truck rolled at ~220 deg/s, over onto its roof): nothing damps a rigidbody's
    ///    rotation. In the air the rotation is now damped, so a vehicle holds its attitude and lands on its wheels.
    ///  * Floaty landings: once all four wheels are off the ground a little extra gravity (x1.35) brings it back down with weight.
    ///  * The sports car gets downforce that grows with the square of its speed, and every vehicle a lower centre of mass.
    /// Only the simulating peer (the driver's machine, or the server for a parked vehicle) runs the physics; this does nothing elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleDriveTuning : MonoBehaviour
    {
        private struct Profile
        {
            public float travel;          // suspension travel multiplier
            public float bump;            // bump damping multiplier
            public float reboundRatio;    // rebound damping = this x the (new) bump damping, at least
            public float comDrop;         // metres the centre of mass is lowered
            public float downforce;       // N per (m/s)^2
            public float airGravity;      // gravity multiplier with every wheel off the ground
            public float airAngularDamping;   // 1/s
        }

        private static Profile For(string displayName)
        {
            string n = (displayName ?? "").ToUpperInvariant();
            if (n.Contains("SPORT")) return new Profile { travel = 1.35f, bump = 0.8f, reboundRatio = 1.6f, comDrop = 0.12f, downforce = 9f, airGravity = 1.35f, airAngularDamping = 2.5f };
            if (n.Contains("MONSTER")) return new Profile { travel = 1f, bump = 0.9f, reboundRatio = 1.8f, comDrop = 0.35f, downforce = 0f, airGravity = 1.35f, airAngularDamping = 3f };
            return new Profile { travel = 1f, bump = 1f, reboundRatio = 1.4f, comDrop = 0.1f, downforce = 0f, airGravity = 1.3f, airAngularDamping = 2f };
        }

        private RailboundVehicle vehicle;
        private Profile p;
        private float airborneFor;
        private bool applied;

        public static void Attach(RailboundVehicle v)
        {
            if (v == null || v.GetComponent<VehicleDriveTuning>() != null) return;
            var t = v.gameObject.AddComponent<VehicleDriveTuning>();
            t.vehicle = v;
            t.p = For(v.DisplayName);
            t.Apply();
        }

        private void Apply()
        {
            if (applied || vehicle == null || vehicle.Controller == null) return;
            applied = true;
            var wheels = vehicle.Controller.powertrain.wheels;
            for (int i = 0; i < wheels.Count; i++)
            {
                var w = wheels[i].wheelUAPI;
                if (w == null) continue;
                w.SpringMaxLength *= p.travel;
                float bump = w.DamperBumpRate * p.bump;
                w.DamperBumpRate = bump;
                w.DamperReboundRate = Mathf.Max(w.DamperReboundRate, bump * p.reboundRatio);
            }
            var com = vehicle.GetComponent<VariableCenterOfMass>();
            if (com != null && p.comDrop > 0f)
            {
                com.useDefaultCenterOfMass = false;
                com.centerOfMass += Vector3.down * p.comDrop;
            }
            else if (vehicle.Body != null && p.comDrop > 0f) vehicle.Body.centerOfMass += Vector3.down * p.comDrop;
            Debug.Log($"[VehicleTuning] {vehicle.DisplayName}: travel x{p.travel}, bump x{p.bump}, rebound {p.reboundRatio}x bump, centre of mass -{p.comDrop} m, downforce {p.downforce}, air gravity x{p.airGravity}.");
        }

        private void FixedUpdate()
        {
            if (vehicle == null || !vehicle.IsSimulating || vehicle.Body == null || vehicle.Body.isKinematic || vehicle.Controller == null) return;
            var body = vehicle.Body;
            var wheels = vehicle.Controller.powertrain.wheels;
            int down = 0;
            for (int i = 0; i < wheels.Count; i++) if (wheels[i].wheelUAPI != null && wheels[i].wheelUAPI.IsGrounded) down++;

            float speed = body.velocity.magnitude;
            if (down > 0)
            {
                airborneFor = 0f;
                if (p.downforce > 0f) body.AddForce(-vehicle.transform.up * Mathf.Min(p.downforce * speed * speed, body.mass * 12f), ForceMode.Force);
                return;
            }

            airborneFor += Time.fixedDeltaTime;
            if (airborneFor < 0.08f) return;   // a wheel skipping over a bump is not a flight
            body.AddForce(Physics.gravity * (p.airGravity - 1f), ForceMode.Acceleration);
            body.angularVelocity *= Mathf.Exp(-p.airAngularDamping * Time.fixedDeltaTime);
        }
    }
}
