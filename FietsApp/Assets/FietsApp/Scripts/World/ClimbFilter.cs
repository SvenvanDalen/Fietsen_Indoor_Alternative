using System.Collections.Generic;
using System.Linq;

namespace FietsApp.World
{
    /// <summary>Wat het ritmenu van een klim laat zien (story 6.3).</summary>
    public readonly struct ClimbSummary
    {
        public readonly string Name;
        public readonly float LengthMeters, AverageGradePercent, ElevationGain;
        public readonly ClimbCategory Category;

        public ClimbSummary(string name, float lengthMeters, float averageGradePercent, float elevationGain, ClimbCategory category)
        {
            Name = name;
            LengthMeters = lengthMeters;
            AverageGradePercent = averageGradePercent;
            ElevationGain = elevationGain;
            Category = category;
        }

        public static ClimbSummary Of(string name, ClimbStats stats) =>
            new ClimbSummary(name, stats.LengthMeters, stats.AverageGradePercent, stats.ElevationGain, ClimbCategories.Of(stats));

        /// <summary>Bijv. "21,4 km · 7,4% · 1574 hm · HC".</summary>
        public string Details(System.IFormatProvider culture)
        {
            string label = ClimbCategories.Label(Category);
            string text = string.Format(culture, "{0:0.0} km · {1:0.0}% · {2:0} hm", LengthMeters / 1000f, AverageGradePercent, ElevationGain);
            return label.Length > 0 ? $"{text} · {label}" : text;
        }
    }

    /// <summary>
    /// Filter van het ritmenu op lengte, gemiddelde helling en categorie (story 6.3). Elk filter heeft een vaste
    /// reeks keuzes waar een knop doorheen stapt; keuze 0 is steeds "alle".
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

        public void NextLength() => Length = (Length + 1) % Lengths.Length;
        public void NextGrade() => Grade = (Grade + 1) % Grades.Length;
        public void NextCategory() => Category = (Category + 1) % Categories.Length;

        public string LengthLabel => Lengths[Length].Label;
        public string GradeLabel => Grades[Grade].Label;
        public string CategoryLabel => Categories[Category] is { } c ? ClimbCategories.Label(c) : "elke categorie";

        public bool Matches(ClimbSummary climb) =>
            Lengths[Length].Contains(climb.LengthMeters)
            && Grades[Grade].Contains(climb.AverageGradePercent)
            && (Categories[Category] is not { } c || climb.Category == c);

        /// <summary>De klimmen die door het filter komen, met hun plek in de oorspronkelijke lijst.</summary>
        public IEnumerable<int> Apply(IReadOnlyList<ClimbSummary> climbs) =>
            Enumerable.Range(0, climbs.Count).Where(i => Matches(climbs[i]));
    }
}
