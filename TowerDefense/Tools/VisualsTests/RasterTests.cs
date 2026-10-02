using System;
using NUnit.Framework;
using TowerDefense.Visuals.Pure;

namespace TowerDefense.Visuals.Tests
{
    public class SdfTests
    {
        [Test]
        public void Circle_DistanceIsSignedAndExact()
        {
            Sdf c = SdfShapes.Circle(0f, 0f, 0.5f);
            Assert.That(c(0f, 0f), Is.EqualTo(-0.5f).Within(1e-5));
            Assert.That(c(0.5f, 0f), Is.EqualTo(0f).Within(1e-5));
            Assert.That(c(0f, 1f), Is.EqualTo(0.5f).Within(1e-5));
        }

        [Test]
        public void RoundedBox_InsideOutsideAndEdge()
        {
            Sdf b = SdfShapes.RoundedBox(0f, 0f, 0.5f, 0.25f, 0.1f);
            Assert.That(b(0f, 0f), Is.LessThan(0f));
            Assert.That(b(0.5f, 0f), Is.EqualTo(0f).Within(1e-5));
            Assert.That(b(0f, 0.25f), Is.EqualTo(0f).Within(1e-5));
            Assert.That(b(1f, 0f), Is.EqualTo(0.5f).Within(1e-5));
        }

        [Test]
        public void Polygon_SquareMatchesBoxDistance()
        {
            Sdf p = SdfShapes.Polygon(new Vec2f(-1, -1), new Vec2f(1, -1), new Vec2f(1, 1), new Vec2f(-1, 1));
            Assert.That(p(0f, 0f), Is.EqualTo(-1f).Within(1e-5));
            Assert.That(p(2f, 0f), Is.EqualTo(1f).Within(1e-5));
            Assert.That(p(2f, 2f), Is.EqualTo(MathF.Sqrt(2f)).Within(1e-5));
        }

        [Test]
        public void Polygon_WindingDoesNotMatter()
        {
            Sdf cw = SdfShapes.Polygon(new Vec2f(0, 1), new Vec2f(1, -1), new Vec2f(-1, -1));
            Sdf ccw = SdfShapes.Polygon(new Vec2f(0, 1), new Vec2f(-1, -1), new Vec2f(1, -1));
            for (float x = -1.5f; x <= 1.5f; x += 0.25f)
                for (float y = -1.5f; y <= 1.5f; y += 0.25f)
                    Assert.That(cw(x, y), Is.EqualTo(ccw(x, y)).Within(1e-5));
        }

        [Test]
        public void Star_HasPointsAtOuterRadius()
        {
            Sdf s = SdfShapes.Star(0f, 0f, 1f, 0.4f, 5);
            Assert.That(s(0f, 1f), Is.EqualTo(0f).Within(1e-4)); // top tip
            Assert.That(s(0f, 0.95f), Is.LessThan(0f));
            Assert.That(s(0f, -0.5f), Is.GreaterThan(-0.2f)); // notch between the two lower points
        }

        [Test]
        public void Operators_UnionIntersectSubtract()
        {
            Sdf a = SdfShapes.Circle(-0.5f, 0f, 0.6f), b = SdfShapes.Circle(0.5f, 0f, 0.6f);
            Assert.That(SdfShapes.Union(a, b)(-0.9f, 0f), Is.LessThan(0f));
            Assert.That(SdfShapes.Intersect(a, b)(-0.9f, 0f), Is.GreaterThan(0f));
            Assert.That(SdfShapes.Intersect(a, b)(0f, 0f), Is.LessThan(0f));
            Assert.That(SdfShapes.Subtract(a, b)(0f, 0f), Is.GreaterThan(0f));
            Assert.That(SdfShapes.Subtract(a, b)(-0.9f, 0f), Is.LessThan(0f));
        }

