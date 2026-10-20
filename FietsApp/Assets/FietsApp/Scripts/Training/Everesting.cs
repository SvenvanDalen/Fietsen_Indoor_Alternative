using System;

namespace FietsApp.Training
{
    /// <summary>
    /// Everesting-uitdaging (story 9.3): de hoogte van de Mount Everest bij elkaar klimmen, verspreid over meerdere ritten.
    /// Elke bewaarde rit telt zijn hoogtemeters mee zolang de uitdaging loopt. In het profiel bewaard.
    /// </summary>
    [Serializable]
    public sealed class EverestingChallenge
    {
        /// <summary>Hoogte van de Mount Everest in meters.</summary>
        public const float TargetMeters = 8848f;

        public bool active;
        public string startedOn = "";   // jjjj-mm-dd
        public float climbedMeters;
        public int rides;
        public string completedOn = ""; // leeg tot de 8848 m gehaald is

        public bool IsComplete => climbedMeters >= TargetMeters;

        /// <summary>Deel van de 8848 m (0-1), met de hoogtemeters van de rit die nog loopt.</summary>
        public float Progress(float current = 0f) => Math.Min(1f, Math.Max(0f, (climbedMeters + current) / TargetMeters));

        /// <summary>Begint opnieuw bij nul.</summary>
        public void Start(string date)
        {
            active = true;
            startedOn = date;
            climbedMeters = 0f;
            rides = 0;
            completedOn = "";
        }

        public void Stop() => active = false;

        /// <summary>
        /// Telt de hoogtemeters van een afgelopen rit mee. Geeft true als de uitdaging met deze rit voltooid is
        /// (alleen de eerste keer dat de 8848 m gepasseerd wordt).
        /// </summary>
        public bool AddRide(float ascent, string date)
        {
            if (!active || ascent <= 0f) return false;
            bool wasComplete = IsComplete;
            climbedMeters += ascent;
            rides++;
            if (wasComplete || !IsComplete) return false;
            completedOn = date;
            return true;
        }

        /// <summary>Korte regel voor de voortgangsbalk, bijvoorbeeld "Everesting 3.120 / 8.848 m (35%), 4 ritten".</summary>
        public string Describe(float current = 0f)
        {
            float total = climbedMeters + current;
            int ridesSoFar = rides + (current > 0f ? 1 : 0);
            string ridesText = ridesSoFar == 1 ? "1 rit" : $"{ridesSoFar} ritten";
            if (IsComplete)
                return $"Everest gehaald op {completedOn}: {Meters(total)} m in {ridesText}";
            return $"Everesting {Meters(total)} / {Meters(TargetMeters)} m ({Progress(current) * 100f:0}%), {ridesText}";
        }

        /// <summary>Hele meters met een punt als duizendtal, zoals in het Nederlands.</summary>
        static string Meters(float meters) =>
            Math.Floor(meters).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
