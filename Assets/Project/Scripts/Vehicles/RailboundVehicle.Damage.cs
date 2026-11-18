using System;
using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Vehicle health, damage and destruction.
    ///
    ///  * Health is the server's (<see cref="netHealth"/>). Damage is caused where it happens and reported: a player's bullets and
    ///    grenades on that player's machine (<see cref="VehicleDamageHooks"/>), crashes on the machine that simulates the vehicle,
    ///    bots on the server. The server checks the attacker belongs to the reporting client and caps the rate.
    ///  * Damaged: grey smoke from the engine below 35%, fire below 15%.
    ///  * Destroyed (0): a fireball and a blast. Everyone inside dies, characters nearby take splash damage (the kill goes to whoever
    ///    did the last damage, the squad filter still protects teammates), nearby vehicles are hurt. The wreck is thrown up,
    ///    burns, can't be used and is removed after <see cref="WreckLifetime"/> seconds.
    /// </summary>
    public partial class RailboundVehicle
    {
        public enum DamageCause : byte { Bullet, Explosion, Crash, Blast }

        private const float MaxDamagePerReport = 400f;
        private const float MaxDamagePerSecond = 1200f;   // per reporting client
        private const float BlastRadius = 7.5f;
        private const float WreckLifetime = 90f;

        private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
            -1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> netWrecked = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private float localHealth = -1f;
        private bool localWrecked, wreckShown;
        private GameObject lastAttacker;
        private readonly Dictionary<ulong, (float window, float spent)> damageBudget = new Dictionary<ulong, (float, float)>();
        private float nextCrashDamage;

        public static event Action<RailboundVehicle, float, string> Damaged;   // vehicle, damage, cause (every peer that applies it)
        public static event Action<RailboundVehicle> Wrecked;

        /// <summary>Heavier vehicles take more punishment: about 900 for the offroader, up to 1800.</summary>
        public float MaxHealth => TestHealth > 0f ? TestHealth : Mathf.Round(Mathf.Clamp(body != null ? body.mass * 0.75f : 900f, 600f, 1800f));

        // test aid: "-vehicle-health n" gives every vehicle n health (a short destruction test)
        private static float testHealth = float.NaN;
        private static float TestHealth
        {
            get
            {
                if (float.IsNaN(testHealth))
                {
                    testHealth = 0f;
                    string[] a = Environment.GetCommandLineArgs();
                    for (int i = 0; i < a.Length - 1; i++)
                        if (string.Equals(a[i], "-vehicle-health", StringComparison.OrdinalIgnoreCase) && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) testHealth = v;
                }
                return testHealth;
            }
        }
        public float Health
        {
            get
            {
                float h = IsSpawned ? netHealth.Value : localHealth;
                return h < 0f ? MaxHealth : h;
            }
        }
        public float Health01 => Mathf.Clamp01(Health / Mathf.Max(1f, MaxHealth));
        public bool IsWrecked => IsSpawned ? netWrecked.Value : localWrecked;

        // ---------------------------------------------------------------- reporting
        /// <summary>
        /// Any peer: damage this machine caused to the vehicle (its player's bullet or grenade, a crash it simulated). The server
        /// applies it; a client forwards it.
        /// </summary>
        public void ReportDamage(float damage, GameObject attacker, DamageCause cause)
        {
            if (IsWrecked || damage <= 0f) return;
            damage = Mathf.Min(damage, MaxDamagePerReport);
            if (!IsSpawned)
            {
                if (!NetActive) ApplyDamage(damage, attacker, cause);
                return;
            }
            if (IsServer) { ApplyDamage(damage, attacker, cause); return; }

            bool hasAttacker = attacker != null && attacker.TryGetComponent(out NetworkObject ano) && ano.IsSpawned;
            ReportDamageServerRpc(damage, hasAttacker ? new NetworkObjectReference(attacker.GetComponent<NetworkObject>()) : default, hasAttacker, (byte)cause);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportDamageServerRpc(float damage, NetworkObjectReference attackerRef, bool hasAttacker, byte cause, ServerRpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            // only damage a client may cause: its own character as the attacker (or none, for a crash), at a sane rate
            GameObject attacker = null;
            if (hasAttacker && attackerRef.TryGet(out NetworkObject ano))
            {
                if (ano.OwnerClientId != sender) return;
                attacker = ano.gameObject;
            }
            if ((DamageCause)cause == DamageCause.Crash && OwnerClientId != sender) return;   // only the simulating peer reports crashes

            float now = Time.unscaledTime;
            damageBudget.TryGetValue(sender, out var b);
            if (now - b.window > 1f) b = (now, 0f);
            float allowed = Mathf.Max(0f, MaxDamagePerSecond - b.spent);
            float applied = Mathf.Min(Mathf.Clamp(damage, 0f, MaxDamagePerReport), allowed);
            damageBudget[sender] = (b.window, b.spent + applied);
            if (applied > 0f) ApplyDamage(applied, attacker, (DamageCause)cause);
        }

        // ---------------------------------------------------------------- authority
        private void ApplyDamage(float damage, GameObject attacker, DamageCause cause)
        {
            if (IsWrecked) return;
            float h = Mathf.Max(0f, Health - damage);
            if (IsSpawned) netHealth.Value = h; else localHealth = h;
            if (attacker != null) lastAttacker = attacker;
            Damaged?.Invoke(this, damage, cause.ToString());
            if (Application.isEditor || cause != DamageCause.Bullet)
                Debug.Log($"[Vehicle] {name} took {damage:F0} {cause} damage{(attacker != null ? " from " + attacker.name : "")}: {h:F0}/{MaxHealth:F0}.");
            if (h <= 0f) Wreck(cause);
        }

        private void Wreck(DamageCause cause)
        {
            if (IsSpawned) netWrecked.Value = true; else localWrecked = true;
            Vector3 center = transform.TransformPoint(localBounds.center);
            Debug.Log($"[Vehicle] {name} DESTROYED by {cause}{(lastAttacker != null ? " (" + lastAttacker.name + ")" : "")} at {center:F1}.");

            // everyone inside dies; the rest are thrown out by the blast below
            var inside = new HashSet<JUHealth>();
            var s = Seats;
            for (int i = 0; i < SeatCount; i++)
            {
                var go = CharacterOf(s[i]);
                var h = go != null ? go.GetComponent<JUHealth>() : null;
                if (h != null) inside.Add(h);
            }
            foreach (var h in inside) HurtCharacter(h, 500f);
            Publish(default);
            ReturnToServer(IsSimulating ? body.velocity : observedVelocity, Vector3.zero);

            // the blast: characters and other vehicles around
            var hurt = new HashSet<JUHealth>(inside);
            var hurtVehicles = new HashSet<RailboundVehicle> { this };
            foreach (var c in Physics.OverlapSphere(center, BlastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                float d = Vector3.Distance(center, c.bounds.ClosestPoint(center));   // ClosestPoint() refuses concave mesh colliders
                float k = 1f - Mathf.Clamp01(d / BlastRadius);
                var h = c.GetComponentInParent<JUHealth>();
                if (h != null && hurt.Add(h)) HurtCharacter(h, Mathf.Lerp(15f, 160f, k));
                var v = c.GetComponentInParent<RailboundVehicle>();
                if (v != null && hurtVehicles.Add(v)) v.ApplyDamage(Mathf.Lerp(40f, 450f, k), lastAttacker, DamageCause.Blast);
                var rb = c.attachedRigidbody;
                if (rb != null && rb != body && !rb.isKinematic) rb.AddExplosionForce(rb.mass * 6f, center, BlastRadius, 1.5f, ForceMode.Impulse);
            }

            // the wreck: thrown up and over, no engine, no handbrake (it rolls where the blast puts it)
            if (body != null && !body.isKinematic)
            {
                body.AddForce(Vector3.up * body.mass * 5.5f, ForceMode.Impulse);
                body.AddTorque(new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * body.mass * 2.5f, ForceMode.Impulse);
            }
            Wrecked?.Invoke(this);
            ShowWreck();
            if (IsSpawned && IsServer) StartCoroutine(RemoveWreckLater());
        }

        /// <summary>Authority: damage a character through its JUHealth (BRNetHealth publishes it), the vehicle's attacker credited.</summary>
        private void HurtCharacter(JUHealth h, float damage)
        {
            if (h == null || h.IsDead) return;
            var previous = JUHealth.PendingAttacker;
            JUHealth.PendingAttacker = lastAttacker;
            try { h.DoDamage(damage, transform.position); }
            finally { JUHealth.PendingAttacker = previous; }
            Debug.Log($"[Vehicle] {name} explosion hurt {h.name} for {damage:F0}: health {h.Health:F0}{(h.IsDead ? " (dead)" : "")}, credited to {(h.LastAttacker != null ? h.LastAttacker.name : "nobody")}.");
        }

        private IEnumerator RemoveWreckLater()
        {
            yield return new WaitForSeconds(WreckLifetime);
            if (IsSpawned && IsServer) NetworkObject.Despawn(true);
        }

        private void OnWreckedChanged(bool previous, bool current) { if (current) ShowWreck(); }

        // ---------------------------------------------------------------- crashes (simulating peer)
        /// <summary>A hard hit on the world (not a character) hurts the vehicle: about 1 point per 1.4 cm/s over 9 m/s of impact.</summary>
        private void CrashDamage(Collision c)
        {
            if (Time.time < nextCrashDamage || c.contactCount == 0) return;
            float impact = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));
            if (impact < 9f) return;
            nextCrashDamage = Time.time + 0.4f;
            ReportDamage((impact - 9f) * 14f, null, DamageCause.Crash);
        }

        // ---------------------------------------------------------------- visuals (every machine with a screen)
        private ParticleSystem engineSmoke, engineFire, wreckSmoke;
        private Material smokeMaterial;

        private static bool HasScreen => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private static Material fxMaterial;

        /// <summary>The vehicle's own smoke material (NWH's smoke sheet on the vertex-colour particle shader), untinted so the
        /// particles' colours (fire, black smoke) come through. One copy shared by every vehicle.</summary>
        private Material SmokeMaterial
        {
            get
            {
                if (fxMaterial != null) return fxMaterial;
                if (smokeMaterial == null)
                    foreach (var r in GetComponentsInChildren<ParticleSystemRenderer>(true))
                        if (r.sharedMaterial != null) { smokeMaterial = r.sharedMaterial; break; }
                if (smokeMaterial == null) return null;
                fxMaterial = new Material(smokeMaterial) { name = "Vehicle FX (runtime)" };
                if (fxMaterial.HasProperty("_UnlitColor")) fxMaterial.SetColor("_UnlitColor", Color.white);
                return fxMaterial;
            }
        }

        private static Material fireMaterial;

        /// <summary>The same, brightened past white: flames glow in daylight exposure.</summary>
        private Material FireMaterial
        {
            get
            {
                if (fireMaterial == null && SmokeMaterial != null)
                {
                    fireMaterial = new Material(SmokeMaterial) { name = "Vehicle Fire FX (runtime)" };
                    if (fireMaterial.HasProperty("_UnlitColor")) fireMaterial.SetColor("_UnlitColor", new Color(2.6f, 2.6f, 2.6f, 1f));
                }
                return fireMaterial;
            }
        }

        /// <summary>Smoke and fire rise straight up from the top of the vehicle, however it lies (a wreck often lands on its roof).</summary>
        private void LateUpdate()
        {
            if (engineSmoke == null && engineFire == null && wreckSmoke == null) return;
            Vector3 top = transform.TransformPoint(localBounds.center) + Vector3.up * (localBounds.extents.y * 0.6f);
            Vector3 engine = IsUpright ? transform.TransformPoint(EngineLocal) : top;
            Quaternion up = Quaternion.LookRotation(Vector3.up);
            if (engineSmoke != null) engineSmoke.transform.SetPositionAndRotation(engine, up);
            if (engineFire != null) engineFire.transform.SetPositionAndRotation(wreckShown ? top : engine, up);
            if (wreckSmoke != null) wreckSmoke.transform.SetPositionAndRotation(top + Vector3.up * 0.4f, up);
        }

        private Vector3 EngineLocal => new Vector3(0f, localBounds.max.y - 0.25f, localBounds.max.z - 0.6f);

        /// <summary>Smoke and fire from the engine as the vehicle gets hurt.</summary>
        private void UpdateDamageFx()
        {
            if (!HasScreen || wreckShown) return;
            float h = Health01;
            if (h < 0.35f && engineSmoke == null)
                engineSmoke = MakeEmitter("EngineSmoke", EngineLocal, new Color(0.35f, 0.35f, 0.35f, 0.55f), new Color(0.15f, 0.15f, 0.15f, 0f), 10f, 2.2f, 0.6f, 2.4f, 1.6f);
            if (h < 0.15f && engineFire == null)
                engineFire = MakeEmitter("EngineFire", EngineLocal, new Color(1f, 0.55f, 0.15f, 0.9f), new Color(0.6f, 0.1f, 0.02f, 0f), 22f, 0.6f, 0.4f, 1.1f, 2.2f);
        }

        private void ShowWreck()
        {
            if (wreckShown) return;
            wreckShown = true;
            if (vc != null)
            {
                var engine = vc.powertrain.engine;
                if (engine.IsRunning) engine.StopEngine();
                vc.effectsManager.lightsManager.SetStateFromInt(0);
            }
            if (!HasScreen) return;

            Vector3 center = transform.TransformPoint(localBounds.center);
            Fireball(center);
            var clip = Resources.Load<AudioClip>("NWH Vehicle Physics 2/Defaults/Sound/Crash");
            if (clip != null) Boom(center, clip);

            // burnt out: every surface near black
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(0.09f, 0.085f, 0.08f, 1f));
                block.SetColor("_Color", new Color(0.09f, 0.085f, 0.08f, 1f));
                r.SetPropertyBlock(block);
            }
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                if (ps != engineSmoke && ps != engineFire) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (engineFire == null) engineFire = MakeEmitter("EngineFire", EngineLocal, new Color(1f, 0.55f, 0.15f, 0.9f), new Color(0.6f, 0.1f, 0.02f, 0f), 28f, 0.7f, 0.5f, 1.4f, 2.4f);
            wreckSmoke = MakeEmitter("WreckSmoke", localBounds.center + Vector3.up * 0.6f, new Color(0.12f, 0.12f, 0.12f, 0.7f), new Color(0.05f, 0.05f, 0.05f, 0f), 14f, 5f, 1.2f, 5f, 2.6f);
        }

        /// <summary>A looping particle column (smoke or fire) on the vehicle, drawn with the vehicle's own smoke material.</summary>
        private ParticleSystem MakeEmitter(string id, Vector3 local, Color start, Color end, float rate, float life, float sizeFrom, float sizeTo, float rise)
        {
            var mat = id.Contains("Fire") ? FireMaterial : SmokeMaterial;
            if (mat == null) return null;
            var go = new GameObject("[FX] " + id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeFrom * 0.8f, sizeFrom * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startColor = start;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.12f), new GradientAlphaKey(start.a * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeTo / Mathf.Max(0.01f, sizeFrom)));
            var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.numTilesX = 6; tsa.numTilesY = 5;   // NWH's smoke sheet is 6 x 5 frames
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        /// <summary>The explosion: a fast orange fireball turning to black smoke, and flying debris sparks.</summary>
        private void Fireball(Vector3 at)
        {
            var mat = FireMaterial;
            if (mat == null) return;
            var go = new GameObject("[FX] Explosion");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.7f, 0.3f, 1f);
            var emission = ps.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60), new ParticleSystem.Burst(0.12f, 25) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.2f;
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 2f; limit.dampen = 0.25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.25f), new GradientColorKey(new Color(0.12f, 0.1f, 0.09f), 0.6f), new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
            var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.numTilesX = 6; tsa.numTilesY = 5;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            ps.Play();
            Destroy(go, 4f);
        }

        private static void Boom(Vector3 at, AudioClip clip)
        {
            var go = new GameObject("[SFX] Explosion");
            go.transform.position = at;
            foreach (var pitch in new[] { 0.45f, 0.62f })
            {
                var a = go.AddComponent<AudioSource>();
                a.clip = clip; a.pitch = pitch; a.volume = 1f; a.spatialBlend = 1f;
                a.minDistance = 8f; a.maxDistance = 260f; a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.Play();
            }
            Destroy(go, 6f);
        }
    }

    /// <summary>
    /// Feeds bullet hits and explosions into vehicle damage, on the machine where they happen: a client only has its own
    /// player's bullets and grenades (the bullets other players fire are visual tracers there), the server has the bots'.
    /// </summary>
    public static class VehicleDamageHooks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            JUTPS.WeaponSystem.Bullet.AnyHit -= OnBulletHit;
            JUTPS.WeaponSystem.Bullet.AnyHit += OnBulletHit;
            JUTPS.PhysicsScripts.Explosion.Exploded -= OnExplosion;
            JUTPS.PhysicsScripts.Explosion.Exploded += OnExplosion;
        }

        private static void OnBulletHit(JUTPS.WeaponSystem.Bullet bullet, Collision col)
        {
            if (bullet == null || col == null || col.collider == null) return;
            var v = col.collider.GetComponentInParent<RailboundVehicle>();
            if (v == null) return;
            var owner = bullet.GetOwner();
            if (owner == bullet.gameObject) owner = null;
            v.ReportDamage(bullet.BulletDamage, owner, RailboundVehicle.DamageCause.Bullet);
        }

        private static void OnExplosion(JUTPS.PhysicsScripts.Explosion e)
        {
            if (e == null) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            // on a client the grenade is the local player's; on the server it is a bot's (no attacker to credit)
            GameObject attacker = nm != null && nm.IsListening && !nm.IsServer && JUGameManager.PlayerController != null ? JUGameManager.PlayerController.gameObject : null;
            float radius = Mathf.Max(1f, e.ExplosionRadious * 1.5f);
            foreach (var v in RailboundVehicle.All.ToArray())
            {
                if (v == null) continue;
                float d = v.DistanceTo(e.transform.position);
                if (d > radius) continue;
                v.ReportDamage(e.Damage * 3f * (1f - d / radius * 0.8f), attacker, RailboundVehicle.DamageCause.Explosion);
            }
        }
    }
}
