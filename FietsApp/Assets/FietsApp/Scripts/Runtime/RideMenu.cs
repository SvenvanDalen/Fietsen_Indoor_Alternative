using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FietsApp.Climbs;
using FietsApp.Training;
using FietsApp.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Ritmenu (story 6.3): kies een klim uit de bibliotheek (story 4.5), gefilterd op lengte, gemiddelde helling,
    /// categorie, gebied en land of gezocht op naam (story 4.6), per pagina van negen. Enter in het zoekveld kiest de eerste. Opent met M of via Rijden op het startscherm; zolang het open is staat de rit stil.
    /// Een klim kiezen zet je aan de voet. Cijfertoetsen 1-9 kiezen, pijl links/rechts bladert. De knop Weerstand
    /// (of G) stelt in hoeveel van de helling de trainer laat voelen (story 2.3); dat wordt per klim onthouden.
    /// </summary>
    public sealed class RideMenu : MonoBehaviour, IPanel
    {
        [SerializeField] RouteView routeView;
        [SerializeField] RideController ride;
        [SerializeField] bool openAtStart = true;

        const int PageSize = 9;
        const float Width = 900f, RowHeight = 44f, HeaderHeight = 176f, FooterHeight = 56f;
        static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

        readonly ClimbFilter _filter = new ClimbFilter();
        ClimbSummary[] _summaries;
        GameObject _panel;
        Climb[] climbs;
        RectTransform _panelRect, _footer;
        Text _lengthLabel, _gradeLabel, _categoryLabel, _regionLabel, _countryLabel, _empty, _pageLabel, _scaleLabel, _everestingLabel, _stageLabel, _title;
        EverestingView _everesting;
        StageController _stageController;
        // Etappe samenstellen (4.7): zolang dit aan staat, kiest een klik een klim voor de etappe in plaats van te starten.
        readonly Stage _stage = new Stage();
        bool _composing;
        string _stageNote;
        InputField _search;
        int _gradeScale = RiderProfile.DefaultGradeScale;
        readonly List<(Button button, Text label)> _rows = new List<(Button, Text)>();
        readonly List<int> _shown = new List<int>();
        int _page;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            _everesting = FindAnyObjectByType<EverestingView>();
            _stageController = FindAnyObjectByType<StageController>();
            climbs = ClimbLibrary.All.ToArray();
            UiFactory.EnsureEventSystem();
            Build();
            _panel.SetActive(false);
        }

        void Start()
        {
            _summaries = climbs.Select(Summarize).ToArray();
            _filter.SetRegions(_summaries.Select(s => s.Region));
            _filter.SetCountries(_summaries.Select(s => s.Country));
            if (routeView.Climb != null)
                ride.GradeScale = ProfileStore.Current.GradeScale(routeView.Climb.Name) / 100f;
            if (openAtStart && FindAnyObjectByType<HomeScreen>() == null) Open(); // anders opent het startscherm
        }

        /// <summary>Kerncijfers uit het pakket als die er zijn; anders de GPX inlezen.</summary>
        ClimbSummary Summarize(Climb c)
        {
            if (c.LengthMeters > 0f)
                return ClimbSummary.Of(c.Name, c.LengthMeters, c.AverageGradePercent, c.ElevationGain, c.Region, c.Country);
            var route = c == routeView.Climb ? routeView.OwnRoute : ClimbImport.FromGpx(Gpx.Parse(c.Gpx));
            return ClimbSummary.Of(c.Name, ClimbStats.Of(route), c.Region, c.Country);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || UiFactory.IsTyping) return;
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
            ride.Hold(this); // rit staat stil zolang je kiest
            _gradeScale = routeView.Climb != null
                ? ProfileStore.Current.GradeScale(routeView.Climb.Name)
                : RiderProfile.DefaultGradeScale;
            Refresh();
        }

        public void Close()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null); // zoekveld los, sneltoetsen weer aan
            _panel.SetActive(false);
            ride.Release(this);
            StopComposing();
        }

        /// <summary>Start een rit op klim <paramref name="index"/> uit de lijst, aan de voet.</summary>
        public void Choose(int index)
        {
            if (_composing)
            {
                ChooseForStage(climbs[index]);
                return;
            }
            if (climbs[index] != routeView.Climb) routeView.SetClimb(climbs[index]);
            if (ProfileStore.Current.GradeScale(climbs[index].Name) != _gradeScale)
            {
                ProfileStore.Current.SetGradeScale(climbs[index].Name, _gradeScale);
                ProfileStore.Save();
            }
            ride.GradeScale = _gradeScale / 100f;
            ride.StartAt(0f);
            Close();
        }

        /// <summary>
        /// Start een rit op <paramref name="climb"/> met zijn eigen opgeslagen weerstand, bijvoorbeeld vanaf de
        /// wereldkaart (story 4.11c). In de doorlopende wereld begin je bij het knooppunt in het dal.
        /// </summary>
        public void Ride(Climb climb)
        {
            if (climb != routeView.Climb) routeView.SetClimb(climb);
            ride.GradeScale = ProfileStore.Current.GradeScale(climb.Name) / 100f;
            ride.StartAt(0f);
            if (IsOpen) Close();
        }

        /// <summary>Etappeknop: eerst samenstellen, met twee of meer klimmen starten, anders annuleren.</summary>
        void StageButton()
        {
            if (!_composing) _composing = true;
            else if (_stage.CanStart)
            {
                var chosen = _stage.Climbs.Select(id => climbs.First(c => c.Id == id)).ToList();
                StopComposing();
                _stageController.Begin(chosen);
                return;
            }
            else StopComposing();
            Refresh();
        }

        void ChooseForStage(Climb climb)
        {
            _stageNote = _stage.Toggle(climb.Id, climb.Region) switch
            {
                StageChoice.OtherRegion => $"alleen klimmen uit {_stage.Region}",
                StageChoice.Full => $"hooguit {Stage.MaxClimbs} klimmen",
                _ => null,
            };
            Refresh();
        }

        static int IndexOf(IReadOnlyList<string> list, string item)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return i;
            return -1;
        }

        void StopComposing()
        {
            _composing = false;
            _stage.Clear();
            _stageNote = null;
        }

        /// <summary>De klimmen in het menu, voor de wereldkaart.</summary>
        public IReadOnlyList<Climb> Climbs => climbs;

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
            _countryLabel.text = _filter.CountryLabel;
            _scaleLabel.text = $"Weerstand {_gradeScale}%";
            if (_everesting != null)
                _everestingLabel.text = _everesting.IsActive ? "Everesting stoppen" : "Everesting starten";
            _stageLabel.text = !_composing ? "Etappe samenstellen"
                : _stage.CanStart ? $"Etappe starten ({_stage.Count})"
                : "Etappe annuleren";
            _title.text = _composing
                ? $"Etappe{(_stage.Region != null ? " " + _stage.Region : "")}  <size=16>" +
                  (_stageNote != null ? $"<color=#ff9a60>{_stageNote}</color>"
                      : $"<color=#bbb>{_stage.Count}/{Stage.MaxClimbs} gekozen; klik 2-4 klimmen uit één gebied</color>") + "</size>"
                : "Kies een klim  <size=16><color=#bbb>(M sluit, 1-9 kiest, G weerstand)</color></size>";

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
                int inStage = IndexOf(_stage.Climbs, climbs[index].Id);
                label.text = (inStage >= 0 ? $"<color=#ffd060>[{inStage + 1}]</color> " : "") +
                             $"{i + 1}.  <b>{climb.Name}</b>{(current ? "  <color=#9fd>(nu)</color>" : "")}" +
                             $"   <color=#ddd>{climb.Details(Dutch)}</color>" +
                             Place(climb) +
                             ScaleNote(climbs[index].Name) +
                             RecordNote(climbs[index].Name);
            }
            _empty.gameObject.SetActive(_shown.Count == 0);
            _pageLabel.text = $"{_shown.Count} klimmen · {_page + 1}/{pages}";

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
            panel.gameObject.AddComponent<ScreenFit>(); // groter op een telefoon (story 14.2)
            _panel = panel.gameObject;
            _panelRect = panel;

            float y = -14f;
            _title = UiFactory.Text(panel, "Titel", 30, y, 40f);
            var (scaleButton, scaleLabel) = UiFactory.Button(panel, "Weerstand", new Vector2(Width - 236f, y), 220f, 40f,
                NextGradeScale);
            scaleButton.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.12f, 1f);
            _scaleLabel = scaleLabel;
            y -= 52f;

            float w = (Width - 32f - 4 * 10f) / 5f;
            _lengthLabel = FilterButton(panel, "Lengte", 0, y, w, () => { _filter.NextLength(); FilterChanged(); });
            _gradeLabel = FilterButton(panel, "Helling", 1, y, w, () => { _filter.NextGrade(); FilterChanged(); });
            _categoryLabel = FilterButton(panel, "Categorie", 2, y, w, () => { _filter.NextCategory(); FilterChanged(); });
            _regionLabel = FilterButton(panel, "Gebied", 3, y, w, () => { _filter.NextRegion(); FilterChanged(); });
            _countryLabel = FilterButton(panel, "Land", 4, y, w, () => { _filter.NextCountry(); FilterChanged(); });
            y -= 54f;

            _search = UiFactory.TextInput(panel, "Zoeken", new Vector2(16f, y), Width - 32f, 42f,
                "Zoek op naam, gebied of land (Enter kiest de eerste)");
            _search.onValueChanged.AddListener(text => { _filter.Query = text; FilterChanged(); });
            _search.onSubmit.AddListener(_ => { if (_shown.Count > 0) Choose(_shown[0]); });
            y -= 52f;

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
            // Tussen vorige en volgende: Everesting (9.3), etappe (4.7) en het paginanummer.
            _pageLabel = UiFactory.Text(_footer, "Pagina", 18, -6f, 40f);
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            _pageLabel.rectTransform.offsetMin = new Vector2(586f, _pageLabel.rectTransform.offsetMin.y);
            _pageLabel.rectTransform.offsetMax = new Vector2(-146f, _pageLabel.rectTransform.offsetMax.y);
            // Everesting-uitdaging aan of uit; de voortgang staat rechtsonder in beeld.
            var (everestingButton, everestingLabel) = UiFactory.Button(_footer, "Everesting", new Vector2(146f, -6f), 210f, 40f,
                () => { _everesting.Toggle(); Refresh(); });
            everestingButton.GetComponent<Image>().color = new Color(0.45f, 0.36f, 0.1f, 1f);
            everestingButton.gameObject.SetActive(_everesting != null);
            _everestingLabel = everestingLabel;
            var (stageButton, stageLabel) = UiFactory.Button(_footer, "Etappe", new Vector2(366f, -6f), 210f, 40f, StageButton);
            stageButton.GetComponent<Image>().color = new Color(0.2f, 0.42f, 0.25f, 1f);
            stageButton.gameObject.SetActive(_stageController != null);
            _stageLabel = stageLabel;
            UiFactory.Button(_footer, "Volgende", new Vector2(Width - 136f, -6f), 120f, 40f, () => Page(+1)).label.text = "volgende →";
        }

        /// <summary>Gebied en land, bijv. "Alpen · Italië"; het land valt weg als het gebied al zo heet.</summary>
        static string Place(ClimbSummary climb)
        {
            string place = climb.Country.Length > 0 && climb.Country != climb.Region
                ? (climb.Region.Length > 0 ? $"{climb.Region} · {climb.Country}" : climb.Country)
                : climb.Region;
            return place.Length > 0 ? $"   <color=#9ab>{place}</color>" : "";
        }

        /// <summary>Je beste tijd op een klim (story 9.2), als je hem al eens hebt uitgereden.</summary>
        static string RecordNote(string climb) =>
            ProfileStore.Current.Record(climb) is { } r ? $"   <color=#ffd060>PR {ClimbResult.Clock(r.bestSeconds)}</color>" : "";

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
