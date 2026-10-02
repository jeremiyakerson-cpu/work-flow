using System.Collections.Generic;
using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Draws a <see cref="LevelData"/>: tiled ground in groundColor with soft
    /// mottling, paths as thick rounded anti-aliased polylines (shadow, border,
    /// fill, inner highlight, pebbles) in pathColor, a deterministic decoration
    /// scatter (trees, rocks, bushes, flowers) that avoids paths, slots and
    /// markers plus a forest border outside the play area, spawn portals, exit
    /// flags and a vignette. Purely visual: slots/hero/towers are spawned
    /// separately (see RuntimeTemplates).
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        /// <summary>Visual path width in world units (enemies are ~0.8 wide).</summary>
        public const float PathWidth = 1.7f;
        /// <summary>Ground and vignette extend this far beyond the play area.</summary>
        public const float GroundMargin = 14f;

        private static readonly Color VignetteColor = new Color(0.04f, 0.06f, 0.05f, 1f);

        private readonly List<Vector3> spawnPoints = new List<Vector3>();
        private readonly List<Vector3> exitPoints = new List<Vector3>();
        private Sprite vignette;

        public LevelData Level { get; private set; }
        public Vector2 WorldSize { get; private set; }
        /// <summary>Spawn portal positions (one per distinct path start).</summary>
        public IReadOnlyList<Vector3> SpawnPoints => spawnPoints;
        /// <summary>Exit flag anchor positions (one per distinct path end).</summary>
        public IReadOnlyList<Vector3> ExitPoints => exitPoints;
        /// <summary>Number of decoration sprites placed (diagnostics).</summary>
        public int DecorCount { get; private set; }

        /// <summary>Build the map for <paramref name="level"/> under <paramref name="parent"/> (origin = world origin).</summary>
        public static MapView Build(LevelData level, Transform parent = null)
        {
            var go = new GameObject("[Map] " + (level != null ? level.id : "none"));
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<MapView>();
            if (level != null) view.BuildInternal(level);
            return view;
        }

        /// <summary>Destroy the map and the per-level textures it created.</summary>
        public void Clear()
        {
            if (this != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SpriteFactory.Release(vignette);
            vignette = null;
        }

        // ------------------------------------------------------------ build

        private void BuildInternal(LevelData level)
        {
            Level = level;
            WorldSize = level.worldSize.x > 0f && level.worldSize.y > 0f ? level.worldSize : new Vector2(32f, 18f);
            uint seed = StableHash.Fnv1a(level.id);

            var paths = new List<IReadOnlyList<Vec2f>>();
            if (level.paths != null)
                foreach (var def in level.paths)
                {
                    if (def == null || def.points == null || def.points.Count < 2) continue;
                    var pts = new Vec2f[def.points.Count];
                    for (int i = 0; i < pts.Length; i++) pts[i] = new Vec2f(def.points[i].x, def.points[i].y);
                    paths.Add(pts);
                }

            BuildGround(level, seed);
            for (int i = 0; i < paths.Count; i++) BuildPathLayers(paths[i], level.pathColor, i);
            BuildPebbles(paths, level.pathColor, seed);
            var markers = BuildMarkers(paths, level);
            BuildDecor(level, paths, markers, seed);
            BuildVignette();
        }

        private void BuildGround(LevelData level, uint seed)
        {
            Vector2 size = WorldSize + new Vector2(GroundMargin * 2f, GroundMargin * 2f);
            var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(WorldSize.x * 0.5f, WorldSize.y * 0.5f, 0f);
            ground.sprite = SpriteFactory.GroundTile;
            ground.sharedMaterial = SpriteFactory.SpriteMaterial;
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.size = size;
            ground.color = WithAlpha(level.groundColor, 1f);
            ground.sortingOrder = SortingOrders.Ground;

            // Large soft blotches break up the tiling: lighter sun patches and darker accent patches.
            var rng = new DeterministicRandom(seed ^ 0xB10Bu);
            Transform blotches = VisualBuilder.Child(transform, "GroundBlotches", Vector3.zero);
            int count = Mathf.RoundToInt(WorldSize.x * WorldSize.y / 10f);
            Color light = Color.Lerp(level.groundColor, Color.white, 0.3f);
            Color dark = Color.Lerp(level.groundColor, level.accentColor, 0.7f);
            for (int i = 0; i < count; i++)
            {
                float x = rng.Range(-4f, WorldSize.x + 4f), y = rng.Range(-4f, WorldSize.y + 4f);
                bool isLight = rng.NextFloat() < 0.5f;
                Color c = isLight ? light : dark;
                c.a = isLight ? 0.22f : 0.28f;
                float s = rng.Range(3f, 7f);
                SpriteRenderer r = VisualBuilder.Sprite(blotches, "Blotch", SpriteFactory.SoftGlow, SortingOrders.GroundDetail, new Vector3(x, y, 0f), 1f, c);
                r.transform.localScale = new Vector3(s, s * rng.Range(0.55f, 0.85f), 1f);
            }
        }

        private void BuildPathLayers(IReadOnlyList<Vec2f> pts, Color pathColor, int index)
        {
            var positions = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) positions[i] = new Vector3(pts[i].X, pts[i].Y, 0f);
            var shadowPositions = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) shadowPositions[i] = positions[i] + new Vector3(0.08f, -0.16f, 0f);

            Color border = Color.Lerp(WithAlpha(pathColor, 1f), Color.black, 0.38f);
            Color fill = WithAlpha(pathColor, 1f);
            Color highlight = Color.Lerp(fill, Color.white, 0.3f);
            highlight.a = 0.45f;

            Transform root = VisualBuilder.Child(transform, "Path " + index, Vector3.zero);
            Line(root, "Shadow", shadowPositions, PathWidth + 0.45f, new Color(0f, 0f, 0f, 0.18f), SortingOrders.PathShadow);
            Line(root, "Border", positions, PathWidth + 0.32f, border, SortingOrders.PathBorder);
            Line(root, "Fill", positions, PathWidth, fill, SortingOrders.PathFill);
            Line(root, "Highlight", positions, PathWidth * 0.36f, highlight, SortingOrders.PathHighlight);
        }

        private static LineRenderer Line(Transform parent, string name, Vector3[] positions, float width, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = positions.Length;
            lr.SetPositions(positions);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCornerVertices = 8;
            lr.numCapVertices = 8;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = SpriteFactory.LineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = order;
            return lr;
        }

        private void BuildPebbles(List<IReadOnlyList<Vec2f>> paths, Color pathColor, uint seed)
        {
            var rng = new DeterministicRandom(seed ^ 0x9EBBu);
            Transform root = VisualBuilder.Child(transform, "Pebbles", Vector3.zero);
            Color dark = Color.Lerp(WithAlpha(pathColor, 1f), Color.black, 0.22f);
            Color light = Color.Lerp(WithAlpha(pathColor, 1f), Color.white, 0.25f);
            foreach (var path in paths)
            {
                float len = PathGeometry.Length(path);
                int count = Mathf.RoundToInt(len * 0.9f);
                for (int i = 0; i < count; i++)
                {
                    float d = rng.Range(0f, len);
                    Vec2f p = PathGeometry.PointAtDistance(path, d);
                    Vec2f dir = PathGeometry.DirectionAtDistance(path, d);
                    float side = rng.Range(-0.38f, 0.38f) * PathWidth;
                    var pos = new Vector3(p.X - dir.Y * side, p.Y + dir.X * side, 0f);
                    bool isDark = rng.NextFloat() < 0.6f;
                    float s = rng.Range(0.08f, 0.15f);
                    SpriteRenderer r = VisualBuilder.Sprite(root, "Pebble", SpriteFactory.Dot, SortingOrders.PathHighlight + 1, pos, 1f,
                                                            isDark ? dark : light);
                    r.transform.localScale = new Vector3(s * 1.3f, s, 1f);
                }
            }
        }

        private List<Vec2f> BuildMarkers(List<IReadOnlyList<Vec2f>> paths, LevelData level)
        {
            var markers = new List<Vec2f> { new Vec2f(level.heroStart.x, level.heroStart.y) };
            Transform root = VisualBuilder.Child(transform, "Markers", Vector3.zero);
            foreach (var path in paths)
            {
                Vec2f start = PathGeometry.InsetEndpoint(path, WorldSize.x, WorldSize.y, 0.9f, fromEnd: false);
                if (!NearAny(spawnPoints, start, 1.2f))
                {
                    spawnPoints.Add(new Vector3(start.X, start.Y, 0f));
                    markers.Add(start);
                    Transform portal = VisualBuilder.Child(root, "SpawnPortal", new Vector3(start.X, start.Y, 0f));
                    VisualBuilder.Sprite(portal, "Base", SpriteFactory.PortalBase, SortingOrders.PathMarkers, Vector3.zero, 0.95f);
                    SpriteRenderer swirl = VisualBuilder.Sprite(portal, "Swirl", SpriteFactory.PortalSwirl, SortingOrders.PathMarkers + 1, Vector3.zero, 0.95f);
                    swirl.gameObject.AddComponent<Spinner>().SetSpeed(-120f);
                    swirl.gameObject.AddComponent<Pulse>().Setup(swirl, 2.2f, 0.08f, 0.65f, 1f);
                }

                Vec2f end = PathGeometry.InsetEndpoint(path, WorldSize.x, WorldSize.y, 0.9f, fromEnd: true);
                if (!NearAny(exitPoints, end, 1.2f))
                {
                    exitPoints.Add(new Vector3(end.X, end.Y, 0f));
                    markers.Add(end);
                    // Flag stands beside the road on its upper side, facing travel.
                    Vec2f dir = PathGeometry.DirectionAtDistance(path, PathGeometry.Length(path));
                    var perp = new Vec2f(-dir.Y, dir.X);
                    if (perp.Y < 0f || (Mathf.Abs(perp.Y) < 0.01f && perp.X > 0f)) perp = -perp;
                    Vec2f flagPos = end + perp * (PathWidth * 0.5f + 0.55f);
                    markers.Add(flagPos);
                    VisualBuilder.Sprite(root, "ExitFlag", SpriteFactory.ExitFlag, SortingOrders.ForY(SortingOrders.Decor, flagPos.Y),
                                         new Vector3(flagPos.X, flagPos.Y + 0.4f, 0f), 0.9f);
                }
            }
            return markers;
        }

        private void BuildDecor(LevelData level, List<IReadOnlyList<Vec2f>> paths, List<Vec2f> markers, uint seed)
        {
            var slots = new List<Vec2f>();
            if (level.buildSlots != null)
                foreach (var s in level.buildSlots) slots.Add(new Vec2f(s.x, s.y));

            var settings = new ScatterSettings
            {
                Seed = seed,
                WorldWidth = WorldSize.x,
                WorldHeight = WorldSize.y,
                PathClearance = PathWidth * 0.5f + 0.35f,
            };
            List<DecorItem> items = DecorScatter.Scatter(settings, paths, slots, markers);

            Color foliage = Color.Lerp(WithAlpha(level.accentColor, 1f), WithAlpha(level.groundColor, 1f), 0.25f);
            Sprite[] sprites = SpriteFactory.DecorSprites(foliage);
            Transform root = VisualBuilder.Child(transform, "Decor", Vector3.zero);
            foreach (var it in items)
            {
                Sprite sprite = sprites[SpriteFactory.DecorIndex(it.Kind, it.Variant)];
                SpriteRenderer r = VisualBuilder.Sprite(root, it.Kind.ToString(), sprite, SortingOrders.ForY(SortingOrders.Decor, it.Y),
                                                        new Vector3(it.X, it.Y, 0f), it.Scale);
                r.flipX = it.FlipX;
                if (it.Border) r.color = new Color(0.86f, 0.88f, 0.86f, 1f); // the forest edge reads slightly darker
            }
            DecorCount = items.Count;
        }

        private void BuildVignette()
        {
            const int size = 256;
            float w = WorldSize.x + GroundMargin * 2f, h = WorldSize.y + GroundMargin * 2f;
            var canvas = new PixelCanvas(size, size);
            Vector2 world = WorldSize;
            canvas.PaintPixels((i, j) =>
            {
                float x = (i + 0.5f) / size * w - GroundMargin;
                float y = (j + 0.5f) / size * h - GroundMargin;
                float d = PathGeometry.RectSignedDistance(new Vec2f(x, y), world.x, world.y);
                return new Rgba(VignetteColor.r, VignetteColor.g, VignetteColor.b, Profiles.Vignette(d));
            });
            Sprite sprite = SpriteFactory.FromCanvas(canvas, "TD Vignette", new Vector2(0.5f, 0.5f), Palette.ToRgba(VignetteColor));
            vignette = sprite;
            // 256 px at 64 ppu = 4 units; stretch to cover the ground.
            SpriteRenderer r = VisualBuilder.Sprite(transform, "Vignette", sprite, SortingOrders.Vignette,
                                                    new Vector3(WorldSize.x * 0.5f, WorldSize.y * 0.5f, 0f));
            r.transform.localScale = new Vector3(w / 4f, h / 4f, 1f);
        }

        private static bool NearAny(List<Vector3> points, Vec2f p, float radius)
        {
            foreach (var q in points)
            {
                float dx = q.x - p.X, dy = q.y - p.Y;
                if (dx * dx + dy * dy < radius * radius) return true;
            }
            return false;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