        [Test]
        public void Transform_ScalesRotatesAndMoves()
        {
            Sdf unit = SdfShapes.Circle(1f, 0f, 0.1f);
            Sdf t = SdfShapes.Transform(unit, 2f, 3f, 2f, MathF.PI * 0.5f);
            // (1,0) scaled by 2 -> (2,0), rotated 90deg -> (0,2), translated -> (2,5).
            Assert.That(t(2f, 5f), Is.EqualTo(-0.2f).Within(1e-4));
        }

        [TestCase(0f, 0.5f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, 0f)]
        public void Coverage_FromDistance(float dInPixels, float expected)
        {
            Assert.That(Coverage.FromDistance(dInPixels * 0.1f, 0.1f), Is.EqualTo(expected).Within(1e-5));
        }

        [Test]
        public void Coverage_IsMonotonic()
        {
            float prev = 1f;
            for (float d = -0.2f; d <= 0.2f; d += 0.01f)
            {
                float c = Coverage.FromDistance(d, 0.1f);
                Assert.That(c, Is.LessThanOrEqualTo(prev + 1e-6f));
                prev = c;
            }
        }

        [Test]
        public void NamedShapes_FitInsideUnitCircleAndContainOrigin()
        {
            var shapes = new[] { SdfShapes.UnitTriangle(), SdfShapes.UnitDiamond(), SdfShapes.UnitHexagon(), SdfShapes.UnitStar(),
                                 SdfShapes.UnitShield(), SdfShapes.UnitCrown(), SdfShapes.UnitArrow(), SdfShapes.UnitDroplet() };
            foreach (var s in shapes)
            {
                for (int k = 0; k < 64; k++)
                {
                    float a = k * MathF.PI * 2f / 64f;
                    Assert.That(s(MathF.Cos(a) * 1.2f, MathF.Sin(a) * 1.2f), Is.GreaterThan(0f), "shape leaks well outside the unit circle");
                }
            }
            Assert.That(SdfShapes.UnitShield()(0f, 0f), Is.LessThan(0f));
            Assert.That(SdfShapes.UnitDroplet()(0f, 0f), Is.LessThan(0f));
            Assert.That(SdfShapes.UnitArrow()(0f, 0f), Is.LessThan(0f));
            Assert.That(SdfShapes.UnitRing()(0f, 0f), Is.GreaterThan(0f), "ring must be hollow");
        }
    }

    public class CanvasTests
    {
        [Test]
        public void FilledCircle_AreaMatchesPiRSquared()
        {
            var c = new PixelCanvas(128);
            c.Fill(SdfShapes.Circle(0f, 0f, 0.5f), Rgba.White);
            float radiusPx = 0.5f / c.PixelSize;
            float expected = MathF.PI * radiusPx * radiusPx;
            Assert.That(c.TotalAlpha(), Is.EqualTo(expected).Within(expected * 0.01f));
        }

        [Test]
        public void EdgesAreAntiAliased()
        {
            var c = new PixelCanvas(64);
            c.Fill(SdfShapes.Circle(0f, 0f, 0.6f), Rgba.White);
            int partial = 0;
            for (int j = 0; j < 64; j++)
                for (int i = 0; i < 64; i++)
                {
                    float a = c.AlphaAt(i, j);
                    if (a > 0.05f && a < 0.95f) partial++;
                }
            Assert.That(partial, Is.GreaterThan(40), "expected a ring of partially covered edge pixels");
        }

        [Test]
        public void Outline_SurroundsFill()
        {
            var c = new PixelCanvas(128);
            var style = ShapeStyle.Toon(new Rgba(1f, 0f, 0f), 0.1f, 0f, 0f);
            c.Draw(SdfShapes.Circle(0f, 0f, 0.5f), style);
            Rgba centre = c.GetPixel(64, 64);
            Assert.That(centre.R, Is.GreaterThan(0.9f));
            // A pixel ~0.55 from centre lies in the outline band.
            int px = 64 + (int)(0.55f / c.PixelSize);
            Rgba ring = c.GetPixel(px, 64);
            Assert.That(ring.A, Is.GreaterThan(0.9f));
            Assert.That(ring.R, Is.LessThan(0.3f));
        }

