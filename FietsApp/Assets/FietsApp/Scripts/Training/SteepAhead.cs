using System;
using System.Collections.Generic;

namespace FietsApp.Training
{
    /// <summary>Een steil stuk voor je: waar het begint (vanaf je positie), hoe lang en hoe steil.</summary>
    public readonly struct SteepStretch
    {
        public readonly float StartM, LengthM, AverageGrade;
        public SteepStretch(float startM, float lengthM, float averageGrade) { StartM = startM; LengthM = lengthM; AverageGrade = averageGrade; }

        /// <summary>Bijv. "over 300 m: 800 m aan 11%", of "nu: 800 m aan 11%" als je er al op zit.</summary>
        public string Describe(float distanceToStart) =>
            $"{(distanceToStart < 25f ? "nu" : $"over {Math.Round(distanceToStart / 50.0) * 50:0} m")}: {Math.Round(LengthM / 50.0) * 50:0} m aan {AverageGrade:0}%";
    }

    /// <summary>
    /// Vooruitblik op de helling (story 6.6): zoekt het eerstvolgende stuk dat minstens <c>threshold</c> procent steil is
    /// en minstens <see cref="MinLengthM"/> lang, en het steilste stuk van de hele klim voor het hoogteprofiel.
    /// De hoogte wordt per <see cref="Step"/> meter bekeken, zodat een enkele ruwe meting geen "steil stuk" wordt.
    /// </summary>
    public static class SteepAhead
    {
        public const float Step = 50f, MinLengthM = 200f, DefaultThreshold = 10f, DefaultLookAheadM = 500f;

        /// <summary>Het eerste steile stuk binnen <paramref name="lookAheadM"/> meter voor <paramref name="position"/>, of null.</summary>
        public static SteepStretch? Next(IReadOnlyList<float> distances, IReadOnlyList<float> elevations, float position,
            float threshold, float lookAheadM = DefaultLookAheadM)
        {
            if (distances.Count < 2) return null;
            float end = distances[distances.Count - 1];
            float? start = null;
            float runStartElevation = 0f;
            for (float d = position; d < end; d += Step)
            {
                float next = Math.Min(d + Step, end);
                float grade = Grade(distances, elevations, d, next);
                if (grade >= threshold)
                {
                    if (start == null) { start = d; runStartElevation = ElevationAt(distances, elevations, d); }
                }
                else if (start != null)
                {
                    var stretch = Finish(distances, elevations, start.Value, d, runStartElevation, position);
                    if (stretch != null) return stretch;
                    start = null;
                }
                if (start == null && d - position > lookAheadM) return null;
            }
            return start != null ? Finish(distances, elevations, start.Value, end, runStartElevation, position) : null;
        }

        /// <summary>Het steilste stuk van minstens <see cref="MinLengthM"/> lang over de hele route, of null als die korter is.</summary>
        public static SteepStretch? Steepest(IReadOnlyList<float> distances, IReadOnlyList<float> elevations)
        {
            if (distances.Count < 2) return null;
            float end = distances[distances.Count - 1];
            SteepStretch? best = null;
            for (float d = 0f; d + MinLengthM <= end + 0.01f; d += Step)
            {
                float grade = Grade(distances, elevations, d, d + MinLengthM);
                if (best == null || grade > best.Value.AverageGrade) best = new SteepStretch(d, MinLengthM, grade);
            }
            return best;
        }

        static SteepStretch? Finish(IReadOnlyList<float> distances, IReadOnlyList<float> elevations, float start, float end,
            float startElevation, float position)
        {
            float length = end - start;
            if (length < MinLengthM) return null;
            float grade = (ElevationAt(distances, elevations, end) - startElevation) / length * 100f;
            return new SteepStretch(start - position, length, grade);
        }

        static float Grade(IReadOnlyList<float> distances, IReadOnlyList<float> elevations, float from, float to) =>
            to > from ? (ElevationAt(distances, elevations, to) - ElevationAt(distances, elevations, from)) / (to - from) * 100f : 0f;

        static float ElevationAt(IReadOnlyList<float> distances, IReadOnlyList<float> elevations, float d)
        {
            if (d <= distances[0]) return elevations[0];
            int last = distances.Count - 1;
            if (d >= distances[last]) return elevations[last];
            int lo = 0, hi = last;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (distances[mid] <= d) lo = mid; else hi = mid; }
            float t = (d - distances[lo]) / (distances[hi] - distances[lo]);
            return elevations[lo] + t * (elevations[hi] - elevations[lo]);
        }
    }
}
