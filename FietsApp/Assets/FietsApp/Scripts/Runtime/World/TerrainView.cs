using System.Collections.Generic;
using FietsApp.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace FietsApp
{
    /// <summary>
    /// Bouwt terrein, bomen en rotsen rond de route (story 3.2) in low-poly-stijl: platte driehoeken,
    /// gras op vlakke stukken en rots op steile hellingen. Alles in stukken (chunks), zodat Unity
    /// wat buiten beeld valt niet tekent.
    /// </summary>
    public sealed class TerrainView : MonoBehaviour
    {
        [SerializeField] RouteView routeView;
        [SerializeField] Material grassMaterial;
        [SerializeField] Material darkGrassMaterial;
        [SerializeField] Material rockMaterial;
        [SerializeField] Material trunkMaterial;
        [SerializeField] Material leavesMaterial;
        [SerializeField] Material stoneMaterial;
        [SerializeField] int seed = 42;

        const int ChunkCells = 48;
        const float RockSlopeNormalY = 0.8f; // steiler dan ~37° wordt rots

        readonly TerrainSettings _settings = new TerrainSettings();

        void Start()
        {
            // RouteView maakt de route in Awake.
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            _settings.Seed = seed;

            float t0 = Time.realtimeSinceStartup;
            var grid = TerrainHeights.Generate(routeView.Route, _settings);
            var items = Scatter.Place(routeView.Route, grid, _settings);
            float t1 = Time.realtimeSinceStartup;

            BuildTerrain(grid);
            BuildScatter(grid, items);
            Debug.Log($"Terrein: {grid.CountX}×{grid.CountZ} punten, {items.Count} bomen en rotsen; " +
                      $"berekenen {(t1 - t0) * 1000f:0} ms, meshes {(Time.realtimeSinceStartup - t1) * 1000f:0} ms");
        }

        void BuildTerrain(HeightGrid grid)
        {
            var root = new GameObject("Terrein").transform;
            root.SetParent(transform, false);
            var materials = new[] { grassMaterial, darkGrassMaterial, rockMaterial };

            for (int cz = 0; cz < grid.CountZ - 1; cz += ChunkCells)
            for (int cx = 0; cx < grid.CountX - 1; cx += ChunkCells)
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };

                int maxX = Mathf.Min(cx + ChunkCells, grid.CountX - 1), maxZ = Mathf.Min(cz + ChunkCells, grid.CountZ - 1);
                for (int z = cz; z < maxZ; z++)
                for (int x = cx; x < maxX; x++)
                {
                    Vector3 a = Corner(grid, x, z), b = Corner(grid, x, z + 1), c = Corner(grid, x + 1, z + 1), d = Corner(grid, x + 1, z);
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

                var mesh = new Mesh { name = $"Terrein {cx},{cz}", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                AddRenderer(root, mesh.name, mesh, materials);
            }
        }

        static Vector3 Corner(HeightGrid grid, int x, int z) =>
            new Vector3(grid.MinX + x * grid.CellSize, grid[x, z], grid.MinZ + z * grid.CellSize);

        /// <summary>Driehoek met eigen hoekpunten (platte belichting) in de submesh die bij de helling past.</summary>
        static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int x, int z, int half,
            List<Vector3> vertices, List<Vector3> normals, List<int>[] triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int submesh;
            if (normal.y < RockSlopeNormalY) submesh = 2;
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
            var root = new GameObject("Begroeiing").transform;
            root.SetParent(transform, false);

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
                    if (item.Kind == ScatterKind.Tree) AddTree(trees, pose);
                    else AddRock(rocks, pose);
                }
                if (trees.HasGeometry)
                    AddRenderer(root, $"Bomen {group.Key}", trees.ToMesh($"Bomen {group.Key}"), new[] { trunkMaterial, leavesMaterial });
                if (rocks.HasGeometry)
                    AddRenderer(root, $"Rotsen {group.Key}", rocks.ToMesh($"Rotsen {group.Key}"), new[] { stoneMaterial });
            }
        }

        /// <summary>Low-poly den: zeskantige stam en twee kegels naalden, ongeveer 8 m hoog bij schaal 1.</summary>
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

        static void AddRenderer(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
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
