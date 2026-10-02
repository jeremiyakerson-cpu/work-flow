using System;
using static TowerDefense.Visuals.Pure.SdfShapes;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Procedural art: every sprite in the game composed from SDF primitives.
    /// Drawing functions assume a square canvas whose normalized units map to
    /// world units when shown at 64 px/unit on a 128 px texture (so "0.5" here
    /// is half a world unit). Silhouettes face +x; turrets point along +x.
    /// </summary>
    public static class ShapeArt
    {
        private static readonly Rgba Silver = Rgba.Hex(0xDCE2EA);
        private static readonly Rgba Ivory = Rgba.Hex(0xF4E8C8);
        private static readonly Rgba Glass = Rgba.Hex(0xD8F2EC, 0.9f);
        private static readonly Rgba PortalDark = Rgba.Hex(0x2B1846);

        private static ShapeStyle Toon(Rgba fill, float outline = 0.045f, float shading = 0.4f, float highlight = 0.35f) =>
            ShapeStyle.Toon(fill, outline, shading, highlight);

        private static Rgba ShadowColor(float a = 0.28f) => new Rgba(0f, 0f, 0f, a);

        // =================================================================== primitives

        /// <summary>White primitive with dark outline and bevel; tint via SpriteRenderer.color.</summary>
        public static void Primitive(PixelCanvas c, Sdf unitShape, bool outline = true, bool shadow = false)
        {
            var style = outline ? Toon(Rgba.White, 0.07f, 0.3f, 0.25f) : ShapeStyle.Flat(Rgba.White);
            if (shadow) style = style.WithShadow(0.04f, -0.08f, 0.08f, 0.3f);
            c.Draw(Scale(unitShape, outline ? 0.86f : 0.95f), style);
        }

        /// <summary>White radial glow (additive-looking soft light).</summary>
        public static void SoftGlow(PixelCanvas c) => c.Glow(0f, 0f, 1f, Rgba.White, 2.2f);

        /// <summary>Soft white disc used for puffs and ground shadows.</summary>
        public static void SoftDisc(PixelCanvas c, float softness = 0.45f) =>
            c.Fill(Circle(0f, 0f, 1f - softness * 0.6f), Rgba.White, softness);

        /// <summary>Thin anti-aliased white ring for shockwaves and range hints.</summary>
        public static void ThinRing(PixelCanvas c, float thickness = 0.06f) =>
            c.Fill(Annulus(Circle(0f, 0f, 0.92f - thickness), thickness), Rgba.White);

        /// <summary>Four-point sparkle.</summary>
        public static void Sparkle(PixelCanvas c)
        {
            c.Glow(0f, 0f, 0.8f, Rgba.White.WithAlpha(0.6f), 2f);
            c.Fill(Star(0f, 0f, 0.95f, 0.2f, 4), Rgba.White);
        }

        /// <summary>Muzzle flash burst.</summary>
        public static void Flash(PixelCanvas c)
        {
            c.Glow(0f, 0f, 1f, ArtPalette.Spark.WithAlpha(0.8f), 1.8f);
            c.Fill(Star(0f, 0f, 0.8f, 0.35f, 7), ArtPalette.Spark, 0.04f);
            c.Fill(Circle(0f, 0f, 0.3f), Rgba.White, 0.05f);
        }

        // =================================================================== towers

        public static void TowerColors(TowerArtKind kind, out Rgba body, out Rgba roof, out Rgba emblem)
        {
            switch (kind)
            {
                case TowerArtKind.Archer: body = ArtPalette.Archer; roof = ArtPalette.ArcherRoof; emblem = ArtPalette.Cream; break;
                case TowerArtKind.Mage: body = ArtPalette.Mage; roof = ArtPalette.MageRoof; emblem = ArtPalette.Gold; break;
                case TowerArtKind.Artillery: body = ArtPalette.Artillery; roof = ArtPalette.ArtilleryRoof; emblem = ArtPalette.Fire; break;
                case TowerArtKind.Frost: body = ArtPalette.Frost; roof = ArtPalette.FrostRoof; emblem = Rgba.White; break;
                case TowerArtKind.Poison: body = ArtPalette.Poison; roof = ArtPalette.PoisonRoof; emblem = ArtPalette.Toxic; break;
                default: body = ArtPalette.Generic; roof = ArtPalette.GenericRoof; emblem = ArtPalette.Cream; break;
            }
        }

        private static float BodyHalfWidth(TowerArtKind kind) => kind == TowerArtKind.Artillery ? 0.6f : 0.48f;
        private static float BodyTop(TowerArtKind kind) => kind == TowerArtKind.Artillery ? 0.16f : 0.34f;
        private const float BodyBottom = -0.6f;

        /// <summary>Local y (world units) where the turret pivot sits above the tower origin.</summary>
        public static float TurretMountY(TowerArtKind kind) => BodyTop(kind) + 0.26f;

        /// <summary>Stone platform, coloured body with masonry, door, battlements and an emblem.</summary>
        public static void TowerBase(PixelCanvas c, TowerArtKind kind)
        {
            TowerColors(kind, out Rgba body, out Rgba roof, out Rgba emblem);
            float hw = BodyHalfWidth(kind), top = BodyTop(kind);

            c.Fill(Ellipse(0.06f, -0.7f, 0.86f, 0.24f), ShadowColor(0.3f), 0.14f);
            c.Draw(RoundedBox(0f, -0.66f, 0.76f, 0.16f, 0.12f), Toon(ArtPalette.Stone, 0.045f, 0.45f, 0.3f));

            // Slightly tapered body.
            Sdf bodyShape = Round(Polygon(
                new Vec2f(-hw - 0.04f, BodyBottom), new Vec2f(hw + 0.04f, BodyBottom),
                new Vec2f(hw - 0.02f, top), new Vec2f(-hw + 0.02f, top)), 0.05f);
            c.Draw(bodyShape, Toon(body, 0.045f, 0.45f, 0.3f));

            // Masonry courses and joints, clipped inside the body.
            Sdf inner = Round(bodyShape, -0.05f);
            Rgba mortar = body.Darken(0.35f).WithAlpha(0.5f);
            int row = 0;
            for (float y = BodyBottom + 0.22f; y < top - 0.08f; y += 0.24f, row++)
            {
                c.Fill(Intersect(Capsule(-hw, y, hw, y, 0.014f), inner), mortar);
                float offset = (row & 1) == 0 ? -0.2f : 0.05f;
                for (float x = -hw + 0.1f + offset; x < hw; x += 0.34f)
                    c.Fill(Intersect(Capsule(x, y + 0.02f, x, y + 0.22f, 0.012f), inner), mortar);
            }

            // Door.
            c.Draw(Intersect(RoundedBox(0f, -0.45f, 0.15f, 0.19f, 0.15f), HalfPlane(0f, -1f, 0.62f)),
                   Toon(ArtPalette.WoodDark, 0.035f, 0.3f, 0.2f));

            // Battlements.
            float rimY = top + 0.05f;
            Sdf rim = Union(
                RoundedBox(0f, rimY, hw + 0.12f, 0.11f, 0.06f),
                RoundedBox(-hw - 0.02f, rimY + 0.15f, 0.12f, 0.09f, 0.04f),
                RoundedBox(0f, rimY + 0.15f, 0.12f, 0.09f, 0.04f),
                RoundedBox(hw + 0.02f, rimY + 0.15f, 0.12f, 0.09f, 0.04f));
            c.Draw(rim, Toon(roof, 0.045f, 0.4f, 0.4f));

            Emblem(c, kind, 0f, (BodyBottom + top) * 0.5f + 0.16f, 0.15f, emblem);
        }

        /// <summary>Kind-specific badge on the tower body.</summary>
        public static void Emblem(PixelCanvas c, TowerArtKind kind, float x, float y, float size, Rgba color)
        {
            Sdf shape;
            switch (kind)
            {
                case TowerArtKind.Archer: shape = Transform(UnitArrow(), x, y, size, MathF.PI * 0.5f); break;
                case TowerArtKind.Mage: shape = Transform(UnitStar(), x, y, size); break;
                case TowerArtKind.Artillery: shape = Union(Circle(x, y - 0.02f, size * 0.75f), Capsule(x + size * 0.4f, y + size * 0.5f, x + size * 0.75f, y + size * 0.95f, size * 0.15f)); break;
                case TowerArtKind.Frost: shape = Star(x, y, size, size * 0.38f, 6); break;
                case TowerArtKind.Poison: shape = Transform(UnitDroplet(), x, y, size); break;
                default: shape = Transform(UnitHexagon(), x, y, size); break;
            }
            c.Draw(shape, Toon(color, 0.035f, 0.25f, 0.3f));
        }

        /// <summary>Two pennants hanging from the battlements; white so the branch colour can tint them.</summary>
        public static void TowerAccent(PixelCanvas c, TowerArtKind kind)
        {
            float hw = BodyHalfWidth(kind), top = BodyTop(kind);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (hw - 0.14f);
                Sdf flag = Polygon(
                    new Vec2f(x - 0.1f, top - 0.02f), new Vec2f(x + 0.1f, top - 0.02f),
                    new Vec2f(x + 0.1f, top - 0.4f), new Vec2f(x, top - 0.32f), new Vec2f(x - 0.1f, top - 0.4f));
                c.Draw(flag, Toon(Rgba.White, 0.035f, 0.35f, 0.3f));
            }
            c.Draw(RoundedBox(0f, top + 0.05f, hw + 0.13f, 0.035f, 0.03f), Toon(Rgba.White, 0.03f, 0.2f, 0.2f));
        }

        /// <summary>Rotating top part, pointing along +x, pivot at the canvas centre.</summary>
        public static void TowerTurret(PixelCanvas c, TowerArtKind kind)
        {
            switch (kind)
            {
                case TowerArtKind.Mage:
                    c.Glow(0f, 0f, 0.6f, ArtPalette.Magic.WithAlpha(0.55f), 1.6f);
                    c.Draw(Annulus(Ellipse(0f, 0f, 0.4f, 0.15f), 0.022f), Toon(ArtPalette.Gold, 0.025f, 0.2f, 0.3f));
                    c.Draw(Circle(0f, 0f, 0.24f), Toon(ArtPalette.Magic.Lighten(0.15f), 0.045f, 0.5f, 0.5f));
                    c.Fill(Circle(-0.07f, 0.08f, 0.07f), Rgba.White.WithAlpha(0.85f), 0.02f);
                    break;

                case TowerArtKind.Artillery:
                    c.Draw(RoundedBox(0.2f, 0f, 0.34f, 0.13f, 0.06f), Toon(ArtPalette.Iron, 0.045f, 0.45f, 0.45f));
                    c.Draw(RoundedBox(0.52f, 0f, 0.06f, 0.17f, 0.04f), Toon(ArtPalette.Iron.Darken(0.25f), 0.04f, 0.3f, 0.4f));
                    c.Draw(Circle(-0.1f, 0f, 0.27f), Toon(ArtPalette.Iron.Lighten(0.12f), 0.045f, 0.45f, 0.45f));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * MathF.PI * 0.5f + MathF.PI * 0.25f;
                        c.Fill(Circle(-0.1f + MathF.Cos(a) * 0.17f, MathF.Sin(a) * 0.17f, 0.035f), ArtPalette.Stone);
                    }
                    break;

                case TowerArtKind.Frost:
                    c.Glow(0.1f, 0f, 0.55f, ArtPalette.Ice.WithAlpha(0.45f), 1.8f);
                    c.Draw(Polygon(new Vec2f(-0.22f, 0.02f), new Vec2f(-0.02f, 0.3f), new Vec2f(0.12f, 0.14f)), Toon(ArtPalette.FrostRoof, 0.035f));
                    c.Draw(Polygon(new Vec2f(-0.22f, -0.02f), new Vec2f(-0.02f, -0.3f), new Vec2f(0.12f, -0.14f)), Toon(ArtPalette.FrostRoof, 0.035f));
                    c.Draw(Polygon(new Vec2f(-0.28f, 0f), new Vec2f(0.05f, 0.17f), new Vec2f(0.58f, 0f), new Vec2f(0.05f, -0.17f)),
                           Toon(ArtPalette.Ice, 0.045f, 0.4f, 0.55f));
                    c.Draw(Circle(-0.24f, 0f, 0.12f), Toon(ArtPalette.Frost, 0.04f));
                    break;

                case TowerArtKind.Poison:
                    c.Draw(Capsule(0.2f, 0.02f, 0.44f, 0.12f, 0.06f), Toon(ArtPalette.Iron, 0.04f));
                    c.Draw(Circle(0f, -0.03f, 0.3f), Toon(ArtPalette.Iron, 0.045f, 0.45f, 0.4f));
                    c.Draw(Ellipse(0f, 0.12f, 0.23f, 0.08f), Toon(ArtPalette.Toxic, 0.025f, 0.2f, 0.4f));
                    c.Draw(Circle(-0.07f, 0.2f, 0.05f), Toon(ArtPalette.Toxic.Lighten(0.3f), 0.02f));
                    c.Draw(Circle(0.08f, 0.24f, 0.035f), Toon(ArtPalette.Toxic.Lighten(0.3f), 0.02f));
                    break;

                default: // Archer and Generic: a mounted ballista.
                    Rgba wood = kind == TowerArtKind.Generic ? ArtPalette.Iron.Lighten(0.2f) : ArtPalette.Wood;
                    c.Draw(Circle(-0.12f, 0f, 0.15f), Toon(ArtPalette.WoodDark, 0.04f));
                    c.Draw(Intersect(Annulus(Circle(-0.06f, 0f, 0.42f), 0.045f), HalfPlane(-1f, 0f, -0.1f)), Toon(wood, 0.035f, 0.35f, 0.4f));
                    c.Fill(Capsule(0.12f, 0.37f, -0.24f, 0f, 0.014f), ArtPalette.Cream);
                    c.Fill(Capsule(0.12f, -0.37f, -0.24f, 0f, 0.014f), ArtPalette.Cream);
                    c.Draw(Capsule(-0.32f, 0f, 0.4f, 0f, 0.035f), Toon(ArtPalette.Wood.Lighten(0.1f), 0.03f));
                    c.Draw(Polygon(new Vec2f(0.36f, 0.1f), new Vec2f(0.6f, 0f), new Vec2f(0.36f, -0.1f)), Toon(Silver, 0.03f));
                    break;
            }
        }

        /// <summary>Where the projectile leaves the turret, in turret-local units.</summary>
        public static float MuzzleDistance(TowerArtKind kind)
        {
            switch (kind)
            {
                case TowerArtKind.Mage: return 0.15f;
                case TowerArtKind.Artillery: return 0.6f;
                case TowerArtKind.Frost: return 0.55f;
                case TowerArtKind.Poison: return 0.45f;
                default: return 0.55f;
            }
        }

        // =================================================================== projectiles (64 px canvas)

        public static void Projectile(PixelCanvas c, ProjectileArtKind kind)
        {
            switch (kind)
            {
                case ProjectileArtKind.Orb:
                    c.Glow(0f, 0f, 1f, ArtPalette.Magic.WithAlpha(0.75f), 1.5f);
                    c.Draw(Circle(0f, 0f, 0.34f), Toon(ArtPalette.Magic.Lighten(0.35f), 0.06f, 0.3f, 0.6f));
                    c.Fill(Circle(-0.1f, 0.1f, 0.1f), Rgba.White, 0.03f);
                    break;
                case ProjectileArtKind.Shell:
                    c.Draw(Capsule(0.26f, 0.28f, 0.42f, 0.5f, 0.06f), Toon(ArtPalette.WoodDark, 0.05f));
                    c.Draw(Circle(0f, 0f, 0.42f), Toon(ArtPalette.Iron, 0.07f, 0.4f, 0.6f));
                    c.Glow(0.46f, 0.54f, 0.4f, ArtPalette.Fire.WithAlpha(0.9f), 1.5f);
                    c.Fill(Star(0.46f, 0.54f, 0.22f, 0.09f, 5), ArtPalette.Spark);
                    break;
                case ProjectileArtKind.Shard:
                    c.Glow(0f, 0f, 0.95f, ArtPalette.Ice.WithAlpha(0.5f), 1.8f);
                    c.Draw(Polygon(new Vec2f(-0.75f, 0f), new Vec2f(0f, 0.25f), new Vec2f(0.82f, 0f), new Vec2f(0f, -0.25f)),
                           Toon(ArtPalette.Ice, 0.07f, 0.35f, 0.6f));
                    break;
                case ProjectileArtKind.Flask:
                    c.Draw(RoundedBox(0f, 0.36f, 0.13f, 0.16f, 0.05f), Toon(Glass, 0.06f, 0.2f, 0.4f));
                    c.Draw(RoundedBox(0f, 0.55f, 0.12f, 0.08f, 0.03f), Toon(ArtPalette.Wood, 0.05f));
                    c.Draw(Circle(0f, -0.14f, 0.42f), Toon(Glass, 0.07f, 0.2f, 0.4f));
                    c.Fill(Intersect(Circle(0f, -0.14f, 0.34f), HalfPlane(0f, 1f, -0.08f)), ArtPalette.Toxic);
                    c.Fill(Circle(-0.14f, 0.0f, 0.06f), Rgba.White.WithAlpha(0.8f));
                    break;
                default: // Arrow
                    c.Draw(Capsule(-0.7f, 0f, 0.45f, 0f, 0.06f), Toon(ArtPalette.Wood, 0.06f, 0.2f, 0.3f));
                    c.Draw(Polygon(new Vec2f(0.38f, 0.2f), new Vec2f(0.88f, 0f), new Vec2f(0.38f, -0.2f)), Toon(Silver, 0.06f, 0.3f, 0.4f));
                    c.Draw(Polygon(new Vec2f(-0.85f, 0.24f), new Vec2f(-0.5f, 0.02f), new Vec2f(-0.72f, 0.02f)), Toon(ArtPalette.BranchA, 0.05f));
                    c.Draw(Polygon(new Vec2f(-0.85f, -0.24f), new Vec2f(-0.5f, -0.02f), new Vec2f(-0.72f, -0.02f)), Toon(ArtPalette.BranchA, 0.05f));
                    break;
            }
        }

        // =================================================================== enemies (128 px canvas, facing +x)

        /// <summary>Full-colour enemy body with the data tint baked in.</summary>
        public static void Enemy(PixelCanvas c, EnemyArtKind kind, Rgba tint)
        {
            tint = tint.WithAlpha(1f);
            Rgba dark = tint.Darken(0.4f);
            switch (kind)
            {
                case EnemyArtKind.Rogue: Rogue(c, tint, dark); break;
                case EnemyArtKind.Brute: Brute(c, tint, dark); break;
                case EnemyArtKind.Flyer: Flyer(c, tint, dark); break;
                case EnemyArtKind.Boss: Boss(c, tint, dark); break;
                default: Grunt(c, tint, dark); break;
            }
        }

        private static void Eye(PixelCanvas c, float x, float y, float r, Rgba pupil)
        {
            c.Draw(Circle(x, y, r), Toon(Rgba.White, 0.03f, 0.15f, 0f));
            c.Fill(Circle(x + r * 0.35f, y - r * 0.05f, r * 0.48f), pupil);
            c.Fill(Circle(x + r * 0.15f, y + r * 0.25f, r * 0.18f), Rgba.White);
        }

        private static void Feet(PixelCanvas c, Rgba dark, float spread, float y)
        {
            c.Draw(Ellipse(-spread, y, 0.17f, 0.1f), Toon(dark, 0.04f, 0.3f, 0.2f));
            c.Draw(Ellipse(spread, y, 0.17f, 0.1f), Toon(dark, 0.04f, 0.3f, 0.2f));
        }

        private static void Grunt(PixelCanvas c, Rgba tint, Rgba dark)
        {
            Feet(c, dark, 0.22f, -0.6f);
            c.Draw(Polygon(new Vec2f(-0.34f, 0.18f), new Vec2f(-0.46f, 0.56f), new Vec2f(-0.1f, 0.3f)), Toon(tint.Darken(0.2f), 0.04f));
            c.Draw(Polygon(new Vec2f(0.06f, 0.32f), new Vec2f(0.2f, 0.66f), new Vec2f(0.3f, 0.26f)), Toon(tint.Darken(0.2f), 0.04f));
            Sdf body = Ellipse(0f, -0.12f, 0.52f, 0.48f);
            c.Draw(body, Toon(tint, 0.05f, 0.45f, 0.4f));
            c.Fill(Intersect(Ellipse(0.12f, -0.26f, 0.28f, 0.24f), Round(body, -0.04f)), tint.Lighten(0.35f));
            Eye(c, 0.02f, 0.1f, 0.11f, ArtPalette.Outline);
            Eye(c, 0.25f, 0.08f, 0.13f, ArtPalette.Outline);
            c.Fill(Capsule(0.16f, -0.12f, 0.38f, -0.09f, 0.025f), ArtPalette.Outline);
            c.Fill(Polygon(new Vec2f(0.22f, -0.11f), new Vec2f(0.26f, -0.2f), new Vec2f(0.3f, -0.1f)), Rgba.White);
        }

        private static void Rogue(PixelCanvas c, Rgba tint, Rgba dark)
        {
            Feet(c, dark, 0.18f, -0.62f);
            Sdf body = Ellipse(0f, -0.18f, 0.4f, 0.46f);
            c.Draw(body, Toon(tint, 0.05f, 0.45f, 0.4f));
            c.Fill(Intersect(RoundedBox(0f, -0.3f, 0.5f, 0.05f, 0.02f), Round(body, -0.03f)), ArtPalette.WoodDark);
            c.Fill(Circle(0.12f, -0.3f, 0.05f), ArtPalette.Gold);
            // Hood with a face opening.
            Sdf hood = Union(Circle(0f, 0.14f, 0.38f), Polygon(new Vec2f(-0.36f, 0.22f), new Vec2f(-0.2f, 0.74f), new Vec2f(0.22f, 0.32f)));
            c.Draw(hood, Toon(tint.Darken(0.35f), 0.05f, 0.45f, 0.35f));
            c.Fill(Ellipse(0.17f, 0.07f, 0.2f, 0.16f), ArtPalette.Outline.WithAlpha(0.9f));
            c.Fill(Circle(0.12f, 0.08f, 0.045f), ArtPalette.Gold, 0.01f);
            c.Fill(Circle(0.27f, 0.08f, 0.045f), ArtPalette.Gold, 0.01f);
            // Dagger.
            c.Draw(Capsule(0.36f, -0.26f, 0.62f, -0.04f, 0.035f), Toon(Silver, 0.03f));
            c.Draw(Capsule(0.3f, -0.34f, 0.42f, -0.2f, 0.04f), Toon(ArtPalette.WoodDark, 0.03f));
        }

        private static void Brute(PixelCanvas c, Rgba tint, Rgba dark)
        {
            Feet(c, dark, 0.26f, -0.62f);
            c.Draw(RoundedBox(0f, -0.16f, 0.56f, 0.46f, 0.26f), Toon(tint, 0.05f, 0.45f, 0.35f));
            c.Draw(RoundedBox(0.06f, -0.2f, 0.4f, 0.24f, 0.12f), Toon(ArtPalette.Stone, 0.045f, 0.45f, 0.5f));
            c.Fill(Capsule(0.06f, -0.08f, 0.06f, -0.34f, 0.018f), ArtPalette.StoneDark);
            c.Draw(Circle(-0.36f, 0.06f, 0.2f), Toon(ArtPalette.StoneDark, 0.045f, 0.4f, 0.5f));
            // Helmet with horns and a visor slit.
            c.Draw(Polygon(new Vec2f(-0.2f, 0.42f), new Vec2f(-0.42f, 0.74f), new Vec2f(-0.06f, 0.52f)), Toon(Ivory, 0.04f));
            c.Draw(Polygon(new Vec2f(0.28f, 0.44f), new Vec2f(0.5f, 0.76f), new Vec2f(0.38f, 0.38f)), Toon(Ivory, 0.04f));
            Sdf helmet = Intersect(Circle(0.06f, 0.2f, 0.4f), HalfPlane(0f, -1f, -0.06f));
            c.Draw(helmet, Toon(ArtPalette.Stone, 0.05f, 0.45f, 0.55f));
            c.Fill(Capsule(0.14f, 0.24f, 0.42f, 0.24f, 0.04f), ArtPalette.Outline);
            c.Glow(0.32f, 0.24f, 0.1f, ArtPalette.Blood.Lighten(0.3f), 1.2f);
        }

        private static void Flyer(PixelCanvas c, Rgba tint, Rgba dark)
        {
            c.Draw(Polygon(new Vec2f(-0.28f, 0.2f), new Vec2f(-0.34f, 0.5f), new Vec2f(-0.1f, 0.3f)), Toon(dark, 0.04f));
            c.Draw(Polygon(new Vec2f(0.08f, 0.32f), new Vec2f(0.16f, 0.6f), new Vec2f(0.26f, 0.28f)), Toon(dark, 0.04f));
            c.Draw(Ellipse(0f, 0.04f, 0.38f, 0.32f), Toon(tint, 0.05f, 0.45f, 0.4f));
            Eye(c, 0.15f, 0.1f, 0.14f, ArtPalette.Blood.Darken(0.3f));
            c.Fill(Polygon(new Vec2f(0.12f, -0.1f), new Vec2f(0.16f, -0.2f), new Vec2f(0.2f, -0.1f)), Rgba.White);
            c.Fill(Polygon(new Vec2f(0.24f, -0.09f), new Vec2f(0.28f, -0.19f), new Vec2f(0.32f, -0.09f)), Rgba.White);
        }

        /// <summary>Bat wing rooted at the canvas centre, spreading up and back (flap by scaling y).</summary>
        public static void Wing(PixelCanvas c, Rgba tint)
        {
            Sdf wing = Polygon(
                new Vec2f(0.05f, -0.05f), new Vec2f(0.05f, 0.12f), new Vec2f(-0.25f, 0.62f), new Vec2f(-0.75f, 0.72f),
                new Vec2f(-0.62f, 0.45f), new Vec2f(-0.78f, 0.3f), new Vec2f(-0.5f, 0.26f), new Vec2f(-0.52f, 0.06f),
                new Vec2f(-0.28f, 0.1f));
            c.Draw(wing, Toon(tint.WithAlpha(1f).Darken(0.3f), 0.045f, 0.35f, 0.35f));
            c.Fill(Intersect(Capsule(0f, 0.05f, -0.68f, 0.66f, 0.016f), Round(wing, -0.02f)), tint.Darken(0.6f).WithAlpha(0.6f));
        }

        private static void Boss(PixelCanvas c, Rgba tint, Rgba dark)
        {
            Feet(c, dark, 0.32f, -0.66f);
            c.Draw(Polygon(new Vec2f(-0.4f, 0.32f), new Vec2f(-0.72f, 0.7f), new Vec2f(-0.62f, 0.3f)), Toon(Ivory, 0.045f));
            c.Draw(Polygon(new Vec2f(0.3f, 0.38f), new Vec2f(0.56f, 0.8f), new Vec2f(0.5f, 0.32f)), Toon(Ivory, 0.045f));
            Sdf body = RoundedBox(0f, -0.12f, 0.62f, 0.54f, 0.34f);
            c.Draw(body, Toon(tint, 0.055f, 0.45f, 0.35f));
            c.Fill(Intersect(Ellipse(0.12f, -0.3f, 0.34f, 0.26f), Round(body, -0.05f)), tint.Lighten(0.3f));
            c.Fill(Intersect(RoundedBox(0f, -0.42f, 0.7f, 0.07f, 0.03f), Round(body, -0.02f)), ArtPalette.WoodDark);
            c.Draw(RoundedBox(0.12f, -0.42f, 0.08f, 0.07f, 0.02f), Toon(ArtPalette.Gold, 0.03f));
            Eye(c, 0.06f, 0.18f, 0.12f, ArtPalette.Blood);
            Eye(c, 0.34f, 0.16f, 0.12f, ArtPalette.Blood);
            c.Fill(Capsule(-0.06f, 0.36f, 0.16f, 0.28f, 0.035f), ArtPalette.Outline);
            c.Fill(Capsule(0.24f, 0.28f, 0.46f, 0.34f, 0.035f), ArtPalette.Outline);
            c.Draw(RoundedBox(0.24f, -0.08f, 0.2f, 0.07f, 0.05f), Toon(ArtPalette.Outline.Lighten(0.1f), 0.02f, 0f, 0f));
            c.Draw(Polygon(new Vec2f(0.1f, -0.12f), new Vec2f(0.14f, 0.04f), new Vec2f(0.18f, -0.12f)), Toon(Ivory, 0.02f));
            c.Draw(Polygon(new Vec2f(0.3f, -0.12f), new Vec2f(0.34f, 0.04f), new Vec2f(0.38f, -0.12f)), Toon(Ivory, 0.02f));
        }

        /// <summary>Gold crown for bosses.</summary>
        public static void Crown(PixelCanvas c) =>
            c.Draw(Scale(UnitCrown(), 0.8f), Toon(ArtPalette.Gold, 0.07f, 0.4f, 0.5f).WithShadow(0.03f, -0.06f, 0.05f, 0.25f));

        // =================================================================== hero / barricade / slot

        public static void Hero(PixelCanvas c)
        {
            Rgba armor = Silver.Darken(0.05f);
            Feet(c, ArtPalette.Iron, 0.15f, -0.62f);
            c.Draw(Polygon(new Vec2f(-0.12f, 0.12f), new Vec2f(-0.44f, -0.5f), new Vec2f(0.04f, -0.42f)), Toon(ArtPalette.Hero, 0.045f));
            c.Draw(Capsule(-0.18f, -0.18f, -0.4f, 0.42f, 0.035f), Toon(Silver, 0.03f, 0.2f, 0.5f));
            c.Draw(Capsule(-0.26f, -0.04f, -0.08f, -0.1f, 0.03f), Toon(ArtPalette.Gold, 0.025f));
            c.Draw(RoundedBox(0f, -0.24f, 0.28f, 0.32f, 0.14f), Toon(armor, 0.045f, 0.45f, 0.5f));
            c.Draw(Capsule(-0.3f, 0.5f, 0.0f, 0.58f, 0.07f), Toon(ArtPalette.BranchA, 0.04f));
            c.Draw(Circle(0.02f, 0.26f, 0.26f), Toon(armor, 0.045f, 0.45f, 0.55f));
            c.Fill(Capsule(0.1f, 0.26f, 0.3f, 0.26f, 0.035f), ArtPalette.Outline);
            Sdf shield = Transform(UnitShield(), 0.3f, -0.16f, 0.3f);
            c.Draw(shield, Toon(ArtPalette.Hero, 0.045f, 0.4f, 0.45f));
            c.Draw(Star(0.3f, -0.14f, 0.12f, 0.05f, 5), Toon(ArtPalette.Gold, 0.02f, 0.2f, 0.3f));
        }

        public static void Barricade(PixelCanvas c)
        {
            c.Fill(Ellipse(0.04f, -0.56f, 0.8f, 0.18f), ShadowColor(0.28f), 0.12f);
            c.Draw(Capsule(-0.72f, -0.18f, 0.72f, -0.26f, 0.06f), Toon(ArtPalette.WoodDark, 0.04f));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.48f + i * 0.32f;
                float tilt = (i & 1) == 0 ? 0.12f : -0.08f;
                Sdf stake = Round(Polygon(
                    new Vec2f(x - 0.1f, -0.55f), new Vec2f(x + 0.1f, -0.55f),
                    new Vec2f(x + 0.1f + tilt, 0.25f), new Vec2f(x + tilt * 1.3f, 0.48f), new Vec2f(x - 0.1f + tilt, 0.25f)), 0.02f);
                c.Draw(stake, Toon(ArtPalette.Wood, 0.04f, 0.45f, 0.4f));
            }
            c.Draw(Capsule(-0.72f, 0.0f, 0.72f, -0.06f, 0.055f), Toon(ArtPalette.WoodDark.Lighten(0.1f), 0.04f));
        }

        /// <summary>Crack overlay; stage 1 is a single split, stage 2 adds more damage.</summary>
        public static void Cracks(PixelCanvas c, int stage)
        {
            Rgba col = ArtPalette.Outline.WithAlpha(0.9f);
            c.Fill(Stroke(new[] { new Vec2f(-0.5f, 0.3f), new Vec2f(-0.42f, 0.05f), new Vec2f(-0.52f, -0.15f), new Vec2f(-0.44f, -0.4f) }, 0.022f), col);
            c.Fill(Stroke(new[] { new Vec2f(0.18f, 0.28f), new Vec2f(0.24f, 0.08f), new Vec2f(0.12f, -0.05f) }, 0.02f), col);
            if (stage < 2) return;
            c.Fill(Stroke(new[] { new Vec2f(-0.18f, 0.32f), new Vec2f(-0.1f, 0.1f), new Vec2f(-0.22f, -0.12f), new Vec2f(-0.12f, -0.42f) }, 0.024f), col);
            c.Fill(Stroke(new[] { new Vec2f(0.5f, 0.3f), new Vec2f(0.44f, 0.0f), new Vec2f(0.56f, -0.24f) }, 0.022f), col);
            c.Fill(Stroke(new[] { new Vec2f(-0.6f, -0.2f), new Vec2f(0.1f, -0.24f) }, 0.018f), col.WithAlpha(0.6f));
        }

        /// <summary>Build plot: worn dirt patch ringed by stones.</summary>
        public static void Slot(PixelCanvas c)
        {
            c.Fill(Ellipse(0f, -0.08f, 0.86f, 0.6f), ShadowColor(0.2f), 0.12f);
            c.Draw(Ellipse(0f, -0.04f, 0.76f, 0.52f), Toon(ArtPalette.Dirt, 0.035f, 0.35f, 0.15f).WithOutline(ArtPalette.Dirt.Darken(0.5f), 0.035f));
            c.Fill(Ellipse(0.04f, 0f, 0.56f, 0.34f), ArtPalette.Dirt.Lighten(0.12f), 0.06f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * MathF.PI * 2f / 9f + 0.2f;
                float r = (i % 3 == 0) ? 0.1f : 0.075f;
                c.Draw(Ellipse(MathF.Cos(a) * 0.74f, MathF.Sin(a) * 0.5f - 0.04f, r * 1.2f, r),
                       Toon(ArtPalette.Stone, 0.03f, 0.4f, 0.45f));
            }
        }

        // =================================================================== map markers

        public static void PortalBase(PixelCanvas c)
        {
            c.Glow(0f, 0f, 1f, ArtPalette.Portal.WithAlpha(0.55f), 1.6f);
            c.Draw(Circle(0f, 0f, 0.5f), Toon(PortalDark, 0.0f, 0.3f, 0f));
            c.Draw(Annulus(Circle(0f, 0f, 0.56f), 0.08f), Toon(ArtPalette.Stone, 0.04f, 0.45f, 0.45f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI * 0.25f;
                c.Fill(Capsule(MathF.Cos(a) * 0.49f, MathF.Sin(a) * 0.49f, MathF.Cos(a) * 0.63f, MathF.Sin(a) * 0.63f, 0.012f), ArtPalette.StoneDark);
            }
        }

        public static void PortalSwirl(PixelCanvas c)
        {
            for (int arm = 0; arm < 3; arm++)
            {
                var pts = new Vec2f[12];
                for (int k = 0; k < pts.Length; k++)
                {
                    float t = k / (float)(pts.Length - 1);
                    float a = arm * MathF.PI * 2f / 3f + t * 2.6f;
                    float r = 0.06f + t * 0.36f;
                    pts[k] = new Vec2f(MathF.Cos(a) * r, MathF.Sin(a) * r);
                }
                c.Fill(Stroke(pts, 0.045f), ArtPalette.Portal.Lighten(0.45f).WithAlpha(0.85f), 0.02f);
            }
            c.Glow(0f, 0f, 0.3f, ArtPalette.Magic.Lighten(0.4f).WithAlpha(0.8f), 1.5f);
        }

        public static void ExitFlag(PixelCanvas c)
        {
            c.Fill(Ellipse(0.06f, -0.62f, 0.5f, 0.14f), ShadowColor(0.28f), 0.1f);
            c.Draw(Ellipse(0f, -0.56f, 0.34f, 0.13f), Toon(ArtPalette.Stone, 0.04f));
            c.Draw(Capsule(-0.12f, -0.52f, -0.12f, 0.7f, 0.04f), Toon(ArtPalette.WoodDark, 0.035f));
            c.Draw(Polygon(new Vec2f(-0.09f, 0.68f), new Vec2f(0.52f, 0.5f), new Vec2f(-0.09f, 0.28f)), Toon(ArtPalette.Hero, 0.04f, 0.4f, 0.45f));
            c.Draw(Star(0.1f, 0.49f, 0.1f, 0.045f, 5), Toon(ArtPalette.Gold, 0.02f, 0.2f, 0.3f));
            c.Draw(Circle(-0.12f, 0.76f, 0.07f), Toon(ArtPalette.Gold, 0.03f));
        }

        // =================================================================== decor (128 px cells)

        /// <summary>One decoration; foliage colour comes from the level's accent colour.</summary>
        public static void Decor(PixelCanvas c, DecorKind kind, int variant, Rgba foliage)
        {
            foliage = foliage.WithAlpha(1f);
            switch (kind)
            {
                case DecorKind.Tree:
                    c.Fill(Ellipse(0.1f, -0.66f, 0.56f, 0.16f), ShadowColor(0.3f), 0.12f);
                    c.Draw(RoundedBox(0f, -0.48f, 0.1f, 0.22f, 0.05f), Toon(ArtPalette.Wood, 0.04f));
                    Rgba leaf = variant % 2 == 0 ? foliage : foliage.Lighten(0.08f);
                    Sdf canopy = Union(Circle(-0.28f, -0.06f, 0.36f), Circle(0.26f, -0.04f, 0.34f), Circle(0f, 0.26f, 0.42f));
                    c.Draw(canopy, Toon(leaf, 0.045f, 0.5f, 0.4f));
                    c.Fill(Circle(-0.12f, 0.36f, 0.09f), leaf.Lighten(0.3f).WithAlpha(0.7f), 0.04f);
                    if (variant >= 2)
                    {
                        c.Fill(Circle(0.22f, 0.05f, 0.05f), ArtPalette.Blood, 0.01f);
                        c.Fill(Circle(-0.3f, -0.1f, 0.05f), ArtPalette.Blood, 0.01f);
                    }
                    break;

                case DecorKind.Pine:
                    c.Fill(Ellipse(0.1f, -0.68f, 0.46f, 0.14f), ShadowColor(0.3f), 0.12f);
                    c.Draw(RoundedBox(0f, -0.56f, 0.08f, 0.14f, 0.04f), Toon(ArtPalette.Wood, 0.04f));
                    Rgba needles = foliage.Darken(0.22f);
                    c.Draw(Round(Polygon(new Vec2f(-0.48f, -0.42f), new Vec2f(0.48f, -0.42f), new Vec2f(0f, 0.12f)), 0.04f), Toon(needles, 0.045f, 0.45f, 0.35f));
                    c.Draw(Round(Polygon(new Vec2f(-0.38f, -0.12f), new Vec2f(0.38f, -0.12f), new Vec2f(0f, 0.42f)), 0.04f), Toon(needles.Lighten(0.05f), 0.045f, 0.45f, 0.35f));
                    c.Draw(Round(Polygon(new Vec2f(-0.27f, 0.2f), new Vec2f(0.27f, 0.2f), new Vec2f(0f, 0.72f)), 0.04f), Toon(needles.Lighten(0.1f), 0.045f, 0.45f, 0.35f));
                    break;

                case DecorKind.Bush:
                    c.Fill(Ellipse(0.06f, -0.5f, 0.5f, 0.13f), ShadowColor(0.25f), 0.1f);
                    Rgba bush = foliage.Lighten(0.05f);
                    c.Draw(Union(Circle(-0.26f, -0.32f, 0.24f), Circle(0.24f, -0.32f, 0.22f), Circle(0f, -0.18f, 0.3f)), Toon(bush, 0.045f, 0.45f, 0.4f));
                    if (variant % 2 == 1)
                        for (int i = 0; i < 4; i++)
                            c.Fill(Circle(-0.2f + i * 0.14f, -0.2f + (i % 2) * 0.1f, 0.045f), i % 2 == 0 ? ArtPalette.Blood : ArtPalette.Gold, 0.01f);
                    break;

                case DecorKind.Rock:
                    c.Fill(Ellipse(0.06f, -0.46f, 0.42f, 0.12f), ShadowColor(0.25f), 0.1f);
                    Sdf rock = variant % 2 == 0
                        ? Round(Polygon(new Vec2f(-0.36f, -0.44f), new Vec2f(0.34f, -0.44f), new Vec2f(0.38f, -0.2f), new Vec2f(0.1f, 0.06f), new Vec2f(-0.22f, 0f)), 0.06f)
                        : Union(Round(Polygon(new Vec2f(-0.4f, -0.44f), new Vec2f(0.05f, -0.44f), new Vec2f(0f, -0.12f), new Vec2f(-0.3f, -0.08f)), 0.05f),
                                Round(Polygon(new Vec2f(0.02f, -0.44f), new Vec2f(0.36f, -0.44f), new Vec2f(0.3f, -0.24f), new Vec2f(0.08f, -0.22f)), 0.05f));
                    c.Draw(rock, Toon(ArtPalette.Stone, 0.045f, 0.5f, 0.5f));
                    break;

                case DecorKind.Flowers:
                    Rgba petal = variant switch { 0 => Rgba.Hex(0xFF7BA9), 1 => ArtPalette.Gold, 2 => Rgba.White, _ => Rgba.Hex(0xB58CFF) };
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -0.3f + i * 0.15f, y = -0.3f + ((i * 37) % 5) * 0.06f;
                        c.Fill(Capsule(x, y - 0.18f, x, y, 0.02f), foliage.Darken(0.2f));
                        c.Draw(Circle(x, y, 0.08f), Toon(petal, 0.03f, 0.3f, 0.3f));
                        c.Fill(Circle(x, y, 0.03f), ArtPalette.Gold.Darken(0.1f));
                    }
                    break;

                case DecorKind.Stump:
                    c.Fill(Ellipse(0.06f, -0.4f, 0.32f, 0.1f), ShadowColor(0.25f), 0.08f);
                    c.Draw(RoundedBox(0f, -0.3f, 0.22f, 0.13f, 0.06f), Toon(ArtPalette.Wood, 0.04f));
                    c.Draw(Ellipse(0f, -0.17f, 0.22f, 0.08f), Toon(ArtPalette.Wood.Lighten(0.25f), 0.03f, 0.2f, 0.2f));
                    c.Fill(Annulus(Ellipse(0f, -0.17f, 0.12f, 0.04f), 0.008f), ArtPalette.WoodDark);
                    break;

                default: // Grass tuft
                    Rgba blade = foliage.Lighten(0.1f);
                    c.Fill(Capsule(-0.12f, -0.4f, -0.2f, -0.1f, 0.03f), blade);
                    c.Fill(Capsule(0f, -0.4f, 0f, -0.04f, 0.03f), blade.Lighten(0.1f));
                    c.Fill(Capsule(0.12f, -0.4f, 0.22f, -0.14f, 0.03f), blade);
                    break;
            }
        }

        // =================================================================== ground / glyphs / bars

        /// <summary>
        /// Seamless near-white ground tile (tinted by the level ground colour):
        /// low-frequency mottling, fine grain and a few lighter grass specks.
        /// </summary>
        public static void GroundTile(PixelCanvas c, uint seed, int period = 4)
        {
            int w = c.Width, h = c.Height;
            c.PaintPixels((i, j) =>
            {
                float u = (float)i / w * period, v = (float)j / h * period;
                float n = TileableNoise.Fractal(u, v, period, seed);
                float grain = StableHash.Lattice(i, j, seed + 7u);
                float k = 0.86f + n * 0.14f + (grain - 0.5f) * 0.035f;
                if (grain > 0.985f) k += 0.07f;
                return new Rgba(k, k, k, 1f);
            });
        }

        public static void Glyph(PixelCanvas c, char ch)
        {
            Sdf shape = DigitFont.GlyphShape(ch, 1.15f);
            c.Draw(shape, ShapeStyle.Toon(Rgba.White, 0.13f, 0.25f, 0.35f));
        }

        /// <summary>Rounded bar on a wide canvas (health bar back or fill).</summary>
        public static void Bar(PixelCanvas c, Rgba fill, float inset)
        {
            float hw = c.Aspect - inset, hh = 1f - inset;
            c.Fill(RoundedBox(0f, 0f, hw, hh, hh), fill);
        }
    }
}
