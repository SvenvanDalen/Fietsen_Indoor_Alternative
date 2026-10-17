using System;
using FietsApp.Physics;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class CornerSpeedsTests
    {
        [Test]
        public void StraightRoadHasNoLimit()
        {
            var corners = new CornerSpeeds(new RouteBuilder(2f).Straight(500f, -8f).Build());
            for (float d = 0f; d <= 500f; d += 25f)
            {
                Assert.That(corners.CurvatureAt(d), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(corners.SpeedLimitAt(d), Is.EqualTo(corners.Settings.MaxSpeed));
            }
        }

        [Test]
        public void HairpinOfTenMetres()
        {
            // Haarspeldbocht naar links (in de RouteBuilder is negatief naar links) met straal 10 m: π·10 m lang.
            var route = new RouteBuilder(1f).Straight(200f, -7f).Curve((float)Math.PI * 10f, -7f, -180f).Straight(200f, -7f).Build();
            var corners = new CornerSpeeds(route);
            float middle = 200f + (float)Math.PI * 5f;

            Assert.That(corners.RadiusAt(middle), Is.EqualTo(10f).Within(0.5f));
            Assert.That(corners.CurvatureAt(middle), Is.GreaterThan(0f), "links is positief");
            // √(5 m/s² · 10 m) = 7,07 m/s, zo'n 25 km/u.
            Assert.That(corners.SpeedLimitAt(middle), Is.EqualTo(7.07f).Within(0.3f));
        }

        [Test]
        public void RightTurnHasNegativeCurvature()
        {
            var route = new RouteBuilder(1f).Straight(100f, 0f).Curve(50f, 0f, 90f).Straight(100f, 0f).Build();
            Assert.That(new CornerSpeeds(route).CurvatureAt(125f), Is.LessThan(0f));
        }

        [Test]
        public void BrakesInTimeBeforeTheBend()
        {
            var route = new RouteBuilder(1f).Straight(500f, -8f).Curve((float)Math.PI * 10f, -8f, 180f).Build();
            var corners = new CornerSpeeds(route);

            // De bocht begint op 500 m; de bochtsnelheid (√50) geldt uiterlijk 10 m erna (meetvenster).
            // Terugrekenen met 2,5 m/s²: v² = 50 + 2·2,5·afstand, los uitgerekend.
            float atHundredBefore = corners.SpeedLimitAt(400f);
            Assert.That(atHundredBefore, Is.GreaterThanOrEqualTo((float)Math.Sqrt(50 + 5 * 100) - 0.3f));
            Assert.That(atHundredBefore, Is.LessThanOrEqualTo((float)Math.Sqrt(50 + 5 * 110) + 0.3f));
            Assert.That(corners.SpeedLimitAt(0f), Is.EqualTo(corners.Settings.MaxSpeed), "500 m ervoor: van 40 m/s afremmen kost maar 310 m");
        }

        [Test]
        public void RiderSlowsToTheLimitAndNoFurther()
        {
            var physics = new RiderPhysics(RiderParameters.Default);
            float v = 20f;
            for (int i = 0; i < 500; i++)
            {
                v = physics.Step(v, 0f, -10f, 0.02f, 0f, 10f);
                Assert.That(v, Is.GreaterThanOrEqualTo(10f));
            }
            Assert.That(v, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void LimitBrakesNoHarderThanTheBrake()
        {
            var physics = new RiderPhysics(RiderParameters.Default);
            float free = physics.Step(20f, 0f, -10f, 0.1f);
            float limited = physics.Step(20f, 0f, -10f, 0.1f, 0f, 5f);
            Assert.That(free - limited, Is.EqualTo(RiderPhysics.MaxBrakeDeceleration * 0.1f).Within(1e-4f));
        }

        [Test]
        public void BelowTheLimitNothingChanges()
        {
            var physics = new RiderPhysics(RiderParameters.Default);
            Assert.That(physics.Step(8f, 200f, -3f, 0.02f, 0f, 15f), Is.EqualTo(physics.Step(8f, 200f, -3f, 0.02f)));
        }
    }
}
