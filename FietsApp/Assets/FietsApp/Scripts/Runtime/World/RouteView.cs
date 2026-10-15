using System.Collections.Generic;
using FietsApp.World;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Bouwt de weg als mesh langs de route (story 3.1b): asfalt, berm en een talud naar beneden,
    /// zodat de weg tegen een berg lijkt te liggen. Echt terrein volgt in story 3.2.
    /// </summary>
    public sealed class RouteView : MonoBehaviour
    {
        [SerializeField] Material roadMaterial;
        [SerializeField] Material vergeMaterial;
        [SerializeField] Material slopeMaterial;
        [SerializeField, Min(2f)] float roadWidth = 6f;
        [SerializeField, Min(0f)] float vergeWidth = 2.5f;
        [SerializeField, Min(0f)] float slopeWidth = 25f;
        [SerializeField, Min(0f)] float slopeDrop = 18f;

        public Route Route { get; private set; }

        void Awake()
        {
            // Tot er GPX-routes zijn (story 3.1d) rijden we de oefenklim.
            Route = TestRoutes.Testklim();
            Build();
        }

        void Build()
        {
            var pts = Route.Points;
            float half = roadWidth / 2f;

            // Dwarsprofiel per routepunt, van links naar rechts: (afstand uit het midden, hoogteverschil).
            var road = new[] { new Vector2(-half, 0f), new Vector2(half, 0f) };
            var verges = new[]
            {
                new Vector2(-half - vergeWidth, -0.15f), new Vector2(-half, 0f),
                new Vector2(half, 0f), new Vector2(half + vergeWidth, -0.15f),
            };
            var slopes = new[]
            {
                new Vector2(-half - vergeWidth - slopeWidth, -slopeDrop), new Vector2(-half - vergeWidth, -0.15f),
                new Vector2(half + vergeWidth, -0.15f), new Vector2(half + vergeWidth + slopeWidth, -slopeDrop),
            };

            AddStrip("Weg", road, roadMaterial, pts);
            AddStrip("Berm", verges, vergeMaterial, pts);
            AddStrip("Talud", slopes, slopeMaterial, pts);
        }

        /// <summary>
        /// Maakt een strook langs de route. Het profiel bestaat uit paren punten (links, rechts);
        /// elk paar wordt een eigen band, zodat berm en talud aan beide kanten in één mesh passen.
        /// </summary>
        void AddStrip(string name, Vector2[] profile, Material material, IReadOnlyList<RoutePoint> pts)
        {
            var vertices = new List<Vector3>(pts.Count * profile.Length);
            var uvs = new List<Vector2>(pts.Count * profile.Length);
            var triangles = new List<int>();

            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 center = new Vector3(pts[i].X, pts[i].Elevation, pts[i].Z);
                Vector3 right = RightAt(pts, i);
                foreach (var p in profile)
                {
                    vertices.Add(center + right * p.x + Vector3.up * p.y);
                    uvs.Add(new Vector2(p.x, pts[i].Distance));
                }
            }

            int stride = profile.Length;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                for (int band = 0; band < stride; band += 2)
                {
                    int a = i * stride + band, b = a + 1, c = a + stride, d = b + stride;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>Rechtervector op punt i, uit de richting tussen buurpunten (vloeiend in bochten).</summary>
        static Vector3 RightAt(IReadOnlyList<RoutePoint> pts, int i)
        {
            var prev = pts[Mathf.Max(0, i - 1)];
            var next = pts[Mathf.Min(pts.Count - 1, i + 1)];
            var forward = new Vector3(next.X - prev.X, 0f, next.Z - prev.Z).normalized;
            return new Vector3(forward.z, 0f, -forward.x);
        }
    }
}
