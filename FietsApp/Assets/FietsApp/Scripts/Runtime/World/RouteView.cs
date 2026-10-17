using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("Onverharde weg, voor het stuk dat de klim als onverhard aangeeft.")]
        [SerializeField] Material unpavedRoadMaterial;
        [SerializeField] Material vergeMaterial;
        [Tooltip("Berm boven de boomgrens: grind in plaats van gras.")]
        [SerializeField] Material bareVergeMaterial;
        [SerializeField, Min(2f)] float roadWidth = 6f;
        [SerializeField, Min(0f)] float vergeWidth = 2.5f;

        [Tooltip("De klim (Assets/FietsApp/Routes). Leeg = de oefenklim.")]
        [SerializeField] Climb climb;
        [Tooltip("Alle klimmen; samen vormen ze de doorlopende wereld (story 4.11). Leeg = alleen de klim zelf.")]
        [SerializeField] ClimbLibrary library;

        readonly GeneratedContent _generated = new GeneratedContent();
        Route _route, _climbRoute, _ownRoute;
        Journey _journey;
        bool _journeyResolved;
        IHeightSource _naturalHeights;

        static ClimbLibrary _worldFor;
        static WorldMap _world;

        /// <summary>De doorlopende wereld van alle klimmen in de bibliotheek, eenmalig opgebouwd.</summary>
        public WorldMap World
        {
            get
            {
                if (library == null || library.climbs == null || library.climbs.Length == 0) return null;
                if (_worldFor != library || _world == null)
                {
                    _world = WorldMap.Build(library.climbs.Where(c => c != null && c.gpx != null)
                        .Select(c => new WorldClimb(c.name, c.displayName, c.region, ClimbImport.FromGpx(Gpx.Parse(c.gpx.text)))));
                    _worldFor = library;
                }
                return _world;
            }
        }

        /// <summary>De rit door de wereld voor de gekozen klim, of null buiten de wereld (oefenklim, geen bibliotheek).</summary>
        public Journey Journey
        {
            get
            {
                if (_journeyResolved) return _journey;
                _journeyResolved = true;
                var placed = climb != null ? World?.Find(climb.name) : null;
                _journey = placed != null ? World.Journey(placed) : null;
                return _journey;
            }
        }

        /// <summary>De klim zelf in eigen meters en echte hoogte (voor geografie en statistiek).</summary>
        public Route OwnRoute => _ownRoute ?? (_ownRoute = climb != null && climb.gpx != null
            ? ClimbImport.FromGpx(Gpx.Parse(climb.gpx.text))
            : TestRoutes.Testklim());

        // Lui aangemaakt, zodat andere componenten de route kunnen opvragen ongeacht in welke volgorde OnEnable draait.
        /// <summary>Wat er gereden wordt: de rit door de wereld, of anders de klim zelf.</summary>
        public Route Route => _route ?? (_route = Journey?.Route ?? OwnRoute);

        /// <summary>De klim op zijn plek in de wereld (zelfde punten als in <see cref="Route"/>); afstanden vanaf de voet.</summary>
        public Route ClimbRoute => _climbRoute ?? (_climbRoute = Journey != null ? Journey.Climb.WorldRoute() : OwnRoute);

        /// <summary>Afstand langs <see cref="Route"/> waar de klim begint (de voet) en eindigt (de top).</summary>
        public float ClimbStart => Journey != null ? Journey[JourneyPart.Climb].Start : 0f;
        public float ClimbEnd => Journey != null ? Journey[JourneyPart.Climb].End : Route.Length;

        /// <summary>Wereldhoogte = echte hoogte + deze verschuiving (0 buiten de wereld).</summary>
        public float ElevationOffset => Journey != null ? Journey.Climb.Placement.ElevationOffset : 0f;

        public float RealElevation(float worldElevation) => worldElevation - ElevationOffset;

        /// <summary>
        /// Waar je op de klim bent, in meters vanaf de voet, ook tijdens de afdaling over dezelfde weg; null op de
        /// dalweg. Voor alles wat per plek op de klim hoort (onverhard stuk, borden).
        /// </summary>
        public float? ClimbDistanceAt(float distance)
        {
            if (Journey == null) return distance;
            var up = Journey[JourneyPart.Climb];
            var down = Journey[JourneyPart.Descent];
            if (distance >= up.Start && distance <= up.End) return distance - up.Start;
            if (distance > down.Start && distance <= down.End) return up.End - up.Start - (distance - down.Start);
            return null;
        }

        /// <summary>Breedte- en lengtegraad van een plek in de wereld (via de echte ligging van de klim), of null zonder GPX.</summary>
        public (double latitude, double longitude)? ToGeo(float x, float z)
        {
            var projection = OwnRoute.Projection;
            if (projection == null) return null;
            if (Journey == null) return projection.ToGeo(x, z);
            var (lx, lz) = Journey.Climb.Placement.ToLocal(x, z);
            return projection.ToGeo(lx, lz);
        }

        /// <summary>Plek in de wereld van een echte coördinaat (bijv. de top van de Ventoux), of null zonder GPX.</summary>
        public (float x, float z)? FromGeo(double latitude, double longitude)
        {
            var projection = OwnRoute.Projection;
            if (projection == null) return null;
            var (lx, lz) = projection.ToLocal(latitude, longitude);
            return Journey == null ? (lx, lz) : Journey.Climb.Placement.ToWorld(lx, lz);
        }

        /// <summary>De gekozen klim, of null voor de oefenklim.</summary>
        public Climb Climb => climb;

        /// <summary>Na <see cref="SetClimb"/>: de weg is opnieuw gebouwd; terrein en HUD bouwen zich hierop opnieuw op.</summary>
        public event System.Action ClimbChanged;

        /// <summary>Kies een andere klim tijdens het spelen (ritmenu, story 6.3).</summary>
        public void SetClimb(Climb value)
        {
            climb = value;
            _generated.DestroyAll();
            OnEnable();
            ClimbChanged?.Invoke();
        }

        /// <summary>Echt landschap in de lokale meters van de route, of null als er geen is.</summary>
        public IHeightSource NaturalHeights =>
            _naturalHeights ?? (_naturalHeights = climb == null || climb.terrain == null || OwnRoute.Projection == null ? null
                : Journey != null
                    ? new WorldHeights(GeoHeightField.Parse(climb.terrain.bytes), OwnRoute.Projection, Journey.Climb.Placement)
                    : (IHeightSource)new LocalHeights(GeoHeightField.Parse(climb.terrain.bytes), OwnRoute.Projection));

        void OnEnable()
        {
            // Opnieuw inlezen, voor als de GPX of het landschap in de inspector is gewisseld.
            _route = _climbRoute = _ownRoute = null;
            _journey = null;
            _journeyResolved = false;
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

            AddStrip("Weg", road, new[] { roadMaterial, unpavedRoadMaterial ?? roadMaterial }, pts,
                i => climb != null && ClimbDistanceAt(pts[i].Distance) is float d && climb.IsUnpaved(d) ? 1 : 0);
            float gravelFrom = climb != null && climb.gravelVergeAboveTreeLine ? climb.treeLine : float.MaxValue;
            AddStrip("Berm", verges, new[] { vergeMaterial, bareVergeMaterial ?? vergeMaterial }, pts,
                i => RealElevation(pts[i].Elevation) > gravelFrom ? 1 : 0);
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
