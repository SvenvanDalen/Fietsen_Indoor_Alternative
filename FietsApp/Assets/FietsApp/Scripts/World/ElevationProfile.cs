using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Hellingsklasse voor kleuren in het profiel en op borden.</summary>
    public enum GradeBand { Descent, Flat, Moderate, Hard, VeryHard, Extreme }

    public static class Grades
    {
        /// <summary>Grenzen zoals op gangbare klimprofielen: &lt;3% vlak, 3-6, 6-9, 9-12, ≥12%.</summary>
        public static GradeBand Band(float gradePercent) =>
            gradePercent < -1f ? GradeBand.Descent :
            gradePercent < 3f ? GradeBand.Flat :
            gradePercent < 6f ? GradeBand.Moderate :
            gradePercent < 9f ? GradeBand.Hard :
            gradePercent < 12f ? GradeBand.VeryHard :
            GradeBand.Extreme;
    }

    public readonly struct ProfileSegment
    {
        public readonly float StartDistance, EndDistance, StartElevation, EndElevation;

        public ProfileSegment(float startDistance, float endDistance, float startElevation, float endElevation)
        {
            StartDistance = startDistance;
            EndDistance = endDistance;
            StartElevation = startElevation;
            EndElevation = endElevation;
        }

        public float GradePercent =>
            EndDistance > StartDistance ? (EndElevation - StartElevation) / (EndDistance - StartDistance) * 100f : 0f;
    }

    /// <summary>
    /// Hoogteprofiel van een route in vaste stukken (standaard 100 m) met de gemiddelde helling per stuk
    /// (story 6.2). Grof genoeg om leesbaar te zijn, fijn genoeg om steile stukken te zien.
    /// </summary>
    public sealed class ElevationProfile
    {
        public IReadOnlyList<ProfileSegment> Segments { get; }
        public float Length { get; }
        public float MinElevation { get; }
        public float MaxElevation { get; }
        public float SummitDistance { get; }

        ElevationProfile(List<ProfileSegment> segments, float length, float min, float max, float summit)
        {
            Segments = segments;
            Length = length;
            MinElevation = min;
            MaxElevation = max;
            SummitDistance = summit;
        }

        public static ElevationProfile Build(Route route, float segmentLength = 100f)
        {
            var segments = new List<ProfileSegment>();
            float length = route.Length;
            float min = float.MaxValue, max = float.MinValue, summit = 0f;

            for (float start = 0f; start < length - 0.001f; start += segmentLength)
            {
                float end = Math.Min(start + segmentLength, length);
                var segment = new ProfileSegment(start, end, route.Sample(start).Elevation, route.Sample(end).Elevation);
                segments.Add(segment);
            }

            // Min, max en top uit de routepunten zelf, zodat een korte piek binnen een stuk niet wegvalt.
            foreach (var p in route.Points)
            {
                min = Math.Min(min, p.Elevation);
                if (p.Elevation > max)
                {
                    max = p.Elevation;
                    summit = p.Distance;
                }
            }

            return new ElevationProfile(segments, length, min, max, summit);
        }
    }
}
