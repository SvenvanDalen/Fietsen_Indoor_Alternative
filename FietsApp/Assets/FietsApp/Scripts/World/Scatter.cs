using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    public enum ScatterKind { Tree, Rock }

    public readonly struct ScatterItem
    {
        public readonly ScatterKind Kind;
        public readonly float X, Y, Z, Scale, RotationDegrees;
        public readonly float RoadDistance; // benaderd; voor eenvoudiger modellen ver van de weg

        public ScatterItem(ScatterKind kind, float x, float y, float z, float scale, float rotationDegrees, float roadDistance = 0f)
        {
            Kind = kind;
            RoadDistance = roadDistance;
            X = x;
            Y = y;
            Z = z;
            Scale = scale;
            RotationDegrees = rotationDegrees;
        }
    }

    /// <summary>
    /// Verspreidt bomen en rotsen over het terrein (story 3.2). Per vak van een raster hoogstens één
    /// exemplaar op een verschoven plek; alles volgt uit een hash van vak en seed, dus elke keer gelijk.
    /// Bomen op niet te steile grond dicht bij de weg, rotsen vooral op steile stukken.
    /// </summary>
    public static class Scatter
    {
        const float TreeMaxSlope = 0.55f, RockMinSlope = 0.35f;
        const float KeepOffRoad = 4f; // extra ruimte zodat een boomkruin niet boven de berm hangt

        public static List<ScatterItem> Place(Route route, HeightGrid grid, TerrainSettings s)
        {
            var items = new List<ScatterItem>();
            var index = new RouteIndex(route);
            var biome = new Biome(s);
            PlaceKind(items, ScatterKind.Tree, s.TreeSpacing, grid, s, index, biome);
            PlaceKind(items, ScatterKind.Rock, s.RockSpacing, grid, s, index, biome);
            return items;
        }

        static void PlaceKind(List<ScatterItem> items, ScatterKind kind, float spacing,
            HeightGrid grid, TerrainSettings s, RouteIndex index, Biome biome)
        {
            int nx = (int)((grid.MaxX - grid.MinX) / spacing), nz = (int)((grid.MaxZ - grid.MinZ) / spacing);
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                uint h = Hash(ix, iz, s.Seed * 31 + (int)kind);
                float x = grid.MinX + (ix + Unit(h, 0)) * spacing;
                float z = grid.MinZ + (iz + Unit(h, 1)) * spacing;

                // Eerst de goedkope, benaderde afstand; exact controleren alleen vlak bij de weg.
                float distance = grid.RoadDistanceAt(x, z);
                if (distance > (kind == ScatterKind.Tree ? s.TreeBand : s.RockBand)) continue;
                float keepOff = s.ClearWidth + KeepOffRoad;
                if (distance < keepOff + 2f * grid.CellSize && index.Nearest(x, z, keepOff).Distance <= keepOff) continue;

                float slope = grid.SlopeAt(x, z);
                bool bare = biome.IsAboveTreeLine(x, z, grid.HeightAt(x, z));
                float chance;
                if (kind == ScatterKind.Tree)
                {
                    if (bare || distance > s.TreeBand || slope > TreeMaxSlope) continue;
                    // Dichter bij de weg dichter bos; verder weg wordt het open.
                    chance = 0.75f * (1f - distance / s.TreeBand) + 0.1f;
                }
                else
                {
                    if (distance > s.RockBand) continue;
                    // Boven de boomgrens ligt overal los gesteente.
                    chance = slope > RockMinSlope ? 0.7f : bare ? 0.45f : 0.08f;
                }
                if (Unit(h, 2) > chance) continue;

                items.Add(new ScatterItem(kind, x, grid.HeightAt(x, z), z,
                    0.7f + Unit(h, 3) * 0.7f, Unit(h, 4) * 360f, distance));
            }
        }

        /// <summary>Getal in [0, 1) uit de hash; kanaal kiest een ander deel van de bits.</summary>
        static float Unit(uint hash, int channel) => (Mix(hash + (uint)channel * 0x9E3779B9u) & 0xFFFFFF) / 16777216f;

        static uint Hash(int x, int z, int seed) => Mix((uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)seed * 83492791u);

        static uint Mix(uint h)
        {
            h ^= h >> 16;
            h *= 0x7feb352d;
            h ^= h >> 15;
            h *= 0x846ca68b;
            h ^= h >> 16;
            return h;
        }
    }
}
