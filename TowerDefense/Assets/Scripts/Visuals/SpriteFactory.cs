using System;
using System.Collections.Generic;
using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Generates and caches every runtime sprite from the engine-free
    /// <see cref="ShapeArt"/> compositions. Textures are power-of-two RGBA32,
    /// bilinear, uploaded once and made non-readable; sprites use a fixed 64
    /// pixels-per-unit and FullRect meshes. Everything shares one material
    /// (built-in "Sprites/Default"); paths use a second one with a soft-edge
    /// texture. Call <see cref="Clear"/> only at app teardown.
    /// </summary>
    public static class SpriteFactory
    {
        public const float PixelsPerUnit = 64f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        private static readonly Dictionary<uint, Sprite[]> DecorAtlases = new Dictionary<uint, Sprite[]>();
        private static Material spriteMaterial;
        private static Material lineMaterial;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ------------------------------------------------------------ materials

        /// <summary>The one material every SpriteRenderer, trail and FX sprite uses.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    spriteMaterial = new Material(FindSpriteShader()) { name = "TD Sprites (shared)", hideFlags = HideFlags.DontSave };
                    Owned.Add(spriteMaterial);
                }
                return spriteMaterial;
            }
        }

        /// <summary>
        /// Material for LineRenderers: Sprites/Default with a texture whose alpha
        /// fades at both edges across the line width (anti-aliased thick lines;
        /// colour comes from the line's vertex colours).
        /// </summary>
        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                {
                    const int h = 64;
                    var c = new PixelCanvas(4, h);
                    c.PaintPixels((i, j) => new Rgba(1f, 1f, 1f, Profiles.LineEdge((j + 0.5f) / h, 0.08f)));
                    Texture2D tex = CreateTexture(c, Rgba.White, "TD Line Edge", TextureWrapMode.Clamp);
                    lineMaterial = new Material(FindSpriteShader()) { name = "TD Lines (shared)", hideFlags = HideFlags.DontSave, mainTexture = tex };
                    Owned.Add(lineMaterial);
                }
                return lineMaterial;
            }
        }

        private static Shader FindSpriteShader()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) Debug.LogError("SpriteFactory: built-in shader 'Sprites/Default' not found (is it stripped?).");
            return s;
        }

        // ------------------------------------------------------------ core

        /// <summary>Cached square sprite drawn by <paramref name="draw"/> on first request.</summary>
        public static Sprite Get(string key, int size, Action<PixelCanvas> draw) => Get(key, size, size, draw, Center, Rgba.White);

        /// <summary>Cached sprite of any power-of-two size with a custom pivot and bleed colour.</summary>
        public static Sprite Get(string key, int width, int height, Action<PixelCanvas> draw, Vector2 pivot, Rgba bleed)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var canvas = new PixelCanvas(width, height);
            draw(canvas);
            Sprite sprite = FromCanvas(canvas, key, pivot, bleed);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Upload a canvas as a texture-backed sprite (not cached; caller owns it unless via Get).</summary>
        public static Sprite FromCanvas(PixelCanvas canvas, string name, Vector2 pivot, Rgba bleed,
                                        TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            Texture2D tex = CreateTexture(canvas, bleed, name, wrap);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, canvas.Width, canvas.Height), pivot, PixelsPerUnit,
                                          0, SpriteMeshType.FullRect, Vector4.zero);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Owned.Add(sprite);
            return sprite;
        }

        /// <summary>RGBA32 texture from a canvas, uploaded once and released from CPU memory.</summary>
        public static Texture2D CreateTexture(PixelCanvas canvas, Rgba bleed, string name, TextureWrapMode wrap)
        {
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = wrap,
                hideFlags = HideFlags.DontSave,
            };
            byte[] bytes = canvas.ToRgba32(bleed);
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int i = 0, k = 0; i < pixels.Length; i++, k += 4)
                pixels[i] = new Color32(bytes[k], bytes[k + 1], bytes[k + 2], bytes[k + 3]);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Owned.Add(tex);
            return tex;
        }

        /// <summary>Destroy an uncached sprite made with <see cref="FromCanvas"/> and its texture.</summary>
        public static void Release(Sprite sprite)
        {
            if (sprite == null) return;
            Texture2D tex = sprite.texture;
            Owned.Remove(sprite);
            UnityEngine.Object.Destroy(sprite);
            if (tex != null)
            {
                Owned.Remove(tex);
                UnityEngine.Object.Destroy(tex);
            }
        }

        /// <summary>Destroy every generated texture, sprite and material (app teardown only).</summary>
        public static void Clear()
        {
            foreach (var o in Owned)
                if (o != null) UnityEngine.Object.Destroy(o);
            Owned.Clear();
            Cache.Clear();
            DecorAtlases.Clear();
            spriteMaterial = null;
            lineMaterial = null;
        }

        // ------------------------------------------------------------ primitives (white, tint via SpriteRenderer.color)

        public static Sprite Circle => Get("prim/circle", 64, c => ShapeArt.Primitive(c, SdfShapes.Circle(0f, 0f, 0.9f)));
        public static Sprite Ring => Get("prim/ring", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitRing()));
        public static Sprite RoundedRect => Get("prim/rrect", 64, c => ShapeArt.Primitive(c, SdfShapes.RoundedBox(0f, 0f, 0.92f, 0.92f, 0.3f)));
        public static Sprite Triangle => Get("prim/triangle", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitTriangle()));
        public static Sprite Diamond => Get("prim/diamond", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitDiamond()));
        public static Sprite Hexagon => Get("prim/hexagon", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitHexagon()));
        public static Sprite Star => Get("prim/star", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitStar()));
        public static Sprite Shield => Get("prim/shield", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitShield()));
        public static Sprite Crown => Get("prim/crown", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitCrown()));
        public static Sprite Arrow => Get("prim/arrow", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitArrow()));
        /// <summary>Flat white dot without outline (pebbles, tiny particles).</summary>
        public static Sprite Dot => Get("prim/dot", 32, c => ShapeArt.Primitive(c, SdfShapes.Circle(0f, 0f, 0.9f), outline: false));
        public static Sprite Droplet => Get("prim/droplet", 64, c => ShapeArt.Primitive(c, SdfShapes.UnitDroplet()));

        /// <summary>Radial white glow (1 unit wide at scale 1).</summary>
        public static Sprite SoftGlow => Get("fx/glow", 64, ShapeArt.SoftGlow);
        /// <summary>Soft white disc for puffs and ground shadows.</summary>
        public static Sprite SoftDisc => Get("fx/disc", 64, c => ShapeArt.SoftDisc(c));
        /// <summary>Thin crisp ring (2 units wide at scale 1) for shockwaves.</summary>
        public static Sprite ThinRing => Get("fx/ring", 128, c => ShapeArt.ThinRing(c));
        public static Sprite Sparkle => Get("fx/sparkle", 64, ShapeArt.Sparkle);
        public static Sprite MuzzleFlash => Get("fx/flash", 64, ShapeArt.Flash);

        // ------------------------------------------------------------ game art

        public static Sprite TowerBase(TowerArtKind kind) => Get("tower/base/" + kind, 128, c => ShapeArt.TowerBase(c, kind));
        public static Sprite TowerTurret(TowerArtKind kind) => Get("tower/turret/" + kind, 128, c => ShapeArt.TowerTurret(c, kind));
        public static Sprite TowerAccent(TowerArtKind kind) => Get("tower/accent/" + kind, 128, c => ShapeArt.TowerAccent(c, kind));
        public static Sprite Pip => Get("tower/pip", 32, c => c.Draw(SdfShapes.Scale(SdfShapes.UnitDiamond(), 0.8f),
                                                                     ShapeStyle.Toon(ArtPalette.Gold, 0.14f, 0.3f, 0.4f)));

        public static Sprite Projectile(ProjectileArtKind kind) => Get("projectile/" + kind, 64, c => ShapeArt.Projectile(c, kind));

        public static Sprite Enemy(EnemyArtKind kind, Color tint)
        {
            Rgba t = Palette.ToRgba(tint);
            return Get("enemy/" + kind + "/" + t.Key.ToString("X8"), 128, c => ShapeArt.Enemy(c, kind, t));
        }

        /// <summary>White silhouette of an enemy body, faded in for hit flashes.</summary>
        public static Sprite EnemyFlash(EnemyArtKind kind, Color tint)
        {
            Rgba t = Palette.ToRgba(tint);
            return Get("enemy-flash/" + kind + "/" + t.Key.ToString("X8"), 128, 128, c =>
            {
                var body = new PixelCanvas(128);
                ShapeArt.Enemy(body, kind, t);
                body.ToSilhouette(Rgba.White).CopyInto(c, 0, 0);
            }, Center, Rgba.White);
        }

        public static Sprite Wing(Color tint)
        {
            Rgba t = Palette.ToRgba(tint);
            return Get("enemy-wing/" + t.Key.ToString("X8"), 128, c => ShapeArt.Wing(c, t));
        }

        public static Sprite BossCrown => Get("enemy/crown", 64, ShapeArt.Crown);

        public static Sprite Hero => Get("hero/body", 128, ShapeArt.Hero);
        public static Sprite HeroSilhouette => Get("hero/silhouette", 128, 128, c =>
        {
            var body = new PixelCanvas(128);
            ShapeArt.Hero(body);
            body.ToSilhouette(Rgba.White).CopyInto(c, 0, 0);
        }, Center, Rgba.White);

        public static Sprite Barricade => Get("barricade/body", 128, ShapeArt.Barricade);
        public static Sprite Cracks(int stage) => Get("barricade/cracks" + stage, 128, c => ShapeArt.Cracks(c, stage));
        public static Sprite Slot => Get("map/slot", 128, ShapeArt.Slot);
        public static Sprite PortalBase => Get("map/portal", 128, ShapeArt.PortalBase);
        public static Sprite PortalSwirl => Get("map/portal-swirl", 128, ShapeArt.PortalSwirl);
        public static Sprite ExitFlag => Get("map/exit", 128, ShapeArt.ExitFlag);

        /// <summary>Health bar background (1 x 0.25 units), centre pivot.</summary>
        public static Sprite HealthBarBack => Get("ui/hp-back", 64, 16, c => ShapeArt.Bar(c, ArtPalette.HealthBack, 0.02f), Center, ArtPalette.Outline);
        /// <summary>Health bar fill, white, left pivot so x scale shrinks it toward the left edge.</summary>
        public static Sprite HealthBarFill => Get("ui/hp-fill", 64, 16, c => ShapeArt.Bar(c, Rgba.White, 0.28f), new Vector2(0f, 0.5f), Rgba.White);

        /// <summary>Sprite for a DigitFont character (1 unit tall cell at scale 1).</summary>
        public static Sprite Glyph(char ch) => Get("glyph/" + ch, 64, c => ShapeArt.Glyph(c, ch));

        /// <summary>Seamless near-white ground tile (2x2 units), repeat-wrapped for tiled drawing.</summary>
        public static Sprite GroundTile
        {
            get
            {
                const string key = "map/ground";
                if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
                var c = new PixelCanvas(128);
                ShapeArt.GroundTile(c, 7u);
                s = FromCanvas(c, key, Center, Rgba.White, TextureWrapMode.Repeat);
                Cache[key] = s;
                return s;
            }
        }

        // ------------------------------------------------------------ decor atlas

        /// <summary>Variants generated per decor kind.</summary>
        public const int DecorVariants = 2;

        /// <summary>
        /// All decor sprites for one foliage colour, packed into a single 512 px
        /// atlas texture (so a forest of hundreds of y-sorted sprites batches).
        /// Index with <see cref="DecorIndex"/>.
        /// </summary>
        public static Sprite[] DecorSprites(Color foliage)
        {
            Rgba f = Palette.ToRgba(foliage);
            if (DecorAtlases.TryGetValue(f.Key, out Sprite[] sprites) && sprites[0] != null) return sprites;

            const int cell = 128, cols = 4;
            int kinds = Enum.GetValues(typeof(DecorKind)).Length;
            int count = kinds * DecorVariants;
            int rows = (count + cols - 1) / cols;
            int atlasH = Mathf.NextPowerOfTwo(rows * cell);
            var atlas = new PixelCanvas(cols * cell, atlasH);
            for (int i = 0; i < count; i++)
            {
                var c = new PixelCanvas(cell);
                ShapeArt.Decor(c, (DecorKind)(i / DecorVariants), i % DecorVariants, f);
                c.CopyInto(atlas, (i % cols) * cell, (i / cols) * cell);
            }
            Texture2D tex = CreateTexture(atlas, f, "TD Decor Atlas " + f.Key.ToString("X8"), TextureWrapMode.Clamp);
            sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect((i % cols) * cell, (i / cols) * cell, cell, cell);
                // Pivot near the bottom of the cell so y-sorting uses the item's base.
                Sprite s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.2f), PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero);
                s.name = "decor/" + (DecorKind)(i / DecorVariants) + "/" + (i % DecorVariants);
                s.hideFlags = HideFlags.DontSave;
                Owned.Add(s);
                sprites[i] = s;
            }
            DecorAtlases[f.Key] = sprites;
            return sprites;
        }

        public static int DecorIndex(DecorKind kind, int variant) => (int)kind * DecorVariants + Mathf.Abs(variant) % DecorVariants;
    }
}
