using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Punt op de route; Distance is de afgelegde afstand langs de weg (m).</summary>
    public readonly struct RoutePoint
    {
        public readonly float Distance, X, Z, Elevation;

        public RoutePoint(float distance, float x, float z, float elevation)
        {
            Distance = distance;
            X = x;
            Z = z;
            Elevation = elevation;
        }
    }

    /// <summary>Toestand op een afstand langs de route. Heading is de genormaliseerde rijrichting in het XZ-vlak.</summary>
    public readonly struct RouteSample
    {
        public readonly float X, Z, Elevation, HeadingX, HeadingZ, GradePercent;

        public RouteSample(float x, float z, float elevation, float headingX, float headingZ, float gradePercent)
        {
            X = x;
            Z = z;
            Elevation = elevation;
            HeadingX = headingX;
            HeadingZ = headingZ;
            GradePercent = gradePercent;
        }
    }

    /// <summary>
    /// Een rijdbare route: punten met horizontale positie en hoogte, oplopend in afstand.
    /// Tussen punten wordt lineair geïnterpoleerd. Bron later: GPX (story 3.1d / 4.1).
    /// </summary>
    public sealed class Route
    {
        readonly RoutePoint[] _points;

        public Route(IReadOnlyList<RoutePoint> points)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("Een route heeft minstens twee punten nodig.");
            _points = new RoutePoint[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = points[i];

            float max = 0f;
            for (int i = 1; i < _points.Length; i++) max = Math.Max(max, SegmentGrade(i - 1));
            MaxGradePercent = max;
        }

        public IReadOnlyList<RoutePoint> Points => _points;
        public float Length => _points[_points.Length - 1].Distance;
        public float MaxGradePercent { get; }

        public RouteSample Sample(float distance)
        {
            int i = SegmentIndex(distance);
            var a = _points[i];
            var b = _points[i + 1];
            float span = b.Distance - a.Distance;
            float t = span > 0f ? Clamp01((distance - a.Distance) / span) : 0f;

            float dx = b.X - a.X, dz = b.Z - a.Z;
            float horizontal = (float)Math.Sqrt(dx * dx + dz * dz);
            float hx = horizontal > 0f ? dx / horizontal : 0f;
            float hz = horizontal > 0f ? dz / horizontal : 1f;

            return new RouteSample(
                a.X + dx * t,
                a.Z + dz * t,
                a.Elevation + (b.Elevation - a.Elevation) * t,
                hx, hz,
                SegmentGrade(i));
        }

        float SegmentGrade(int i)
        {
            float span = _points[i + 1].Distance - _points[i].Distance;
            return span > 0f ? (_points[i + 1].Elevation - _points[i].Elevation) / span * 100f : 0f;
        }

        /// <summary>Index van het segment [i, i+1] dat de afstand bevat (binair zoeken, begrensd op de route).</summary>
        int SegmentIndex(float distance)
        {
            int lo = 0, hi = _points.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_points[mid].Distance <= distance) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
