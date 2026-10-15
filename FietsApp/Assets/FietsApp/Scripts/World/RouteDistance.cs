using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Dichtstbijzijnde punt op de weg: horizontale afstand en de weghoogte daar.</summary>
    public readonly struct NearestRoad
    {
        public readonly float Distance, Elevation;

        public NearestRoad(float distance, float elevation)
        {
            Distance = distance;
            Elevation = elevation;
        }
    }

    public static class RouteDistance
    {
        /// <summary>Langs alle segmenten zoeken; eenvoudig en exact, voor losse vragen en tests.</summary>
        public static NearestRoad Nearest(Route route, float x, float z)
        {
            var best = new NearestRoad(float.MaxValue, 0f);
            for (int i = 0; i < route.Points.Count - 1; i++)
                best = Closer(best, ToSegment(route.Points[i], route.Points[i + 1], x, z));
            return best;
        }

        internal static NearestRoad ToSegment(RoutePoint a, RoutePoint b, float x, float z)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float lengthSq = dx * dx + dz * dz;
            float t = lengthSq > 0f ? ((x - a.X) * dx + (z - a.Z) * dz) / lengthSq : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float px = a.X + dx * t - x, pz = a.Z + dz * t - z;
            return new NearestRoad((float)Math.Sqrt(px * px + pz * pz), a.Elevation + (b.Elevation - a.Elevation) * t);
        }

        internal static NearestRoad Closer(NearestRoad a, NearestRoad b) => b.Distance < a.Distance ? b : a;
    }

    /// <summary>
    /// Ruimtelijke index op de wegsegmenten (vakken van vaste grootte), zodat "dichtstbijzijnde weg"
    /// voor tienduizenden terreinpunten snel gaat. Zoekt in ringen rond het vak tot niets dichterbij kan liggen.
    /// </summary>
    public sealed class RouteIndex
    {
        readonly Route _route;
        readonly float _cell;
        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        readonly int _minCx, _maxCx, _minCz, _maxCz;

        public RouteIndex(Route route, float cellSize = 50f)
        {
            _route = route;
            _cell = cellSize;
            _minCx = _minCz = int.MaxValue;
            _maxCx = _maxCz = int.MinValue;

            var pts = route.Points;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int x0 = CellOf(Math.Min(pts[i].X, pts[i + 1].X)), x1 = CellOf(Math.Max(pts[i].X, pts[i + 1].X));
                int z0 = CellOf(Math.Min(pts[i].Z, pts[i + 1].Z)), z1 = CellOf(Math.Max(pts[i].Z, pts[i + 1].Z));
                for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                {
                    long key = Key(cx, cz);
                    if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<int>();
                    list.Add(i);
                    _minCx = Math.Min(_minCx, cx); _maxCx = Math.Max(_maxCx, cx);
                    _minCz = Math.Min(_minCz, cz); _maxCz = Math.Max(_maxCz, cz);
                }
            }
        }

        /// <summary>
        /// Dichtstbijzijnde weg. Met <paramref name="maxDistance"/> zoekt hij alleen zo ver; ligt de weg verder,
        /// dan is Distance groter dan maxDistance (mogelijk float.MaxValue). Dat houdt verre punten goedkoop.
        /// </summary>
        public NearestRoad Nearest(float x, float z, float maxDistance = float.MaxValue)
        {
            int cx = CellOf(x), cz = CellOf(z);
            var best = new NearestRoad(float.MaxValue, 0f);
            int maxRing = Math.Max(Math.Max(Math.Abs(cx - _minCx), Math.Abs(cx - _maxCx)),
                                   Math.Max(Math.Abs(cz - _minCz), Math.Abs(cz - _maxCz)));
            if (maxDistance < float.MaxValue) maxRing = Math.Min(maxRing, (int)Math.Ceiling(maxDistance / _cell) + 1);

            for (int ring = 0; ring <= maxRing; ring++)
            {
                // Alles in deze ring ligt minstens (ring - 1) vakken ver; is het beste al dichterbij, dan stoppen.
                if (best.Distance < (ring - 1) * _cell) break;
                for (int dx = -ring; dx <= ring; dx++)
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Abs(dx) != ring && Math.Abs(dz) != ring) continue; // alleen de rand van de ring
                    if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out var segments)) continue;
                    foreach (int i in segments)
                        best = RouteDistance.Closer(best, RouteDistance.ToSegment(_route.Points[i], _route.Points[i + 1], x, z));
                }
            }
            return best;
        }

        int CellOf(float v) => (int)Math.Floor(v / _cell);
        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
    }
}
