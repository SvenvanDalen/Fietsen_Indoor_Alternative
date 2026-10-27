using System;
using System.Collections.Generic;
using FietsApp.Climbs;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class GpxTests
    {
        [Test]
        public void Parse_ReadsTrackPointsWithElevation()
        {
            var points = Gpx.Parse(
                "<?xml version=\"1.0\"?><gpx version=\"1.1\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>" +
                "<trkpt lat=\"44.1245\" lon=\"5.1793\"><ele>313.0</ele></trkpt>" +
                "<trkpt lat=\"44.1250\" lon=\"5.1800\"><ele>315.5</ele></trkpt>" +
                "</trkseg></trk></gpx>");

            Assert.That(points, Has.Count.EqualTo(2));
            Assert.That(points[1].Latitude, Is.EqualTo(44.1250).Within(1e-9));
            Assert.That(points[1].Longitude, Is.EqualTo(5.1800).Within(1e-9));
            Assert.That(points[1].Elevation, Is.EqualTo(315.5).Within(1e-9));
        }

        [Test]
        public void Parse_AlsoReadsGpx10AndRoutePoints()
        {
            var points = Gpx.Parse(
                "<gpx version=\"1.0\" xmlns=\"http://www.topografix.com/GPX/1/0\"><rte>" +
                "<rtept lat=\"1\" lon=\"2\"><ele>3</ele></rtept><rtept lat=\"4\" lon=\"5\"><ele>6</ele></rtept>" +
                "</rte></gpx>");

            Assert.That(points, Has.Count.EqualTo(2));
            Assert.That(points[0].Elevation, Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void Parse_WithoutElevation_Throws()
        {
            Assert.Throws<FormatException>(() => Gpx.Parse(
                "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>" +
                "<trkpt lat=\"1\" lon=\"2\"/><trkpt lat=\"1.001\" lon=\"2\"/></trkseg></trk></gpx>"));
        }
    }

    public class ClimbImportTests
    {
        /// <summary>Rechte weg naar het noorden; hoogte per punt opgegeven, punten om de 20 m.</summary>
        static List<GeoPoint> NorthwardRoad(Func<int, double> elevation, int count)
        {
            const double metersPerDegree = 6371000.0 * Math.PI / 180.0;
            var points = new List<GeoPoint>();
            for (int i = 0; i < count; i++)
                points.Add(new GeoPoint(44.0 + i * 20.0 / metersPerDegree, 5.0, elevation(i)));
            return points;
        }

        [Test]
        public void Projects_NorthToPlusZ_AndKeepsLength()
        {
            var route = ClimbImport.FromGpx(NorthwardRoad(i => 100 + i * 1.0, 51)); // 1000 m, 5%

            var end = route.Sample(route.Length);
            Assert.That(route.Length, Is.EqualTo(1000f).Within(1f));
            Assert.That(end.Z, Is.EqualTo(1000f).Within(1f));
            Assert.That(end.X, Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void KeepsRealElevation()
        {
            var route = ClimbImport.FromGpx(NorthwardRoad(i => 300 + i * 1.0, 51));

            Assert.That(route.Sample(0f).Elevation, Is.EqualTo(300f).Within(2f));
            Assert.That(route.Sample(route.Length).Elevation, Is.EqualTo(350f).Within(2f));
        }

        [Test]
        public void Resamples_ToRequestedSpacing()
        {
            var route = ClimbImport.FromGpx(NorthwardRoad(i => 0, 51), new ClimbImportSettings { Spacing = 10f });

            Assert.That(route.Points.Count, Is.EqualTo(101));
            Assert.That(route.Points[1].Distance, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void RemovesMeasurementSpikes()
        {
            // Gelijkmatig 6%, met één punt 15 m te hoog: een typische fout van een hoogtemodel.
            var route = ClimbImport.FromGpx(NorthwardRoad(i => i * 1.2 + (i == 25 ? 15 : 0), 51));

            Assert.That(ClimbStats.Of(route).MaxGradePercent, Is.LessThan(7.5f));
        }

        [Test]
        public void GradeChangesGradually_NoMoreThan1PercentPerMeter()
        {
            // Story 4.2: geen pieken boven 1% per meter, ook niet bij een plotselinge knik van vlak naar 12%.
            var route = ClimbImport.FromGpx(NorthwardRoad(i => i < 25 ? 0 : (i - 25) * 2.4, 51));

            var pts = route.Points;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float g1 = (pts[i].Elevation - pts[i - 1].Elevation) / (pts[i].Distance - pts[i - 1].Distance) * 100f;
                float g2 = (pts[i + 1].Elevation - pts[i].Elevation) / (pts[i + 1].Distance - pts[i].Distance) * 100f;
                Assert.That(Math.Abs(g2 - g1) / (pts[i].Distance - pts[i - 1].Distance), Is.LessThanOrEqualTo(1f));
            }
        }
    }

    public class ClimbStatsTests
    {
        [Test]
        public void LengthGainAverageAndMax()
        {
            var route = new RouteBuilder().Straight(1000f, 4f).Straight(500f, 10f).Straight(500f, -2f).Build();

            var stats = ClimbStats.Of(route);

            Assert.That(stats.LengthMeters, Is.EqualTo(2000f).Within(0.5f));
            Assert.That(stats.ElevationGain, Is.EqualTo(90f).Within(0.5f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(4f).Within(0.01f)); // (40 + 50 - 10) / 2000
            Assert.That(stats.MaxGradePercent, Is.EqualTo(10f).Within(0.01f));
        }
    }

    public class MontVentouxTests
    {
        // Het klimpakket uit tools/build_climbs.py.
        const string Id = "MontVentoux_Bedoin";

        static Route Load() => TestPackages.Route(Id);

        [Test]
        public void MatchesTheKnownClimbFromBedoin()
        {
            var stats = ClimbStats.Of(Load());

            // Bédoin: ±21,5 km van ±300 m naar ±1900 m, gemiddeld ±7,5%, steilste stukken rond 10-12%.
            Assert.That(stats.LengthMeters / 1000f, Is.EqualTo(21.4f).Within(0.3f));
            Assert.That(stats.StartElevation, Is.EqualTo(310f).Within(25f));
            Assert.That(stats.EndElevation, Is.EqualTo(1890f).Within(25f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(7.4f).Within(0.3f));
            Assert.That(stats.MaxGradePercent, Is.InRange(10f, 13.5f));
        }

        [Test]
        public void IsHorsCategorie()
        {
            Assert.That(ClimbCategories.Of(ClimbStats.Of(Load())), Is.EqualTo(ClimbCategory.HC));
        }

        [Test]
        public void HasNoFakeDescents()
        {
            // De klim vanaf Bédoin gaat nergens noemenswaardig omlaag; een daling wijst op meetruis.
            var stats = ClimbStats.Of(Load());

            Assert.That(stats.MinGradePercent, Is.GreaterThan(-2f));
        }
    }

    public class AlpeDHuezTests
    {
        const string Id = "AlpeDHuez";

        static Route Load() => TestPackages.Route(Id);

        [Test]
        public void MatchesTheKnownClimb()
        {
            var stats = ClimbStats.Of(Load());

            // Van de voet bij Le Bourg-d'Oisans (±720 m) tot het dorp (±1790 m); de bekende 13,8 km aan 8,1%
            // loopt nog een stuk verder door het skidorp naar de Tour-finish.
            Assert.That(stats.LengthMeters / 1000f, Is.EqualTo(12.9f).Within(0.3f));
            Assert.That(stats.StartElevation, Is.EqualTo(720f).Within(25f));
            Assert.That(stats.EndElevation, Is.EqualTo(1790f).Within(25f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(8.3f).Within(0.3f));
            Assert.That(ClimbCategories.Of(stats), Is.EqualTo(ClimbCategory.HC));
        }

        [Test]
        public void HairpinErrorsOfTheElevationModelAreSmoothedOut()
        {
            // Met te weinig gladstrijken gaf SRTM in de haarspelden 16-17% per 100 m; de steilste echte stukken zijn ±12%.
            var stats = ClimbStats.Of(Load());

            Assert.That(stats.MaxGradePercent, Is.InRange(10f, 14.5f));
            Assert.That(stats.MinGradePercent, Is.GreaterThan(-2.5f)); // het vlakke dal aan het begin
        }

        [Test]
        public void HasTheFamousHairpins()
        {
            // 21 genummerde bochten.
            Assert.That(Hairpins.Count(Load()), Is.InRange(18, 23));
        }
    }

    public class StelvioTests
    {
        const string Id = "Stelvio_Prato";

        static Route Load() => TestPackages.Route(Id);

        [Test]
        public void MatchesTheKnownClimbFromPrato()
        {
            var stats = ClimbStats.Of(Load());

            // Prato allo Stelvio (±910 m) tot de pas (2757 m): ±24,5 km aan gemiddeld ±7,4%, steilste km rond 9-10%.
            Assert.That(stats.LengthMeters / 1000f, Is.EqualTo(24.6f).Within(0.4f));
            Assert.That(stats.StartElevation, Is.EqualTo(910f).Within(25f));
            Assert.That(stats.EndElevation, Is.EqualTo(2757f).Within(25f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(7.5f).Within(0.3f));
            Assert.That(stats.MaxGradePercent, Is.InRange(9f, 13f));
            Assert.That(stats.MinGradePercent, Is.GreaterThan(-1f));
            Assert.That(ClimbCategories.Of(stats), Is.EqualTo(ClimbCategory.HC));
        }

        [Test]
        public void HasTheFamousHairpins()
        {
            // 48 genummerde bochten vanaf Prato.
            Assert.That(Hairpins.Count(Load()), Is.InRange(42, 52));
        }
    }

    public class FinestreTests
    {
        const string Id = "Finestre_Susa";

        static Route Load() => TestPackages.Route(Id);

        [Test]
        public void MatchesTheKnownClimbFromSusa()
        {
            var stats = ClimbStats.Of(Load());

            // Susa (±500 m) tot de col (2178 m): ±18,5 km aan gemiddeld ±9,2%, heel gelijkmatig.
            Assert.That(stats.LengthMeters / 1000f, Is.EqualTo(17.9f).Within(0.4f));
            Assert.That(stats.StartElevation, Is.EqualTo(490f).Within(25f));
            Assert.That(stats.EndElevation, Is.EqualTo(2165f).Within(25f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(9.3f).Within(0.3f));
            Assert.That(stats.MaxGradePercent, Is.InRange(10f, 15f));
            Assert.That(stats.MinGradePercent, Is.GreaterThan(-1f));
            Assert.That(ClimbCategories.Of(stats), Is.EqualTo(ClimbCategory.HC));
        }

        [Test]
        public void HasManyHairpins()
        {
            // 55 bochten, veel kort op elkaar in het bos; de teller vindt alleen bochten die 120 m uit elkaar liggen.
            Assert.That(Hairpins.Count(Load()), Is.GreaterThanOrEqualTo(30));
        }
    }

    public class BananenbergTests
    {
        const string Id = "Bananenberg";

        static Route Load() => TestPackages.Route(Id);

        [Test]
        public void MatchesTheDesign()
        {
            var stats = ClimbStats.Of(Load());

            // Ontwerp in tools/climbs.json: 9,94 km van 1250 naar 2105 m (8,6%), met een muur van 600 m aan 17%
            // die na het gladstrijken (Gauss σ 150 m) nog ±16,6% per 100 m is.
            Assert.That(stats.LengthMeters / 1000f, Is.EqualTo(9.94f).Within(0.05f));
            Assert.That(stats.StartElevation, Is.EqualTo(1250f).Within(5f));
            Assert.That(stats.EndElevation, Is.EqualTo(2105f).Within(5f));
            Assert.That(stats.AverageGradePercent, Is.EqualTo(8.6f).Within(0.1f));
            Assert.That(stats.MaxGradePercent, Is.InRange(15f, 17.5f));
            Assert.That(stats.MinGradePercent, Is.GreaterThan(3f));
            Assert.That(ClimbCategories.Of(stats), Is.EqualTo(ClimbCategory.HC));
        }

        [Test]
        public void HasTheStairOfHairpins()
        {
            Assert.That(Hairpins.Count(Load()), Is.EqualTo(8));
        }
    }

    public class ClimbSurfaceTests
    {
        [Test]
        public void GravelSection()
        {
            var climb = new Climb { Surfaces = new[] { new SurfaceSection { from = 10480f, to = 100000f, surface = Surface.Gravel } } };

            Assert.That(climb.SurfaceAt(10000f), Is.EqualTo(Surface.Asphalt));
            Assert.That(climb.SurfaceAt(10480f), Is.EqualTo(Surface.Gravel));
            Assert.That(climb.SurfaceAt(17000f), Is.EqualTo(Surface.Gravel));
        }

        [Test]
        public void Asphalt_ByDefault()
        {
            var climb = new Climb();

            Assert.That(climb.SurfaceAt(0f), Is.EqualTo(Surface.Asphalt));
            Assert.That(climb.SurfaceAt(5000f), Is.EqualTo(Surface.Asphalt));
        }
    }

    static class Hairpins
    {
        /// <summary>Plekken waar de weg binnen 60 m bijna omkeert, minstens 120 m uit elkaar.</summary>
        public static int Count(Route route)
        {
            int hairpins = 0;
            float last = float.MinValue;
            for (float d = 30f; d < route.Length - 30f; d += 5f)
            {
                var before = route.Sample(d - 30f);
                var after = route.Sample(d + 30f);
                float dot = before.HeadingX * after.HeadingX + before.HeadingZ * after.HeadingZ;
                if (dot < -0.85f && d - last > 120f)
                {
                    hairpins++;
                    last = d;
                }
            }
            return hairpins;
        }
    }
}
