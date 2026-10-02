using System;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Visuals.Pure;

namespace TowerDefense.Visuals.Tests
{
    public class CameraFitTests
    {
        [Test]
        public void IPhoneWideScreen_IsHeightBound()
        {
            float aspect = 19.5f / 9f;
            float size = CameraFit.OrthoSizeToFit(32f, 18f, aspect);
            Assert.That(size, Is.EqualTo(9f).Within(1e-4));
            Vec2f visible = CameraFit.VisibleSize(size, aspect);
            Assert.That(visible.X, Is.GreaterThanOrEqualTo(32f));
            Assert.That(visible.Y, Is.EqualTo(18f).Within(1e-4));
        }

        [Test]
        public void IPad4By3_IsWidthBound()
        {
            float aspect = 4f / 3f;
            float size = CameraFit.OrthoSizeToFit(32f, 18f, aspect);
            Assert.That(size, Is.EqualTo(12f).Within(1e-4));
            Vec2f visible = CameraFit.VisibleSize(size, aspect);
            Assert.That(visible.X, Is.EqualTo(32f).Within(1e-3));
            Assert.That(visible.Y, Is.GreaterThanOrEqualTo(18f));
        }

        [TestCase(16f / 9f)]
        [TestCase(19.5f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(3f / 2f)]
        [TestCase(1f)]
        public void Fit_AlwaysShowsWholeWorld_AndFillNeverShowsOutside(float aspect)
        {
            float fit = CameraFit.OrthoSizeToFit(32f, 18f, aspect, 0.05f);
            Vec2f v = CameraFit.VisibleSize(fit, aspect);
            Assert.That(v.X, Is.GreaterThanOrEqualTo(32f - 1e-3f));
            Assert.That(v.Y, Is.GreaterThanOrEqualTo(18f - 1e-3f));

            float fill = CameraFit.OrthoSizeToFill(32f, 18f, aspect);
            Vec2f f = CameraFit.VisibleSize(fill, aspect);
            Assert.That(f.X, Is.LessThanOrEqualTo(32f + 1e-3f));
            Assert.That(f.Y, Is.LessThanOrEqualTo(18f + 1e-3f));
        }

        [Test]
        public void Padding_GrowsSize()
        {
            Assert.That(CameraFit.OrthoSizeToFit(32f, 18f, 16f / 9f, 0.1f), Is.EqualTo(9.9f).Within(1e-4));
        }
    }

    public class PathGeometryTests
    {
        private static readonly Vec2f[] L = { new Vec2f(0, 0), new Vec2f(4, 0), new Vec2f(4, 3) };

        [Test]
        public void Length_SumsSegments() => Assert.That(PathGeometry.Length(L), Is.EqualTo(7f).Within(1e-5));

        [Test]
        public void PointAndDirectionAtDistance()
        {
            Vec2f p = PathGeometry.PointAtDistance(L, 5f);
            Assert.That(p.X, Is.EqualTo(4f).Within(1e-5));
            Assert.That(p.Y, Is.EqualTo(1f).Within(1e-5));
            Vec2f d = PathGeometry.DirectionAtDistance(L, 5f);
            Assert.That(d.Y, Is.EqualTo(1f).Within(1e-5));
            Assert.That(PathGeometry.PointAtDistance(L, 100f).Y, Is.EqualTo(3f).Within(1e-5));
            Assert.That(PathGeometry.PointAtDistance(L, -1f).X, Is.EqualTo(0f));
        }

