using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class ElevationProfileTests
    {
        [Test]
        public void Segments_CoverRouteWithAverageGrade()
        {
            var route = new RouteBuilder().Straight(300f, 0f).Straight(200f, 10f).Build();

            var profile = ElevationProfile.Build(route, segmentLength: 100f);

            Assert.That(profile.Segments, Has.Count.EqualTo(5));
            Assert.That(profile.Segments[0].GradePercent, Is.EqualTo(0f).Within(0.01f));
            Assert.That(profile.Segments[4].GradePercent, Is.EqualTo(10f).Within(0.01f));
            Assert.That(profile.Segments[4].EndDistance, Is.EqualTo(500f).Within(0.01f));
            Assert.That(profile.Segments[4].EndElevation, Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void LastSegment_IsShorterWhenRouteDoesNotDivideEvenly()
        {
            var route = new RouteBuilder().Straight(250f, 4f).Build();

            var profile = ElevationProfile.Build(route, segmentLength: 100f);

            Assert.That(profile.Segments, Has.Count.EqualTo(3));
            Assert.That(profile.Segments[2].StartDistance, Is.EqualTo(200f).Within(0.01f));
            Assert.That(profile.Segments[2].EndDistance, Is.EqualTo(250f).Within(0.01f));
            Assert.That(profile.Segments[2].GradePercent, Is.EqualTo(4f).Within(0.01f));
        }

        [Test]
        public void MinAndMaxElevation()
        {
            var route = new RouteBuilder().Straight(1000f, 5f).Straight(500f, -8f).Build();

            var profile = ElevationProfile.Build(route);

            Assert.That(profile.MinElevation, Is.EqualTo(0f).Within(0.01f));
            Assert.That(profile.MaxElevation, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void SummitDistance_IsHighestPoint()
        {
            var route = new RouteBuilder().Straight(1000f, 5f).Straight(500f, -8f).Build();

            Assert.That(ElevationProfile.Build(route).SummitDistance, Is.EqualTo(1000f).Within(0.01f));
        }

        [TestCase(-6f, GradeBand.Descent)]
        [TestCase(0f, GradeBand.Flat)]
        [TestCase(2.9f, GradeBand.Flat)]
        [TestCase(3f, GradeBand.Moderate)]
        [TestCase(6f, GradeBand.Hard)]
        [TestCase(9f, GradeBand.VeryHard)]
        [TestCase(12f, GradeBand.Extreme)]
        public void GradeBands(float grade, GradeBand expected)
        {
            Assert.That(Grades.Band(grade), Is.EqualTo(expected));
        }
    }
}
