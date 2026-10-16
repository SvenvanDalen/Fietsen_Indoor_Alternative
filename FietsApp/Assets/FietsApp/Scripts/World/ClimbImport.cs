using System;
using System.Collections.Generic;
using System.Linq;

namespace FietsApp.World
{
    public sealed class ClimbImportSettings
    {
        public float Spacing = 10f;          // m tussen routepunten
        public float MedianRadius = 100f;    // m; mediaan haalt losse uitschieters weg
        // m; Gauss maakt de helling vloeiend. Zo breed omdat hoogtemodellen in haarspeldbochten hoogtes van de
        // bocht erboven of eronder oppikken (Alpe d'Huez: 16% gevolgd door 5% per 100 m); 150 m middelt dat uit.
        public float SmoothingSigma = 150f;
    }

    /// <summary>
    /// Pijplijn van GPX naar rijdbare klim (stories 4.1 en 4.2):
    /// 1. projecteren naar meters rond het startpunt (oost = +X, noord = +Z);
    /// 2. op vaste afstand opnieuw bemonsteren;
    /// 3. hoogte gladstrijken: eerst een mediaan tegen uitschieters van het hoogtemodel, dan een Gauss-filter,
    ///    zodat de helling geleidelijk verandert en de trainer niet schokt.
    /// De echte hoogte boven zeeniveau blijft behouden.
    /// </summary>
    public static class ClimbImport
    {
        public static Route FromGpx(IReadOnlyList<GeoPoint> points, ClimbImportSettings settings = null)
        {
            settings = settings ?? new ClimbImportSettings();
            var projection = new GeoProjection(points[0].Latitude, points[0].Longitude);
            var (x, z, distance) = Project(points, projection);
            var elevation = points.Select(p => p.Elevation).ToArray();

            // Opnieuw bemonsteren op vaste afstand.
            double length = distance[distance.Length - 1];
            int count = (int)Math.Floor(length / settings.Spacing) + 1;
            bool extra = length - (count - 1) * settings.Spacing > settings.Spacing * 0.25;
            int total = count + (extra ? 1 : 0);
            var rx = new double[total];
            var rz = new double[total];
            var rd = new double[total];
            var re = new double[total];
            int segment = 0;
            for (int i = 0; i < total; i++)
            {
                double d = i < count ? i * settings.Spacing : length;
                while (segment < distance.Length - 2 && distance[segment + 1] < d) segment++;
                double span = distance[segment + 1] - distance[segment];
                double t = span > 0 ? Math.Min(1.0, Math.Max(0.0, (d - distance[segment]) / span)) : 0.0;
                rx[i] = x[segment] + (x[segment + 1] - x[segment]) * t;
                rz[i] = z[segment] + (z[segment + 1] - z[segment]) * t;
                re[i] = elevation[segment] + (elevation[segment + 1] - elevation[segment]) * t;
                rd[i] = d;
            }

            // Aan begin en eind spiegelen (zie Pad), anders trekken de filters de hoogte daar naar het midden.
            double reach = Math.Max(settings.MedianRadius, 3 * settings.SmoothingSigma);
            var (pe, pd, offset) = Pad(re, rd, reach);
            var padded = Gaussian(Median(pe, pd, settings.MedianRadius), pd, settings.SmoothingSigma);
            var smooth = new double[total];
            Array.Copy(padded, offset, smooth, 0, total);

            var routePoints = new List<RoutePoint>(total);
            for (int i = 0; i < total; i++)
                routePoints.Add(new RoutePoint((float)rd[i], (float)rx[i], (float)rz[i], (float)smooth[i]));
            return new Route(routePoints, projection);
        }

        /// <summary>Equirectangulaire projectie rond het eerste punt; op een klim van tientallen km nauwkeurig genoeg.</summary>
        static (double[] x, double[] z, double[] distance) Project(IReadOnlyList<GeoPoint> points, GeoProjection projection)
        {
            var x = new double[points.Count];
            var z = new double[points.Count];
            var d = new double[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var (px, pz) = projection.ToLocal(points[i].Latitude, points[i].Longitude);
                x[i] = px;
                z[i] = pz;
                if (i > 0) d[i] = d[i - 1] + Math.Sqrt((x[i] - x[i - 1]) * (x[i] - x[i - 1]) + (z[i] - z[i - 1]) * (z[i] - z[i - 1]));
            }
            return (x, z, d);
        }

