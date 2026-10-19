using FietsApp.World;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Hoogteprofiel van de hele route met de positie van de rijder (story 6.2):
    /// stukken van 100 m gekleurd naar helling, een stip op de huidige plek, en de helling,
    /// hoogte, afstand tot de top en klimcategorie (story 4.3) als tekst. Toets H toont of verbergt het paneel (P is pauze, story 6.4).
    /// </summary>
    public sealed class ElevationProfileHud : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [SerializeField] bool visibleAtStart = true;

        const float Width = 640f, Height = 230f, GraphHeight = 150f;

        ElevationProfile _profile;
        GameObject _panel;
        ProfileGraphic _graph;
        RectTransform _dot, _marker;
        Text _grade, _details;
        string _category;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            Build();
            _panel.SetActive(visibleAtStart);
        }

        void Start()
        {
            routeView.ClimbChanged += ShowClimb;
            ShowClimb();
        }

        void OnDestroy()
        {
            if (routeView != null) routeView.ClimbChanged -= ShowClimb;
        }

        void ShowClimb()
        {
            _profile = ElevationProfile.Build(routeView.Route);
            _graph.Profile = _profile;
            // Categorie van de klim zelf, niet van de hele rit met dalweg en afdaling (doorlopende wereld, 4.11b).
            string label = ClimbCategories.Label(ClimbCategories.Of(ClimbStats.Of(routeView.ClimbRoute)));
            _category = label.Length > 0 ? $"<b>{label}</b>   " : "";
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : UnityEngine.InputSystem.Keyboard.current; // niet tijdens typen in een tekstveld
            if (kb != null && kb.hKey.wasPressedThisFrame) _panel.SetActive(!_panel.activeSelf);
            if (_profile == null || !_panel.activeSelf) return;

            var route = routeView.Route;
            float distance = Mathf.Clamp(ride.DistanceM, 0f, route.Length);
            var here = route.Sample(distance);

            var p = _graph.PointAt(distance, here.Elevation);
            _dot.anchoredPosition = p;
            _marker.anchoredPosition = new Vector2(p.x, _marker.anchoredPosition.y);

            float grade = ride.GradePercent;
            _grade.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ProfileGraphic.ColorFor(Grades.Band(grade)))}>{grade:0.0}%</color>";

            string toSummit;
            if (distance < _profile.SummitDistance)
            {
                float km = (_profile.SummitDistance - distance) / 1000f;
                float climb = _profile.MaxElevation - here.Elevation;
                toSummit = $"nog {km:0.0} km tot de top, {climb:0} m hoger";
            }
            else
            {
                toSummit = routeView.Journey != null
                    ? $"afdaling, nog {(route.Length - distance) / 1000f:0.0} km tot het dal"
                    : $"nog {(route.Length - distance) / 1000f:0.0} km tot de finish";
            }
            // Echte hoogte boven zeeniveau, ook als de klim in de wereld verschoven ligt.
            _details.text = $"{_category}hoogte {routeView.RealElevation(here.Elevation):0} m   {toSummit}";
        }

        void Build()
        {
            var canvasGo = new GameObject("Profiel Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = NewRect("Paneel", canvasGo.transform, typeof(Image));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-24f, 24f);
            panel.sizeDelta = new Vector2(Width, Height);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            panel.GetComponent<Image>().raycastTarget = false;
            _panel = panel.gameObject;

            _grade = AddText(panel, "Helling", 34, new Vector2(16f, -8f), new Vector2(200f, 44f), TextAnchor.UpperLeft);
            _grade.fontStyle = FontStyle.Bold;
            _details = AddText(panel, "Details", 18, new Vector2(150f, -18f), new Vector2(Width - 166f, 30f), TextAnchor.UpperRight);

            var graphRect = NewRect("Grafiek", panel, typeof(ProfileGraphic));
            graphRect.anchorMin = new Vector2(0f, 0f);
            graphRect.anchorMax = new Vector2(1f, 0f);
            graphRect.pivot = new Vector2(0.5f, 0f);
            graphRect.offsetMin = new Vector2(12f, 12f);
            graphRect.offsetMax = new Vector2(-12f, 12f + GraphHeight);
            _graph = graphRect.GetComponent<ProfileGraphic>();
            _graph.raycastTarget = false;

            // Verticale lijn en stip op de huidige positie. Verankerd op het draaipunt van de grafiek
            // (midden onder), zodat anchoredPosition gelijk is aan de lokale coördinaten van PointAt.
            var pivot = graphRect.pivot;
            _marker = NewRect("Lijn", graphRect, typeof(Image));
            _marker.anchorMin = _marker.anchorMax = pivot;
            _marker.pivot = new Vector2(0.5f, 0f);
            _marker.sizeDelta = new Vector2(2f, GraphHeight);
            _marker.anchoredPosition = Vector2.zero;
            _marker.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);

            _dot = NewRect("Stip", graphRect, typeof(Image));
            _dot.anchorMin = _dot.anchorMax = pivot;
            _dot.sizeDelta = new Vector2(14f, 14f);
            _dot.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _dot.GetComponent<Image>().color = Color.white;
        }

        static RectTransform NewRect(string name, Transform parent, System.Type component)
        {
            // CanvasRenderer expliciet: een eigen Graphic-subklasse krijgt die niet vanzelf, en zonder wordt er niets getekend.
            var rt = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), component).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (rt.TryGetComponent<Graphic>(out var g)) g.raycastTarget = false;
            return rt;
        }

        static Text AddText(RectTransform parent, string name, int size, Vector2 topLeft, Vector2 sizeDelta, TextAnchor anchor)
        {
            var text = NewRect(name, parent, typeof(Text)).GetComponent<Text>();
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = sizeDelta;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            return text;
        }
    }
}