        [Test]
        public void DistanceToPolyline()
        {
            Assert.That(PathGeometry.DistanceToPolyline(new Vec2f(2, 1), L), Is.EqualTo(1f).Within(1e-5));
            Assert.That(PathGeometry.DistanceToPolyline(new Vec2f(5, 2), L), Is.EqualTo(1f).Within(1e-5));
            Assert.That(PathGeometry.DistanceToPolyline(new Vec2f(0, 0), Array.Empty<Vec2f>()), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void InsetEndpoint_WalksIntoTheWorld()
        {
            var path = new[] { new Vec2f(-2, 5), new Vec2f(10, 5), new Vec2f(10, 20) };
            Vec2f start = PathGeometry.InsetEndpoint(path, 32f, 18f, 1f, fromEnd: false);
            Assert.That(start.X, Is.EqualTo(1f).Within(0.11f));
            Assert.That(start.Y, Is.EqualTo(5f).Within(1e-4));
            Vec2f end = PathGeometry.InsetEndpoint(path, 32f, 18f, 1f, fromEnd: true);
            Assert.That(end.X, Is.EqualTo(10f).Within(1e-4));
            Assert.That(end.Y, Is.EqualTo(17f).Within(0.11f));
            Vec2f inside = PathGeometry.InsetEndpoint(new[] { new Vec2f(5, 5), new Vec2f(6, 6) }, 32f, 18f, 1f, false);
            Assert.That(inside.X, Is.EqualTo(5f));
        }

        [Test]
        public void RectSignedDistance()
        {
            Assert.That(PathGeometry.RectSignedDistance(new Vec2f(16, 9), 32, 18), Is.EqualTo(-9f).Within(1e-4));
            Assert.That(PathGeometry.RectSignedDistance(new Vec2f(-3, 9), 32, 18), Is.EqualTo(3f).Within(1e-4));
            Assert.That(PathGeometry.RectSignedDistance(new Vec2f(35, 22), 32, 18), Is.EqualTo(5f).Within(1e-4));
        }

        [Test]
        public void Profiles_LineEdgeAndVignette()
        {
            Assert.That(Profiles.LineEdge(0.5f, 0.1f), Is.EqualTo(1f));
            Assert.That(Profiles.LineEdge(0f, 0.1f), Is.EqualTo(0f));
            Assert.That(Profiles.LineEdge(1f, 0.1f), Is.EqualTo(0f));
            Assert.That(Profiles.LineEdge(0.05f, 0.1f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(Profiles.Vignette(-10f), Is.EqualTo(0f).Within(1e-5));
            Assert.That(Profiles.Vignette(0f), Is.EqualTo(0.16f).Within(1e-4));
            Assert.That(Profiles.Vignette(10f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(Profiles.Vignette(1f), Is.GreaterThan(Profiles.Vignette(0f)));
        }
    }

    public class DecorScatterTests
    {
        private static readonly List<IReadOnlyList<Vec2f>> Paths = new List<IReadOnlyList<Vec2f>>
        {
            new[] { new Vec2f(-1, 14), new Vec2f(7, 14), new Vec2f(7, 8), new Vec2f(15, 8), new Vec2f(15, 13), new Vec2f(24, 13), new Vec2f(24, 6), new Vec2f(33, 6) },
            new[] { new Vec2f(12, -1), new Vec2f(12, 3), new Vec2f(24, 3), new Vec2f(24, 6) },
        };
        private static readonly Vec2f[] Slots = { new Vec2f(4.5f, 11f), new Vec2f(10, 11), new Vec2f(18, 6), new Vec2f(27, 9.5f) };
        private static readonly Vec2f[] Markers = { new Vec2f(11, 8), new Vec2f(1, 14), new Vec2f(31, 6) };

        private static ScatterSettings Settings(uint seed = 42) => new ScatterSettings { Seed = seed, WorldWidth = 32f, WorldHeight = 18f };

        [Test]
        public void IsDeterministic()
        {
            var a = DecorScatter.Scatter(Settings(), Paths, Slots, Markers);
            var b = DecorScatter.Scatter(Settings(), Paths, Slots, Markers);
            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                Assert.That(a[i].Y, Is.EqualTo(b[i].Y));
                Assert.That(a[i].Kind, Is.EqualTo(b[i].Kind));
            }
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            var a = DecorScatter.Scatter(Settings(1), Paths, Slots, Markers);
            var b = DecorScatter.Scatter(Settings(2), Paths, Slots, Markers);
            Assert.That(a[0].X != b[0].X || a[0].Y != b[0].Y, Is.True);
        }

        [Test]
        public void ProducesInteriorAndBorderItems()
        {
            var items = DecorScatter.Scatter(Settings(), Paths, Slots, Markers);
            int interior = 0, border = 0;
            foreach (var it in items) { if (it.Border) border++; else interior++; }
            Assert.That(interior, Is.GreaterThan(10));
            Assert.That(border, Is.GreaterThan(80));
        }

        [Test]
        public void RespectsPathSlotMarkerAndEdgeClearance()
        {
            var s = Settings();
            var items = DecorScatter.Scatter(s, Paths, Slots, Markers);
            foreach (var it in items)
            {
                var p = new Vec2f(it.X, it.Y);
                float r = DecorScatter.Radius(it.Kind) * it.Scale;
                foreach (var path in Paths)
                    Assert.That(PathGeometry.DistanceToPolyline(p, path), Is.GreaterThanOrEqualTo(s.PathClearance + r - 1e-4f), $"{it.Kind} on path at {p}");
                foreach (var slot in Slots)
                    Assert.That(Vec2f.Distance(p, slot), Is.GreaterThanOrEqualTo(s.SlotClearance + r - 1e-4f), $"{it.Kind} on slot at {p}");
                foreach (var m in Markers)
                    Assert.That(Vec2f.Distance(p, m), Is.GreaterThanOrEqualTo(s.MarkerClearance + r - 1e-4f));

                float rectD = PathGeometry.RectSignedDistance(p, s.WorldWidth, s.WorldHeight);
                if (it.Border)
                {
                    Assert.That(rectD, Is.GreaterThan(0f), "border item inside the play area");
                    Assert.That(rectD, Is.LessThanOrEqualTo(s.BorderWidth + 1e-4f));
                }
                else
                {
                    Assert.That(it.X, Is.InRange(s.EdgeMargin + r - 1e-4f, s.WorldWidth - s.EdgeMargin - r + 1e-4f));
                    Assert.That(it.Y, Is.InRange(s.EdgeMargin + r - 1e-4f, s.WorldHeight - s.EdgeMargin - r + 1e-4f));
                }
            }
        }

        [Test]
        public void InteriorItemsDoNotOverlap()
        {
            var s = Settings();
            var items = DecorScatter.Scatter(s, Paths, Slots, Markers);
            var interior = items.FindAll(i => !i.Border);
            for (int i = 0; i < interior.Count; i++)
                for (int j = i + 1; j < interior.Count; j++)
                {
                    float min = DecorScatter.Radius(interior[i].Kind) * interior[i].Scale + DecorScatter.Radius(interior[j].Kind) * interior[j].Scale + s.MinSpacing;
                    float d = Vec2f.Distance(new Vec2f(interior[i].X, interior[i].Y), new Vec2f(interior[j].X, interior[j].Y));
                    Assert.That(d, Is.GreaterThanOrEqualTo(min - 1e-4f));
                }
        }

        [Test]
        public void IsClear_RejectsPointOnPathAcceptsOpenGround()
        {
            var s = Settings();
            Assert.That(DecorScatter.IsClear(s, 7f, 11f, 0.3f, false, Paths, Slots, Markers), Is.False);
            Assert.That(DecorScatter.IsClear(s, 19.5f, 10.5f, 0.3f, false, Paths, Slots, Markers), Is.True);
            Assert.That(DecorScatter.IsClear(s, 19.5f, 10.5f, 0.3f, true, Paths, Slots, Markers), Is.False, "interior point is not border");
            Assert.That(DecorScatter.IsClear(s, 16f, 21f, 0.3f, true, Paths, Slots, Markers), Is.True);
            Assert.That(DecorScatter.IsClear(s, 0.2f, 10.5f, 0.3f, false, Paths, Slots, Markers), Is.False, "too close to the edge");
        }

        [Test]
        public void StableHash_IsProcessIndependent()
        {
            Assert.That(StableHash.Fnv1a("meadow"), Is.EqualTo(StableHash.Fnv1a("meadow")));
            Assert.That(StableHash.Fnv1a(""), Is.EqualTo(2166136261u));
            Assert.That(StableHash.Fnv1a("a"), Is.EqualTo(0xE40C292Cu));
        }

        [Test]
        public void DeterministicRandom_RangesAndRepeatability()
        {
            var a = new DeterministicRandom(7);
            var b = new DeterministicRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                float f = a.NextFloat();
                Assert.That(f, Is.EqualTo(b.NextFloat()));
                Assert.That(f, Is.InRange(0f, 0.9999999f));
                int n = a.Range(3, 9);
                b.Range(3, 9);
                Assert.That(n, Is.InRange(3, 8));
            }
        }

        [Test]
        public void TileableNoise_WrapsAtPeriod()
        {
            for (float x = 0f; x < 4f; x += 0.37f)
                for (float y = 0f; y < 4f; y += 0.41f)
                {
                    float n = TileableNoise.Fractal(x, y, 4, 9u);
                    Assert.That(TileableNoise.Fractal(x + 4f, y, 4, 9u), Is.EqualTo(n).Within(1e-4));
                    Assert.That(TileableNoise.Fractal(x, y + 4f, 4, 9u), Is.EqualTo(n).Within(1e-4));
                    Assert.That(n, Is.InRange(0f, 1f));
                }
        }
    }

    public class DigitFontTests
    {
        [Test]
        public void AllCharactersHaveStrokesInsideTheCell()
        {
            foreach (char ch in DigitFont.Characters)
            {
                var strokes = DigitFont.Strokes(ch);
                Assert.That(strokes.Length, Is.GreaterThan(0), ch.ToString());
                foreach (var line in strokes)
                {
                    Assert.That(line.Length, Is.GreaterThanOrEqualTo(2));
                    foreach (var p in line)
                    {
                        Assert.That(p.X, Is.InRange(0f, DigitFont.CellWidth));
                        Assert.That(p.Y, Is.InRange(0f, DigitFont.CellHeight));
                    }
                }
            }
        }

        [Test]
        public void GlyphsAreDistinct()
        {
            var c8 = new PixelCanvas(32);
            c8.Fill(DigitFont.GlyphShape('8', 1f), Rgba.White);
            var c0 = new PixelCanvas(32);
            c0.Fill(DigitFont.GlyphShape('0', 1f), Rgba.White);
            Assert.That(c8.TotalAlpha(), Is.GreaterThan(c0.TotalAlpha() + 5f), "8 has a middle bar that 0 lacks");
        }

        [Test]
        public void FormatAndLayout()
        {
            var buf = new char[12];
            int n = DigitFont.Format(125, true, buf);
            Assert.That(new string(buf, 0, n), Is.EqualTo("+125"));
            n = DigitFont.Format(-7, true, buf);
            Assert.That(new string(buf, 0, n), Is.EqualTo("-7"));
            n = DigitFont.Format(0, false, buf);
            Assert.That(new string(buf, 0, n), Is.EqualTo("0"));

            var offsets = new float[8];
            int count = DigitFont.Layout("+12", offsets);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(offsets[0] + offsets[2], Is.EqualTo(0f).Within(1e-5), "centred");
            Assert.That(offsets[1] - offsets[0], Is.EqualTo(DigitFont.Advance).Within(1e-5));
            Assert.That(DigitFont.Layout("a1", offsets), Is.EqualTo(1));
        }
    }

    public class ClassifierTests
    {
        [TestCase("archer", false, false, false, false, false, TowerArtKind.Archer)]
        [TestCase("mage_tower", false, false, false, false, false, TowerArtKind.Mage)]
        [TestCase("Artillery", false, false, true, false, false, TowerArtKind.Artillery)]
        [TestCase("frost", false, false, false, true, false, TowerArtKind.Frost)]
        [TestCase("venom_spire", false, false, false, false, true, TowerArtKind.Poison)]
        [TestCase("tower7", true, false, false, false, false, TowerArtKind.Mage)]
        [TestCase("tower8", false, false, true, false, false, TowerArtKind.Artillery)]
        [TestCase("tower9", false, false, false, true, false, TowerArtKind.Frost)]
        [TestCase("tower10", false, true, false, false, false, TowerArtKind.Poison)]
        [TestCase("tower11", false, false, false, false, false, TowerArtKind.Archer)]
        [TestCase("", false, false, false, false, false, TowerArtKind.Generic)]
        [TestCase(null, false, false, false, false, false, TowerArtKind.Generic)]
        public void Tower(string id, bool magic, bool poisonDmg, bool splash, bool slow, bool poison, TowerArtKind expected)
        {
            Assert.That(ArtClassifier.Tower(id, magic, poisonDmg, splash, slow, poison), Is.EqualTo(expected));
        }

        [Test]
        public void ProjectileFollowsTower()
        {
            Assert.That(ArtClassifier.Projectile(TowerArtKind.Archer), Is.EqualTo(ProjectileArtKind.Arrow));
            Assert.That(ArtClassifier.Projectile(TowerArtKind.Mage), Is.EqualTo(ProjectileArtKind.Orb));
            Assert.That(ArtClassifier.Projectile(TowerArtKind.Artillery), Is.EqualTo(ProjectileArtKind.Shell));
            Assert.That(ArtClassifier.Projectile(TowerArtKind.Frost), Is.EqualTo(ProjectileArtKind.Shard));
            Assert.That(ArtClassifier.Projectile(TowerArtKind.Poison), Is.EqualTo(ProjectileArtKind.Flask));
        }

        [Test]
        public void Enemy()
        {
            Assert.That(ArtClassifier.Enemy(0, false, false), Is.EqualTo(EnemyArtKind.Grunt));
            Assert.That(ArtClassifier.Enemy(1, false, false), Is.EqualTo(EnemyArtKind.Rogue));
            Assert.That(ArtClassifier.Enemy(2, false, false), Is.EqualTo(EnemyArtKind.Brute));
            Assert.That(ArtClassifier.Enemy(2, true, false), Is.EqualTo(EnemyArtKind.Flyer));
            Assert.That(ArtClassifier.Enemy(0, true, true), Is.EqualTo(EnemyArtKind.Boss));
        }

        [Test]
        public void HealthColor_GoesGreenToRed()
        {
            Assert.That(ArtPalette.HealthColor(1f).G, Is.GreaterThan(ArtPalette.HealthColor(1f).R));
            Assert.That(ArtPalette.HealthColor(0f).R, Is.GreaterThan(ArtPalette.HealthColor(0f).G));
        }
    }
}