        /// <summary>
        /// Verlengt de reeks aan beide kanten met een puntgespiegelde kopie (waarde 2·rand − origineel).
        /// Een gelijkmatige helling loopt zo gewoon door voorbij de rand en blijft na filteren exact.
        /// </summary>
        static (double[] values, double[] distance, int offset) Pad(double[] values, double[] distance, double reach)
        {
            int n = values.Length;
            int before = 0, after = 0;
            while (before + 1 < n && distance[before + 1] - distance[0] <= reach) before++;
            while (after + 1 < n && distance[n - 1] - distance[n - 2 - after] <= reach) after++;

            var v = new double[before + n + after];
            var d = new double[before + n + after];
            for (int i = 0; i < before; i++)
            {
                int src = before - i; // spiegelpunt rond index 0
                v[i] = 2 * values[0] - values[src];
                d[i] = 2 * distance[0] - distance[src];
            }
            Array.Copy(values, 0, v, before, n);
            Array.Copy(distance, 0, d, before, n);
            for (int i = 0; i < after; i++)
            {
                int src = n - 2 - i; // spiegelpunt rond index n-1
                v[before + n + i] = 2 * values[n - 1] - values[src];
                d[before + n + i] = 2 * distance[n - 1] - distance[src];
            }
            return (v, d, before);
        }

        static double[] Median(double[] values, double[] distance, double radius)
        {
            var result = new double[values.Length];
            var window = new List<double>();
            int lo = 0, hi = 0;
            for (int i = 0; i < values.Length; i++)
            {
                while (distance[i] - distance[lo] > radius) lo++;
                while (hi < values.Length - 1 && distance[hi + 1] - distance[i] <= radius) hi++;
                window.Clear();
                for (int j = lo; j <= hi; j++) window.Add(values[j]);
                window.Sort();
                result[i] = window[window.Count / 2];
            }
            return result;
        }

        static double[] Gaussian(double[] values, double[] distance, double sigma)
        {
            var result = new double[values.Length];
            double reach = 3 * sigma, twoSigma2 = 2 * sigma * sigma;
            int lo = 0;
            for (int i = 0; i < values.Length; i++)
            {
                while (distance[i] - distance[lo] > reach) lo++;
                double sum = 0, weights = 0;
                for (int j = lo; j < values.Length && distance[j] - distance[i] <= reach; j++)
                {
                    double dd = distance[j] - distance[i];
                    double w = Math.Exp(-dd * dd / twoSigma2);
                    sum += values[j] * w;
                    weights += w;
                }
                result[i] = sum / weights;
            }
            return result;
        }
    }

    /// <summary>Kerncijfers van een klim (story 4.1): lengte, hoogtes, stijging en hellingen.</summary>
    public readonly struct ClimbStats
    {
        public readonly float LengthMeters, StartElevation, EndElevation, ElevationGain;
        public readonly float AverageGradePercent, MaxGradePercent, MinGradePercent;

        ClimbStats(float length, float start, float end, float gain, float average, float max, float min)
        {
            LengthMeters = length;
            StartElevation = start;
            EndElevation = end;
            ElevationGain = gain;
            AverageGradePercent = average;
            MaxGradePercent = max;
            MinGradePercent = min;
        }

        /// <summary>Maximale en minimale helling over stukken van <paramref name="window"/> m, zoals op klimprofielen.</summary>
        public static ClimbStats Of(Route route, float window = 100f)
        {
            var pts = route.Points;
            float gain = 0f;
            for (int i = 1; i < pts.Count; i++) gain += Math.Max(0f, pts[i].Elevation - pts[i - 1].Elevation);

            float max = float.MinValue, min = float.MaxValue;
            for (float d = 0f; d + window <= route.Length + 0.01f; d += window / 2f)
            {
                float grade = (route.Sample(d + window).Elevation - route.Sample(d).Elevation) / window * 100f;
                max = Math.Max(max, grade);
                min = Math.Min(min, grade);
            }
            if (max == float.MinValue) max = min = 0f;

            float start = pts[0].Elevation, end = pts[pts.Count - 1].Elevation;
            return new ClimbStats(route.Length, start, end, gain, (end - start) / route.Length * 100f, max, min);
        }
    }
}
