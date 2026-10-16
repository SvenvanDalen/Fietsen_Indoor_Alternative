using System.Collections.Generic;
using FietsApp.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace FietsApp
{
    /// <summary>
    /// Bouwt terrein, bomen en rotsen rond de route (story 3.2) in low-poly-stijl: platte driehoeken,
    /// gras op vlakke stukken en rots op steile hellingen. Alles in stukken (chunks), zodat Unity
    /// wat buiten beeld valt niet tekent. Ook zichtbaar in de bewerkmodus, zonder in de scène te worden opgeslagen.
    /// </summary>
    [ExecuteAlways]
    public sealed class TerrainView : MonoBehaviour
    {
        [SerializeField] RouteView routeView;
        [SerializeField] Material grassMaterial;
        [SerializeField] Material darkGrassMaterial;
        [SerializeField] Material rockMaterial;
        [SerializeField] Material trunkMaterial;
        [SerializeField] Material leavesMaterial;
        [SerializeField] Material stoneMaterial;
        [Tooltip("Kale grond boven de boomgrens, zoals de witte kalksteen van de Mont Ventoux.")]
        [SerializeField] Material bareMaterial;
        [Tooltip("Bosbodem: van gezien op afstand donker bos, zonder dat er overal bomen hoeven te staan.")]
        [SerializeField] Material forestMaterial;
        [SerializeField] int seed = 42;

        const int ChunkCells = 32;
        const float FarChunkDistance = 200f; // m van de weg; verder weg grovere driehoeken
        const float DetailedTreeDistance = 120f;
        const int FarStep = 4;
        const float SkirtDepth = 25f;
        const float RockSlopeNormalY = 0.8f; // steiler dan ~37° wordt rots

        readonly TerrainSettings _settings = new TerrainSettings();
        Material _aboveTreeLine;
        readonly GeneratedContent _generated = new GeneratedContent();
        Biome _biome;

        void OnDisable() => _generated.DestroyAll();

        void OnEnable()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (routeView == null) return;
            _settings.Seed = seed;
            // Boomgrens en bos komen van de klim; de oefenklim heeft overal gras.
            var climb = routeView.Climb;
            _settings.TreeLine = climb != null ? climb.treeLine : float.MaxValue;
            _settings.ForestFrom = climb != null ? climb.forestFrom : float.MaxValue;
            _aboveTreeLine = climb != null && climb.aboveTreeLine != null ? climb.aboveTreeLine : bareMaterial;
            _biome = new Biome(_settings);

            float t0 = Time.realtimeSinceStartup;
            var grid = TerrainHeights.Generate(routeView.Route, _settings, routeView.NaturalHeights);
            var items = Scatter.Place(routeView.Route, grid, _settings);
            float t1 = Time.realtimeSinceStartup;

            BuildTerrain(grid);
            BuildScatter(grid, items);
            Debug.Log($"Terrein: {grid.CountX}×{grid.CountZ} punten, {items.Count} bomen en rotsen; " +
                      $"berekenen {(t1 - t0) * 1000f:0} ms, meshes {(Time.realtimeSinceStartup - t1) * 1000f:0} ms");
        }

        void BuildTerrain(HeightGrid grid)
        {
            var root = _generated.NewObject("Terrein", transform).transform;
            var materials = new[] { grassMaterial, darkGrassMaterial, rockMaterial, _aboveTreeLine, forestMaterial };

            for (int cz = 0; cz < grid.CountZ - 1; cz += ChunkCells)
            for (int cx = 0; cx < grid.CountX - 1; cx += ChunkCells)
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };

                int maxX = Mathf.Min(cx + ChunkCells, grid.CountX - 1), maxZ = Mathf.Min(cz + ChunkCells, grid.CountZ - 1);

                // Ver van de weg ziet niemand detail: daar driehoeken van FarStep cellen.
                float nearest = float.MaxValue;
                for (int z = cz; z <= maxZ; z++)
                for (int x = cx; x <= maxX; x++)
                    nearest = Mathf.Min(nearest, grid.RoadDistance(x, z));
                int step = nearest > FarChunkDistance ? FarStep : 1;

                for (int z = cz; z < maxZ; z += step)
                for (int x = cx; x < maxX; x += step)
                {
                    int x2 = Mathf.Min(x + step, maxX), z2 = Mathf.Min(z + step, maxZ);
                    Vector3 a = Corner(grid, x, z), b = Corner(grid, x, z2), c = Corner(grid, x2, z2), d = Corner(grid, x2, z);
                    // Diagonaal afwisselen geeft een minder streperig patroon.
                    if (((x + z) & 1) == 0)
                    {
                        AddTriangle(a, b, c, x, z, 0, vertices, normals, triangles);
                        AddTriangle(a, c, d, x, z, 1, vertices, normals, triangles);
                    }
                    else
                    {
                        AddTriangle(a, b, d, x, z, 0, vertices, normals, triangles);
                        AddTriangle(b, c, d, x, z, 1, vertices, normals, triangles);
                    }
                }
                AddSkirts(grid, cx, cz, maxX, maxZ, step, vertices, normals, triangles[2]);

                var mesh = _generated.Track(new Mesh { name = $"Terrein {cx},{cz}", indexFormat = IndexFormat.UInt32 });
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                AddRenderer(root, mesh.name, mesh, materials);
            }
        }

        /// <summary>
        /// Verticale rand ("rok") langs de vier zijden van een stuk, naar beneden. Waar een grof en een fijn stuk
        /// aan elkaar grenzen kloppen de randhoogtes niet precies; de rok verbergt de naad. Beide kanten zichtbaar.
        /// </summary>
        static void AddSkirts(HeightGrid grid, int cx, int cz, int maxX, int maxZ, int step,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            void Edge(int x0, int z0, int x1, int z1)
            {
                Vector3 p = Corner(grid, x0, z0), q = Corner(grid, x1, z1);
                Vector3 pd = p + Vector3.down * SkirtDepth, qd = q + Vector3.down * SkirtDepth;
                Vector3 n = Vector3.Cross(q - p, Vector3.down).normalized;
                foreach (var (v0, v1, v2, normal) in new[] { (p, q, qd, n), (p, qd, pd, n), (p, qd, q, -n), (p, pd, qd, -n) })
                {
                    int i = vertices.Count;
                    vertices.Add(v0); vertices.Add(v1); vertices.Add(v2);
                    normals.Add(normal); normals.Add(normal); normals.Add(normal);
                    triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                }
            }

            for (int x = cx; x < maxX; x += step)
            {
                int x2 = Mathf.Min(x + step, maxX);
                Edge(x, cz, x2, cz);
                Edge(x, maxZ, x2, maxZ);
            }
            for (int z = cz; z < maxZ; z += step)
            {
                int z2 = Mathf.Min(z + step, maxZ);
                Edge(cx, z, cx, z2);
                Edge(maxX, z, maxX, z2);
            }
        }

        static Vector3 Corner(HeightGrid grid, int x, int z) =>
            new Vector3(grid.MinX + x * grid.CellSize, grid[x, z], grid.MinZ + z * grid.CellSize);

        /// <summary>
        /// Driehoek met eigen hoekpunten (platte belichting) in de submesh die erbij past:
        /// 0 gras, 1 donker gras, 2 rots (steil), 3 kale grond (boven de boomgrens), 4 bosbodem.
        /// </summary>
        void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int x, int z, int half,
            List<Vector3> vertices, List<Vector3> normals, List<int>[] triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vector3 center = (a + b + c) / 3f;
            int submesh;
            if (normal.y < RockSlopeNormalY) submesh = 2;
            else if (_biome.IsAboveTreeLine(center.x, center.z, center.y)) submesh = 3;
            else if (_biome.IsForest(center.x, center.z, center.y)) submesh = 4;
            else submesh = (Hash(x, z, half) % 100) < 22 ? 1 : 0; // plekken donker gras

            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            triangles[submesh].Add(i);
            triangles[submesh].Add(i + 1);
            triangles[submesh].Add(i + 2);
        }

        void BuildScatter(HeightGrid grid, List<ScatterItem> items)
        {
            var root = _generated.NewObject("Begroeiing", transform).transform;

            // Per chunk één mesh voor bomen (stam + naalden) en één voor rotsen.
            float chunkSize = ChunkCells * grid.CellSize;
            var groups = new Dictionary<Vector2Int, List<ScatterItem>>();
            foreach (var item in items)
            {
                var key = new Vector2Int(Mathf.FloorToInt((item.X - grid.MinX) / chunkSize), Mathf.FloorToInt((item.Z - grid.MinZ) / chunkSize));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<ScatterItem>();
                list.Add(item);
            }

            foreach (var group in groups)
            {
                var trees = new MeshBuilder(2);
                var rocks = new MeshBuilder(1);
                foreach (var item in group.Value)
                {
                    var pose = Matrix4x4.TRS(new Vector3(item.X, item.Y, item.Z),
                        Quaternion.Euler(0f, item.RotationDegrees, 0f), Vector3.one * item.Scale);
                    if (item.Kind == ScatterKind.Tree)
                    {
                        if (item.RoadDistance < DetailedTreeDistance) AddTree(trees, pose);
                        else AddSimpleTree(trees, pose);
                    }
                    else AddRock(rocks, pose);
                }
                if (trees.HasGeometry)
                    AddRenderer(root, $"Bomen {group.Key}", _generated.Track(trees.ToMesh($"Bomen {group.Key}")), new[] { trunkMaterial, leavesMaterial });
                if (rocks.HasGeometry)
                    AddRenderer(root, $"Rotsen {group.Key}", _generated.Track(rocks.ToMesh($"Rotsen {group.Key}")), new[] { stoneMaterial });
            }
        }

        /// <summary>Low-poly den: zeskantige stam en twee kegels naalden, ongeveer 8 m hoog bij schaal 1.</summary>
        /// <summary>Verre den: één kegel zonder stam, ruim drie keer minder hoekpunten.</summary>
        static void AddSimpleTree(MeshBuilder b, Matrix4x4 pose) =>
            b.Cone(1, pose, radius: 2.2f, bottom: 0.5f, top: 8.5f, sides: 5);

        static void AddTree(MeshBuilder b, Matrix4x4 pose)
        {
            b.Prism(0, pose, radius: 0.25f, bottom: -0.5f, top: 2f, sides: 5);
            b.Cone(1, pose, radius: 2.4f, bottom: 1.5f, top: 6f, sides: 7);
            b.Cone(1, pose, radius: 1.7f, bottom: 4f, top: 8.5f, sides: 7);
        }

        /// <summary>Low-poly rots: platgedrukte, scheve achthoek, half in de grond.</summary>
        static void AddRock(MeshBuilder b, Matrix4x4 pose)
        {
            var shape = pose * Matrix4x4.TRS(new Vector3(0f, 0.3f, 0f), Quaternion.Euler(12f, 0f, 8f), new Vector3(2.4f, 1.4f, 1.8f));
            b.Octahedron(0, shape);
        }

        void AddRenderer(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = _generated.NewObject(name, parent, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        static uint Hash(int x, int z, int salt)
        {
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)salt * 83492791u;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            return h ^ (h >> 15);
        }

        /// <summary>Verzamelt platte driehoeken per submesh. Volgorde met de klok mee, gezien van de buitenkant.</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> _vertices = new List<Vector3>();
            readonly List<Vector3> _normals = new List<Vector3>();
            readonly List<int>[] _triangles;

            public MeshBuilder(int submeshes)
            {
                _triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) _triangles[i] = new List<int>();
            }

            public bool HasGeometry => _vertices.Count > 0;

            void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                int i = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
                _normals.Add(n); _normals.Add(n); _normals.Add(n);
                _triangles[submesh].Add(i);
                _triangles[submesh].Add(i + 1);
                _triangles[submesh].Add(i + 2);
            }

            static Vector3 Ring(float radius, float y, int i, int sides)
            {
                float angle = i * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
            }

            public void Prism(int submesh, Matrix4x4 m, float radius, float bottom, float top, int sides)
            {
                for (int i = 0; i < sides; i++)
                {
                    Vector3 b0 = m.MultiplyPoint3x4(Ring(radius, bottom, i, sides)), b1 = m.MultiplyPoint3x4(Ring(radius, bottom, i + 1, sides));
                    Vector3 t0 = m.MultiplyPoint3x4(Ring(radius, top, i, sides)), t1 = m.MultiplyPoint3x4(Ring(radius, top, i + 1, sides));
                    Triangle(submesh, b0, t1, t0);
                    Triangle(submesh, b0, b1, t1);
                }
            }

            public void Cone(int submesh, Matrix4x4 m, float radius, float bottom, float top, int sides)
            {
                Vector3 tip = m.MultiplyPoint3x4(new Vector3(0f, top, 0f));
                Vector3 center = m.MultiplyPoint3x4(new Vector3(0f, bottom, 0f));
                for (int i = 0; i < sides; i++)
                {
                    Vector3 r0 = m.MultiplyPoint3x4(Ring(radius, bottom, i, sides)), r1 = m.MultiplyPoint3x4(Ring(radius, bottom, i + 1, sides));
                    Triangle(submesh, r0, r1, tip);
                    Triangle(submesh, r0, center, r1); // onderkant
                }
            }

            public void Octahedron(int submesh, Matrix4x4 m)
            {
                Vector3 top = m.MultiplyPoint3x4(Vector3.up), bottom = m.MultiplyPoint3x4(Vector3.down);
                var ring = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = m.MultiplyPoint3x4(ring[i]), b = m.MultiplyPoint3x4(ring[(i + 1) % 4]);
                    Triangle(submesh, a, b, top);
                    Triangle(submesh, a, bottom, b);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++) mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
