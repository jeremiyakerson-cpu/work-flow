using NUnit.Framework;
using TowerDefense.Levels;

namespace TowerDefense.ContentTests
{
    public class GeometryTests
    {
        private static LayoutPoint P(float x, float y) => new LayoutPoint(x, y);

        [Test]
        public void PointSegmentDistance_PerpendicularFoot()
        {
            Assert.That(LevelGeometry.PointSegmentDistance(P(5, 3), P(0, 0), P(10, 0)), Is.EqualTo(3f).Within(1e-5));
        }

        [Test]
        public void PointSegmentDistance_ClampsToEndpoints()
        {
            Assert.That(LevelGeometry.PointSegmentDistance(P(-3, 4), P(0, 0), P(10, 0)), Is.EqualTo(5f).Within(1e-5));
            Assert.That(LevelGeometry.PointSegmentDistance(P(13, -4), P(0, 0), P(10, 0)), Is.EqualTo(5f).Within(1e-5));
        }

        [Test]
        public void PointSegmentDistance_DiagonalSegment()
        {
            // Distance from (0,2) to the line y = x is sqrt(2).
            Assert.That(LevelGeometry.PointSegmentDistance(P(0, 2), P(-5, -5), P(5, 5)), Is.EqualTo(1.41421f).Within(1e-4));
        }

        [Test]
        public void PointSegmentDistance_DegenerateSegmentIsPointDistance()
        {
            Assert.That(LevelGeometry.PointSegmentDistance(P(3, 4), P(0, 0), P(0, 0)), Is.EqualTo(5f).Within(1e-5));
        }

        [Test]
        public void DistanceToPolyline_TakesNearestSegment()
        {
            var line = new[] { P(0, 0), P(10, 0), P(10, 10) };
            Assert.That(LevelGeometry.DistanceToPolyline(P(12, 5), line), Is.EqualTo(2f).Within(1e-5));
            Assert.That(LevelGeometry.DistanceToPolyline(P(5, 1), line), Is.EqualTo(1f).Within(1e-5));
            Assert.That(LevelGeometry.DistanceToPolyline(P(5, 1), new LayoutPoint[0]), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void PolylineLength_SumsSegments()
        {
            Assert.That(LevelGeometry.PolylineLength(new[] { P(0, 0), P(3, 4), P(3, 10) }), Is.EqualTo(11f).Within(1e-5));
        }

        [Test]
        public void InsideBounds_RespectsMargin()
        {
            Assert.That(LevelGeometry.InsideBounds(P(1, 1), 32, 18, 1f), Is.True);
            Assert.That(LevelGeometry.InsideBounds(P(0.9f, 5), 32, 18, 1f), Is.False);
            Assert.That(LevelGeometry.InsideBounds(P(31.5f, 5), 32, 18, 1f), Is.False);
            Assert.That(LevelGeometry.InsideBounds(P(5, 17.2f), 32, 18, 1f), Is.False);
        }

        [Test]
        public void SignedDistanceToEdge_InsideOnAndOutside()
        {
            Assert.That(LevelGeometry.SignedDistanceToEdge(P(16, 9), 32, 18), Is.EqualTo(9f).Within(1e-5));
            Assert.That(LevelGeometry.SignedDistanceToEdge(P(0, 9), 32, 18), Is.EqualTo(0f).Within(1e-5));
            Assert.That(LevelGeometry.SignedDistanceToEdge(P(-1, 9), 32, 18), Is.EqualTo(-1f).Within(1e-5));
            Assert.That(LevelGeometry.SignedDistanceToEdge(P(35, 22), 32, 18), Is.EqualTo(-5f).Within(1e-5));
            Assert.That(LevelGeometry.DistanceOutside(P(5, 5), 32, 18), Is.EqualTo(0f));
        }
    }
}
