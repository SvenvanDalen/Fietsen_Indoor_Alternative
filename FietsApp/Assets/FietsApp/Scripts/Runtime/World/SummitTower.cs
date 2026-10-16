using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Herkenningspunt op een top, zoals de zendmast van de Mont Ventoux: een laag gebouw, een witte toren en
    /// een rood-witte antenne, low-poly uit eenvoudige vormen. Staat op echte coördinaten en op de hoogte
    /// van het echte landschap. Alleen bij een klim met <see cref="Climb.summitTower"/> en een hoogtebestand.
    /// </summary>
    [ExecuteAlways]
    public sealed class SummitTower : MonoBehaviour
    {
        [SerializeField] RouteView routeView;
        [SerializeField] Material wallMaterial;
        [SerializeField] Material redMaterial;

        readonly GeneratedContent _generated = new GeneratedContent();

        void OnEnable()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (routeView != null) routeView.ClimbChanged += Rebuild;
            Build();
        }

        void Rebuild()
        {
            _generated.DestroyAll();
            Build();
        }

        void Build()
        {
            var route = routeView != null ? routeView.Route : null;
            var heights = routeView != null ? routeView.NaturalHeights : null;
            var climb = routeView != null ? routeView.Climb : null;
            if (climb == null || !climb.summitTower || route?.Projection == null || heights == null) return;

            var (x, z) = route.Projection.ToLocal(climb.summitLatitude, climb.summitLongitude);
            var root = _generated.NewObject("Zendmast", transform).transform;
            root.position = new Vector3(x, heights.HeightAt(x, z) - 1f, z);

            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 5f, 0f), new Vector3(26f, 5f, 26f), wallMaterial);   // gebouw
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 22f, 0f), new Vector3(7f, 14f, 7f), wallMaterial);   // toren
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 38f, 0f), new Vector3(9f, 2.5f, 9f), wallMaterial);  // omloop
            for (int i = 0; i < 6; i++)                                                                              // antenne
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 43f + i * 4f, 0f), new Vector3(1.6f, 2f, 1.6f),
                    i % 2 == 0 ? redMaterial : wallMaterial);
        }

        void OnDisable()
        {
            if (routeView != null) routeView.ClimbChanged -= Rebuild;
            _generated.DestroyAll();
        }

        void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = _generated.NewObject(type.ToString(), parent, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type == PrimitiveType.Cylinder ? "Cylinder.fbx" : "Cube.fbx");
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
