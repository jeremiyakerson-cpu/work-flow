using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Pooled, allocation-free gameplay effects: hit sparks, splash explosions,
    /// death puffs, floating "+N" gold numbers, boss shockwaves, upgrade
    /// sparkles and build dust. One fixed pool of sprite particles is updated
    /// in a single loop with Time.deltaTime (so effects respect pause and game
    /// speed). <see cref="Bind"/> subscribes to the gameplay static events;
    /// <see cref="Unbind"/> unsubscribes and destroys the pool.
    /// </summary>
    public sealed class GameplayFx : MonoBehaviour
    {
        /// <summary>The bound instance, or null.</summary>
        public static GameplayFx Instance { get; private set; }

        /// <summary>True when effects can be spawned (bound and not quitting).</summary>
        public static bool CanSpawn => Instance != null && !quitting;

        /// <summary>
        /// CanSpawn, and the caller's scene is still loaded: use from OnDestroy so
        /// tearing down a scene doesn't spawn effects.
        /// </summary>
        public static bool CanSpawnFrom(Component source) =>
            CanSpawn && source != null && source.gameObject.scene.isLoaded;

        private const int Capacity = 384;
        private const int MaxSparksPerFrame = 14;

        private static bool quitting;

        private enum Curve : byte { Fade, Pop, Expand }

        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Gravity;
            public float Drag;
            public float Age;
            public float Life;
            public float Scale0;
            public float Scale1;
            public float Rotation;
            public float Spin;
            public float FadeStart;
            public Color Color;
            public Curve Curve;
        }

        private Particle[] particles;
        private SpriteRenderer[] renderers;
        private Transform[] transforms;
        private int[] active;
        private int activeCount;
        private int[] free;
        private int freeCount;

        private Sprite glow, disc, ring, sparkle;
        private readonly Sprite[] glyphs = new Sprite[128];
        private readonly char[] textBuffer = new char[12];
        private readonly float[] textOffsets = new float[12];

        private WaveManager wave;
        private bool subscribed;
        private int sparkFrame = -1;
        private int sparksThisFrame;

        // ------------------------------------------------------------ lifecycle

        /// <summary>Create the effects pool (once) and subscribe to gameplay events.</summary>
        public static GameplayFx Bind(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[GameplayFx]");
            if (parent != null) go.transform.SetParent(parent, false);
            var fx = go.AddComponent<GameplayFx>(); // Awake builds the pool and sets Instance
            fx.Subscribe();
            return fx;
        }

        /// <summary>Unsubscribe from all events and destroy the pool.</summary>
        public static void Unbind()
        {
            if (Instance == null) return;
            GameplayFx fx = Instance;
            fx.Unsubscribe();
            Instance = null;
            Destroy(fx.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildPool();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void BuildPool()
        {
            glow = SpriteFactory.SoftGlow;
            disc = SpriteFactory.SoftDisc;
            ring = SpriteFactory.ThinRing;
            sparkle = SpriteFactory.Sparkle;
            foreach (char ch in DigitFont.Characters) glyphs[ch] = SpriteFactory.Glyph(ch);

            particles = new Particle[Capacity];
            renderers = new SpriteRenderer[Capacity];
            transforms = new Transform[Capacity];
            active = new int[Capacity];
            free = new int[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("fx");
                go.transform.SetParent(transform, false);
                var r = VisualBuilder.AddRenderer(go, disc, SortingOrders.Fx, Color.white);
                r.enabled = false;
                renderers[i] = r;
                transforms[i] = go.transform;
                free[freeCount++] = Capacity - 1 - i;
            }
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            Enemy.AnyDamaged += OnEnemyDamaged;
            Enemy.AnyDied += OnEnemyDied;
            Enemy.AnyLeaked += OnEnemyLeaked;
            Tower.AnyUpgraded += OnTowerUpgraded;
            TowerPlacement.AnySlotChanged += OnSlotChanged;
            AttachWaveManager(WaveManager.Instance);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            Enemy.AnyDamaged -= OnEnemyDamaged;
            Enemy.AnyDied -= OnEnemyDied;
            Enemy.AnyLeaked -= OnEnemyLeaked;
            Tower.AnyUpgraded -= OnTowerUpgraded;
            TowerPlacement.AnySlotChanged -= OnSlotChanged;
            AttachWaveManager(null);
        }

        /// <summary>Listen to a WaveManager's spawns (boss shockwave). Bind attaches WaveManager.Instance automatically.</summary>
        public void AttachWaveManager(WaveManager manager)
        {
            if (wave == manager) return;
            if (wave != null) wave.EnemySpawned -= OnEnemySpawned;
            wave = manager;
            if (wave != null && subscribed) wave.EnemySpawned += OnEnemySpawned;
        }

        // ------------------------------------------------------------ event handlers

        private void OnEnemyDamaged(Enemy e, float amount, DamageType type)
        {
            if (type == DamageType.Poison || e == null) return; // poison ticks every frame
            int frame = Time.frameCount;
            if (frame != sparkFrame) { sparkFrame = frame; sparksThisFrame = 0; }
            if (++sparksThisFrame > MaxSparksPerFrame) return;
            float s = e.Data != null ? Mathf.Max(0.6f, e.Data.visualScale) : 1f;
            Vector3 p = e.transform.position + new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0.3f * s + UnityEngine.Random.Range(-0.1f, 0.15f), 0f);
            if (e.IsFlying) p.y += 0.45f * s;
            SpawnHitSpark(p, Palette.DamageColor(type));
        }

        private void OnEnemyDied(Enemy e)
        {
            if (e == null) return;
            Vector3 p = e.transform.position;
            float s = e.Data != null ? Mathf.Max(0.6f, e.Data.visualScale) : 1f;
            Color tint = e.Data != null ? e.Data.tint : Color.white;
            if (e.IsFlying) p.y += 0.45f * s;
            SpawnDeathPuff(p, tint, s);
            SpawnNumber(p + new Vector3(0f, 0.7f * s, 0f), e.goldReward, Palette.Gold, true);
            if (e.IsBoss)
            {
                SpawnExplosion(p, 1.8f * s, Palette.Fire);
                SpawnShockwave(p, 3.5f, Palette.Gold);
            }
        }

        private void OnEnemyLeaked(Enemy e)
        {
            if (e == null) return;
            Vector3 p = e.transform.position;
            SpawnDeathPuff(p, Palette.Leak, 0.9f);
            SpawnNumber(p + new Vector3(0f, 0.7f, 0f), -Mathf.Max(1, e.damageToBase), Palette.Leak, false);
        }

        private void OnTowerUpgraded(Tower t)
        {
            if (t == null) return;
            Vector3 p = t.transform.position;
            SpawnSparkle(p + new Vector3(0f, 0.6f, 0f), Palette.Gold);
            Spawn(ring, p + new Vector3(0f, -0.3f, 0f), Vector3.zero, 0.3f, 0.9f, 0.4f, Palette.Gold, Curve.Expand, SortingOrders.Fx + 1);
        }

        private void OnSlotChanged(TowerPlacement slot)
        {
            if (slot == null) return;
            if (slot.IsOccupied) SpawnDust(slot.transform.position);
            else SpawnSparkle(slot.transform.position, Palette.Gold);
        }

        private void OnEnemySpawned(Enemy e)
        {
            if (e == null || !e.IsBoss) return;
            SpawnShockwave(e.transform.position, 4f, Palette.Portal);
        }

        // ------------------------------------------------------------ public spawn API (no-ops when unbound)

        public static void HitSpark(Vector3 position, Color color) { if (CanSpawn) Instance.SpawnHitSpark(position, color); }
        public static void Explosion(Vector3 position, float radius, Color color) { if (CanSpawn) Instance.SpawnExplosion(position, radius, color); }
        public static void Splash(Vector3 position, float radius, Color color) { if (CanSpawn) Instance.SpawnSplash(position, radius, color); }
        public static void DeathPuff(Vector3 position, Color color, float scale = 1f) { if (CanSpawn) Instance.SpawnDeathPuff(position, color, scale); }
        public static void Shockwave(Vector3 position, float radius, Color color) { if (CanSpawn) Instance.SpawnShockwave(position, radius, color); }
        public static void Sparkle(Vector3 position, Color color) { if (CanSpawn) Instance.SpawnSparkle(position, color); }
        public static void Dust(Vector3 position) { if (CanSpawn) Instance.SpawnDust(position); }

        /// <summary>Floating number, e.g. "+15" gold or "-1" lives.</summary>
        public static void FloatingNumber(Vector3 position, int value, Color color, bool plusSign = true)
        {
            if (CanSpawn) Instance.SpawnNumber(position, value, color, plusSign);
        }

        /// <summary>Particles currently alive (for tests/diagnostics).</summary>
        public int ActiveCount => activeCount;

        // ------------------------------------------------------------ effect recipes

        private void SpawnHitSpark(Vector3 p, Color c)
        {
            Spawn(sparkle, p, Vector3.zero, 0.55f, 0.15f, 0.16f, c, Curve.Fade, SortingOrders.Fx + 3, spin: UnityEngine.Random.Range(-400f, 400f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 v = RandomDir() * UnityEngine.Random.Range(2f, 3.5f);
                Spawn(disc, p, v, 0.12f, 0.04f, 0.22f, c, Curve.Fade, SortingOrders.Fx + 3, drag: 6f);
            }
        }

        private void SpawnExplosion(Vector3 p, float radius, Color c)
        {
            radius = Mathf.Max(0.5f, radius);
            Spawn(glow, p, Vector3.zero, radius * 2.2f, radius * 2.8f, 0.28f, new Color(c.r, c.g, c.b, 0.9f), Curve.Fade, SortingOrders.Fx);
            Spawn(ring, p, Vector3.zero, 0.2f, radius, 0.32f, c, Curve.Expand, SortingOrders.Fx + 1);
            Color smoke = new Color(0.35f, 0.32f, 0.3f, 0.7f);
            for (int i = 0; i < 7; i++)
            {
                Vector3 v = RandomDir() * UnityEngine.Random.Range(1.5f, 3f) * radius;
                Spawn(disc, p, v, 0.45f * radius, 0.9f * radius, 0.55f, smoke, Curve.Fade, SortingOrders.Fx + 2, drag: 5f, gravity: 0.6f, fadeStart: 0.3f);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 v = RandomDir() * UnityEngine.Random.Range(3f, 5f) * radius;
                Spawn(disc, p, v, 0.14f, 0.05f, 0.35f, Palette.Spark, Curve.Fade, SortingOrders.Fx + 3, drag: 4f, gravity: -4f);
            }
        }

        private void SpawnSplash(Vector3 p, float radius, Color c)
        {
            radius = Mathf.Max(0.5f, radius);
            Spawn(glow, p, Vector3.zero, radius * 2f, radius * 2.4f, 0.3f, new Color(c.r, c.g, c.b, 0.7f), Curve.Fade, SortingOrders.Fx);
            Spawn(ring, p, Vector3.zero, 0.2f, radius, 0.35f, c, Curve.Expand, SortingOrders.Fx + 1);
            for (int i = 0; i < 7; i++)
            {
                Vector3 v = RandomDir() * UnityEngine.Random.Range(1.5f, 3f) + new Vector3(0f, 2.5f, 0f);
                Spawn(disc, p, v, 0.2f, 0.1f, 0.45f, c, Curve.Fade, SortingOrders.Fx + 2, drag: 2f, gravity: -9f);
            }
        }

        private void SpawnDeathPuff(Vector3 p, Color tint, float scale)
        {
            Color puff = Color.Lerp(tint, Color.white, 0.55f);
            puff.a = 0.85f;
            for (int i = 0; i < 7; i++)
            {
                Vector3 v = RandomDir() * UnityEngine.Random.Range(0.8f, 1.6f) * scale + new Vector3(0f, 0.4f, 0f);
                Spawn(disc, p + v * 0.1f, v, 0.35f * scale, 0.7f * scale, 0.45f, puff, Curve.Fade, SortingOrders.Fx + 2, drag: 4f, fadeStart: 0.2f);
            }
            Spawn(sparkle, p, Vector3.zero, 0.7f * scale, 0.2f, 0.2f, Color.white, Curve.Fade, SortingOrders.Fx + 3, spin: 300f);
        }

        private void SpawnShockwave(Vector3 p, float radius, Color c)
        {
            Spawn(glow, p, Vector3.zero, radius * 0.8f, radius * 2.2f, 0.45f, new Color(c.r, c.g, c.b, 0.55f), Curve.Fade, SortingOrders.Fx);
            Spawn(ring, p, Vector3.zero, 0.3f, radius, 0.5f, c, Curve.Expand, SortingOrders.Fx + 1);
            Spawn(ring, p, Vector3.zero, 0.2f, radius * 0.7f, 0.42f, Color.white, Curve.Expand, SortingOrders.Fx + 1);
        }

        private void SpawnSparkle(Vector3 p, Color c)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.45f, 0.45f), UnityEngine.Random.Range(-0.2f, 0.4f), 0f);
                Spawn(sparkle, p + offset, new Vector3(0f, UnityEngine.Random.Range(0.6f, 1.4f), 0f), 0.45f, 0.1f,
                      UnityEngine.Random.Range(0.45f, 0.7f), c, Curve.Fade, SortingOrders.Fx + 3, spin: UnityEngine.Random.Range(-200f, 200f), fadeStart: 0.4f);
            }
        }

        private void SpawnDust(Vector3 p)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0f);
                Spawn(disc, p + dir * 0.5f + new Vector3(0f, -0.4f, 0f), dir * 1.4f, 0.35f, 0.65f, 0.5f, Palette.Dust, Curve.Fade, SortingOrders.Fx + 2,
                      drag: 4f, fadeStart: 0.25f);
            }
        }

        private void SpawnNumber(Vector3 p, int value, Color color, bool plusSign)
        {
            int len = DigitFont.Format(value, plusSign, textBuffer);
            const float scale = 0.5f;
            float step = scale * ShapeArt.GlyphCellWidth; // world width of one glyph cell at this scale
            float start = -(len - 1) * DigitFont.Advance * step * 0.5f;
            var v = new Vector3(0f, 1.3f, 0f);
            for (int i = 0; i < len; i++)
            {
                Sprite g = glyphs[textBuffer[i]];
                if (g == null) continue;
                var pos = p + new Vector3(start + i * DigitFont.Advance * step, 0f, 0f);
                Spawn(g, pos, v, scale, scale, 0.9f, color, Curve.Pop, SortingOrders.FxText, drag: 1.5f, fadeStart: 0.6f);
            }
        }

        // ------------------------------------------------------------ pool

        private static Vector3 RandomDir()
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }

        private void Spawn(Sprite sprite, Vector3 position, Vector3 velocity, float scale0, float scale1, float life, Color color,
                           Curve curve, int order, float spin = 0f, float drag = 0f, float gravity = 0f, float fadeStart = 0f)
        {
            if (freeCount == 0 || sprite == null) return; // pool exhausted: drop the effect rather than allocate
            int i = free[--freeCount];
            active[activeCount++] = i;
            particles[i] = new Particle
            {
                Position = position,
                Velocity = velocity,
                Gravity = gravity,
                Drag = drag,
                Age = 0f,
                Life = Mathf.Max(0.01f, life),
                Scale0 = scale0,
                Scale1 = scale1,
                Rotation = spin != 0f ? UnityEngine.Random.Range(0f, 360f) : 0f,
                Spin = spin,
                FadeStart = fadeStart,
                Color = color,
                Curve = curve,
            };
            SpriteRenderer r = renderers[i];
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = color;
            r.enabled = true;
            Apply(i, 0f);
        }

        private void Update()
        {
            if (wave == null && WaveManager.Instance != null && subscribed) AttachWaveManager(WaveManager.Instance);

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int k = activeCount - 1; k >= 0; k--)
            {
                int i = active[k];
                ref Particle p = ref particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    renderers[i].enabled = false;
                    active[k] = active[--activeCount];
                    free[freeCount++] = i;
                    continue;
                }
                if (p.Drag > 0f) p.Velocity *= 1f / (1f + p.Drag * dt);
                p.Velocity.y += p.Gravity * dt;
                p.Position += p.Velocity * dt;
                p.Rotation += p.Spin * dt;
                Apply(i, p.Age / p.Life);
            }
        }

        private void Apply(int i, float t)
        {
            ref Particle p = ref particles[i];
            float scale;
            float alpha = p.Color.a;
            switch (p.Curve)
            {
                case Curve.Pop:
                    scale = t < 0.12f ? Mathf.Lerp(p.Scale0 * 0.4f, p.Scale0 * 1.3f, t / 0.12f)
                          : Mathf.Lerp(p.Scale0 * 1.3f, p.Scale1, Mathf.Clamp01((t - 0.12f) / 0.12f));
                    break;
                case Curve.Expand:
                    float e = 1f - (1f - t) * (1f - t);
                    scale = Mathf.Lerp(p.Scale0, p.Scale1, e);
                    break;
                default:
                    scale = Mathf.Lerp(p.Scale0, p.Scale1, 1f - (1f - t) * (1f - t));
                    break;
            }
            if (t > p.FadeStart) alpha *= 1f - (t - p.FadeStart) / Mathf.Max(0.001f, 1f - p.FadeStart);

            Transform tr = transforms[i];
            tr.position = p.Position;
            tr.localScale = new Vector3(scale, scale, 1f);
            tr.localRotation = p.Spin != 0f ? Quaternion.Euler(0f, 0f, p.Rotation) : Quaternion.identity;
            Color c = p.Color;
            c.a = alpha;
            renderers[i].color = c;
        }
    }
}
