using System;
using System.Collections.Generic;
using System.Linq;

namespace FietsApp.World
{
    /// <summary>
    /// Waar een klim in de wereld ligt (story 4.11a): gedraaid en verschoven vanuit zijn eigen meters, en in hoogte
    /// verschoven zodat de voet op de hoogte van het dal ligt. Hellingen blijven precies gelijk.
    /// </summary>
    public readonly struct ClimbPlacement
    {
        public readonly float Rotation;        // radialen, tegen de klok in
        public readonly float OffsetX, OffsetZ; // waar het eerste punt van de klim komt
        public readonly float ElevationOffset;  // wereldhoogte = echte hoogte + deze waarde
        readonly float _originX, _originZ;      // eerste punt in de eigen meters van de klim

        public ClimbPlacement(float rotation, float originX, float originZ, float offsetX, float offsetZ, float elevationOffset)
        {
            Rotation = rotation;
            _originX = originX;
            _originZ = originZ;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            ElevationOffset = elevationOffset;
        }

        public (float x, float z) ToWorld(float x, float z)
        {
            float dx = x - _originX, dz = z - _originZ;
            float c = (float)Math.Cos(Rotation), s = (float)Math.Sin(Rotation);
            return (OffsetX + dx * c - dz * s, OffsetZ + dx * s + dz * c);
        }

        public (float x, float z) ToLocal(float x, float z)
        {
            float dx = x - OffsetX, dz = z - OffsetZ;
            float c = (float)Math.Cos(Rotation), s = (float)Math.Sin(Rotation);
            return (_originX + dx * c + dz * s, _originZ - dx * s + dz * c);
        }

        public float RealElevation(float worldElevation) => worldElevation - ElevationOffset;
    }

    public sealed class WorldClimb
    {
        public string Id { get; }
        public string Name { get; }
        public string Region { get; }
        public Route Route { get; }   // eigen meters, echte hoogte
        public ClimbPlacement Placement { get; internal set; }
        public float Angle { get; internal set; } // richting vanaf het knooppunt, radialen

        public WorldClimb(string id, string name, string region, Route route)
        {
            Id = id;
            Name = name;
            Region = region;
            Route = route;
        }

        /// <summary>De klim in wereldcoördinaten: gedraaid, verschoven en op dalhoogte.</summary>
        public Route WorldRoute()
        {
            var p = Placement;
            return new Route(Route.Points.Select(pt =>
            {
                var (x, z) = p.ToWorld(pt.X, pt.Z);
                return new RoutePoint(pt.Distance, x, z, pt.Elevation + p.ElevationOffset);
            }).ToList());
        }
    }

    public sealed class WorldRegion
    {
        public string Name { get; }
        public float HubX { get; internal set; }
        public float HubZ { get; internal set; }
        public float ValleyElevation { get; internal set; }
        public List<WorldClimb> Climbs { get; } = new List<WorldClimb>();

        public WorldRegion(string name) => Name = name;
    }

    public sealed class WorldSettings
    {
        public float ApproachLength = 1500f;   // m van het knooppunt tot de voet van een klim
        public float RegionSpacing = 50000f;   // m tussen de knooppunten van de gebieden (ring)
        public float Spacing = 10f;            // m tussen routepunten van dalwegen
    }

    /// <summary>Een deel van een rit door de wereld.</summary>
    public enum JourneyPart { Approach, Climb, Descent, Return }

    public readonly struct JourneySegment
    {
        public readonly JourneyPart Part;
        public readonly float Start, End;

        public JourneySegment(JourneyPart part, float start, float end)
        {
            Part = part;
            Start = start;
            End = end;
        }
    }

    /// <summary>Een doorlopende rit: knooppunt → dalweg → klim → dezelfde weg terug → knooppunt.</summary>
    public sealed class Journey
    {
        public WorldClimb Climb { get; }
        public Route Route { get; }
        public IReadOnlyList<JourneySegment> Segments { get; }

        public Journey(WorldClimb climb, Route route, IReadOnlyList<JourneySegment> segments)
        {
            Climb = climb;
            Route = route;
            Segments = segments;
        }

        public JourneySegment this[JourneyPart part] => Segments.First(s => s.Part == part);

        public JourneyPart PartAt(float distance)
        {
            foreach (var s in Segments)
                if (distance < s.End) return s.Part;
            return Segments[Segments.Count - 1].Part;
        }
    }

    /// <summary>
    /// De doorlopende wereld (story 4.11a, besluit "Doorlopende wereld"): per gebied een dal met een knooppunt,
    /// de klimmen er in een kring omheen, en de knooppunten in een ring. Alleen de corridor rond de gereden weg wordt
    /// gebouwd, dus klimmen mogen elkaar op de kaart kruisen.
    /// </summary>
    public sealed class WorldMap
    {
        public IReadOnlyList<WorldRegion> Regions { get; }
        public WorldSettings Settings { get; }

        WorldMap(IReadOnlyList<WorldRegion> regions, WorldSettings settings)
        {
            Regions = regions;
            Settings = settings;
        }

        public IEnumerable<WorldClimb> Climbs => Regions.SelectMany(r => r.Climbs);

        public WorldClimb Find(string id) => Climbs.FirstOrDefault(c => c.Id == id);

        public WorldRegion RegionOf(WorldClimb climb) => Regions.First(r => r.Climbs.Contains(climb));

        public static WorldMap Build(IEnumerable<WorldClimb> climbs, WorldSettings settings = null)
        {
            settings = settings ?? new WorldSettings();
            var regions = climbs.GroupBy(c => c.Region).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var region = new WorldRegion(g.Key);
                    region.Climbs.AddRange(g.OrderBy(c => c.Name, StringComparer.Ordinal));
                    return region;
                }).ToList();

