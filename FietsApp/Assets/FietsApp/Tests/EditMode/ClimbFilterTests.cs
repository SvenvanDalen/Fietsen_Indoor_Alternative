using System.Globalization;
using System.Linq;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class ClimbFilterTests
    {
        // De vier iconen, afgerond; de categorie hier los vastgesteld (alle vier HC) plus twee kleinere.
        static readonly ClimbSummary[] Climbs =
        {
            new ClimbSummary("Mont Ventoux", 21360f, 7.4f, 1574f, ClimbCategory.HC),
            new ClimbSummary("Alpe d'Huez", 12900f, 8.3f, 1068f, ClimbCategory.HC),
            new ClimbSummary("Stelvio", 24580f, 7.5f, 1848f, ClimbCategory.HC),
            new ClimbSummary("Finestre", 17890f, 9.3f, 1673f, ClimbCategory.HC),
            new ClimbSummary("Heuvel", 3000f, 6f, 180f, ClimbCategory.Cat3),
            new ClimbSummary("Bult", 2000f, 4f, 80f, ClimbCategory.Cat4),
        };

        static string[] Names(ClimbFilter filter) => filter.Apply(Climbs).Select(i => Climbs[i].Name).ToArray();

        [Test]
        public void ShowsEverything_ByDefault()
        {
            var filter = new ClimbFilter();

            Assert.That(Names(filter), Has.Length.EqualTo(6));
            Assert.That(filter.LengthLabel, Is.EqualTo("alle lengtes"));
            Assert.That(filter.CategoryLabel, Is.EqualTo("elke categorie"));
        }

        [Test]
        public void FiltersOnLength()
        {
            var filter = new ClimbFilter();
            filter.NextLength(); // tot 15 km

            Assert.That(Names(filter), Is.EqualTo(new[] { "Alpe d'Huez", "Heuvel", "Bult" }));

            filter.NextLength(); // 15-20 km
            Assert.That(Names(filter), Is.EqualTo(new[] { "Finestre" }));

            filter.NextLength(); // vanaf 20 km
            Assert.That(Names(filter), Is.EqualTo(new[] { "Mont Ventoux", "Stelvio" }));

            filter.NextLength(); // weer alles
            Assert.That(Names(filter), Has.Length.EqualTo(6));
        }

        [Test]
        public void FiltersOnAverageGrade()
        {
            var filter = new ClimbFilter();
            filter.NextGrade(); // vanaf 7%
            filter.NextGrade(); // vanaf 8%

            Assert.That(Names(filter), Is.EqualTo(new[] { "Alpe d'Huez", "Finestre" }));
        }

        [Test]
        public void FiltersOnCategory()
        {
            var filter = new ClimbFilter();
            for (int i = 0; i < 4; i++) filter.NextCategory(); // HC, 1, 2, 3

            Assert.That(filter.CategoryLabel, Is.EqualTo("cat. 3"));
            Assert.That(Names(filter), Is.EqualTo(new[] { "Heuvel" }));
        }

        [Test]
        public void FiltersCombine()
        {
            var filter = new ClimbFilter();
            filter.NextLength();   // tot 15 km
            filter.NextCategory(); // HC

            Assert.That(Names(filter), Is.EqualTo(new[] { "Alpe d'Huez" }));
        }

        [Test]
        public void FiltersOnRegion()
        {
            var climbs = new[]
            {
                new ClimbSummary("Tourmalet", 18800f, 7.4f, 1390f, ClimbCategory.HC, "Pyreneeën"),
                new ClimbSummary("Galibier", 18100f, 6.9f, 1245f, ClimbCategory.HC, "Alpen"),
                new ClimbSummary("Cauberg", 1200f, 5.8f, 70f, ClimbCategory.None, "Limburg"),
            };
            var filter = new ClimbFilter();
            filter.SetRegions(climbs.Select(c => c.Region));

            Assert.That(filter.RegionLabel, Is.EqualTo("alle gebieden"));
            filter.NextRegion(); // op alfabet: Alpen, Limburg, Pyreneeën
            Assert.That(filter.RegionLabel, Is.EqualTo("Alpen"));
            Assert.That(filter.Apply(climbs).Select(i => climbs[i].Name), Is.EqualTo(new[] { "Galibier" }));
            filter.NextRegion();
            filter.NextRegion();
            filter.NextRegion(); // weer alles
            Assert.That(filter.Apply(climbs).Count(), Is.EqualTo(3));
        }

        static readonly ClimbSummary[] Abroad =
        {
            new ClimbSummary("Col du Télégraphe", 11800f, 7.1f, 840f, ClimbCategory.Cat1, "Alpen", "Frankrijk"),
            new ClimbSummary("Passo dello Stelvio (Prato)", 24580f, 7.5f, 1848f, ClimbCategory.HC, "Alpen", "Italië"),
            new ClimbSummary("Großglockner Hochtor (Heiligenblut)", 15000f, 8.4f, 1260f, ClimbCategory.HC, "Oostenrijk", "Oostenrijk"),
            new ClimbSummary("Koppenberg", 600f, 11.6f, 64f, ClimbCategory.None, "Vlaanderen", "België"),
        };

        static string[] Names(ClimbFilter filter, ClimbSummary[] climbs) => filter.Apply(climbs).Select(i => climbs[i].Name).ToArray();

        [Test]
        public void FiltersOnCountry()
        {
            var filter = new ClimbFilter();
            filter.SetCountries(Abroad.Select(c => c.Country));
            Assert.That(filter.CountryLabel, Is.EqualTo("alle landen"));
            filter.NextCountry(); // op alfabet: België, Frankrijk, Italië, Oostenrijk
            Assert.That(filter.CountryLabel, Is.EqualTo("België"));
            Assert.That(Names(filter, Abroad), Is.EqualTo(new[] { "Koppenberg" }));
            filter.NextCountry();
            filter.NextCountry();
            Assert.That(Names(filter, Abroad), Is.EqualTo(new[] { "Passo dello Stelvio (Prato)" }));
        }

        [TestCase("telegraphe", "Col du Télégraphe")]
        [TestCase("STELVIO", "Passo dello Stelvio (Prato)")]
        [TestCase("gross", "Großglockner Hochtor (Heiligenblut)")]
        [TestCase("italie", "Passo dello Stelvio (Prato)")]
        [TestCase("  col   tele ", "Col du Télégraphe")]
        public void SearchesOnNameWithoutAccents(string query, string expected)
        {
            var filter = new ClimbFilter { Query = query };
            Assert.That(Names(filter, Abroad), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void SearchCombinesWithFiltersAndCanFindNothing()
        {
            var filter = new ClimbFilter { Query = "alpen" };
            Assert.That(Names(filter, Abroad), Has.Length.EqualTo(2));
            filter.NextGrade(); // vanaf 7%
            filter.NextGrade(); // vanaf 8%
            Assert.That(Names(filter, Abroad), Is.Empty);
            filter.Query = "";
            Assert.That(Names(filter, Abroad), Is.EqualTo(new[] { "Großglockner Hochtor (Heiligenblut)", "Koppenberg" }));
        }

        [Test]
        public void SummaryFromStoredNumbers_DerivesTheCategory()
        {
            var summary = ClimbSummary.Of("Galibier", 18100f, 6.9f, 1245f, "Alpen");

            Assert.That(summary.Category, Is.EqualTo(ClimbCategory.HC)); // 18100 × 6,9 = 124890
        }

        [Test]
        public void Details_UseTheGivenCulture()
        {
            var details = Climbs[0].Details(CultureInfo.GetCultureInfo("nl-NL"));

            Assert.That(details, Is.EqualTo("21,4 km · 7,4% · 1574 hm · HC"));
        }
    }
}
