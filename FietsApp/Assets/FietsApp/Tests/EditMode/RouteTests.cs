using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class RouteTests
    {
        [Test]
        public void Builder_LengthAndElevationFollowSegments()
        {
            var route = new RouteBuilder()
                .Straight(1000f, 0f)
                .Straight(2000f, 5f)
                .Straight(500f, -4f)
                .Build();

            Assert.That(route.Length, Is.EqualTo(3500f).Within(0.01f));
            Assert.That(route.Sample(1000f).Elevation, Is.EqualTo(0f).Within(0.01f));
            Assert.That(route.Sample(3000f).Elevation, Is.EqualTo(100f).Within(0.01f));
            Assert.That(route.Sample(3500f).Elevation, Is.EqualTo(80f).Within(0.01f));
        }

        [Test]
        public void Sample_GradeOfSegment()
        {
            var route = new RouteBuilder().Straight(1000f, 0f).Straight(1000f, 8f).Build();

            Assert.That(route.Sample(500f).GradePercent, Is.EqualTo(0f).Within(0.01f));
            Assert.That(route.Sample(1500f).GradePercent, Is.EqualTo(8f).Within(0.01f));
        }

        [Test]
        public void Sample_InterpolatesBetweenPoints()
        {
            var route = new RouteBuilder(spacing: 10f).Straight(100f, 10f).Build();

            var s = route.Sample(15f);

            Assert.That(s.Elevation, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(s.Z, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void Straight_StartsAtOriginHeadingAlongZ()
        {
            var route = new RouteBuilder().Straight(100f, 0f).Build();

            var start = route.Sample(0f);
            var end = route.Sample(100f);

            Assert.That(start.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(start.Z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(end.Z, Is.EqualTo(100f).Within(0.01f));
            Assert.That(end.HeadingX, Is.EqualTo(0f).Within(0.001f));
            Assert.That(end.HeadingZ, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Curve_TurnsHeading()
        {
            // Een bocht van 90° naar rechts over een kwartcirkel: daarna rijden we langs +X.
            var route = new RouteBuilder(spacing: 1f).Curve(100f, 0f, 90f).Build();

            var end = route.Sample(route.Length);

            Assert.That(end.HeadingX, Is.EqualTo(1f).Within(0.01f));
            Assert.That(end.HeadingZ, Is.EqualTo(0f).Within(0.01f));
            // Straal = 100 / (π/2) ≈ 63,66 m; eindpunt ligt op (r, r).
            Assert.That(end.X, Is.EqualTo(63.66f).Within(0.5f));
            Assert.That(end.Z, Is.EqualTo(63.66f).Within(0.5f));
        }

        [Test]
        public void Sample_ClampsOutsideRoute()
        {
            var route = new RouteBuilder().Straight(100f, 5f).Build();

            Assert.That(route.Sample(-10f).Elevation, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route.Sample(200f).Elevation, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Testklim_IsPlausible()
        {
            var route = TestRoutes.Testklim();

            Assert.That(route.Length, Is.GreaterThan(3000f));
            Assert.That(route.MaxGradePercent, Is.EqualTo(10f).Within(0.01f));
        }
    }
}
