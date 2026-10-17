using System;
using System.Collections.Generic;
using System.Linq;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class WorldMapTests
    {
        /// <summary>Rechte klim vanaf (x0, z0) in richting <paramref name="headingDeg"/> (0 = +X), met vaste helling.</summary>
        static Route Climb(float x0, float z0, float headingDeg, float length, float startElevation, float grade)
        {
            double a = headingDeg * Math.PI / 180;
            var pts = new List<RoutePoint>();
            for (float d = 0; d <= length + 0.01f; d += 10f)
                pts.Add(new RoutePoint(d, x0 + d * (float)Math.Cos(a), z0 + d * (float)Math.Sin(a), startElevation + d * grade / 100f));
            return new Route(pts);
        }

        static WorldMap Sample() => WorldMap.Build(new[]
        {
            new WorldClimb("a", "Alpha", "Alpen", Climb(100f, -50f, 30f, 5000f, 700f, 8f)),
            new WorldClimb("b", "Beta", "Alpen", Climb(0f, 0f, 200f, 3000f, 1400f, 6f)),
            new WorldClimb("c", "Gamma", "Alpen", Climb(-20f, 40f, 90f, 4000f, 900f, 7f)),
            new WorldClimb("k", "Kapel", "Vlaanderen", Climb(0f, 0f, 0f, 1000f, 20f, 9f)),
        });

        [Test]
        public void RegionsGetAValleyAtTheLowestFoot()
        {
            var map = Sample();
            Assert.That(map.Regions.Select(r => r.Name), Is.EqualTo(new[] { "Alpen", "Vlaanderen" }));
            Assert.That(map.Regions[0].ValleyElevation, Is.EqualTo(700f));
            Assert.That(map.Regions[1].ValleyElevation, Is.EqualTo(20f));
            // Twee gebieden in een ring op 50 km van elkaar.
            var (a, b) = (map.Regions[0], map.Regions[1]);
            Assert.That(Math.Sqrt((a.HubX - b.HubX) * (a.HubX - b.HubX) + (a.HubZ - b.HubZ) * (a.HubZ - b.HubZ)), Is.EqualTo(50000).Within(1));
        }

        [Test]
        public void ClimbsSpreadAroundTheHubPointingOutwards()
        {
            var map = Sample();
            var region = map.Regions[0];
            foreach (var climb in region.Climbs)
            {
                var route = climb.WorldRoute();
                var foot = route.Points[0];
                var top = route.Points[route.Points.Count - 1];
                double footDistance = Math.Sqrt((foot.X - region.HubX) * (foot.X - region.HubX) + (foot.Z - region.HubZ) * (foot.Z - region.HubZ));
                Assert.That(footDistance, Is.EqualTo(1500).Within(0.5), climb.Name);
                double direction = Math.Atan2(top.Z - foot.Z, top.X - foot.X);
                Assert.That(Math.Abs(Math.IEEERemainder(direction - climb.Angle, 2 * Math.PI)), Is.LessThan(1e-3), climb.Name);
            }
            // Drie klimmen: 0°, 120° en 240°.
            Assert.That(region.Climbs.Select(c => c.Angle * 180 / Math.PI), Is.EqualTo(new[] { 0.0, 120.0, 240.0 }).Within(1e-3));
        }

        [Test]
        public void ClimbKeepsItsGradesAndRealElevation()
        {
            var map = Sample();
            var beta = map.Find("b");
            var world = beta.WorldRoute();
            Assert.That(world.Points[0].Elevation, Is.EqualTo(700f).Within(1e-3)); // op dalhoogte
            Assert.That(beta.Placement.RealElevation(world.Points[0].Elevation), Is.EqualTo(1400f).Within(1e-3));
            Assert.That(world.Length, Is.EqualTo(beta.Route.Length).Within(0.5));
            var stats = ClimbStats.Of(world);
            Assert.That(stats.AverageGradePercent, Is.EqualTo(6f).Within(0.01));
            Assert.That(stats.ElevationGain, Is.EqualTo(180f).Within(0.5));
        }

        [Test]
        public void PlacementRoundTrips()
        {
            var p = Sample().Find("a").Placement;
            var (wx, wz) = p.ToWorld(1234f, -567f);
            var (lx, lz) = p.ToLocal(wx, wz);
            Assert.That(lx, Is.EqualTo(1234f).Within(1e-2));
            Assert.That(lz, Is.EqualTo(-567f).Within(1e-2));
        }

        [Test]
        public void JourneyGoesUpAndBackDownToTheHub()
        {
            var map = Sample();
            var climb = map.Find("a");
            var region = map.RegionOf(climb);
            var journey = map.Journey(climb);
            var pts = journey.Route.Points;

            Assert.That(pts[0].X, Is.EqualTo(region.HubX).Within(0.01));
            Assert.That(pts[pts.Count - 1].X, Is.EqualTo(region.HubX).Within(0.01));
            Assert.That(pts[pts.Count - 1].Z, Is.EqualTo(region.HubZ).Within(0.01));

            // Geen gaten en geen dubbele punten.
            for (int i = 1; i < pts.Count; i++)
                Assert.That(pts[i].Distance - pts[i - 1].Distance, Is.InRange(0.01f, 10.5f), $"punt {i}");

            // Delen sluiten op elkaar aan; klim en afdaling even lang; de top op dalhoogte + 400 m (5 km aan 8%).
            var s = journey.Segments;
            Assert.That(s.Select(x => x.Part), Is.EqualTo(new[] { JourneyPart.Approach, JourneyPart.Climb, JourneyPart.Descent, JourneyPart.Return }));
            for (int i = 1; i < s.Count; i++) Assert.That(s[i].Start, Is.EqualTo(s[i - 1].End));
            Assert.That(s[1].End - s[1].Start, Is.EqualTo(5000f).Within(1f));
            Assert.That(s[2].End - s[2].Start, Is.EqualTo(5000f).Within(1f));
            Assert.That(s[0].End - s[0].Start, Is.EqualTo(s[3].End - s[3].Start).Within(0.5f));
            Assert.That(journey.Route.Sample(s[1].End).Elevation, Is.EqualTo(1100f).Within(0.5f));
            Assert.That(journey.PartAt(s[1].Start + 100f), Is.EqualTo(JourneyPart.Climb));
            Assert.That(journey.PartAt(s[2].Start + 100f), Is.EqualTo(JourneyPart.Descent));
        }

        [Test]
        public void ApproachIsFlatAndJoinsTheClimbSmoothly()
        {
            var map = Sample();
            var journey = map.Journey(map.Find("b")); // klim wijst oorspronkelijk naar 200°
            var approach = journey[JourneyPart.Approach];
            var route = journey.Route;
            for (float d = 0f; d < approach.End - 10f; d += 25f)
                Assert.That(route.Sample(d).Elevation, Is.EqualTo(700f).Within(0.01f));

            var before = route.Sample(approach.End - 15f);
            var after = route.Sample(approach.End + 15f);
            double turn = Math.Acos(Math.Max(-1, Math.Min(1, before.HeadingX * after.HeadingX + before.HeadingZ * after.HeadingZ)));
            Assert.That(turn * 180 / Math.PI, Is.LessThan(10), "knik bij de voet");
        }
    }
}
