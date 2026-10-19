using System;

namespace FietsApp.Training
{
    /// <summary>
    /// Beste en laatste klimtijd van één klim (story 9.2), in het profiel bewaard. Een record betekent ook dat je de
    /// klim hebt uitgereden: dat is je badge (story 9.1).
    /// </summary>
    [Serializable]
    public sealed class ClimbRecord
    {
        public string climb = "";
        public string firstOn = "";  // jjjj-mm-dd: de dag van de badge
        public double bestSeconds;
        public string bestOn = "";   // jjjj-mm-dd
        public double lastSeconds;
        public string lastOn = "";
        public int attempts;
    }

    /// <summary>Uitkomst van een nieuwe klimtijd: record of niet, en het verschil met record en vorige poging.</summary>
    public readonly struct ClimbResult
    {
        public readonly double Seconds;
        public readonly double? PreviousBest, PreviousAttempt;

        public ClimbResult(double seconds, double? previousBest, double? previousAttempt)
        {
            Seconds = seconds;
            PreviousBest = previousBest;
            PreviousAttempt = previousAttempt;
        }

        public bool IsFirst => PreviousBest == null;
        public bool IsRecord => PreviousBest is double best && Seconds < best;

        /// <summary>Bijv. "nieuw record, 0:32 sneller" of "+1:05 op je record (58:10), 0:12 sneller dan vorige keer".</summary>
        public string Describe()
        {
            if (IsFirst) return "badge verdiend! eerste keer boven, dus meteen je record";
            string text = IsRecord
                ? $"nieuw record, {Delta(PreviousBest.Value - Seconds)} sneller"
                : Seconds == PreviousBest.Value
                    ? "precies je record"
                    : $"+{Delta(Seconds - PreviousBest.Value)} op je record ({Clock(PreviousBest.Value)})";
            // Vorige keer alleen noemen als dat niet al het record was (dan zegt het hetzelfde).
            if (PreviousAttempt is double last && last != PreviousBest.Value && Math.Round(last) != Math.Round(Seconds))
                text += Seconds < last
                    ? $", {Delta(last - Seconds)} sneller dan vorige keer"
                    : $", {Delta(Seconds - last)} trager dan vorige keer";
            return text;
        }

        /// <summary>Tijd als h:mm:ss, of m:ss onder het uur.</summary>
        public static string Clock(double seconds)
        {
            int s = (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        static string Delta(double seconds) => Clock(Math.Abs(seconds));
    }
}