        [Test]
        public void DropShadow_IsOffsetAndTranslucent()
        {
            var c = new PixelCanvas(128);
            c.Draw(SdfShapes.Circle(0f, 0f, 0.3f), ShapeStyle.Flat(Rgba.White).WithShadow(0.3f, -0.3f, 0.02f, 0.4f));
            int sx = 64 + (int)(0.45f / c.PixelSize), sy = 64 - (int)(0.45f / c.PixelSize);
            Assert.That(c.AlphaAt(sx, sy), Is.EqualTo(0.4f).Within(0.05f));
            Assert.That(c.GetPixel(64, 64).R, Is.GreaterThan(0.95f));
        }

        [Test]
        public void ExportBleedsColourIntoTransparentTexels()
        {
            var c = new PixelCanvas(32);
            c.Fill(SdfShapes.Circle(0f, 0f, 0.5f), new Rgba(1f, 0.5f, 0f));
            byte[] bytes = c.ToRgba32(Rgba.White);
            Assert.That(bytes.Length, Is.EqualTo(32 * 32 * 4));
            // Find a fully transparent texel next to the shape: its RGB must not be black.
            bool checkedOne = false;
            for (int i = 0; i < 32 && !checkedOne; i++)
            {
                int k = (16 * 32 + i) * 4;
                int kn = (16 * 32 + i + 1) * 4;
                if (bytes[k + 3] == 0 && bytes[kn + 3] > 0)
                {
                    Assert.That(bytes[k], Is.GreaterThan(200));
                    checkedOne = true;
                }
            }
            Assert.That(checkedOne, Is.True);
        }

        [Test]
        public void Silhouette_KeepsAlphaReplacesColour()
        {
            var c = new PixelCanvas(32);
            c.Fill(SdfShapes.Circle(0f, 0f, 0.5f), new Rgba(0.2f, 0.3f, 0.4f));
            var s = c.ToSilhouette(Rgba.White);
            Assert.That(s.TotalAlpha(), Is.EqualTo(c.TotalAlpha()).Within(1e-3));
            Assert.That(s.GetPixel(16, 16).R, Is.EqualTo(1f).Within(1e-4));
        }

        [Test]
        public void CopyInto_PlacesCellInAtlas()
        {
            var cell = new PixelCanvas(16);
            cell.Fill(SdfShapes.Circle(0f, 0f, 2f), Rgba.White);
            var atlas = new PixelCanvas(64);
            cell.CopyInto(atlas, 16, 32);
            Assert.That(atlas.AlphaAt(20, 40), Is.EqualTo(1f).Within(1e-4));
            Assert.That(atlas.AlphaAt(5, 5), Is.EqualTo(0f));
            Assert.That(atlas.TotalAlpha(), Is.EqualTo(256f).Within(1e-2));
        }

        [Test]
        public void BlockCulling_MatchesBruteForce()
        {
            // Small shape far from most blocks: culled render must equal a per-pixel reference.
            Sdf shape = SdfShapes.Union(SdfShapes.Circle(0.6f, 0.6f, 0.1f), SdfShapes.RoundedBox(-0.5f, -0.4f, 0.2f, 0.05f, 0.02f));
            var c = new PixelCanvas(64);
            c.Fill(shape, Rgba.White);
            for (int j = 0; j < 64; j++)
                for (int i = 0; i < 64; i++)
                {
                    float expected = Coverage.FromDistance(shape(c.X(i), c.Y(j)), c.PixelSize);
                    Assert.That(c.AlphaAt(i, j), Is.EqualTo(expected).Within(1e-5), $"pixel {i},{j}");
                }
        }
    }
}
