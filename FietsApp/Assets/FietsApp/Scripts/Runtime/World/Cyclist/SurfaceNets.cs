using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FietsApp
{
    /// <summary>Hoekpunten, driehoeken per materiaal-slot en eventueel huidgewichten van een gebouwde vorm.</summary>
    public sealed class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;      // uit het veld: glad, zonder de kleine oneffenheden van het net
        public int[][] Triangles;      // per slot
        public BoneWeight[] Weights;   // alleen bij een vervormbare mesh

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (Vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = Vertices;
            mesh.normals = Normals;
            mesh.subMeshCount = Triangles.Length;
            for (int i = 0; i < Triangles.Length; i++) mesh.SetTriangles(Triangles[i], i, false);
            if (Weights != null) mesh.boneWeights = Weights;
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Maakt van een <see cref="Sdf"/> een gladde mesh met "surface nets": één hoekpunt per roostercel waar het
    /// oppervlak doorheen gaat, met een Newton-stap op het oppervlak gezet. Het rooster wordt alleen uitgerekend in
    /// blokken bij het oppervlak, elk blok met alleen de onderdelen in de buurt, in Burst-jobs over alle
    /// processorkernen; zo blijven cellen van een paar millimeter betaalbaar. Elke driehoek krijgt het slot van het
    /// onderdeel dat er de doorslag geeft; met <c>bones</c> &gt; 0 komen er huidgewichten bij.
    /// </summary>
    public static class SurfaceNets
    {
        const int B = 8;                         // cellen per blok en richting
        const int Points = (B + 1) * (B + 1) * (B + 1);
        const int Cells = B * B * B;
        const int MaxNear = 96;                  // meer onderdelen bij één blok: dan rekent dat blok met allemaal
        const int SlotSpan = 32;                 // driehoek: (onderdeel + 1) * SlotSpan + slot

        public static MeshData Build(Sdf sdf, float cell, int slots, int bones = 0, float weightFalloff = 0.02f)
        {
            var extent = sdf.Extent;
            var origin = (float3)(extent.min - Vector3.one * (cell * 2f));
            var size = extent.size + Vector3.one * (cell * 4f);
            var grid = new int3(Mathf.CeilToInt(size.x / (cell * B)), Mathf.CeilToInt(size.y / (cell * B)), Mathf.CeilToInt(size.z / (cell * B)));
            int blockCount = grid.x * grid.y * grid.z;
            float blockSize = cell * B, halfDiagonal = blockSize * 0.8660254f;

            var prims = sdf.ToNative(Allocator.TempJob);
            var centerDistance = new NativeArray<float>(blockCount, Allocator.TempJob);
            new CoarseJob { Prims = prims, Distance = centerDistance, Origin = origin, Grid = grid, BlockSize = blockSize }
                .Schedule(blockCount, 16).Complete();

            // Actieve blokken: die het oppervlak kunnen raken.
            var blockMap = new NativeArray<int>(blockCount, Allocator.TempJob);
            var active = new List<int>();
            for (int n = 0; n < blockCount; n++)
            {
                bool hit = centerDistance[n] <= halfDiagonal * 1.3f + cell;
                blockMap[n] = hit ? active.Count : -1;
                if (hit) active.Add(n);
            }
            int activeCount = active.Count;
            var activeBlocks = new NativeArray<int>(active.ToArray(), Allocator.TempJob);
            var near = new NativeArray<int>(activeCount * MaxNear, Allocator.TempJob);
            var nearCount = new NativeArray<int>(activeCount, Allocator.TempJob);
            var values = new NativeArray<float>(activeCount * Points, Allocator.TempJob);
            var cellPosition = new NativeArray<float3>(activeCount * Cells, Allocator.TempJob);
            var cellNormal = new NativeArray<float3>(activeCount * Cells, Allocator.TempJob);
            var cellHas = new NativeArray<byte>(activeCount * Cells, Allocator.TempJob);

            var setup = new BlockSetup { Origin = origin, Grid = grid, Cell = cell, BlockSize = blockSize, HalfDiagonal = halfDiagonal };
            var handle = new NearJob { Prims = prims, Active = activeBlocks, CenterDistance = centerDistance, Near = near, NearCount = nearCount, Setup = setup }
                .Schedule(activeCount, 4);
            handle = new VertexJob
            {
                Prims = prims, Active = activeBlocks, Near = near, NearCount = nearCount, Values = values,
                Position = cellPosition, Normal = cellNormal, Has = cellHas, Setup = setup,
            }.Schedule(activeCount, 1, handle);
            handle.Complete();

            // Hoekpunten nummeren.
            var cellIndex = new NativeArray<int>(activeCount * Cells, Allocator.TempJob);
            int count = 0;
            for (int i = 0; i < cellIndex.Length; i++) cellIndex[i] = cellHas[i] != 0 ? count++ : -1;
            var positions = new NativeArray<float3>(count, Allocator.TempJob);
            var normals = new NativeArray<float3>(count, Allocator.TempJob);
            var vertexBlock = new NativeArray<int>(count, Allocator.TempJob);
            for (int i = 0; i < cellIndex.Length; i++)
                if (cellIndex[i] >= 0)
                {
                    positions[cellIndex[i]] = cellPosition[i];
                    normals[cellIndex[i]] = cellNormal[i];
                    vertexBlock[cellIndex[i]] = i / Cells;
                }

            // Vierhoeken over elke ribbe met een tekenwissel.
            var triangles = new NativeList<int4>(count * 3 + 1024, Allocator.TempJob);
            new QuadJob
            {
                Prims = prims, Active = activeBlocks, BlockMap = blockMap, Near = near, NearCount = nearCount, Values = values,
                CellIndex = cellIndex, Positions = positions, Normals = normals, Triangles = triangles.AsParallelWriter(), Setup = setup,
            }.Schedule(activeCount, 1).Complete();

            Smooth(positions, triangles, 6);
            Snap(positions, triangles, prims, near, nearCount, vertexBlock, cell);
            new NormalJob { Prims = prims, Near = near, NearCount = nearCount, Block = vertexBlock, Positions = positions, Normals = normals, Eps = cell * 0.5f }
                .Schedule(count, 64).Complete();

            var data = new MeshData { Vertices = new Vector3[count], Normals = new Vector3[count], Triangles = new int[slots][] };
            for (int i = 0; i < count; i++) { data.Vertices[i] = positions[i]; data.Normals[i] = normals[i]; }
            var perSlot = new List<int>[slots];
            for (int s = 0; s < slots; s++) perSlot[s] = new List<int>();
            for (int t = 0; t < triangles.Length; t++)
            {
                var tri = triangles[t];
                var list = perSlot[math.clamp(tri.w % SlotSpan, 0, slots - 1)];
                list.Add(tri.x); list.Add(tri.y); list.Add(tri.z);
            }
            for (int s = 0; s < slots; s++) data.Triangles[s] = perSlot[s].ToArray();

            if (bones > 0)
            {
                var bone = new NativeArray<int2>(count, Allocator.TempJob);
                var weight = new NativeArray<float2>(count, Allocator.TempJob);
                new WeightJob { Prims = prims, Positions = positions, Bones = bones, Falloff = weightFalloff, Bone = bone, Weight = weight }
                    .Schedule(count, 64).Complete();
                data.Weights = new BoneWeight[count];
                for (int i = 0; i < count; i++)
                    data.Weights[i] = new BoneWeight { boneIndex0 = bone[i].x, weight0 = weight[i].x, boneIndex1 = bone[i].y, weight1 = weight[i].y };
                bone.Dispose();
                weight.Dispose();
            }

            prims.Dispose(); centerDistance.Dispose(); blockMap.Dispose(); activeBlocks.Dispose(); near.Dispose(); nearCount.Dispose();
            values.Dispose(); cellPosition.Dispose(); cellNormal.Dispose(); cellHas.Dispose(); cellIndex.Dispose();
            positions.Dispose(); normals.Dispose(); vertexBlock.Dispose(); triangles.Dispose();
            return data;
        }

        /// <summary>
        /// Strijkt het net glad (Taubin: om en om naar het gemiddelde van de buren toe en er iets voorbij terug, zodat
        /// de vorm niet krimpt). Haalt de traptreetjes van het rooster weg, die anders als korrel en zelfschaduw te
        /// zien zijn.
        /// </summary>
        static void Smooth(NativeArray<float3> positions, NativeList<int4> triangles, int rounds)
        {
            int count = positions.Length;
            // Buren per hoekpunt (CSR): eerst tellen, dan vullen; dubbele buren tellen gewoon dubbel mee.
            var start = new NativeArray<int>(count + 1, Allocator.TempJob);
            for (int t = 0; t < triangles.Length; t++)
            {
                var tri = triangles[t];
                start[tri.x + 1] += 2; start[tri.y + 1] += 2; start[tri.z + 1] += 2;
            }
            for (int i = 0; i < count; i++) start[i + 1] += start[i];
            var neighbours = new NativeArray<int>(start[count], Allocator.TempJob);
            var fill = new NativeArray<int>(count, Allocator.TempJob);
            for (int t = 0; t < triangles.Length; t++)
            {
                var tri = triangles[t];
                void Link(int a, int b) { neighbours[start[a] + fill[a]++] = b; }
                Link(tri.x, tri.y); Link(tri.x, tri.z);
                Link(tri.y, tri.x); Link(tri.y, tri.z);
                Link(tri.z, tri.x); Link(tri.z, tri.y);
            }
            var other = new NativeArray<float3>(count, Allocator.TempJob);
            for (int r = 0; r < rounds; r++)
            {
                new SmoothJob { From = positions, To = other, Start = start, Neighbours = neighbours, Factor = 0.5f }.Schedule(count, 256).Complete();
                new SmoothJob { From = other, To = positions, Start = start, Neighbours = neighbours, Factor = -0.53f }.Schedule(count, 256).Complete();
            }
            start.Dispose(); neighbours.Dispose(); fill.Dispose(); other.Dispose();
        }

        /// <summary>
        /// Maakt kleurgrenzen strak: een hoekpunt tussen driehoeken van verschillende kleur schuift naar de echte grens,
        /// zodat de rand van een mouw, kraag of haarlijn een vloeiende lijn wordt in plaats van een trap over de
        /// driehoeken. Wisselt de kleur langs de kleurlijn van één onderdeel, dan schuift het hoekpunt langs die lijn
        /// naar het breekpunt; grenzen twee onderdelen met elk hun eigen kleur, dan schuift het over het oppervlak naar
        /// waar ze even dichtbij zijn.
        /// </summary>
        static void Snap(NativeArray<float3> positions, NativeList<int4> triangles, NativeArray<SdfPrim> prims, NativeArray<int> near,
            NativeArray<int> nearCount, NativeArray<int> block, float cell)
        {
            int count = positions.Length;
            // Per hoekpunt de eerste twee (onderdeel, slot)-paren met een verschillend slot.
            var pairs = new NativeArray<int4>(count, Allocator.TempJob);
            for (int v = 0; v < count; v++) pairs[v] = new int4(-1);
            for (int t = 0; t < triangles.Length; t++)
            {
                var tri = triangles[t];
                int slot = tri.w % SlotSpan, owner = tri.w / SlotSpan - 1;
                if (owner < 0) continue;
                for (int e = 0; e < 3; e++)
                {
                    int v = e == 0 ? tri.x : e == 1 ? tri.y : tri.z;
                    var pair = pairs[v];
                    if (pair.x < 0) pair.xy = new int2(owner, slot);
                    else if (pair.z < 0 && pair.y != slot) pair.zw = new int2(owner, slot);
                    pairs[v] = pair;
                }
            }
            new SnapJob { Prims = prims, Near = near, NearCount = nearCount, Block = block, Pairs = pairs, Positions = positions, Cell = cell }
                .Schedule(count, 64).Complete();
            pairs.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SnapJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<int> Near, NearCount, Block;
            [ReadOnly] public NativeArray<int4> Pairs;
            public NativeArray<float3> Positions;
            public float Cell;

            public void Execute(int v)
            {
                var pair = Pairs[v];
                if (pair.z < 0) return;
                var start = Positions[v];
                var p = start;
                var a = Prims[pair.x];
                var b = Prims[pair.z];
                bool alongA = SdfMath.Paint(a, p) == SdfMath.Paint(b, p) || pair.x == pair.z;
                if (alongA && (a.PaintsAlong || b.PaintsAlong))
                {
                    // Binnen één kleurlijn: naar het dichtstbijzijnde breekpunt.
                    var rule = a.PaintsAlong ? a : b;
                    float t = SdfMath.Along(rule, p), best = 9f;
                    for (int i = 0; i < 4; i++)
                        if (rule.Breaks[i] < 8f && math.abs(rule.Breaks[i] - t) < math.abs(best - t)) best = rule.Breaks[i];
                    if (best > 8f) return;
                    var axis = rule.PaintB - rule.PaintA;
                    p += math.normalize(axis) * ((best - t) * math.length(axis));
                }
                else if (!alongA)
                {
                    // Tussen twee onderdelen: over het oppervlak naar waar ze even ver zijn.
                    int offset = Block[v] * MaxNear, n = NearCount[Block[v]];
                    for (int step = 0; step < 3; step++)
                    {
                        float eps = Cell * 0.25f;
                        float f = Difference(a, b, p);
                        var df = new float3(
                            Difference(a, b, p + new float3(eps, 0f, 0f)) - Difference(a, b, p - new float3(eps, 0f, 0f)),
                            Difference(a, b, p + new float3(0f, eps, 0f)) - Difference(a, b, p - new float3(0f, eps, 0f)),
                            Difference(a, b, p + new float3(0f, 0f, eps)) - Difference(a, b, p - new float3(0f, 0f, eps))) / (2f * eps);
                        var normal = math.normalizesafe(SdfMath.Gradient(Prims, Near, offset, n, p, eps));
                        var tangent = df - normal * math.dot(df, normal);
                        float t2 = math.lengthsq(tangent);
                        if (t2 < 1e-6f) return;
                        p -= tangent * (f / t2);
                    }
                }
                else return;
                if (math.distance(p, start) > Cell * 1.5f) return;

                // Terug op het oppervlak.
                {
                    int offset = Block[v] * MaxNear, n = NearCount[Block[v]];
                    var g = SdfMath.Gradient(Prims, Near, offset, n, p, Cell * 0.25f);
                    float g2 = math.lengthsq(g);
                    if (g2 > 1e-8f) p -= g * (SdfMath.Evaluate(Prims, Near, offset, n, p, out _) / g2);
                }
                if (math.distance(p, start) <= Cell * 1.5f) Positions[v] = p;
            }

            // Nul op de grens: twee onderdelen even dichtbij, of een uitsnede die precies het oppervlak raakt.
            static float Difference(SdfPrim a, SdfPrim b, float3 p) => Side(a, p) - Side(b, p);

            static float Side(SdfPrim prim, float3 p) => prim.Subtract != 0 ? -SdfMath.Single(prim, p) : SdfMath.Single(prim, p);
        }

        /// <summary>Normaal van het veld op elk (gladgestreken) hoekpunt.</summary>
        [BurstCompile(CompileSynchronously = true)]
        struct NormalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<int> Near, NearCount, Block;
            [ReadOnly] public NativeArray<float3> Positions;
            public NativeArray<float3> Normals;
            public float Eps;

            public void Execute(int v)
            {
                int a = Block[v];
                var g = SdfMath.Gradient(Prims, Near, a * MaxNear, NearCount[a], Positions[v], Eps);
                Normals[v] = math.normalizesafe(g, Normals[v]);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SmoothJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> From;
            [WriteOnly] public NativeArray<float3> To;
            [ReadOnly] public NativeArray<int> Start, Neighbours;
            public float Factor;

            public void Execute(int v)
            {
                int a = Start[v], b = Start[v + 1];
                if (b == a) { To[v] = From[v]; return; }
                var sum = float3.zero;
                for (int i = a; i < b; i++) sum += From[Neighbours[i]];
                To[v] = From[v] + Factor * (sum / (b - a) - From[v]);
            }
        }

        struct BlockSetup
        {
            public float3 Origin;
            public int3 Grid;
            public float Cell, BlockSize, HalfDiagonal;

            public int3 Coords(int n) => new int3(n / (Grid.y * Grid.z), n / Grid.z % Grid.y, n % Grid.z);
        }

        static int Index(int x, int y, int z) => (x * (B + 1) + y) * (B + 1) + z;

        [BurstCompile(CompileSynchronously = true)]
        struct CoarseJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            public NativeArray<float> Distance;
            public float3 Origin;
            public int3 Grid;
            public float BlockSize;

            public void Execute(int n)
            {
                var c = new int3(n / (Grid.y * Grid.z), n / Grid.z % Grid.y, n % Grid.z);
                var center = Origin + ((float3)c + 0.5f) * BlockSize;
                Distance[n] = math.abs(SdfMath.Evaluate(Prims, default, 0, -1, center, out _));
            }
        }

        /// <summary>Per blok de onderdelen die er invloed kunnen hebben.</summary>
        [BurstCompile(CompileSynchronously = true)]
        struct NearJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<int> Active;
            [ReadOnly] public NativeArray<float> CenterDistance;
            [NativeDisableParallelForRestriction] public NativeArray<int> Near;
            public NativeArray<int> NearCount;
            public BlockSetup Setup;

            public void Execute(int a)
            {
                int n = Active[a];
                var min = Setup.Origin + (float3)Setup.Coords(n) * Setup.BlockSize - 2f * Setup.Cell;
                var max = min + Setup.BlockSize + 4f * Setup.Cell;
                float reach = CenterDistance[n] + Setup.HalfDiagonal * 1.3f + 3f * Setup.Cell;
                int count = 0;
                for (int i = 0; i < Prims.Length; i++)
                {
                    var p = Prims[i];
                    if (SdfMath.BoxToBox(min, max, p.Min, p.Max) > reach + p.Blend) continue;
                    if (count == MaxNear) { count = -1; break; }
                    Near[a * MaxNear + count++] = i;
                }
                NearCount[a] = count;
            }
        }

        /// <summary>Roosterwaarden van een blok en een hoekpunt per cel waar het teken wisselt.</summary>
        [BurstCompile(CompileSynchronously = true)]
        struct VertexJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<int> Active, Near, NearCount;
            [NativeDisableParallelForRestriction] public NativeArray<float> Values;
            [NativeDisableParallelForRestriction] public NativeArray<float3> Position, Normal;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Has;
            public BlockSetup Setup;

            public void Execute(int a)
            {
                var block = Setup.Coords(Active[a]);
                var min = Setup.Origin + (float3)block * Setup.BlockSize;
                int offset = a * MaxNear, count = NearCount[a], v0 = a * Points;
                float cell = Setup.Cell;
                for (int x = 0; x <= B; x++)
                    for (int y = 0; y <= B; y++)
                        for (int z = 0; z <= B; z++)
                            Values[v0 + Index(x, y, z)] = SdfMath.Evaluate(Prims, Near, offset, count, min + new float3(x, y, z) * cell, out _);

                for (int x = 0; x < B; x++)
                    for (int y = 0; y < B; y++)
                        for (int z = 0; z < B; z++)
                        {
                            int c0 = a * Cells + (x * B + y) * B + z;
                            int inside = 0;
                            for (int c = 0; c < 8; c++)
                                if (Values[v0 + Index(x + (c & 1), y + ((c >> 1) & 1), z + ((c >> 2) & 1))] < 0f) inside++;
                            if (inside == 0 || inside == 8) { Has[c0] = 0; continue; }

                            // Gemiddelde van de snijpunten op de ribben van de cel.
                            var sum = float3.zero;
                            int crossings = 0;
                            for (int c = 0; c < 8; c++)
                                for (int axis = 0; axis < 3; axis++)
                                {
                                    int m = c | (1 << axis);
                                    if (m == c) continue;
                                    var pa = new int3(c & 1, (c >> 1) & 1, (c >> 2) & 1);
                                    var pb = new int3(m & 1, (m >> 1) & 1, (m >> 2) & 1);
                                    float va = Values[v0 + Index(x + pa.x, y + pa.y, z + pa.z)];
                                    float vb = Values[v0 + Index(x + pb.x, y + pb.y, z + pb.z)];
                                    if ((va < 0f) == (vb < 0f)) continue;
                                    sum += math.lerp((float3)pa, (float3)pb, va / (va - vb));
                                    crossings++;
                                }
                            var cellOrigin = min + new float3(x, y, z) * cell;
                            var p = cellOrigin + sum / crossings * cell;
                            // Een Newton-stap naar het oppervlak, binnen de cel.
                            var g = SdfMath.Gradient(Prims, Near, offset, count, p, cell * 0.3f);
                            float g2 = math.lengthsq(g);
                            if (g2 > 1e-8f)
                            {
                                p -= g * (SdfMath.Evaluate(Prims, Near, offset, count, p, out _) / g2);
                                p = math.clamp(p, cellOrigin - cell * 0.25f, cellOrigin + cell * 1.25f);
                            }
                            Position[c0] = p;
                            Normal[c0] = math.normalizesafe(g, new float3(0f, 1f, 0f));
                            Has[c0] = 1;
                        }
            }
        }

        /// <summary>Twee driehoeken per ribbe met een tekenwissel, tussen de vier cellen eromheen.</summary>
        [BurstCompile(CompileSynchronously = true)]
        struct QuadJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<int> Active, BlockMap, Near, NearCount, CellIndex;
            [ReadOnly] public NativeArray<float> Values;
            [ReadOnly] public NativeArray<float3> Positions, Normals;
            public NativeList<int4>.ParallelWriter Triangles;
            public BlockSetup Setup;

            public void Execute(int a)
            {
                var block = Setup.Coords(Active[a]);
                int v0 = a * Points;
                for (int x = 0; x < B; x++)
                    for (int y = 0; y < B; y++)
                        for (int z = 0; z < B; z++)
                        {
                            float value = Values[v0 + Index(x, y, z)];
                            var g = block * B + new int3(x, y, z);
                            for (int axis = 0; axis < 3; axis++)
                            {
                                var step = axis == 0 ? new int3(1, 0, 0) : axis == 1 ? new int3(0, 1, 0) : new int3(0, 0, 1);
                                float other = Values[v0 + Index(x + step.x, y + step.y, z + step.z)];
                                if ((value < 0f) == (other < 0f)) continue;
                                // De vier cellen rond de ribbe, rondom op volgorde.
                                var u = axis == 0 ? new int3(0, 1, 0) : axis == 1 ? new int3(0, 0, 1) : new int3(1, 0, 0);
                                var w = axis == 0 ? new int3(0, 0, 1) : axis == 1 ? new int3(1, 0, 0) : new int3(0, 1, 0);
                                int q0 = Vertex(g - u - w), q1 = Vertex(g - w), q2 = Vertex(g), q3 = Vertex(g - u);
                                if (q0 < 0 || q1 < 0 || q2 < 0 || q3 < 0) continue;
                                // Langs de kortste diagonaal splitsen.
                                if (math.distancesq(Positions[q0], Positions[q2]) <= math.distancesq(Positions[q1], Positions[q3]))
                                {
                                    Add(a, q0, q1, q2);
                                    Add(a, q0, q2, q3);
                                }
                                else
                                {
                                    Add(a, q0, q1, q3);
                                    Add(a, q1, q2, q3);
                                }
                            }
                        }
            }

            int Vertex(int3 cell)
            {
                if (math.any(cell < 0)) return -1;
                var block = cell / B;
                if (math.any(block >= Setup.Grid)) return -1;
                int slot = BlockMap[(block.x * Setup.Grid.y + block.y) * Setup.Grid.z + block.z];
                if (slot < 0) return -1;
                var local = cell - block * B;
                return CellIndex[slot * Cells + (local.x * B + local.y) * B + local.z];
            }

            void Add(int a, int i, int j, int k)
            {
                // Draairichting volgens de richting van het veld; kleur van het onderdeel dat het midden bepaalt.
                var face = math.cross(Positions[j] - Positions[i], Positions[k] - Positions[i]);
                if (math.dot(face, Normals[i] + Normals[j] + Normals[k]) < 0f) { int swap = j; j = k; k = swap; }
                var centroid = (Positions[i] + Positions[j] + Positions[k]) / 3f;
                SdfMath.Evaluate(Prims, Near, a * MaxNear, NearCount[a], centroid, out int dominant);
                int slot = dominant >= 0 ? SdfMath.Paint(Prims[dominant], centroid) : 0;
                Triangles.AddNoResize(new int4(i, j, k, (dominant + 1) * SlotSpan + slot));
            }
        }

        /// <summary>
        /// Huidgewichten: elk hoekpunt hoort vooral bij het bot waarvan de onderdelen het dichtst bij liggen, met een
        /// zachte overgang (zoals bij knie en heup), zodat de huid daar mee buigt in plaats van te knikken.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        struct WeightJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SdfPrim> Prims;
            [ReadOnly] public NativeArray<float3> Positions;
            public int Bones;
            public float Falloff;
            public NativeArray<int2> Bone;
            public NativeArray<float2> Weight;

            public void Execute(int v)
            {
                var p = Positions[v];
                // Kleinste afstand per bot (hooguit acht botten).
                var d0 = new float4(1e9f);
                var d1 = new float4(1e9f);
                for (int i = 0; i < Prims.Length; i++)
                {
                    var prim = Prims[i];
                    if (prim.Subtract != 0 || prim.Bone >= Bones || prim.Bone >= 8) continue;
                    float current = prim.Bone < 4 ? d0[prim.Bone] : d1[prim.Bone - 4];
                    if (SdfMath.BoxDistance(p, prim.Min, prim.Max) >= current) continue;
                    float d = SdfMath.Single(prim, p);
                    if (d >= current) continue;
                    if (prim.Bone < 4) d0[prim.Bone] = d; else d1[prim.Bone - 4] = d;
                }
                float nearest = math.min(math.cmin(d0), math.cmin(d1));
                int first = -1, second = -1;
                float w1 = 0f, w2 = 0f;
                for (int b = 0; b < Bones && b < 8; b++)
                {
                    float w = math.exp(-((b < 4 ? d0[b] : d1[b - 4]) - nearest) / Falloff);
                    if (w > w1) { second = first; w2 = w1; first = b; w1 = w; }
                    else if (w > w2) { second = b; w2 = w; }
                }
                if (second < 0 || w2 < 0.02f * w1) { second = 0; w2 = 0f; }
                Bone[v] = new int2(first, second);
                Weight[v] = new float2(w1, w2) / (w1 + w2);
            }
        }
    }
}
