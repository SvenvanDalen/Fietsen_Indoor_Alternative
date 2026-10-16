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
        public void Details_UseTheGivenCulture()
        {
            var details = Climbs[0].Details(CultureInfo.GetCultureInfo("nl-NL"));

            Assert.That(details, Is.EqualTo("21,4 km · 7,4% · 1574 hm · HC"));
        }
    }
}
