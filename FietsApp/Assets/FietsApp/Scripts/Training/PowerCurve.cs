using System;
using System.Collections.Generic;

namespace FietsApp.Training
{
    /// <summary>Beste gemiddeld vermogen over één duur, met de dag waarop het gereden is (story 9.5).</summary>
    [Serializable]
    public sealed class PowerBest
    {
        public int seconds;
        public int watts;
        public string on = ""; // jjjj-mm-dd
    }

    /// <summary>
    /// Vermogenscurve en eFTP (story 9.5): het beste gemiddelde vermogen over 5 s, 1, 5, 20 en 60 minuten, uit ritten met
    /// één meting per seconde.
    /// </summary>
    public static class PowerCurve
    {
        public static readonly int[] Durations = { 5, 60, 300, 1200, 3600 };

        /// <summary>Het eFTP is dit deel van het beste 20-minutenvermogen, zoals bij de FTP-test (8.1).</summary>
        public const double EftpFactor = 0.95;

        /// <summary>Hoeveel watt het eFTP boven of onder de FTP moet liggen voordat we een nieuwe FTP voorstellen.</summary>
        public const int SuggestionMarginWatts = 3;

        /// <summary>Beste gemiddelde over <paramref name="window"/> opeenvolgende seconden, of null als de rit korter is.</summary>
        public static double? BestAverage(IReadOnlyList<float> powerPerSecond, int window)
        {
            if (window <= 0 || powerPerSecond.Count < window) return null;
            double sum = 0;
            for (int i = 0; i < window; i++) sum += powerPerSecond[i];
            double best = sum;
            for (int i = window; i < powerPerSecond.Count; i++)
            {
                sum += powerPerSecond[i] - powerPerSecond[i - window];
                if (sum > best) best = sum;
            }
            return best / window;
        }

        /// <summary>
        /// Verwerk een rit: per duur komt het beste van die rit erbij, met de datum. Zo kan de laatste-90-dagenlijst
        /// (<see cref="Since"/>) later nog uit de bewaarde ritten gerekend worden.
        /// </summary>
        public static void Record(List<PowerBest> bests, IReadOnlyList<float> powerPerSecond, string date)
        {
            foreach (int duration in Durations)
            {
                var avg = BestAverage(powerPerSecond, duration);
                if (avg != null) bests.Add(new PowerBest { seconds = duration, watts = (int)Math.Round(avg.Value), on = date });
            }
        }

        /// <summary>Het beste vermogen voor een duur over de gegeven ritten, of 0.</summary>
        public static int Of(IEnumerable<PowerBest> bests, int seconds)
        {
            int best = 0;
            foreach (var b in bests) if (b.seconds == seconds && b.watts > best) best = b.watts;
            return best;
        }

        /// <summary>Het bewaarde beste voor elke duur in <see cref="Durations"/>, met de datum van het beste.</summary>
        public static List<PowerBest> Curve(IEnumerable<PowerBest> bests)
        {
            var curve = new List<PowerBest>();
            foreach (int duration in Durations)
            {
                PowerBest top = null;
                foreach (var b in bests)
                    if (b.seconds == duration && (top == null || b.watts > top.watts)) top = b;
                if (top != null) curve.Add(top);
            }
            return curve;
        }

        /// <summary>Geschatte FTP uit het beste 20-minutenvermogen, of 0 als dat nog niet bekend is.</summary>
        public static int Eftp(IEnumerable<PowerBest> bests) => (int)Math.Round(Of(bests, 1200) * EftpFactor);

        /// <summary>Voorstel voor een nieuwe FTP, of null als de huidige binnen de marge ligt of er nog geen 20-minutenvermogen is.</summary>
        public static int? SuggestedFtp(IEnumerable<PowerBest> bests, int currentFtp)
        {
            int eftp = Eftp(bests);
            return eftp > 0 && Math.Abs(eftp - currentFtp) > SuggestionMarginWatts ? eftp : (int?)null;
        }

        /// <summary>Alleen de ritten van de laatste <paramref name="days"/> dagen tellen: filter bests op datum.</summary>
        public static List<PowerBest> Since(IEnumerable<PowerBest> bests, DateTime today, int days)
        {
            var cutoff = today.Date.AddDays(-days);
            var result = new List<PowerBest>();
            foreach (var b in bests)
                if (DateTime.TryParseExact(b.on, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var d) && d >= cutoff)
                    result.Add(b);
            return result;
        }

        /// <summary>Etiket voor een duur: "5 s", "1 min", "20 min", "60 min".</summary>
        public static string Label(int seconds) => seconds < 60 ? $"{seconds} s" : $"{seconds / 60} min";
    }
}
