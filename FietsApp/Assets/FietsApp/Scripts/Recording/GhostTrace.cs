using System;

namespace FietsApp.Recording
{
    /// <summary>
    /// Spoor van een klimpoging voor de ghost (story 9.4): per seconde vanaf de voet hoe ver je op de klim was.
    /// Velden in kleine letters voor <c>JsonUtility</c>.
    /// </summary>
    [Serializable]
    public sealed class GhostTrace
    {
        public string climb = "";
        public string date = "";      // jjjj-mm-dd
        public double seconds;        // klimtijd van voet tot top
        public float length;          // lengte van de klim (m)
        public float[] distances = new float[0]; // meters vanaf de voet, op t = 0, 1, 2, ... s

        /// <summary>Spoor van voet (<paramref name="start"/>) tot top (<paramref name="end"/>) uit een opname; null als de klim niet heel gereden is.</summary>
        public static GhostTrace FromLog(RideLog log, float start, float end, string climb, string date)
        {
            var seconds = log.SecondsBetween(start, end);
            var from = log.TimerAtDistance(start);
            if (seconds == null || from == null) return null;
            float length = end - start;
            int n = (int)Math.Ceiling(seconds.Value) + 1;
            var distances = new float[n];
            for (int k = 0; k < n; k++)
                distances[k] = Math.Max(0f, Math.Min(length, log.DistanceAtTimer(from.Value + k) - start));
            distances[n - 1] = length;
            return new GhostTrace { climb = climb, date = date, seconds = seconds.Value, length = length, distances = distances };
        }

        /// <summary>Waar de ghost is, <paramref name="time"/> seconden na de voet; op de top blijft hij staan.</summary>
        public float DistanceAt(double time)
        {
            if (distances.Length == 0 || time <= 0) return 0f;
            if (time >= seconds) return length;
            int i = Math.Min((int)time, distances.Length - 2);
            double t = time - i;
            return (float)(distances[i] + (distances[i + 1] - distances[i]) * Math.Min(1.0, t));
        }

        /// <summary>Wanneer de ghost op <paramref name="distance"/> meter van de voet was (s), om het verschil te tonen.</summary>
        public double TimeAt(float distance)
        {
            if (distances.Length == 0 || distance <= 0f) return 0;
            if (distance >= length) return seconds;
            for (int i = 1; i < distances.Length; i++)
            {
                float a = distances[i - 1], b = distances[i];
                if (b < distance) continue;
                return Math.Min(seconds, i - 1 + (b > a ? (distance - a) / (double)(b - a) : 1.0));
            }
            return seconds;
        }
    }
}
