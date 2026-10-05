using System.Collections.Generic;
using TowerDefense.Pooling;
using TowerDefense.Visuals.Pure;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Runtime "prefabs" built from data fields only (see Docs/ARCHITECTURE.md).
    /// Each template is an active GameObject under the inactive, persistent
    /// "[Templates]" root: Instantiate(template) yields an active clone and the
    /// template's own Awake/OnEnable never run. Components are added only after
    /// parenting under the inactive root for the same reason.
    /// <para>Usage: <see cref="Initialize"/>, then <see cref="AssignTowerPrefabs"/> /
    /// <see cref="AssignEnemyPrefabs"/> for the content catalog; spawn slots,
    /// heroes and barricades from the matching templates; <see cref="Dispose"/>
    /// at teardown.</para>
    /// </summary>
    public static class RuntimeTemplates
    {
        /// <summary>Physics layer name enemies live on (Tower/Hero queries use it).</summary>
        public const string EnemyLayerName = "Enemy";
        /// <summary>Fallback if the TagManager lacks the "Enemy" layer (ARCHITECTURE.md reserves 8).</summary>
        public const int EnemyLayerFallback = 8;

        private static GameObject root;
        private static readonly Dictionary<TowerData, GameObject> Towers = new Dictionary<TowerData, GameObject>();
        private static readonly Dictionary<TowerData, GameObject> Projectiles = new Dictionary<TowerData, GameObject>();
        private static readonly Dictionary<EnemyData, GameObject> Enemies = new Dictionary<EnemyData, GameObject>();
        private static readonly List<TowerData> AssignedTowers = new List<TowerData>();
        private static readonly List<EnemyData> AssignedEnemies = new List<EnemyData>();
        private static GameObject hero;
        private static GameObject barricade;
        private static GameObject slot;

        public static bool IsInitialized => root != null;

        /// <summary>The inactive "[Templates]" root (null before Initialize).</summary>
        public static Transform Root => root != null ? root.transform : null;

        /// <summary>Create the inactive, DontDestroyOnLoad template root. Idempotent.</summary>
        public static void Initialize()
        {
            if (root != null) return;
            root = new GameObject("[Templates]");
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);
        }

        /// <summary>
        /// Destroy every template and clear the prefab fields this registry set
        /// (fields pointing at real prefabs are left alone). Also calls
        /// GameObjectPool.Clear(): parked clones of the destroyed templates would
        /// otherwise linger until the scene unloads. Despawn live enemies and
        /// projectiles first (e.g. WaveManager.ClearEnemies).
        /// </summary>
        public static void Dispose()
        {
            GameObjectPool.Clear();
            foreach (var d in AssignedTowers)
            {
                if (d == null) continue;
                if (Towers.TryGetValue(d, out var t) && d.towerPrefab == t) d.towerPrefab = null;
                if (Projectiles.TryGetValue(d, out var p) && d.projectilePrefab == p) d.projectilePrefab = null;
            }
            foreach (var d in AssignedEnemies)
                if (d != null && Enemies.TryGetValue(d, out var e) && d.prefab == e) d.prefab = null;

            AssignedTowers.Clear();
            AssignedEnemies.Clear();
            Towers.Clear();
            Projectiles.Clear();
            Enemies.Clear();
            hero = barricade = slot = null;
            if (root != null) Object.Destroy(root);
            root = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain-reload-free play mode: drop references to objects from the previous session.
            root = null;
            hero = barricade = slot = null;
            Towers.Clear(); Projectiles.Clear(); Enemies.Clear();
            AssignedTowers.Clear(); AssignedEnemies.Clear();
        }

        // ------------------------------------------------------------ catalog wiring

        /// <summary>
        /// Point each TowerData's towerPrefab and projectilePrefab at its
        /// template. Fields already holding a live prefab are kept unless
        /// <paramref name="overwrite"/> (so real art can replace templates).
        /// Note: a null projectilePrefab is filled too, so a deliberately
        /// hitscan tower must be excluded or have its field cleared afterwards.
        /// </summary>
        public static void AssignTowerPrefabs(IEnumerable<TowerData> towers, bool overwrite = false)
        {
            if (towers == null) return;
            foreach (var d in towers)
            {
                if (d == null) continue;
                if (overwrite || d.towerPrefab == null) d.towerPrefab = BuildTowerTemplate(d);
                if (overwrite || d.projectilePrefab == null) d.projectilePrefab = BuildProjectileTemplate(d);
                if (!AssignedTowers.Contains(d)) AssignedTowers.Add(d);
            }
        }

        /// <summary>
        /// Point each EnemyData's prefab at its template (live prefabs kept unless
        /// <paramref name="overwrite"/>). Include summon/split enemy types too.
        /// </summary>
        public static void AssignEnemyPrefabs(IEnumerable<EnemyData> enemies, bool overwrite = false)
        {
            if (enemies == null) return;
            foreach (var d in enemies)
            {
                if (d == null) continue;
                if (overwrite || d.prefab == null) d.prefab = BuildEnemyTemplate(d);
                if (!AssignedEnemies.Contains(d)) AssignedEnemies.Add(d);
            }
        }

        public static GameObject HeroTemplate => hero != null ? hero : (hero = BuildHeroTemplate());
        public static GameObject BarricadeTemplate => barricade != null ? barricade : (barricade = BuildBarricadeTemplate());
        public static GameObject SlotTemplate => slot != null ? slot : (slot = BuildSlotTemplate());

        // ------------------------------------------------------------ spawning helpers

        /// <summary>One build slot per LevelData.buildSlots entry.</summary>
        public static List<TowerPlacement> SpawnSlots(LevelData level, Transform parent)
        {
            var list = new List<TowerPlacement>();
            if (level == null || level.buildSlots == null) return list;
            GameObject template = SlotTemplate;
            for (int i = 0; i < level.buildSlots.Count; i++)
            {
                Vector2 p = level.buildSlots[i];
                GameObject go = Object.Instantiate(template, new Vector3(p.x, p.y, 0f), Quaternion.identity, parent);
                go.name = "BuildSlot " + i;
                list.Add(go.GetComponent<TowerPlacement>());
            }
            return list;
        }

        /// <summary>Spawn the hero at a position (PlaceAt, no walk).</summary>
        public static HeroUnit SpawnHero(Vector2 position, Transform parent = null)
        {
            var pos = new Vector3(position.x, position.y, 0f);
            GameObject go = Object.Instantiate(HeroTemplate, pos, Quaternion.identity, parent);
            go.name = "Hero";
            HeroUnit h = go.GetComponent<HeroUnit>();
            h.PlaceAt(pos);
            return h;
        }

        /// <summary>Drop a barricade on the path.</summary>
        public static Barricade SpawnBarricade(Vector2 position, Transform parent = null)
        {
            GameObject go = Object.Instantiate(BarricadeTemplate, new Vector3(position.x, position.y, 0f), Quaternion.identity, parent);
            go.name = "Barricade";
            return go.GetComponent<Barricade>();
        }

        // ------------------------------------------------------------ classification

        public static TowerArtKind TowerKind(TowerData d) =>
            d == null ? TowerArtKind.Generic
                      : ArtClassifier.Tower(d.id, d.damageType == DamageType.Magic, d.damageType == DamageType.Poison,
                                            d.splashRadius > 0f, d.appliesSlow, d.appliesPoison);

        public static EnemyArtKind EnemyKind(EnemyData d) =>
            d == null ? EnemyArtKind.Grunt : ArtClassifier.Enemy((int)d.armor, d.moveType == EnemyMoveType.Flying, d.isBoss);

        // ------------------------------------------------------------ builders

        private static GameObject NewTemplate(string name)
        {
            Initialize();
            var go = new GameObject(name);
            // Parent under the inactive root BEFORE adding behaviours so their Awake never runs.
            go.transform.SetParent(root.transform, false);
            return go;
        }

        /// <summary>Tower template: base, turret (with firePoint), accent pennants, 3 level pips, elite crown + aura, TowerVisual.</summary>
        public static GameObject BuildTowerTemplate(TowerData data)
        {
            if (data != null && Towers.TryGetValue(data, out var cached) && cached != null) return cached;
            TowerArtKind kind = TowerKind(data);
            GameObject go = NewTemplate("Tower_" + (data != null ? data.id : "unknown"));

            // Tower.data stays null on the template: TowerPlacement.TryBuild calls Init(data) on the clone.
            Tower tower = go.AddComponent<Tower>();
            SortingGroup group = VisualBuilder.Group(go, SortingOrders.Towers);

            const float lift = 0.2f;
            Transform body = VisualBuilder.Child(go.transform, "Body", Vector3.zero);
            // Elite aura behind everything; tinted by the chosen specialization at level 4.
            SpriteRenderer eliteAura = VisualBuilder.Sprite(body, "EliteAura", SpriteFactory.SoftGlow, 9, new Vector3(0f, lift, 0f), 2.7f);
            eliteAura.enabled = false;
            VisualBuilder.Sprite(body, "Base", SpriteFactory.TowerBase(kind), 10, new Vector3(0f, lift, 0f));
            SpriteRenderer accent = VisualBuilder.Sprite(body, "Accent", SpriteFactory.TowerAccent(kind), 11, new Vector3(0f, lift, 0f));
            accent.enabled = false;

            // Three linear-level pips, then a crown slot for the specialization (mirrors the menu).
            var pips = new SpriteRenderer[Tower.MaxLinearLevel];
            for (int i = 0; i < pips.Length; i++)
            {
                float x = (i - 1.5f) * 0.25f;
                pips[i] = VisualBuilder.Sprite(body, "Pip" + (i + 1), SpriteFactory.Pip, 12, new Vector3(x, lift - 0.66f, 0f), 0.55f);
                pips[i].enabled = false;
            }
            SpriteRenderer eliteCrown = VisualBuilder.Sprite(body, "EliteCrown", SpriteFactory.Crown, 13,
                                                             new Vector3(0.47f, lift - 0.62f, 0f), 0.42f, Palette.Gold);
            eliteCrown.enabled = false;

            Transform turret = VisualBuilder.Child(body, "Turret", new Vector3(0f, lift + ShapeArt.TurretMountY(kind), 0f));
            Transform turretSprite = VisualBuilder.Sprite(turret, "TurretSprite", SpriteFactory.TowerTurret(kind), 20, Vector3.zero).transform;
            SpriteRenderer muzzle = VisualBuilder.Sprite(turretSprite, "FirePoint", SpriteFactory.MuzzleFlash, 21,
                                                         new Vector3(ShapeArt.MuzzleDistance(kind), 0f, 0f), 0.55f);
            muzzle.enabled = false;
            tower.firePoint = muzzle.transform;

            var visual = go.AddComponent<TowerVisual>();
            bool rotates = kind != TowerArtKind.Mage;
            visual.Setup(tower, group, body, turret, turretSprite, muzzle, accent, pips, eliteCrown, eliteAura, rotates);

            if (data != null) Towers[data] = go;
            return go;
        }

        /// <summary>Projectile template matching the tower's look; splash towers explode on impact.</summary>
        public static GameObject BuildProjectileTemplate(TowerData data)
        {
            if (data != null && Projectiles.TryGetValue(data, out var cached) && cached != null) return cached;
            TowerArtKind towerKind = TowerKind(data);
            ProjectileArtKind kind = ArtClassifier.Projectile(towerKind);
            GameObject go = NewTemplate("Projectile_" + (data != null ? data.id : "unknown"));

            Projectile projectile = go.AddComponent<Projectile>();
            Transform spriteRoot = VisualBuilder.Child(go.transform, "Sprite", Vector3.zero);
            float scale = 0.75f;
            bool face = false;
            float spin = 0f, pulse = 0f, impact = data != null ? data.splashRadius : 0f;
            Color impactColor = Palette.Fire;
            switch (kind)
            {
                case ProjectileArtKind.Arrow: projectile.speed = 16f; face = true; scale = 0.8f; break;
                case ProjectileArtKind.Shard: projectile.speed = 15f; face = true; scale = 0.8f; impactColor = Palette.Ice; break;
                case ProjectileArtKind.Orb: projectile.speed = 12f; pulse = 0.12f; scale = 0.8f; impactColor = Palette.Magic; break;
                case ProjectileArtKind.Shell:
                    // Lobbed by default (TowerData.projectileArcHeight overrides); flight time is arc-independent.
                    projectile.speed = 10f; projectile.arcHeight = 1.1f; spin = 360f; scale = 0.7f;
                    break;
                case ProjectileArtKind.Flask:
                    projectile.speed = 11f; projectile.arcHeight = 0.7f; spin = 540f; scale = 0.7f; impactColor = Palette.Toxic;
                    impact = Mathf.Max(impact, 0.7f); // always splash visually
                    break;
            }
            projectile.rotateToVelocity = face;
            VisualBuilder.Sprite(spriteRoot, "Body", SpriteFactory.Projectile(kind), SortingOrders.Projectiles, Vector3.zero, scale);

            TrailRenderer trail = null;
            if (kind == ProjectileArtKind.Orb || kind == ProjectileArtKind.Shard)
            {
                trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = SpriteFactory.SpriteMaterial;
                trail.sortingOrder = SortingOrders.Projectiles - 1;
                trail.time = 0.15f;
                trail.minVertexDistance = 0.05f;
                trail.numCapVertices = 2;
                trail.startWidth = kind == ProjectileArtKind.Orb ? 0.26f : 0.14f;
                trail.endWidth = 0f;
                Color c = kind == ProjectileArtKind.Orb ? Palette.Magic : Palette.Ice;
                trail.startColor = new Color(c.r, c.g, c.b, 0.7f);
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }

            go.AddComponent<ProjectileVisual>().Setup(spriteRoot, trail, kind, spin, pulse, impact, impactColor);
            if (data != null) Projectiles[data] = go;
            return go;
        }

        /// <summary>
        /// Enemy template: Enemy + CircleCollider2D + kinematic Rigidbody2D on
        /// the "Enemy" layer, silhouette by armour/move type/boss, tinted and
        /// scaled from the data, and an EnemyVisual.
        /// </summary>
        public static GameObject BuildEnemyTemplate(EnemyData data)
        {
            if (data != null && Enemies.TryGetValue(data, out var cached) && cached != null) return cached;
            EnemyArtKind kind = EnemyKind(data);
            Color tint = data != null ? data.tint : Color.white;
            float vs = data != null && data.visualScale > 0f ? data.visualScale : 1f;
            bool flying = kind == EnemyArtKind.Flyer || (data != null && data.moveType == EnemyMoveType.Flying);
            GameObject go = NewTemplate("Enemy_" + (data != null ? data.id : "unknown"));
            go.layer = EnemyLayer();

            Enemy enemy = go.AddComponent<Enemy>();
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.36f * vs;
            col.offset = new Vector2(0f, flying ? 0.35f * vs : 0.2f * vs);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            SortingGroup group = VisualBuilder.Group(go, SortingOrders.Units);

            float hover = flying ? 0.45f * vs : 0f;
            SpriteRenderer shadow = VisualBuilder.Sprite(go.transform, "Shadow", SpriteFactory.SoftDisc, 0, new Vector3(0f, -0.02f, 0f), 1f,
                                                         new Color(0f, 0f, 0f, flying ? 0.18f : 0.3f));
            shadow.transform.localScale = new Vector3(0.8f * vs, 0.28f * vs, 1f);

            SpriteRenderer aura = null;
            if (kind == EnemyArtKind.Boss)
            {
                Color a = Color.Lerp(tint, Color.white, 0.3f);
                a.a = 0.5f;
                aura = VisualBuilder.Sprite(go.transform, "Aura", SpriteFactory.SoftGlow, 1, new Vector3(0f, 0.35f * vs, 0f), 2.4f * vs, a);
            }

            Transform pivot = VisualBuilder.Child(go.transform, "Pivot", Vector3.zero);
            float s = 0.55f * vs;
            pivot.localScale = new Vector3(s, s, 1f);
            const float bodyY = 0.62f; // lifts the drawn feet (≈ -0.62) onto the root
            Transform wingBack = null, wingFront = null;
            if (flying)
            {
                wingBack = VisualBuilder.Sprite(pivot, "WingBack", SpriteFactory.Wing(tint), 4, new Vector3(-0.08f, bodyY + 0.12f, 0f), 1f,
                                                new Color(0.7f, 0.7f, 0.75f, 1f)).transform;
                wingFront = VisualBuilder.Sprite(pivot, "WingFront", SpriteFactory.Wing(tint), 7, new Vector3(0.04f, bodyY + 0.08f, 0f)).transform;
            }
            SpriteRenderer body = VisualBuilder.Sprite(pivot, "Body", SpriteFactory.Enemy(kind, tint), 5, new Vector3(0f, bodyY, 0f));
            SpriteRenderer flash = VisualBuilder.Sprite(pivot, "Flash", SpriteFactory.EnemyFlash(kind, tint), 6, new Vector3(0f, bodyY, 0f));
            flash.enabled = false;
            if (kind == EnemyArtKind.Boss)
                VisualBuilder.Sprite(pivot, "Crown", SpriteFactory.BossCrown, 8, new Vector3(0.05f, bodyY + 0.62f, 0f), 0.7f);

            float barWidth = Mathf.Clamp(0.75f * vs, 0.7f, 1.8f);
            HealthBar bar = HealthBar.Create(go.transform, new Vector3(0f, hover + 0.95f * vs + 0.05f, 0f), barWidth, 20);

            go.AddComponent<EnemyVisual>().Setup(enemy, group, pivot, body, flash, shadow, wingBack, wingFront, aura, bar, hover,
                                                 kind == EnemyArtKind.Boss);
            if (data != null) Enemies[data] = go;
            return go;
        }

        private static GameObject BuildHeroTemplate()
        {
            GameObject go = NewTemplate("Hero");
            HeroUnit unit = go.AddComponent<HeroUnit>();
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.offset = new Vector2(0f, 0.35f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            SortingGroup group = VisualBuilder.Group(go, SortingOrders.Units);

            SpriteRenderer ring = VisualBuilder.Sprite(go.transform, "Ring", SpriteFactory.Ring, 0, new Vector3(0f, -0.02f, 0f), 1f,
                                                       new Color(Palette.Hero.r, Palette.Hero.g, Palette.Hero.b, 0.45f));
            ring.transform.localScale = new Vector3(1.1f, 0.5f, 1f);
            SpriteRenderer shadow = VisualBuilder.Sprite(go.transform, "Shadow", SpriteFactory.SoftDisc, 1, new Vector3(0f, -0.02f, 0f), 1f, Palette.Shadow);
            shadow.transform.localScale = new Vector3(0.75f, 0.26f, 1f);

            Transform pivot = VisualBuilder.Child(go.transform, "Pivot", Vector3.zero);
            const float s = 0.62f, bodyY = 0.62f;
            pivot.localScale = new Vector3(s, s, 1f);
            VisualBuilder.Sprite(pivot, "Body", SpriteFactory.Hero, 5, new Vector3(0f, bodyY, 0f));
            SpriteRenderer flash = VisualBuilder.Sprite(pivot, "Flash", SpriteFactory.HeroSilhouette, 6, new Vector3(0f, bodyY, 0f));
            flash.enabled = false;

            SpriteRenderer ghost = VisualBuilder.Sprite(go.transform, "Ghost", SpriteFactory.HeroSilhouette, 7, new Vector3(0f, bodyY * s, 0f), s,
                                                        new Color(0.75f, 0.88f, 1f, 0.3f));
            ghost.enabled = false;

            HealthBar bar = HealthBar.Create(go.transform, new Vector3(0f, 1.0f, 0f), 0.8f, 20);
            go.AddComponent<HeroVisual>().Setup(unit, group, pivot, flash, ghost, ring, bar, bodyY * s);
            return go;
        }

        private static GameObject BuildBarricadeTemplate()
        {
            GameObject go = NewTemplate("Barricade");
            // Barricade polls for enemies itself (EnemyQuery), so it needs no collider or rigidbody.
            Barricade b = go.AddComponent<Barricade>();
            SortingGroup group = VisualBuilder.Group(go, SortingOrders.Units);

            Transform body = VisualBuilder.Child(go.transform, "Body", Vector3.zero);
            const float s = 0.75f;
            var offset = new Vector3(0f, 0.3f, 0f);
            VisualBuilder.Sprite(body, "Fence", SpriteFactory.Barricade, 10, offset, s);
            SpriteRenderer c1 = VisualBuilder.Sprite(body, "Cracks1", SpriteFactory.Cracks(1), 11, offset, s);
            SpriteRenderer c2 = VisualBuilder.Sprite(body, "Cracks2", SpriteFactory.Cracks(2), 12, offset, s);
            c1.enabled = false;
            c2.enabled = false;
            go.AddComponent<BarricadeVisual>().Setup(b, group, body, c1, c2);
            return go;
        }

        private static GameObject BuildSlotTemplate()
        {
            GameObject go = NewTemplate("BuildSlot");
            TowerPlacement placement = go.AddComponent<TowerPlacement>();
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.6f, 1.4f);
            box.offset = new Vector2(0f, 0.1f);
            SpriteRenderer plot = VisualBuilder.Sprite(go.transform, "Plot", SpriteFactory.Slot, SortingOrders.Slots, Vector3.zero);
            go.AddComponent<SlotVisual>().Setup(placement, plot);
            return go;
        }

        private static int EnemyLayer()
        {
            int layer = LayerMask.NameToLayer(EnemyLayerName);
            if (layer >= 0) return layer;
            Debug.LogWarning($"RuntimeTemplates: no \"{EnemyLayerName}\" layer in TagManager; using layer {EnemyLayerFallback}. " +
                             "Towers query LayerMask.GetMask(\"Enemy\") and will not see enemies until the layer is named.");
            return EnemyLayerFallback;
        }
    }
}
