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

            var stats = ClimbStats.Of(ClimbImport.FromGpx(Gpx.Parse(climb.gpx.text)));

            // Opgeslagen kerncijfers moeten bij de GPX horen (anders FietsApp > Klimmen bijwerken draaien).
            Assert.That(climb.lengthMeters, Is.EqualTo(stats.LengthMeters).Within(1).Percent);
            Assert.That(climb.averageGradePercent, Is.EqualTo(stats.AverageGradePercent).Within(0.05f));

            // Het is een klim, zonder uitschieters van het hoogtemodel en zonder onechte afdalingen.
            Assert.That(stats.AverageGradePercent, Is.GreaterThan(3f));
            Assert.That(stats.MaxGradePercent, Is.LessThan(16f), "piek per 100 m; meetruis?");
            Assert.That(stats.MinGradePercent, Is.GreaterThan(-3f), "afdaling per 100 m; verkeerde start of meetruis?");
        }
    }
}
