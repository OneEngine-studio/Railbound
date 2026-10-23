using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Canvas-space high-performance particle overlay for glowing embers and drifting smoke.
    /// Operates in Screen Space - Overlay with 0 draw call overhead and 0 physics raycast interference.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIEmberOverlay : MaskableGraphic
    {
        public enum ParticleType
        {
            Ember,
            Smoke
        }

        private struct Particle
        {
            public Vector2 position;
            public Vector2 velocity;
            public float size;
            public float life;
            public float maxLife;
            public Color baseColor;
            public float seed;
            public float rotation;
            public float rotSpeed;
            public bool active;
        }

        [Header("Particle Config")]
        [SerializeField] private ParticleType particleType = ParticleType.Ember;
        [SerializeField] private Sprite particleSprite;
        [SerializeField] private int maxParticles = 50;
        [SerializeField] private float emissionRate = 12f;

        [Header("Size & Speed")]
        [SerializeField] private float minSize = 4f;
        [SerializeField] private float maxSize = 12f;
        [SerializeField] private float minSpeedY = 40f;
        [SerializeField] private float maxSpeedY = 95f;
        [SerializeField] private float turbulence = 22f;

        [Header("Life & Palette")]
        [SerializeField] private float minLife = 3.5f;
        [SerializeField] private float maxLife = 6.5f;
        [SerializeField] private Color colorA = new Color(1f, 0.415f, 0f, 0.9f);  // #FF6A00
        [SerializeField] private Color colorB = new Color(1f, 0.647f, 0f, 0.95f); // #FFA500
        [SerializeField] private Color hotCore = new Color(1f, 0.85f, 0.55f, 1f);  // #FFD98C

        private Particle[] _particles;
        private float _spawnTimer;
        private bool _isPaused = false;
        private RectTransform _rect;

        public override Texture mainTexture => particleSprite != null ? particleSprite.texture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            _rect = GetComponent<RectTransform>();
            InitPool();
        }

        private void InitPool()
        {
            _particles = new Particle[maxParticles];
            // Pre-warm particles so they are already alive on screen start
            for (int i = 0; i < maxParticles; i++)
            {
                if (Random.value < 0.65f)
                {
                    SpawnParticle(i, prewarm: true);
                }
            }
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public void SetEmission(bool active)
        {
            _isPaused = !active;
        }

        private void Update()
        {
            if (_isPaused || _rect == null) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Spawn
            _spawnTimer += dt;
            float spawnInterval = 1f / Mathf.Max(0.1f, emissionRate);
            while (_spawnTimer >= spawnInterval)
            {
                _spawnTimer -= spawnInterval;
                int freeIdx = FindFreeParticleIndex();
                if (freeIdx >= 0)
                {
                    SpawnParticle(freeIdx, prewarm: false);
                }
            }

            // Update simulation
            Rect bounds = _rect.rect;
            bool anyAlive = false;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].active) continue;

                _particles[i].life += dt;
                if (_particles[i].life >= _particles[i].maxLife)
                {
                    _particles[i].active = false;
                    continue;
                }

                // Sinusoidal horizontal turbulence + vertical rise
                float timeVal = Time.time * 2.2f + _particles[i].seed;
                float driftX = Mathf.Sin(timeVal) * turbulence;
                _particles[i].position.x += (_particles[i].velocity.x + driftX) * dt;
                _particles[i].position.y += _particles[i].velocity.y * dt;
                _particles[i].rotation += _particles[i].rotSpeed * dt;

                // Cull if out of bounds
                if (_particles[i].position.y > bounds.yMax + 40f ||
                    _particles[i].position.x < bounds.xMin - 60f ||
                    _particles[i].position.x > bounds.xMax + 60f)
                {
                    _particles[i].active = false;
                    continue;
                }

                anyAlive = true;
            }

            if (anyAlive || _spawnTimer > 0f)
            {
                SetVerticesDirty();
            }
        }

        private int FindFreeParticleIndex()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].active) return i;
            }
            return -1;
        }

        private void SpawnParticle(int index, bool prewarm)
        {
            Rect bounds = _rect != null ? _rect.rect : new Rect(-960, -540, 1920, 1080);
            float maxLifeVal = Random.Range(minLife, maxLife);

            float startY = prewarm ? Random.Range(bounds.yMin, bounds.yMax) : bounds.yMin - Random.Range(5f, 25f);
            float startX = Random.Range(bounds.xMin + 40f, bounds.xMax - 40f);

            // Palette selection (80% tactical orange glow, 20% bright amber spark core)
            Color col = Random.value < 0.2f
                ? hotCore
                : Color.Lerp(colorA, colorB, Random.value);

            _particles[index] = new Particle
            {
                position = new Vector2(startX, startY),
                velocity = new Vector2(Random.Range(-12f, 18f), Random.Range(minSpeedY, maxSpeedY)),
                size = Random.Range(minSize, maxSize),
                life = prewarm ? Random.Range(0f, maxLifeVal * 0.8f) : 0f,
                maxLife = maxLifeVal,
                baseColor = col,
                seed = Random.Range(0f, 100f),
                rotation = Random.Range(0f, 360f),
                rotSpeed = Random.Range(-45f, 45f),
                active = true
            };
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_particles == null) return;

            Vector4 uv = particleSprite != null ? DataUtility.GetOuterUV(particleSprite) : new Vector4(0, 0, 1, 1);

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].active) continue;

                float progress = _particles[i].life / _particles[i].maxLife;

                // Smooth bell curve alpha fade: in during 15%, sustained glow, out during 25%
                float alpha;
                if (progress < 0.15f)
                    alpha = progress / 0.15f;
                else if (progress > 0.75f)
                    alpha = (1f - progress) / 0.25f;
                else
                    alpha = 1f;

                // Subtle twinkle / pulsing
                if (particleType == ParticleType.Ember)
                {
                    float twinkle = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f + _particles[i].seed);
                    alpha *= twinkle;
                }

                Color col = _particles[i].baseColor;
                col.a *= alpha;

                float halfSize = _particles[i].size * 0.5f;
                Vector2 pos = _particles[i].position;

                float rad = _particles[i].rotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                Vector2 extX = new Vector2(cos, sin) * halfSize;
                Vector2 extY = new Vector2(-sin, cos) * halfSize;

                int vIndex = vh.currentVertCount;
                vh.AddVert(pos - extX - extY, col, new Vector2(uv.x, uv.y));
                vh.AddVert(pos - extX + extY, col, new Vector2(uv.x, uv.w));
                vh.AddVert(pos + extX + extY, col, new Vector2(uv.z, uv.w));
                vh.AddVert(pos + extX - extY, col, new Vector2(uv.z, uv.y));

                vh.AddTriangle(vIndex, vIndex + 1, vIndex + 2);
                vh.AddTriangle(vIndex + 2, vIndex + 3, vIndex);
            }
        }
    }
}
