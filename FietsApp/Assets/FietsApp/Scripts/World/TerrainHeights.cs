using System;

namespace FietsApp.World
{
    /// <summary>Instellingen voor terrein en begroeiing rond een route (story 3.2). Afstanden in meters.</summary>
    public sealed class TerrainSettings
    {
        public int Seed = 42;
        public float CellSize = 10f;
        public float Margin = 600f;          // terrein rond de route

        // Rond de weg
        public float RoadDepth = 0.4f;       // terrein ligt zo ver onder het asfalt
        public float ClearWidth = 12f;       // tot hier volgt het terrein de weg exact (weg + berm + speling)
        public float Falloff = 28f;          // overgang van weg naar natuurlijk terrein

        // Natuurlijk terrein
        public float NoiseScale = 600f;      // golflengte van de grootste heuvels
        public float AmplitudePerMeter = 0.35f;
        public float MaxAmplitude = 160f;
        public float RisePerMeter = 0.12f;   // bergen worden hoger van de weg af
        public float MaxRise = 120f;
        public float SmoothingRadius = 120f; // hoe ver de routehoogte doorwerkt in het terrein

        // Begroeiing
        public float TreeSpacing = 18f;
        public float TreeBand = 350f;        // bomen tot zo ver van de weg
        public float RockSpacing = 45f;
    }

    /// <summary>Hoogteraster: CountX × CountZ hoekpunten, beginnend op (MinX, MinZ).</summary>
    public sealed class HeightGrid
    {
        readonly float[] _heights;
        readonly float[] _roadDistance;

