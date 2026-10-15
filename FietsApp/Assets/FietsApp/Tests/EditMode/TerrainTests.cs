using System;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class NoiseTests
    {
        [Test]
        public void StaysInRange_AndIsDeterministic()
        {
            var a = new Noise(seed: 7);
            var b = new Noise(seed: 7);
            for (int i = 0; i < 1000; i++)
            {
                float x = i * 0.37f, z = i * 0.53f;
                float v = a.Fbm(x, z);
                Assert.That(v, Is.InRange(-1f, 1f));
                Assert.That(b.Fbm(x, z), Is.EqualTo(v));
            }
        }

        [Test]
        public void IsContinuous()
        {
            var noise = new Noise(seed: 3);
            for (int i = 0; i < 200; i++)
            {
                float x = i * 0.71f;
                Assert.That(Math.Abs(noise.Sample(x, 1.3f) - noise.Sample(x + 0.001f, 1.3f)), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentNoise()
        {
            Assert.That(new Noise(1).Sample(3.3f, 4.4f), Is.Not.EqualTo(new Noise(2).Sample(3.3f, 4.4f)));
        }
    }

    public class TerrainHeightsTests
    {
        static readonly Route Route = new RouteBuilder()
            .Straight(300f, 0f)
            .Curve(300f, 8f, 90f)
            .Straight(300f, 6f)
            .Build();

        static readonly TerrainSettings Settings = new TerrainSettings { Margin = 200f };

        static readonly HeightGrid Grid = TerrainHeights.Generate(Route, Settings);

        [Test]
        public void CoversRouteWithMargin()
        {
            foreach (var p in Route.Points)
            {
                Assert.That(p.X, Is.InRange(Grid.MinX + Settings.Margin - 1f, Grid.MaxX - Settings.Margin + 1f));
                Assert.That(p.Z, Is.InRange(Grid.MinZ + Settings.Margin - 1f, Grid.MaxZ - Settings.Margin + 1f));
            }
        }

        [Test]
        public void LiesJustBelowTheRoad()
        {
            for (float d = 0f; d <= Route.Length; d += 7f)
            {
                var s = Route.Sample(d);
                float h = Grid.HeightAt(s.X, s.Z);
                float expected = s.Elevation - Settings.RoadDepth;
                Assert.That(h, Is.EqualTo(expected).Within(0.35f), $"op {d} m");
                Assert.That(h, Is.LessThan(s.Elevation - 0.05f), $"terrein steekt door de weg op {d} m");
            }
        }

        [Test]
        public void IsFreeOfSpikes()
        {
            // Tussen buurpunten nooit meer dan 1,2 × celgrootte hoogteverschil (helling < 50°).
            for (int z = 0; z < Grid.CountZ; z++)
            for (int x = 1; x < Grid.CountX; x++)
                Assert.That(Math.Abs(Grid[x, z] - Grid[x - 1, z]), Is.LessThan(1.2f * Grid.CellSize));
        }

        [Test]
        public void IsDeterministic()
        {
            var again = TerrainHeights.Generate(Route, Settings);
            Assert.That(again[17, 23], Is.EqualTo(Grid[17, 23]));
        }

        [Test]
        public void FarFromRoad_IsShapedByNoise()
        {
            // Ver van de weg wijkt het terrein duidelijk af van de weghoogte.
            float corner = Grid[0, 0];
            float roadHeightNearby = Route.Sample(0f).Elevation;
            Assert.That(Math.Abs(corner - roadHeightNearby), Is.GreaterThan(5f));
        }
    }

    public class ScatterTests
    {
        static readonly Route Route = new RouteBuilder().Straight(400f, 5f).Curve(300f, 6f, 120f).Build();
        static readonly TerrainSettings Settings = new TerrainSettings { Margin = 200f };
        static readonly HeightGrid Grid = TerrainHeights.Generate(Route, Settings);

        [Test]
        public void PlacesTreesAndRocks_NeverOnTheRoad()
        {
            var items = Scatter.Place(Route, Grid, Settings);

            Assert.That(items.Count, Is.GreaterThan(50));
            Assert.That(items.Exists(i => i.Kind == ScatterKind.Tree), Is.True);
            foreach (var item in items)
            {
                var near = RouteDistance.Nearest(Route, item.X, item.Z);
                Assert.That(near.Distance, Is.GreaterThan(Settings.ClearWidth), $"{item.Kind} op {item.X},{item.Z}");
            }
        }

        [Test]
        public void ItemsStandOnTheTerrain()
        {
            foreach (var item in Scatter.Place(Route, Grid, Settings))
                Assert.That(item.Y, Is.EqualTo(Grid.HeightAt(item.X, item.Z)).Within(0.001f));
        }

        [Test]
        public void IsDeterministic()
        {
            var a = Scatter.Place(Route, Grid, Settings);
            var b = Scatter.Place(Route, Grid, Settings);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            Assert.That(b[a.Count / 2].X, Is.EqualTo(a[a.Count / 2].X));
        }
    }

    public class RouteDistanceTests
    {
        [Test]
        public void Nearest_GivesDistanceAndRoadElevation()
        {
            var route = new RouteBuilder().Straight(100f, 10f).Build(); // langs +Z, 10 m hoog op het eind

            var near = RouteDistance.Nearest(route, 4f, 50f);

            Assert.That(near.Distance, Is.EqualTo(4f).Within(0.01f));
            Assert.That(near.Elevation, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void Index_MatchesBruteForce()
        {
            var route = TestRoutes.Testklim();
            var index = new RouteIndex(route);
            var random = new Random(5);

            for (int i = 0; i < 500; i++)
            {
                float x = (float)(random.NextDouble() * 3000 - 1500);
                float z = (float)(random.NextDouble() * 3000 - 500);
                var expected = RouteDistance.Nearest(route, x, z);
                var actual = index.Nearest(x, z);
                Assert.That(actual.Distance, Is.EqualTo(expected.Distance).Within(0.001f), $"({x}, {z})");
                // Ver van de weg liggen buursegmenten binnen de float-precisie even ver weg; de index mag
                // dan het andere kiezen, met een paar centimeter verschil in weghoogte.
                Assert.That(actual.Elevation, Is.EqualTo(expected.Elevation).Within(0.05f), $"({x}, {z})");
            }
        }
    }
}
