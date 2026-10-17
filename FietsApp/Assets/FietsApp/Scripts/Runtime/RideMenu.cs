using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FietsApp.Training;
using FietsApp.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Ritmenu (story 6.3): kies een klim uit de bibliotheek (story 4.5), gefilterd op lengte, gemiddelde helling,
    /// categorie en gebied, per pagina van negen. Opent bij de start en met M; zolang het open is staat de rit stil.
    /// Een klim kiezen zet je aan de voet. Cijfertoetsen 1-9 kiezen, pijl links/rechts bladert. De knop Weerstand
    /// (of G) stelt in hoeveel van de helling de trainer laat voelen (story 2.3); dat wordt per klim onthouden.
    /// </summary>
    public sealed class RideMenu : MonoBehaviour
    {
        [Tooltip("Alle klimmen (Routes/Bibliotheek.asset). Leeg = de lijst hieronder.")]
        [SerializeField] ClimbLibrary library;
        [SerializeField] Climb[] climbs;
        [SerializeField] RouteView routeView;
        [SerializeField] RideController ride;
        [SerializeField] bool openAtStart = true;

        const int PageSize = 9;
        const float Width = 900f, RowHeight = 44f, HeaderHeight = 124f, FooterHeight = 56f;
        static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

        readonly ClimbFilter _filter = new ClimbFilter();
        ClimbSummary[] _summaries;
        GameObject _panel;
        RectTransform _panelRect, _footer;
        Text _lengthLabel, _gradeLabel, _categoryLabel, _regionLabel, _empty, _pageLabel, _scaleLabel;
        int _gradeScale = RiderProfile.DefaultGradeScale;
        readonly List<(Button button, Text label)> _rows = new List<(Button, Text)>();
        readonly List<int> _shown = new List<int>();
        int _page;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            var source = library != null && library.climbs.Length > 0 ? library.climbs : climbs ?? new Climb[0];
            climbs = source.Where(c => c != null && c.gpx != null).ToArray();
            UiFactory.EnsureEventSystem();
            Build();
            _panel.SetActive(false);
        }

        void Start()
        {
            _summaries = climbs.Select(Summarize).ToArray();
            _filter.SetRegions(_summaries.Select(s => s.Region));
            if (routeView.Climb != null)
                ride.GradeScale = ProfileStore.Current.GradeScale(routeView.Climb.displayName) / 100f;
            if (openAtStart) Open();
        }

        /// <summary>Opgeslagen kerncijfers als die er zijn; anders de GPX inlezen.</summary>
        ClimbSummary Summarize(Climb c)
        {
            if (c.lengthMeters > 0f)
                return ClimbSummary.Of(c.displayName, c.lengthMeters, c.averageGradePercent, c.elevationGain, c.region);
            var route = c == routeView.Climb ? routeView.OwnRoute : ClimbImport.FromGpx(Gpx.Parse(c.gpx.text));
            return ClimbSummary.Of(c.displayName, ClimbStats.Of(route), c.region);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
            if (kb.rightArrowKey.wasPressedThisFrame) Page(+1);
            if (kb.leftArrowKey.wasPressedThisFrame) Page(-1);
            if (kb.gKey.wasPressedThisFrame) NextGradeScale();
            int onPage = Mathf.Min(PageSize, _shown.Count - _page * PageSize);
            for (int i = 0; i < onPage; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Choose(_shown[_page * PageSize + i]);
        }

        public void Open()
        {
            _panel.SetActive(true);
            ride.enabled = false; // rit staat stil zolang je kiest
            _gradeScale = routeView.Climb != null
                ? ProfileStore.Current.GradeScale(routeView.Climb.displayName)
                : RiderProfile.DefaultGradeScale;
            Refresh();
        }

        public void Close()
        {
            _panel.SetActive(false);
            ride.enabled = true;
        }

        /// <summary>Start een rit op klim <paramref name="index"/> uit de lijst, aan de voet.</summary>
        public void Choose(int index)
        {
            if (climbs[index] != routeView.Climb) routeView.SetClimb(climbs[index]);
            if (ProfileStore.Current.GradeScale(climbs[index].displayName) != _gradeScale)
            {
                ProfileStore.Current.SetGradeScale(climbs[index].displayName, _gradeScale);
                ProfileStore.Save();
            }
            ride.GradeScale = _gradeScale / 100f;
            ride.StartAt(0f);
            Close();
        }

        void Page(int step)
        {
            int pages = Mathf.Max(1, (_shown.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page + step, 0, pages - 1);
            Refresh();
        }

        /// <summary>Weerstand 50-150% in stappen van 25; geldt voor de klim die je hierna kiest.</summary>
        void NextGradeScale()
        {
            _gradeScale = _gradeScale >= RiderProfile.MaxGradeScale ? RiderProfile.MinGradeScale : _gradeScale + 25;
            Refresh();
        }

        void FilterChanged()
        {
            _page = 0;
            Refresh();
        }

        void Refresh()
        {
            _lengthLabel.text = _filter.LengthLabel;
            _gradeLabel.text = _filter.GradeLabel;
            _categoryLabel.text = _filter.CategoryLabel;
            _regionLabel.text = _filter.RegionLabel;
            _scaleLabel.text = $"Weerstand {_gradeScale}%";

            _shown.Clear();
            if (_summaries != null) _shown.AddRange(_filter.Apply(_summaries));
            int pages = Mathf.Max(1, (_shown.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            int first = _page * PageSize;
            int onPage = Mathf.Min(PageSize, _shown.Count - first);

            for (int i = 0; i < _rows.Count; i++)
            {
                var (button, label) = _rows[i];
                bool used = i < onPage;
                button.gameObject.SetActive(used);
                if (!used) continue;
                int index = _shown[first + i];
                var climb = _summaries[index];
                bool current = climbs[index] == routeView.Climb;
                label.text = $"{i + 1}.  <b>{climb.Name}</b>{(current ? "  <color=#9fd>(nu)</color>" : "")}" +
                             $"   <color=#ddd>{climb.Details(Dutch)}</color>" +
                             (climb.Region.Length > 0 ? $"   <color=#9ab>{climb.Region}</color>" : "") +
                             ScaleNote(climbs[index].displayName);
            }
            _empty.gameObject.SetActive(_shown.Count == 0);
            _pageLabel.text = $"{_shown.Count} klimmen   pagina {_page + 1}/{pages}   <color=#bbb>(← →)</color>";

            float listHeight = Mathf.Max(1, onPage) * (RowHeight + 6f);
            _footer.anchoredPosition = new Vector2(0f, -HeaderHeight - listHeight);
            _panelRect.sizeDelta = new Vector2(Width, HeaderHeight + listHeight + FooterHeight);
        }

        void Build()
        {
            var canvas = UiFactory.Canvas(transform, "Ritmenu Canvas", 20);

            var panel = new GameObject("Paneel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvas, false);
            // Bovenkant vast, zodat het filteren de knoppen niet laat verspringen.
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -190f); // onder de HUD-tegels en de statusregel
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
            _panel = panel.gameObject;
            _panelRect = panel;

            float y = -14f;
            UiFactory.Text(panel, "Titel", 30, y, 40f).text =
                "Kies een klim  <size=16><color=#bbb>(M sluit, 1-9 kiest, G weerstand)</color></size>";
            var (scaleButton, scaleLabel) = UiFactory.Button(panel, "Weerstand", new Vector2(Width - 236f, y), 220f, 40f,
                NextGradeScale);
            scaleButton.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.12f, 1f);
            _scaleLabel = scaleLabel;
            y -= 52f;

            float w = (Width - 32f - 3 * 10f) / 4f;
            _lengthLabel = FilterButton(panel, "Lengte", 0, y, w, () => { _filter.NextLength(); FilterChanged(); });
            _gradeLabel = FilterButton(panel, "Helling", 1, y, w, () => { _filter.NextGrade(); FilterChanged(); });
            _categoryLabel = FilterButton(panel, "Categorie", 2, y, w, () => { _filter.NextCategory(); FilterChanged(); });
            _regionLabel = FilterButton(panel, "Gebied", 3, y, w, () => { _filter.NextRegion(); FilterChanged(); });
            y -= 58f;

            _empty = UiFactory.Text(panel, "Leeg", 20, y, RowHeight);
            _empty.text = "<color=#bbb>Geen klim past bij dit filter.</color>";

            for (int i = 0; i < PageSize; i++)
            {
                int row = i;
                var (button, label) = UiFactory.Button(panel, $"Klim {i}", new Vector2(16f, y), Width - 32f, RowHeight,
                    () => Choose(_shown[_page * PageSize + row]));
                button.GetComponent<Image>().color = new Color(0.16f, 0.3f, 0.5f, 1f);
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.fontSize = 19;
                label.rectTransform.offsetMin = new Vector2(14f, 0f);
                _rows.Add((button, label));
                y -= RowHeight + 6f;
            }

            // Onderbalk met bladeren; verschuift mee met het aantal rijen op de pagina.
            _footer = new GameObject("Onderbalk", typeof(RectTransform)).GetComponent<RectTransform>();
            _footer.SetParent(panel, false);
            _footer.anchorMin = new Vector2(0f, 1f);
            _footer.anchorMax = new Vector2(1f, 1f);
            _footer.pivot = new Vector2(0.5f, 1f);
            _footer.sizeDelta = new Vector2(0f, FooterHeight);
            UiFactory.Button(_footer, "Vorige", new Vector2(16f, -6f), 120f, 40f, () => Page(-1)).label.text = "← vorige";
            _pageLabel = UiFactory.Text(_footer, "Pagina", 18, -6f, 40f);
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            UiFactory.Button(_footer, "Volgende", new Vector2(Width - 136f, -6f), 120f, 40f, () => Page(+1)).label.text = "volgende →";
        }

        /// <summary>Opgeslagen weerstand van een klim, als die afwijkt van 100%.</summary>
        static string ScaleNote(string climb)
        {
            int scale = ProfileStore.Current.GradeScale(climb);
            return scale == RiderProfile.DefaultGradeScale ? "" : $"   <color=#fc9>weerstand {scale}%</color>";
        }

        static Text FilterButton(RectTransform panel, string name, int column, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            var (_, label) = UiFactory.Button(panel, name, new Vector2(16f + column * (width + 10f), y), width, 44f, onClick);
            return label;
        }
    }
}