        public HeightGrid(float minX, float minZ, float cellSize, int countX, int countZ)
        {
            MinX = minX;
            MinZ = minZ;
            CellSize = cellSize;
            CountX = countX;
            CountZ = countZ;
            _heights = new float[countX * countZ];
            _roadDistance = new float[countX * countZ];
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float CellSize { get; }
        public int CountX { get; }
        public int CountZ { get; }
        public float MaxX => MinX + (CountX - 1) * CellSize;
        public float MaxZ => MinZ + (CountZ - 1) * CellSize;

        public float this[int x, int z]
        {
            get => _heights[z * CountX + x];
            set => _heights[z * CountX + x] = value;
        }

        /// <summary>Horizontale afstand van een hoekpunt tot de weg; exact dicht bij de weg, benaderd verder weg.</summary>
        public float RoadDistance(int x, int z) => _roadDistance[z * CountX + x];

        internal void SetRoadDistance(int x, int z, float distance) => _roadDistance[z * CountX + x] = distance;

        /// <summary>Hoogte op een willekeurige plek, bilineair tussen de hoekpunten (begrensd op het raster).</summary>
        public float HeightAt(float x, float z) => Bilinear(_heights, x, z);

        /// <summary>Afstand tot de weg op een willekeurige plek (bilineair, dus benaderd).</summary>
        public float RoadDistanceAt(float x, float z) => Bilinear(_roadDistance, x, z);

        float Bilinear(float[] values, float x, float z)
        {
            float fx = Clamp((x - MinX) / CellSize, 0f, CountX - 1.0001f);
            float fz = Clamp((z - MinZ) / CellSize, 0f, CountZ - 1.0001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int i = iz * CountX + ix;
            float a = values[i] + (values[i + 1] - values[i]) * tx;
            float b = values[i + CountX] + (values[i + CountX + 1] - values[i + CountX]) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>Helling als verhouding hoogte/afstand (0 = vlak, 1 = 45°).</summary>
        public float SlopeAt(float x, float z)
        {
            float d = CellSize;
            float gx = (HeightAt(x + d, z) - HeightAt(x - d, z)) / (2f * d);
            float gz = (HeightAt(x, z + d) - HeightAt(x, z - d)) / (2f * d);
            return (float)Math.Sqrt(gx * gx + gz * gz);
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// Maakt het terrein rond een route. Vlak bij de weg ligt het net onder het asfalt; verder weg
    /// gaat het over in heuvels en bergen die groter worden naarmate je verder van de weg komt.
    /// </summary>
    public static class TerrainHeights
    {
        public static HeightGrid Generate(Route route, TerrainSettings s)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in route.Points)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
            }
            minX = (float)Math.Floor((minX - s.Margin) / s.CellSize) * s.CellSize;
            minZ = (float)Math.Floor((minZ - s.Margin) / s.CellSize) * s.CellSize;
            int countX = (int)Math.Ceiling((maxX + s.Margin - minX) / s.CellSize) + 1;
            int countZ = (int)Math.Ceiling((maxZ + s.Margin - minZ) / s.CellSize) + 1;

            var grid = new HeightGrid(minX, minZ, s.CellSize, countX, countZ);
            var index = new RouteIndex(route);
            var noise = new Noise(s.Seed);
            var anchors = SmoothingAnchors(route, s.SmoothingRadius / 2f);

            // Stap 1: exacte afstand en weghoogte, maar alleen dicht bij de weg (waar het terrein de weg volgt).
            float nearRange = s.ClearWidth + s.Falloff + s.CellSize;
            var roadElevation = new float[countX * countZ];
            for (int iz = 0; iz < countZ; iz++)
            for (int ix = 0; ix < countX; ix++)
            {
                var road = index.Nearest(minX + ix * s.CellSize, minZ + iz * s.CellSize, nearRange);
                grid.SetRoadDistance(ix, iz, road.Distance <= nearRange ? road.Distance : float.MaxValue);
                roadElevation[iz * countX + ix] = road.Elevation;
            }

            // Stap 2: verder weg een benaderde afstand; die bepaalt alleen hoe groot de bergen worden.
            ChamferDistance(grid);

            for (int iz = 0; iz < countZ; iz++)
            for (int ix = 0; ix < countX; ix++)
            {
                float x = minX + ix * s.CellSize, z = minZ + iz * s.CellSize;
                float distance = grid.RoadDistance(ix, iz);

                float t = SmoothStep(s.ClearWidth, s.ClearWidth + s.Falloff, distance);
                float height = roadElevation[iz * countX + ix] - s.RoadDepth;
                if (t > 0f)
                {
                    float amplitude = Math.Min(distance * s.AmplitudePerMeter, s.MaxAmplitude);
                    float rise = Math.Min(distance * s.RisePerMeter, s.MaxRise);
                    float natural = SmoothElevation(anchors, x, z, s.SmoothingRadius)
                                    + noise.Fbm(x / s.NoiseScale, z / s.NoiseScale) * amplitude
                                    + rise;
                    height += (natural - height) * t;
                }
                grid[ix, iz] = height;
            }
            return grid;
        }

        /// <summary>
        /// Vult ontbrekende afstanden met een chamfer-afstandstransformatie (twee vegen over het raster,
        /// 8 buren). Een paar procent te groot in schuine richtingen; ruim genoeg voor de vorm van de bergen.
        /// </summary>
        static void ChamferDistance(HeightGrid g)
        {
            float straight = g.CellSize, diagonal = g.CellSize * 1.41421356f;
            int nx = g.CountX, nz = g.CountZ;

            for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                float d = g.RoadDistance(x, z);
                if (x > 0) d = Math.Min(d, g.RoadDistance(x - 1, z) + straight);
                if (z > 0) d = Math.Min(d, g.RoadDistance(x, z - 1) + straight);
                if (x > 0 && z > 0) d = Math.Min(d, g.RoadDistance(x - 1, z - 1) + diagonal);
                if (x < nx - 1 && z > 0) d = Math.Min(d, g.RoadDistance(x + 1, z - 1) + diagonal);
                g.SetRoadDistance(x, z, d);
            }

            for (int z = nz - 1; z >= 0; z--)
            for (int x = nx - 1; x >= 0; x--)
            {
                float d = g.RoadDistance(x, z);
                if (x < nx - 1) d = Math.Min(d, g.RoadDistance(x + 1, z) + straight);
                if (z < nz - 1) d = Math.Min(d, g.RoadDistance(x, z + 1) + straight);
                if (x < nx - 1 && z < nz - 1) d = Math.Min(d, g.RoadDistance(x + 1, z + 1) + diagonal);
                if (x > 0 && z < nz - 1) d = Math.Min(d, g.RoadDistance(x - 1, z + 1) + diagonal);
                g.SetRoadDistance(x, z, d);
            }
        }

        /// <summary>Routepunten om de ~60 m: genoeg voor een vloeiende hoogte, veel sneller dan alle punten.</summary>
        static RoutePoint[] SmoothingAnchors(Route route, float spacing)
        {
            var result = new System.Collections.Generic.List<RoutePoint>();
            float next = 0f;
            foreach (var p in route.Points)
            {
                if (p.Distance < next) continue;
                result.Add(p);
                next = p.Distance + spacing;
            }
            return result.ToArray();
        }

        /// <summary>
        /// Gewogen gemiddelde van de routehoogte (inverse afstand, verzacht met de straal), zodat het terrein
        /// tussen twee haarspeldbenen vloeiend van de ene naar de andere hoogte loopt.
        /// </summary>
        static float SmoothElevation(RoutePoint[] anchors, float x, float z, float radius)
        {
            double sum = 0, weights = 0, r2 = radius * radius;
            foreach (var p in anchors)
            {
                double dx = p.X - x, dz = p.Z - z;
                double d2 = dx * dx + dz * dz + r2;
                double w = 1.0 / (d2 * d2);
                sum += p.Elevation * w;
                weights += w;
            }
            return (float)(sum / weights);
        }

        static float SmoothStep(float edge0, float edge1, float v)
        {
            float t = (v - edge0) / (edge1 - edge0);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}
