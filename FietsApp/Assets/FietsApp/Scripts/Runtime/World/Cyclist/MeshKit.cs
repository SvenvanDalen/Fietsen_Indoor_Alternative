using System.Collections.Generic;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Bouwt een gladde mesh uit eenvoudige vormen (story 7.3): buizen langs een lijn met per ring een eigen dikte,
    /// ellipsoïden en ringen rond een as (banden, velgen). Elke vorm hoort bij een materiaal-slot en wordt een eigen
    /// submesh. Binnen een vorm delen de hoekpunten elkaar, zodat <see cref="Mesh.RecalculateNormals"/> hem rond maakt.
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly Dictionary<int, List<int>> _triangles = new Dictionary<int, List<int>>();

        List<int> Slot(int slot)
        {
            if (!_triangles.TryGetValue(slot, out var list)) _triangles[slot] = list = new List<int>();
            return list;
        }

        /// <summary>
        /// Rooster van <paramref name="rows"/> ringen van <paramref name="sides"/> punten, rondom gesloten; met een
        /// begin- en eindpunt erbij wordt het een dichte vorm (de punten worden de polen).
        /// </summary>
        void Rings(int slot, int rows, int sides, System.Func<int, int, Vector3> point, Vector3? startPole, Vector3? endPole)
        {
            var tris = Slot(slot);
            int first = _vertices.Count;
            for (int r = 0; r < rows; r++)
                for (int s = 0; s < sides; s++)
                    _vertices.Add(point(r, s));
            for (int r = 0; r < rows - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = first + r * sides + s, b = first + r * sides + (s + 1) % sides;
                    int c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            if (startPole.HasValue)
            {
                int pole = _vertices.Count;
                _vertices.Add(startPole.Value);
                for (int s = 0; s < sides; s++) { tris.Add(pole); tris.Add(first + (s + 1) % sides); tris.Add(first + s); }
            }
            if (endPole.HasValue)
            {
                int pole = _vertices.Count, last = first + (rows - 1) * sides;
                _vertices.Add(endPole.Value);
                for (int s = 0; s < sides; s++) { tris.Add(pole); tris.Add(last + s); tris.Add(last + (s + 1) % sides); }
            }
        }

        /// <summary>
        /// Buis langs <paramref name="points"/> met per punt een doorsnede (x = halve breedte opzij, y = halve diepte).
        /// "Opzij" is de richting van <paramref name="side"/> (meestal rechts), loodrecht op de buis gezet; zo worden
        /// aero-buizen smal en diep. De uiteinden krijgen een bolle dop, tenzij <paramref name="open"/>.
        /// </summary>
        public void Tube(int slot, IList<Vector3> points, IList<Vector2> radii, int sides, Vector3 side, bool open = false)
        {
            int n = points.Count;
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
                tangents[i] = (points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;

            // Assenstelsel per ring, doorgeschoven langs de lijn zodat de buis niet draait.
            var us = new Vector3[n];
            var u = Vector3.ProjectOnPlane(side, tangents[0]);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.ProjectOnPlane(Vector3.up, tangents[0]);
            for (int i = 0; i < n; i++)
            {
                u = Vector3.ProjectOnPlane(u, tangents[i]).normalized;
                us[i] = u;
            }

            Vector3 Point(int r, int s)
            {
                float a = s * Mathf.PI * 2f / sides;
                var v = Vector3.Cross(tangents[r], us[r]);
                return points[r] + us[r] * (Mathf.Cos(a) * radii[r].x) + v * (Mathf.Sin(a) * radii[r].y);
            }

            float startCap = Mathf.Min(radii[0].x, radii[0].y) * 0.6f, endCap = Mathf.Min(radii[n - 1].x, radii[n - 1].y) * 0.6f;
            Rings(slot, n, sides, Point,
                open ? (Vector3?)null : points[0] - tangents[0] * startCap,
                open ? (Vector3?)null : points[n - 1] + tangents[n - 1] * endCap);
        }

        /// <summary>Rechte ronde buis van <paramref name="a"/> naar <paramref name="b"/>, dikte verlopend van ra naar rb.</summary>
        public void Tube(int slot, Vector3 a, Vector3 b, float ra, float rb, int sides = 14) =>
            Tube(slot, new[] { a, b }, new[] { new Vector2(ra, ra), new Vector2(rb, rb) }, sides, Vector3.right);

        /// <summary>Rechte buis met een afgeplatte doorsnede (opzij <paramref name="width"/>, langs <paramref name="depth"/>).</summary>
        public void FlatTube(int slot, Vector3 a, Vector3 b, float width, float depth, int sides = 14) =>
            Tube(slot, new[] { a, b }, new[] { new Vector2(width, depth), new Vector2(width, depth) }, sides, Vector3.right);

        /// <summary>
        /// Gladde buis door een paar steunpunten (Catmull-Rom), voor gebogen onderdelen zoals een stuur of een bril.
        /// </summary>
        public void Curve(int slot, IList<Vector3> controls, Vector2 radius, int sides, Vector3 side, int stepsPerSegment = 6)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < controls.Count - 1; i++)
            {
                Vector3 p0 = controls[Mathf.Max(i - 1, 0)], p1 = controls[i], p2 = controls[i + 1], p3 = controls[Mathf.Min(i + 2, controls.Count - 1)];
                for (int k = 0; k < stepsPerSegment; k++)
                {
                    float t = k / (float)stepsPerSegment;
                    points.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
                                       (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t)));
                }
            }
            points.Add(controls[controls.Count - 1]);
            var radii = new Vector2[points.Count];
            for (int i = 0; i < radii.Length; i++) radii[i] = radius;
            Tube(slot, points, radii, sides, side);
        }

        /// <summary>
        /// Ellipsoïde rond <paramref name="center"/>; <paramref name="radii"/> langs de lokale x, y en z van
        /// <paramref name="rotation"/>. Met <paramref name="minLatitude"/> boven -90° wordt het een kap (helm), open aan de onderkant.
        /// </summary>
        public void Ellipsoid(int slot, Vector3 center, Quaternion rotation, Vector3 radii, int rings = 12, int sides = 18,
            float minLatitude = -90f)
        {
            bool cap = minLatitude > -89f;
            float from = cap ? minLatitude : -90f + 180f / rings;
            float to = 90f - 180f / rings;
            Vector3 P(float latitude, float longitude)
            {
                float la = latitude * Mathf.Deg2Rad, lo = longitude * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Cos(la) * Mathf.Sin(lo) * radii.x, Mathf.Sin(la) * radii.y, Mathf.Cos(la) * Mathf.Cos(lo) * radii.z);
                return center + rotation * local;
            }
            int rows = cap ? rings : rings - 1;
            Rings(slot, rows, sides, (r, s) => P(Mathf.Lerp(from, to, r / (float)(rows - 1)), s * 360f / sides),
                cap ? (Vector3?)null : P(-90f, 0f), P(90f, 0f));
        }

        /// <summary>
        /// Draait een gesloten doorsnede rond de x-as door <paramref name="center"/>: elk punt is (straal, positie langs de as).
        /// Zo ontstaan banden, velgen, naven en kettingbladen.
        /// </summary>
        public void Lathe(int slot, Vector3 center, IList<Vector2> profile, int segments)
        {
            int m = profile.Count;
            // Rond de doorsnede (s) en langs de omtrek (r); Rings sluit de doorsnede, de omtrek sluiten we met een extra ring.
            var tris = Slot(slot);
            int first = _vertices.Count;
            for (int r = 0; r < segments; r++)
            {
                float a = r * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                foreach (var p in profile)
                    _vertices.Add(center + new Vector3(p.y, cos * p.x, sin * p.x));
            }
            for (int r = 0; r < segments; r++)
                for (int s = 0; s < m; s++)
                {
                    int a = first + r * m + s, b = first + r * m + (s + 1) % m;
                    int c = first + (r + 1) % segments * m + s, d = first + (r + 1) % segments * m + (s + 1) % m;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
        }

        /// <summary>Band of andere ronde ring: cirkelvormige doorsnede met straal <paramref name="tube"/> op straal <paramref name="radius"/>.</summary>
        public void Torus(int slot, Vector3 center, float radius, float tube, int segments = 48, int sides = 10, float flatten = 1f)
        {
            var profile = new Vector2[sides];
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                profile[s] = new Vector2(radius + Mathf.Cos(a) * tube, Mathf.Sin(a) * tube * flatten);
            }
            Lathe(slot, center, profile, segments);
        }

        public Mesh Build(string name, int slotCount)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.subMeshCount = slotCount;
            for (int i = 0; i < slotCount; i++)
                mesh.SetTriangles(_triangles.TryGetValue(i, out var t) ? t : new List<int>(), i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public bool IsEmpty => _vertices.Count == 0;
    }
}
