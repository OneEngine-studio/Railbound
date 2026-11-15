using UnityEngine;
using JUTPS.FX;
using JUTPS.WeaponSystem;

namespace Railbound.BR
{
    /// <summary>
    /// A bullet that only LOOKS like one: the tracer (trail, mesh, light) of the weapon's real bullet prefab flies from the
    /// muzzle to the point where the server's bullet hit, then the impact effect is spawned. It has no collider, no rigidbody
    /// and no damage: the real bullet of a bot exists on the server only, and the damage comes from there.
    /// </summary>
    public sealed class BRBulletVisual : MonoBehaviour
    {
        private const float DefaultSpeed = 260f;
        private const float MaxLife = 3f;
        private const float FreeFlightDistance = 220f;

        private Vector3 target;
        private Vector3 normal;
        private float speed;
        private float life;
        private SurfaceFX[] impactFx;
        private Quaternion impactRotation;

        public static void Spawn(GameObject bulletPrefab, Vector3 start, Vector3 end, Quaternion rotation, Vector3 hitNormal)
        {
            if (bulletPrefab == null) return;
            var proto = bulletPrefab.GetComponent<Bullet>();

            var go = Instantiate(bulletPrefab, start, rotation);

            // strip everything that acts: logic first (Bullet requires a Rigidbody), then physics
            var bullet = go.GetComponent<Bullet>();
            if (bullet != null) DestroyImmediate(bullet);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) DestroyImmediate(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

            var visual = go.AddComponent<BRBulletVisual>();
            visual.speed = proto != null && proto.BulletVelocity > 1f ? proto.BulletVelocity : DefaultSpeed;
            visual.impactFx = proto != null ? proto.ImpactFX : null;
            visual.normal = hitNormal;
            bool hasEnd = end != Vector3.zero;
            visual.target = hasEnd ? end : start + (rotation * Vector3.forward) * FreeFlightDistance;
            visual.impactRotation = hasEnd && hitNormal != Vector3.zero ? Quaternion.LookRotation(hitNormal) : rotation;
            if (!hasEnd) visual.impactFx = null;   // it hit nothing: no impact
            Destroy(go, MaxLife + 1f);
        }

        private void Update()
        {
            life += Time.deltaTime;
            Vector3 to = target - transform.position;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step || life > MaxLife)
            {
                transform.position = target;
                if (impactFx != null && impactFx.Length > 0)
                    SurfaceFX.InstantiateParticleFX(impactFx, "Untagged", target, impactRotation, null);
                Destroy(gameObject);
                return;
            }
            transform.position += to.normalized * step;
            transform.rotation = Quaternion.LookRotation(to.normalized);
        }
    }
}
