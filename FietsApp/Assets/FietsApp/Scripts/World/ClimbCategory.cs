namespace FietsApp.World
{
    public enum ClimbCategory
    {
        None,
        Cat4,
        Cat3,
        Cat2,
        Cat1,
        HC,
    }

    /// <summary>
    /// Klimcategorie uit lengte × gemiddelde helling (story 4.3), met de veelgebruikte grenzen
    /// uit fietsapps: score = meters × procent; vanaf 8000 categorie 4, en bij elke verdubbeling
    /// een categorie zwaarder tot 64000 (categorie 1); HC vanaf 80000. Onder 3% gemiddeld is het geen klim.
    /// </summary>
    public static class ClimbCategories
    {
        public const float MinAverageGradePercent = 3f;

        public static ClimbCategory Of(float lengthMeters, float averageGradePercent)
        {
            if (averageGradePercent < MinAverageGradePercent) return ClimbCategory.None;
            float score = lengthMeters * averageGradePercent;
            if (score >= 80000f) return ClimbCategory.HC;
            if (score >= 64000f) return ClimbCategory.Cat1;
            if (score >= 32000f) return ClimbCategory.Cat2;
            if (score >= 16000f) return ClimbCategory.Cat3;
            if (score >= 8000f) return ClimbCategory.Cat4;
            return ClimbCategory.None;
        }

        public static ClimbCategory Of(ClimbStats stats) => Of(stats.LengthMeters, stats.AverageGradePercent);

        /// <summary>Zoals op een klimbord: "HC", "cat. 1" … "cat. 4"; leeg als het geen klim is.</summary>
        public static string Label(ClimbCategory category) => category switch
        {
            ClimbCategory.HC => "HC",
            ClimbCategory.Cat1 => "cat. 1",
            ClimbCategory.Cat2 => "cat. 2",
            ClimbCategory.Cat3 => "cat. 3",
            ClimbCategory.Cat4 => "cat. 4",
            _ => "",
        };
    }
}