            // Knooppunten in een ring, zodat elk gebied twee buren heeft.
            float ringRadius = regions.Count > 1 ? settings.RegionSpacing / (2f * (float)Math.Sin(Math.PI / regions.Count)) : 0f;
            for (int r = 0; r < regions.Count; r++)
            {
                double a = 2 * Math.PI * r / regions.Count;
                regions[r].HubX = ringRadius * (float)Math.Cos(a);
                regions[r].HubZ = ringRadius * (float)Math.Sin(a);
                Place(regions[r], settings);
            }
            return new WorldMap(regions, settings);
        }

        /// <summary>Klimmen gelijk verdeeld rond het knooppunt, elk wijzend van het knooppunt af; het dal op de laagste voet.</summary>
        static void Place(WorldRegion region, WorldSettings s)
        {
            region.ValleyElevation = region.Climbs.Min(c => c.Route.Points[0].Elevation);
            int n = region.Climbs.Count;
            for (int k = 0; k < n; k++)
            {
                var climb = region.Climbs[k];
                float angle = (float)(2 * Math.PI * k / n);
                var first = climb.Route.Points[0];
                var last = climb.Route.Points[climb.Route.Points.Count - 1];
                float own = (float)Math.Atan2(last.Z - first.Z, last.X - first.X); // algemene richting van de klim
                float footX = region.HubX + s.ApproachLength * (float)Math.Cos(angle);
                float footZ = region.HubZ + s.ApproachLength * (float)Math.Sin(angle);
                climb.Angle = angle;
                climb.Placement = new ClimbPlacement(angle - own, first.X, first.Z, footX, footZ,
                    region.ValleyElevation - first.Elevation);
            }
        }

        /// <summary>Rit vanaf het knooppunt van het gebied naar de top van de klim en dezelfde weg terug (afdaling, 4.12).</summary>
        public Journey Journey(WorldClimb climb)
        {
            var region = RegionOf(climb);
            var climbRoute = climb.WorldRoute();
            var approach = Approach(region, climbRoute);

            var points = new List<(float x, float z, float y)>();
            var segments = new List<JourneySegment>();
            float d = 0f;
            void Append(IEnumerable<(float x, float z, float y)> part, JourneyPart kind)
            {
                float start = d;
                foreach (var p in part)
                {
                    if (points.Count > 0)
                    {
                        var prev = points[points.Count - 1];
                        float step = (float)Math.Sqrt((p.x - prev.x) * (p.x - prev.x) + (p.z - prev.z) * (p.z - prev.z));
                        if (step < 0.01f) continue; // aansluitpunt staat al in de lijst
                        d += step;
                    }
                    points.Add(p);
                }
                segments.Add(new JourneySegment(kind, start, d));
            }

            var climbPts = climbRoute.Points.Select(p => (p.X, p.Z, p.Elevation)).ToList();
            Append(approach, JourneyPart.Approach);
            Append(climbPts, JourneyPart.Climb);
            Append(Enumerable.Reverse(climbPts), JourneyPart.Descent);
            Append(Enumerable.Reverse(approach), JourneyPart.Return);

            var routePoints = new List<RoutePoint>(points.Count);
            float dist = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    var a = points[i - 1];
                    var b = points[i];
                    dist += (float)Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                }
                routePoints.Add(new RoutePoint(dist, points[i].x, points[i].z, points[i].y));
            }
            return new Journey(climb, new Route(routePoints), segments);
        }

        /// <summary>
        /// Dalweg van het knooppunt naar de voet: een vloeiende boog (kubische Bézier) die vertrekt in de richting van
        /// de klim vanaf het knooppunt en aankomt in de beginrichting van de klim, vlak op dalhoogte.
        /// </summary>
        List<(float x, float z, float y)> Approach(WorldRegion region, Route climbRoute)
        {
            var foot = climbRoute.Points[0];
            var next = climbRoute.Sample(Math.Min(50f, climbRoute.Length));
            float hx = region.HubX, hz = region.HubZ;
            float len = (float)Math.Sqrt((foot.X - hx) * (foot.X - hx) + (foot.Z - hz) * (foot.Z - hz));
            float ux = len > 0 ? (foot.X - hx) / len : 1f, uz = len > 0 ? (foot.Z - hz) / len : 0f;
            float handle = len / 3f;
            float p1x = hx + ux * handle, p1z = hz + uz * handle;
            float p2x = foot.X - next.HeadingX * handle, p2z = foot.Z - next.HeadingZ * handle;

            // Fijn bemonsteren en daarna op gelijke afstand zetten.
            var fine = new List<(float x, float z)>();
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f, u = 1 - t;
                fine.Add((u * u * u * hx + 3 * u * u * t * p1x + 3 * u * t * t * p2x + t * t * t * foot.X,
                          u * u * u * hz + 3 * u * u * t * p1z + 3 * u * t * t * p2z + t * t * t * foot.Z));
            }
            var result = new List<(float x, float z, float y)> { (hx, hz, region.ValleyElevation) };
            float carried = 0f;
            for (int i = 1; i < fine.Count; i++)
            {
                var a = fine[i - 1];
                var b = fine[i];
                float seg = (float)Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                float pos = Settings.Spacing - carried;
                while (pos <= seg)
                {
                    float t = pos / seg;
                    result.Add((a.x + (b.x - a.x) * t, a.z + (b.z - a.z) * t, region.ValleyElevation));
                    pos += Settings.Spacing;
                }
                carried = seg - (pos - Settings.Spacing);
            }
            result.Add((foot.X, foot.Z, foot.Elevation));
            return result;
        }
    }
}
