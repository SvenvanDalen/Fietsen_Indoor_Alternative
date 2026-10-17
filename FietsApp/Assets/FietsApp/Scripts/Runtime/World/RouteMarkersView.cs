using FietsApp.World;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Kilometerborden en bochtnummers langs de weg (story 3.4). Elke hele kilometer voor de top een wit bord met
    /// gele kop, zoals de bornes in de Franse Alpen: "nog 5 km" en de helling van de kilometer erna in de kleur van
    /// het hoogteprofiel. In elke haarspeldbocht een bord met het bochtnummer, aflopend naar de top.
    /// </summary>
    [ExecuteAlways]
    public sealed class RouteMarkersView : MonoBehaviour
    {
        [SerializeField] RouteView routeView;
        [SerializeField] Material signMaterial;
        [SerializeField] Material capMaterial;
        [SerializeField] Material postMaterial;
        [Tooltip("Afstand van het midden van de weg tot het bord (weg 6 m plus berm).")]
        [SerializeField, Min(3f)] float sideOffset = 6.2f;

        readonly GeneratedContent _generated = new GeneratedContent();
        Font _font;

        void OnEnable()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (routeView != null) routeView.ClimbChanged += Rebuild;
            Build();
        }

        void OnDisable()
        {
            if (routeView != null) routeView.ClimbChanged -= Rebuild;
            _generated.DestroyAll();
        }

        void Rebuild()
        {
            _generated.DestroyAll();
            Build();
        }

        void Build()
        {
            if (routeView == null) return;
            var route = routeView.ClimbRoute; // borden alleen op de klim, ook in de doorlopende wereld
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = _generated.NewObject("Borden", transform).transform;

            foreach (var m in RouteMarkers.Kilometers(route))
            {
                var color = ProfileGraphic.ColorFor(Grades.Band(m.NextKmGradePercent));
                var sign = Sign(root, route, m.Distance, side: 1f, $"Kilometerbord {m.KmToTop}");
                Board(sign, new Vector3(1.1f, 1.5f, 0.12f), 1.55f);
                Label(sign, $"{m.KmToTop}", 2.0f, 70, Color.black);
                Label(sign, "km", 1.6f, 34, Color.black);
                Band(sign, new Vector3(1.0f, 0.42f, 0.13f), 1.1f, color);
                Label(sign, $"{m.NextKmGradePercent:0.0}%".Replace('.', ','), 1.1f, 44, Color.black);
            }

            foreach (var h in RouteMarkers.Hairpins(route))
            {
                // Aan de buitenkant van de bocht, waar je hem ziet aankomen.
                float side = h.TurnDegrees > 0f ? 1f : -1f;
                var sign = Sign(root, route, h.Distance, side, $"Bocht {h.Number}", facingFrom: h.Distance - 60f);
                Board(sign, new Vector3(1.2f, 0.9f, 0.12f), 1.9f);
                Label(sign, $"{h.Number}", 2.05f, 64, Color.black);
                Label(sign, "bocht", 1.62f, 26, Color.black);
            }
        }

        /// <summary>
        /// Paal naast de weg op <paramref name="distance"/>, met de voorkant naar de aankomende rijder: de rijrichting
        /// op <paramref name="facingFrom"/> (in een haarspeld de aanloop, niet de top van de bocht).
        /// </summary>
        Transform Sign(Transform root, Route route, float distance, float side, string name, float? facingFrom = null)
        {
            var here = route.Sample(distance);
            var forward = new Vector3(here.HeadingX, 0f, here.HeadingZ);
            var right = new Vector3(forward.z, 0f, -forward.x);
            var approach = route.Sample(Mathf.Max(0f, facingFrom ?? distance));
            var go = _generated.NewObject(name, root);
            go.transform.position = new Vector3(here.X, here.Elevation - 0.15f, here.Z) + right * (side * sideOffset);
            // TextMesh is leesbaar als je in zijn +Z kijkt; de rijder komt van achteren en kijkt vooruit.
            go.transform.rotation = Quaternion.LookRotation(new Vector3(approach.HeadingX, 0f, approach.HeadingZ));
            go.transform.localScale = Vector3.one * 1.5f; // groter dan echt, zodat je hem van ver kunt lezen

            var post = Part(go.transform, "Cylinder.fbx", postMaterial);
            post.localScale = new Vector3(0.1f, 1f, 0.1f);
            post.localPosition = new Vector3(0f, 1f, 0.08f);
            return go.transform;
        }

        /// <summary>Wit bord met een gele kop; <paramref name="center"/> = hoogte van het midden.</summary>
        void Board(Transform sign, Vector3 size, float center)
        {
            var board = Part(sign, "Cube.fbx", signMaterial);
            board.localScale = size;
            board.localPosition = new Vector3(0f, center, 0f);
            var cap = Part(sign, "Cube.fbx", capMaterial);
            cap.localScale = new Vector3(size.x + 0.04f, 0.18f, size.z + 0.02f);
            cap.localPosition = new Vector3(0f, center + size.y / 2f + 0.06f, 0f);
        }

        /// <summary>Gekleurd vlak op het bord (hellingskleur), via een property block zodat er geen extra materialen nodig zijn.</summary>
        void Band(Transform sign, Vector3 size, float center, Color color)
        {
            var band = Part(sign, "Cube.fbx", signMaterial);
            band.localScale = size;
            band.localPosition = new Vector3(0f, center, -0.01f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            band.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        void Label(Transform sign, string text, float height, int size, Color color)
        {
            var go = _generated.NewObject("Tekst", sign, typeof(MeshRenderer), typeof(TextMesh));
            go.transform.localPosition = new Vector3(0f, height, -0.09f); // voor het bord en de hellingsband
            var mesh = go.GetComponent<TextMesh>();
            mesh.font = _font;
            mesh.text = text;
            mesh.fontSize = size;
            mesh.characterSize = 0.07f; // glyphhoogte ≈ characterSize × fontSize × 0,1 m
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        }

        Transform Part(Transform parent, string builtinMesh, Material material)
        {
            var go = _generated.NewObject(builtinMesh, parent, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(builtinMesh);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
