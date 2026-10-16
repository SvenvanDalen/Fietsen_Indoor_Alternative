using System.Collections.Generic;
using FietsApp.World;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Bouwt de weg als mesh langs de route (story 3.1b): asfalt en berm. Het terrein eromheen
    /// komt van <see cref="TerrainView"/> (story 3.2). Ook zichtbaar in de bewerkmodus, zonder in de scène
    /// te worden opgeslagen.
    /// </summary>
    [ExecuteAlways]
    public sealed class RouteView : MonoBehaviour
    {
        [SerializeField] Material roadMaterial;
        [SerializeField] Material vergeMaterial;
        [Tooltip("Berm boven de boomgrens: grind in plaats van gras.")]
        [SerializeField] Material bareVergeMaterial;
        [SerializeField, Min(2f)] float roadWidth = 6f;
        [SerializeField, Min(0f)] float vergeWidth = 2.5f;

        [Tooltip("De klim (Assets/FietsApp/Routes). Leeg = de oefenklim.")]
        [SerializeField] Climb climb;

        readonly GeneratedContent _generated = new GeneratedContent();
        Route _route;
        IHeightSource _naturalHeights;

        // Lui aangemaakt, zodat andere componenten de route kunnen opvragen ongeacht in welke volgorde OnEnable draait.
        public Route Route => _route ?? (_route = climb != null && climb.gpx != null
            ? ClimbImport.FromGpx(Gpx.Parse(climb.gpx.text))
            : TestRoutes.Testklim());

        /// <summary>De gekozen klim, of null voor de oefenklim.</summary>
        public Climb Climb => climb;

        /// <summary>Echt landschap in de lokale meters van de route, of null als er geen is.</summary>
        public IHeightSource NaturalHeights =>
            _naturalHeights ?? (_naturalHeights = climb != null && climb.terrain != null && Route.Projection != null
                ? new LocalHeights(GeoHeightField.Parse(climb.terrain.bytes), Route.Projection)
                : null);

        void OnEnable()
        {
            // Opnieuw inlezen, voor als de GPX of het landschap in de inspector is gewisseld.
            _route = null;
            _naturalHeights = null;
            Build();
        }

        void OnDisable() => _generated.DestroyAll();

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

            AddStrip("Weg", road, new[] { roadMaterial }, pts, i => 0);
            float gravelFrom = climb != null && climb.gravelVergeAboveTreeLine ? climb.treeLine : float.MaxValue;
            AddStrip("Berm", verges, new[] { vergeMaterial, bareVergeMaterial ?? vergeMaterial }, pts,
                i => pts[i].Elevation > gravelFrom ? 1 : 0);
        }

        /// <summary>
        /// Maakt een strook langs de route. Het profiel bestaat uit paren punten (links, rechts);
        /// elk paar wordt een eigen band, zodat de berm aan beide kanten in één mesh past.
        /// <paramref name="submeshOf"/> kiest per routesegment het materiaal.
        /// </summary>
        void AddStrip(string name, Vector2[] profile, Material[] materials, IReadOnlyList<RoutePoint> pts,
            System.Func<int, int> submeshOf)
        {
            var vertices = new List<Vector3>(pts.Count * profile.Length);
            var uvs = new List<Vector2>(pts.Count * profile.Length);
            var triangles = new List<int>[materials.Length];
            for (int m = 0; m < materials.Length; m++) triangles[m] = new List<int>();

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
                    triangles[submeshOf(i)].AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = _generated.Track(new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = materials.Length;
            for (int m = 0; m < materials.Length; m++) mesh.SetTriangles(triangles[m], m);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = _generated.NewObject(name, transform, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
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
