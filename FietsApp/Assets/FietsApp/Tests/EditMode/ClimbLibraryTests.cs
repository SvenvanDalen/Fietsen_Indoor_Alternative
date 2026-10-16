using System.Collections.Generic;
using System.Linq;
using FietsApp.World;
using NUnit.Framework;
using UnityEditor;

namespace FietsApp.Tests
{
    /// <summary>Elke klim in de bibliotheek (story 4.5) moet rijdbaar en geloofwaardig zijn.</summary>
    public class ClimbLibraryTests
    {
        const string LibraryPath = "Assets/FietsApp/Routes/Bibliotheek.asset";

        static IEnumerable<Climb> Climbs() =>
            AssetDatabase.LoadAssetAtPath<ClimbLibrary>(LibraryPath).climbs;

        static IEnumerable<TestCaseData> Cases() =>
            Climbs().Select(c => new TestCaseData(c).SetName($"Klim {c.name}"));

        [Test]
        public void LibraryHasAllClimbs()
        {
            var climbs = Climbs().ToArray();

            Assert.That(climbs, Is.Not.Empty);
            Assert.That(climbs, Has.None.Null);
            Assert.That(climbs.Select(c => c.displayName), Is.Unique);
        }

        [TestCaseSource(nameof(Cases))]
        public void ClimbIsRideable(Climb climb)
        {
            Assert.That(climb.gpx, Is.Not.Null, "GPX ontbreekt");
            Assert.That(climb.terrain, Is.Not.Null, "landschap ontbreekt");
            Assert.That(climb.region, Is.Not.Empty, "gebied ontbreekt");

            var route = ClimbImport.FromGpx(Gpx.Parse(climb.gpx.text));
            var stats = ClimbStats.Of(route);

            // Opgeslagen kerncijfers moeten bij de GPX horen (anders FietsApp > Klimmen bijwerken draaien).
            Assert.That(climb.lengthMeters, Is.EqualTo(stats.LengthMeters).Within(1).Percent);
            Assert.That(climb.averageGradePercent, Is.EqualTo(stats.AverageGradePercent).Within(0.05f));

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
    }
}
