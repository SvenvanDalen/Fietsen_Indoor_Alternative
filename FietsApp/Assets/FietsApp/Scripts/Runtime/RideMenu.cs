using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FietsApp.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Ritmenu (story 6.3): kies een klim uit de lijst, gefilterd op lengte, gemiddelde helling en categorie.
    /// Opent bij de start en met M; zolang het open is staat de rit stil. Een klim kiezen zet je aan de voet.
    /// Cijfertoetsen 1-9 kiezen de zichtbare klimmen.
    /// </summary>
    public sealed class RideMenu : MonoBehaviour
    {
        [SerializeField] Climb[] climbs;
        [SerializeField] RouteView routeView;
        [SerializeField] RideController ride;
        [SerializeField] bool openAtStart = true;

        const float Width = 820f, RowHeight = 46f, HeaderHeight = 124f; // titel + filterknoppen
        static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

        readonly ClimbFilter _filter = new ClimbFilter();
        ClimbSummary[] _summaries;
        GameObject _panel;
        RectTransform _panelRect;
        Text _lengthLabel, _gradeLabel, _categoryLabel, _empty;
        readonly List<(Button button, Text label)> _rows = new List<(Button, Text)>();
        readonly List<int> _shown = new List<int>();

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            climbs = (climbs ?? new Climb[0]).Where(c => c != null && c.gpx != null).ToArray();
            UiFactory.EnsureEventSystem();
            Build();
            _panel.SetActive(false);
        }

        void Start()
        {
            // Kerncijfers één keer uitrekenen; de route van de huidige klim is er al.
            _summaries = climbs.Select(c => ClimbSummary.Of(c.displayName,
                ClimbStats.Of(c == routeView.Climb ? routeView.Route : ClimbImport.FromGpx(Gpx.Parse(c.gpx.text))))).ToArray();
            if (openAtStart) Open();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
            for (int i = 0; i < Mathf.Min(9, _shown.Count); i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Choose(_shown[i]);
        }

        public void Open()
        {
            _panel.SetActive(true);
            ride.enabled = false; // rit staat stil zolang je kiest
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
            ride.StartAt(0f);
            Close();
        }

        void Refresh()
        {
            _lengthLabel.text = _filter.LengthLabel;
            _gradeLabel.text = _filter.GradeLabel;
            _categoryLabel.text = _filter.CategoryLabel;

            _shown.Clear();
            if (_summaries != null) _shown.AddRange(_filter.Apply(_summaries));
            for (int i = 0; i < _rows.Count; i++)
            {
                var (button, label) = _rows[i];
                bool used = i < _shown.Count;
                button.gameObject.SetActive(used);
                if (!used) continue;
                var climb = _summaries[_shown[i]];
                bool current = climbs[_shown[i]] == routeView.Climb;
                label.text = $"{i + 1}.  <b>{climb.Name}</b>{(current ? "  <color=#9fd>(nu)</color>" : "")}" +
                             $"   <color=#ddd>{climb.Details(Dutch)}</color>";
            }
            _empty.gameObject.SetActive(_shown.Count == 0);
            _panelRect.sizeDelta = new Vector2(Width, HeaderHeight + Mathf.Max(1, _shown.Count) * (RowHeight + 6f) + 10f);
        }

        void Build()
        {
            var canvas = UiFactory.Canvas(transform, "Ritmenu Canvas", 20);

            var panel = new GameObject("Paneel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvas, false);
            // Bovenkant vast, zodat het filteren de knoppen niet laat verspringen.
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -140f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
            _panel = panel.gameObject;
            _panelRect = panel;

            float y = -14f;
            UiFactory.Text(panel, "Titel", 30, y, 40f).text =
                "Kies een klim  <size=16><color=#bbb>(M sluit, 1-9 kiest)</color></size>";
            y -= 52f;

            float w = (Width - 32f - 2 * 12f) / 3f;
            _lengthLabel = FilterButton(panel, "Lengte", 16f, y, w, () => { _filter.NextLength(); Refresh(); });
            _gradeLabel = FilterButton(panel, "Helling", 16f + w + 12f, y, w, () => { _filter.NextGrade(); Refresh(); });
            _categoryLabel = FilterButton(panel, "Categorie", 16f + 2 * (w + 12f), y, w, () => { _filter.NextCategory(); Refresh(); });
            y -= 58f;

            _empty = UiFactory.Text(panel, "Leeg", 20, y, RowHeight);
            _empty.text = "<color=#bbb>Geen klim past bij dit filter.</color>";

            for (int i = 0; i < climbs.Length; i++)
            {
                int row = i;
                var (button, label) = UiFactory.Button(panel, $"Klim {i}", new Vector2(16f, y), Width - 32f, RowHeight,
                    () => Choose(_shown[row]));
                button.GetComponent<Image>().color = new Color(0.16f, 0.3f, 0.5f, 1f);
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.fontSize = 20;
                label.rectTransform.offsetMin = new Vector2(14f, 0f);
                _rows.Add((button, label));
                y -= RowHeight + 6f;
            }
        }

        static Text FilterButton(RectTransform panel, string name, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            var (_, label) = UiFactory.Button(panel, name, new Vector2(x, y), width, 44f, onClick);
            return label;
        }
    }
}
