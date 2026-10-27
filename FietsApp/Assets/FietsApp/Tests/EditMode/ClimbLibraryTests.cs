using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FietsApp.Accounts;
using FietsApp.Climbs;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Elk meegeleverd klimpakket (stories 4.5 en 4.15) moet leesbaar, rijdbaar en geloofwaardig zijn.</summary>
    public class ClimbLibraryTests
    {
        const string ClimbsJson = "../tools/climbs.json";

        static IEnumerable<TestCaseData> Cases() =>
            TestPackages.Files().Select(f => new TestCaseData(f).SetName($"Klim {Path.GetFileName(f)}"));

        static Climb Read(string file) => ClimbPackage.Read(File.ReadAllBytes(file));

        [Test]
        public void LibraryHasAllClimbs()
        {
            var climbs = TestPackages.Files().Select(Read).ToArray();

            Assert.That(climbs, Is.Not.Empty);
            Assert.That(climbs.Select(c => c.Id), Is.Unique);
            Assert.That(climbs.Select(c => c.Name), Is.Unique);
            var catalog = (List<object>)Json.Get(Json.Parse(File.ReadAllText(ClimbsJson)), "climbs");
            var ids = catalog.Select(c => Json.GetString(c, "id"));
            Assert.That(climbs.Select(c => c.Id), Is.EquivalentTo(ids), "elke klim uit tools/climbs.json als pakket");
        }

        [Test]
        public void CatalogMatchesThePackages()
        {
            var catalog = ClimbCatalog.Parse(File.ReadAllText(TestPackages.Folder + "catalogus.json"));

            Assert.That(catalog.Entries.Select(e => e.File), Is.EquivalentTo(TestPackages.Files().Select(Path.GetFileName)));
            foreach (var entry in catalog.Entries)
            {
                var data = File.ReadAllBytes(TestPackages.Folder + entry.File);
                var climb = ClimbPackage.Read(data);
                Assert.That(entry.Id, Is.EqualTo(climb.Id));
                Assert.That(entry.Version, Is.EqualTo(climb.Version), entry.Id);
                Assert.That(entry.Format, Is.LessThanOrEqualTo(ClimbPackage.Format), entry.Id);
                using (var sha = SHA256.Create())
                    Assert.That(entry.Sha256, Is.EqualTo(string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")))), entry.Id);
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void ClimbIsRideable(string file)
        {
            var climb = Read(file);
            Assert.That(Path.GetFileName(file), Is.EqualTo(climb.Id + ClimbPackage.Extension));
            // Een fantasieklim heeft geen echt landschap; Unity verzint er bergen omheen.
            if (climb.Region != "Fantasie") Assert.That(climb.Terrain, Is.Not.Null, "landschap ontbreekt");
            Assert.That(climb.Region, Is.Not.Empty, "gebied ontbreekt");
            Assert.That(climb.Source, Is.Not.Empty, "bron ontbreekt");
            Assert.That(climb.License, Is.Not.Empty, "licentie ontbreekt");

            var route = ClimbImport.FromGpx(Gpx.Parse(climb.Gpx));
            var stats = ClimbStats.Of(route);

            // De kerncijfers in klim.json (tools/climb_package.py) moeten kloppen met wat de app uit de GPX haalt.
            Assert.That(climb.LengthMeters, Is.EqualTo(stats.LengthMeters).Within(0.1).Percent);
            Assert.That(climb.AverageGradePercent, Is.EqualTo(stats.AverageGradePercent).Within(0.01f));
            Assert.That(climb.ElevationGain, Is.EqualTo(stats.ElevationGain).Within(1f));

            // Het is een klim, zonder tunnels of bruggen in het hoogteprofiel. Echte wegen halen 25% (Angliru 23%)
            // en sommige klimmen hebben een echte afdaling (Lagos de Covadonga, Tre Cime), maar niet steiler dan 10%.
            Assert.That(stats.AverageGradePercent, Is.GreaterThan(3f));
            Assert.That(stats.MaxGradePercent, Is.LessThan(25f), "piek per 100 m; tunnel in het hoogteprofiel?");
            Assert.That(stats.MinGradePercent, Is.GreaterThan(-10f), "afdaling per 100 m; brug of verkeerde route?");

            // Begint aan de voet en eindigt op de top, niet met een afdaling uit het dorp of voorbij de pas
            // (Sault zakte eerst 30 m); een vlakke start met een paar meter ruis mag (Alpe d'Huez: 4 m).
            const float slack = 5f;
            float edge = System.Math.Min(300f, route.Length / 4f);
            Assert.That(route.Sample(edge).Elevation, Is.GreaterThan(route.Sample(0f).Elevation - slack), "daalt aan de start");
            Assert.That(route.Sample(route.Length).Elevation,
                Is.GreaterThan(route.Sample(route.Length - edge).Elevation - slack), "daalt aan de finish");
        }

        [Test]
        public void VentouxKeepsItsTransmitterAndBareTop()
        {
            var climb = TestPackages.Load("MontVentoux_Bedoin");

            Assert.That(climb.SummitTower, Is.True);
            Assert.That(climb.SummitLatitude, Is.EqualTo(44.1739).Within(1e-4));
            Assert.That(climb.TreeLine, Is.EqualTo(1450f));
            Assert.That(climb.AboveTreeLine, Is.EqualTo(Ground.Bare));
            Assert.That(climb.GravelVergeAboveTreeLine, Is.True);
        }

        [Test]
        public void AlpeDHuezHasItsOwnVegetation()
        {
            var climb = TestPackages.Load("AlpeDHuez");

            Assert.That(climb.TreeLine, Is.EqualTo(1600f));
            Assert.That(climb.ForestFrom, Is.EqualTo(760f));
            Assert.That(climb.AboveTreeLine, Is.EqualTo(Ground.Grass));
            Assert.That(climb.GravelVergeAboveTreeLine, Is.False);
        }

        [Test]
        public void HillsHaveNoTreeLine_AndFinestreHasGravel()
        {
            Assert.That(TestPackages.Load("Kapelmuur").TreeLine, Is.EqualTo(Climb.None));
            Assert.That(TestPackages.Load("Kapelmuur").SurfaceAt(50f), Is.EqualTo(Surface.Cobbles));
            Assert.That(TestPackages.Load("Finestre_Susa").SurfaceAt(15000f), Is.EqualTo(Surface.Gravel));
        }
    }
}
