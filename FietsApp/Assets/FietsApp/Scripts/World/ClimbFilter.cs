using System.Collections.Generic;
using System.Linq;

namespace FietsApp.World
{
    /// <summary>Wat het ritmenu van een klim laat zien (story 6.3).</summary>
    public readonly struct ClimbSummary
    {
        public readonly string Name, Region, Country;
        public readonly float LengthMeters, AverageGradePercent, ElevationGain;
        public readonly ClimbCategory Category;

        public ClimbSummary(string name, float lengthMeters, float averageGradePercent, float elevationGain, ClimbCategory category,
            string region = "", string country = "")
        {
            Name = name;
            Region = region ?? "";
            Country = country ?? "";
            LengthMeters = lengthMeters;
            AverageGradePercent = averageGradePercent;
            ElevationGain = elevationGain;
            Category = category;
        }

        public static ClimbSummary Of(string name, ClimbStats stats, string region = "", string country = "") =>
            new ClimbSummary(name, stats.LengthMeters, stats.AverageGradePercent, stats.ElevationGain, ClimbCategories.Of(stats),
                region, country);

        /// <summary>Uit opgeslagen kerncijfers; de categorie volgt uit lengte en gemiddelde.</summary>
        public static ClimbSummary Of(string name, float lengthMeters, float averageGradePercent, float elevationGain,
            string region = "", string country = "") =>
            new ClimbSummary(name, lengthMeters, averageGradePercent, elevationGain,
                ClimbCategories.Of(lengthMeters, averageGradePercent), region, country);

        /// <summary>Bijv. "21,4 km · 7,4% · 1574 hm · HC".</summary>
        public string Details(System.IFormatProvider culture)
        {
            string label = ClimbCategories.Label(Category);
            string text = string.Format(culture, "{0:0.0} km · {1:0.0}% · {2:0} hm", LengthMeters / 1000f, AverageGradePercent, ElevationGain);
            return label.Length > 0 ? $"{text} · {label}" : text;
        }
    }

    /// <summary>
    /// Filter van het ritmenu op lengte, gemiddelde helling, categorie (story 6.3), gebied (story 4.5) en land, plus zoeken
    /// op naam (story 4.6). Elk filter heeft een vaste reeks keuzes waar een knop doorheen stapt; keuze 0 is steeds "alle".
    /// </summary>
    public sealed class ClimbFilter
    {
        public readonly struct Choice
        {
            public readonly string Label;
            public readonly float Min, Max;

            public Choice(string label, float min, float max)
            {
                Label = label;
                Min = min;
                Max = max;
            }

            public bool Contains(float value) => value >= Min && value < Max;
        }

        public static readonly Choice[] Lengths =
        {
            new Choice("alle lengtes", 0f, float.MaxValue),
            new Choice("tot 15 km", 0f, 15000f),
            new Choice("15-20 km", 15000f, 20000f),
            new Choice("vanaf 20 km", 20000f, float.MaxValue),
        };

        public static readonly Choice[] Grades =
        {
            new Choice("elke helling", float.MinValue, float.MaxValue),
            new Choice("vanaf 7%", 7f, float.MaxValue),
            new Choice("vanaf 8%", 8f, float.MaxValue),
            new Choice("vanaf 9%", 9f, float.MaxValue),
        };

        // Zwaarste eerst, zoals op een klimbord.
        public static readonly ClimbCategory?[] Categories =
        {
            null, ClimbCategory.HC, ClimbCategory.Cat1, ClimbCategory.Cat2, ClimbCategory.Cat3, ClimbCategory.Cat4,
        };

        public int Length { get; private set; }
        public int Grade { get; private set; }
        public int Category { get; private set; }
        public int Region { get; private set; }
        public int Country { get; private set; }

        /// <summary>Zoektekst; een klim past als naam, gebied of land alle woorden bevat (zonder accenten, hoofdletters maakt niet uit).</summary>
        public string Query { get; set; } = "";

        // Keuze 0 is "alle gebieden" en "alle landen"; daarna wat in de lijst voorkomt.
        string[] _regions = { null };
        string[] _countries = { null };

        /// <summary>De gebieden om doorheen te stappen, uit de klimmen zelf.</summary>
        public void SetRegions(IEnumerable<string> regions)
        {
            _regions = Choices(regions);
            Region = 0;
        }

        /// <summary>De landen om doorheen te stappen, uit de klimmen zelf.</summary>
        public void SetCountries(IEnumerable<string> countries)
        {
            _countries = Choices(countries);
            Country = 0;
        }

        static string[] Choices(IEnumerable<string> values) =>
            new string[] { null }.Concat(values.Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v)).ToArray();

        public void NextLength() => Length = (Length + 1) % Lengths.Length;
        public void NextGrade() => Grade = (Grade + 1) % Grades.Length;
        public void NextCategory() => Category = (Category + 1) % Categories.Length;
        public void NextRegion() => Region = (Region + 1) % _regions.Length;
        public void NextCountry() => Country = (Country + 1) % _countries.Length;

        public string LengthLabel => Lengths[Length].Label;
        public string GradeLabel => Grades[Grade].Label;
        public string CategoryLabel => Categories[Category] is { } c ? ClimbCategories.Label(c) : "elke categorie";
        public string RegionLabel => _regions[Region] ?? "alle gebieden";
        public string CountryLabel => _countries[Country] ?? "alle landen";

        public bool Matches(ClimbSummary climb) =>
            Lengths[Length].Contains(climb.LengthMeters)
            && Grades[Grade].Contains(climb.AverageGradePercent)
            && (Categories[Category] is not { } c || climb.Category == c)
            && (_regions[Region] is not { } r || climb.Region == r)
            && (_countries[Country] is not { } land || climb.Country == land)
            && MatchesQuery(climb);

        bool MatchesQuery(ClimbSummary climb)
        {
            var words = Fold(Query).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;
            string haystack = Fold($"{climb.Name} {climb.Region} {climb.Country}");
            return words.All(haystack.Contains);
        }

        /// <summary>Kleine letters zonder accenten: "Télégraphe" en "telegraphe" zijn gelijk, "Großglockner" vindt je met "gross".</summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text.ToLowerInvariant().Replace("ß", "ss").Normalize(System.Text.NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString();
        }

        /// <summary>De klimmen die door het filter komen, met hun plek in de oorspronkelijke lijst.</summary>
        public IEnumerable<int> Apply(IReadOnlyList<ClimbSummary> climbs) =>
            Enumerable.Range(0, climbs.Count).Where(i => Matches(climbs[i]));
    }
}
