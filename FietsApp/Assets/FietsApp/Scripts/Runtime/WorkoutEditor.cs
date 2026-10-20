using System.Collections.Generic;
using FietsApp.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Workout-editor (story 8.3): de blokken als staafjes, hoogte naar het doel en breedte naar de duur. Sleep een blok
    /// om het te verplaatsen, de bovenrand voor het vermogen (bij een ramp het begin of het eind) en de rechterrand voor de
    /// duur; pijltjes doen hetzelfde voor het gekozen blok. Opslaan maakt een .zwo in <see cref="WorkoutStore.Folder"/>,
    /// om te delen of in andere apps te gebruiken. De rit staat stil zolang de editor open is.
    /// </summary>
    public sealed class WorkoutEditor : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] WorkoutController workouts;

        const float Width = 1240f, Height = 640f, ChartWidth = 1180f, ChartHeight = 300f;
        const double MaxFtp = 2.0;            // bovenkant van de grafiek
        const double MinVisibleSeconds = 1800; // korte workouts vullen niet de hele breedte

        WorkoutDraft _draft = new WorkoutDraft();
        string _path; // het bestand dat je bewerkt, of null voor een nieuwe workout
        int _selected;
        double _scale; // pixels per seconde, vast tijdens het slepen
        bool _dragging;

        GameObject _panel;
        RectTransform _chart, _selection;
        InputField _name;
        Text _summary, _blockInfo, _status;
        readonly Dictionary<DraftBlock, BlockView> _views = new Dictionary<DraftBlock, BlockView>();

        public bool IsOpen => _panel != null && _panel.activeSelf;

        /// <summary>De workout die nu in de editor staat (voor tests en de lijst).</summary>
        public WorkoutDraft Draft => _draft;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (workouts == null) workouts = FindAnyObjectByType<WorkoutController>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        /// <summary>Opent een nieuwe workout (opwarmen, één blok, uitrijden) of een bestaande uit <paramref name="path"/>.</summary>
        public void Open(Workout source = null, string path = null)
        {
            _path = path;
            if (source != null) _draft = WorkoutDraft.From(source);
            else
            {
                _draft = new WorkoutDraft();
                _draft.Blocks.Add(new DraftBlock { Name = "Opwarmen", Seconds = 600, StartFtp = 0.40, EndFtp = 0.75 });
                _draft.Blocks.Add(new DraftBlock { Name = "Tempo", Seconds = 1200, StartFtp = 0.85, EndFtp = 0.85 });
                _draft.Blocks.Add(new DraftBlock { Name = "Uitrijden", Seconds = 300, StartFtp = 0.55, EndFtp = 0.40 });
            }
            _name.text = _draft.Name;
            _selected = 0;
            _status.text = path != null ? $"<color=#bbb>{System.IO.Path.GetFileName(path)}</color>" : "";
            foreach (var view in _views.Values) Destroy(view.Root.gameObject);
            _views.Clear();
            _panel.SetActive(true);
            ride.Hold(this);
            Layout();
        }

        public void Close()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _panel.SetActive(false);
            ride.Release(this);
        }

        void Update()
        {
            if (!IsOpen || UiFactory.IsTyping) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (_draft.Blocks.Count == 0) return;
            var b = _draft.Blocks[_selected];
            bool changed = true;
            if (kb.rightArrowKey.wasPressedThisFrame) _draft.SetSeconds(_selected, b.Seconds + WorkoutDraft.SecondsStep);
            else if (kb.leftArrowKey.wasPressedThisFrame) _draft.SetSeconds(_selected, b.Seconds - WorkoutDraft.SecondsStep);
            else if (kb.upArrowKey.wasPressedThisFrame) _draft.SetPower(_selected, b.StartFtp + WorkoutDraft.FtpStep);
            else if (kb.downArrowKey.wasPressedThisFrame) _draft.SetPower(_selected, b.StartFtp - WorkoutDraft.FtpStep);
            else if (kb.tabKey.wasPressedThisFrame) _selected = (_selected + (kb.shiftKey.isPressed ? -1 : 1) + _draft.Blocks.Count) % _draft.Blocks.Count;
            else if (kb.deleteKey.wasPressedThisFrame) RemoveSelected();
            else changed = false;
            if (changed) Layout();
        }

        // ---- Knoppen ----

        void AddBlock(bool ramp)
        {
            _selected = _draft.Add(ramp, _draft.Blocks.Count > 0 ? _selected : -1);
            Layout();
        }

        void DuplicateSelected()
        {
            if (_draft.Blocks.Count == 0) return;
            _selected = _draft.Duplicate(_selected);
            Layout();
        }

        void RemoveSelected()
        {
            if (_draft.Blocks.Count <= 1) { _status.text = "<color=#ff9a60>een workout heeft minstens één blok</color>"; return; }
            _draft.Remove(_selected);
            _selected = Mathf.Min(_selected, _draft.Blocks.Count - 1);
            Layout();
        }

        /// <summary>Bewaart als .zwo; geeft de workout terug, of null als het niet lukte.</summary>
        public Workout Save()
        {
            _draft.Name = _name.text;
            var workout = _draft.ToWorkout();
            string path = WorkoutStore.Save(workout, _path);
            if (path == null)
            {
                _status.text = "<color=#ff6060>opslaan mislukt</color>";
                return null;
            }
            _path = path;
            _status.text = $"opgeslagen: <b>{System.IO.Path.GetFileName(path)}</b>  <color=#bbb>(deel het bestand, of zet het in Zwift of Intervals.icu)</color>";
            return workout;
        }

        void SaveAndRide()
        {
            var workout = Save();
            if (workout == null) return;
            Close();
            workouts.Begin(workout);
        }

        void OpenFolder()
        {
            System.IO.Directory.CreateDirectory(WorkoutStore.Folder);
            Application.OpenURL("file:///" + WorkoutStore.Folder.Replace('\\', '/'));
        }

        // ---- Slepen ----

        enum DragMode { Move, Power, PowerStart, PowerEnd, Duration }

        void Select(DraftBlock block)
        {
            _selected = _draft.Blocks.IndexOf(block);
            Layout();
        }

        void BeginDrag(DraftBlock block)
        {
            Select(block);
            _dragging = true;
        }

        void Drag(DraftBlock block, DragMode mode, PointerEventData e)
        {
            int index = _draft.Blocks.IndexOf(block);
            if (index < 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_chart, e.position, e.pressEventCamera, out var local)) return;
            double seconds = local.x / _scale, ftp = local.y / ChartHeight * MaxFtp;
            switch (mode)
            {
                case DragMode.Move:
                    int target = _draft.IndexAt(seconds);
                    if (target == index) return;
                    _draft.Move(index, target);
                    _selected = target;
                    break;
                case DragMode.Power: _draft.SetPower(index, ftp); break;
                case DragMode.PowerStart: _draft.SetPower(index, ftp, end: false); break;
                case DragMode.PowerEnd: _draft.SetPower(index, ftp, end: true); break;
                case DragMode.Duration: _draft.SetSeconds(index, seconds - _draft.StartOf(index)); break;
            }
            Layout();
        }

        void EndDrag()
        {
            _dragging = false;
            Layout();
        }

        // ---- Tekenen ----

        void Layout()
        {
            if (!_dragging) _scale = ChartWidth / System.Math.Max(_draft.TotalSeconds, MinVisibleSeconds);

            // Weggehaalde blokken opruimen, nieuwe aanmaken.
            var gone = new List<DraftBlock>();
            foreach (var kv in _views) if (!_draft.Blocks.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var block in gone) { Destroy(_views[block].Root.gameObject); _views.Remove(block); }

            double t = 0;
            for (int i = 0; i < _draft.Blocks.Count; i++)
            {
                var block = _draft.Blocks[i];
                if (!_views.TryGetValue(block, out var view)) _views[block] = view = NewView(block);
                float x = (float)(t * _scale), w = Mathf.Max(4f, (float)(block.Seconds * _scale) - 2f);
                float h0 = Height01(block.StartFtp) * ChartHeight, h1 = Height01(block.EndFtp) * ChartHeight;
                view.Root.anchoredPosition = new Vector2(x, 0f);
                view.Root.sizeDelta = new Vector2(w, Mathf.Max(h0, h1));
                view.Shape.StartHeight = h0;
                view.Shape.EndHeight = h1;
                var zone = Hud.ZoneColor(PowerZones.Of((block.StartFtp + block.EndFtp) / 2, 1.0));
                view.Shape.color = i == _selected ? zone : Color.Lerp(zone, Color.black, 0.3f);
                view.Shape.SetVerticesDirty();
                view.Top.gameObject.SetActive(!block.IsRamp);
                view.TopStart.gameObject.SetActive(block.IsRamp);
                view.TopEnd.gameObject.SetActive(block.IsRamp);
                // Rampgrepen: de linkerhelft van de bovenkant zet het begin, de rechterhelft het eind.
                view.TopStart.anchoredPosition = new Vector2(0f, h0 - 6f);
                view.TopStart.sizeDelta = new Vector2(w / 2f, 12f);
                view.TopEnd.anchoredPosition = new Vector2(w / 2f, h1 - 6f);
                view.TopEnd.sizeDelta = new Vector2(Mathf.Max(1f, w / 2f - 8f), 12f);
                view.Label.text = w > 60f ? $"{block.Name}\n{Clock(block.Seconds)}" : "";
                if (i == _selected)
                {
                    _selection.anchoredPosition = new Vector2(x, -10f);
                    _selection.sizeDelta = new Vector2(w, 4f);
                }
                t += block.Seconds;
            }

            int ftp = ProfileStore.Current.ftpWatts;
            if (_draft.Blocks.Count > 0)
            {
                var sel = _draft.Blocks[_selected];
                string power = sel.IsRamp
                    ? $"{sel.StartFtp * 100:0} → {sel.EndFtp * 100:0}% FTP ({sel.StartFtp * ftp:0} → {sel.EndFtp * ftp:0} W)"
                    : $"{sel.StartFtp * 100:0}% FTP ({sel.StartFtp * ftp:0} W)";
                _blockInfo.text = $"Blok {_selected + 1}/{_draft.Blocks.Count}: <b>{sel.Name}</b>   {Clock(sel.Seconds)}   {power}";
                var probe = _draft.ToWorkout();
                _summary.text = $"<color=#bbb>totaal</color> <b>{Clock(probe.TotalSeconds)}</b>   <color=#bbb>gemiddeld</color> <b>{probe.AverageFtp * 100:0}% FTP</b>";
            }
        }

        static float Height01(double ftp) => Mathf.Clamp01((float)(ftp / MaxFtp));

        static string Clock(double seconds)
        {
            int s = (int)System.Math.Round(seconds);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        sealed class BlockView
        {
            public RectTransform Root, Top, TopStart, TopEnd;
            public BlockShape Shape;
            public Text Label;
        }

        BlockView NewView(DraftBlock block)
        {
            var view = new BlockView();
            view.Root = new GameObject("Blok", typeof(RectTransform)).GetComponent<RectTransform>();
            view.Root.SetParent(_chart, false);
            view.Root.anchorMin = view.Root.anchorMax = view.Root.pivot = Vector2.zero;

            view.Shape = new GameObject("Vlak", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlockShape)).GetComponent<BlockShape>();
            Fill(view.Shape.rectTransform, view.Root);
            Handle(view.Shape.gameObject, block, DragMode.Move);

            view.Label = new GameObject("Naam", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Fill(view.Label.rectTransform, view.Root);
            view.Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.Label.fontSize = 15;
            view.Label.alignment = TextAnchor.LowerCenter;
            view.Label.color = new Color(0f, 0f, 0f, 0.8f);
            view.Label.raycastTarget = false;

            // Grepen: bovenrand (vermogen) en rechterrand (duur), iets breder dan ze lijken zodat ze makkelijk te pakken zijn.
            view.Top = Grip(view.Root, "Boven", block, DragMode.Power, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(-8f, 6f));
            view.TopStart = Grip(view.Root, "Begin", block, DragMode.PowerStart, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            view.TopEnd = Grip(view.Root, "Eind", block, DragMode.PowerEnd, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            view.TopStart.pivot = view.TopEnd.pivot = Vector2.zero; // plek en maat zet Layout
            Grip(view.Root, "Rechts", block, DragMode.Duration, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(4f, 0f));
            return view;
        }

        RectTransform Grip(RectTransform parent, string name, DraftBlock block, DragMode mode, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
            Handle(rt.gameObject, block, mode);
            return rt;
        }

        void Handle(GameObject go, DraftBlock block, DragMode mode)
        {
            var handle = go.AddComponent<BlockHandle>();
            handle.Down = () => Select(block);
            handle.Begin = () => BeginDrag(block);
            handle.Dragged = e => Drag(block, mode, e);
            handle.End = EndDrag;
        }

        static void Fill(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Workout-editor Canvas", 25);
            var panel = UiFactory.Panel(canvas, "Workout-editor", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            _panel = panel.gameObject;

            UiFactory.Text(panel, "Titel", 28, -12f, 40f).text =
                "Workout-editor  <size=16><color=#bbb>(sleep blokken, randen voor vermogen en duur; ← → duur, ↑ ↓ vermogen, Tab volgende, Del weg, Esc sluit)</color></size>";
            _name = UiFactory.TextInput(panel, "Naam", new Vector2(30f, -60f), 520f, 42f, "Naam van de workout");
            _summary = UiFactory.Text(panel, "Totaal", 20, -60f, 42f);
            _summary.alignment = TextAnchor.MiddleRight;
            _summary.rectTransform.offsetMax = new Vector2(-30f, _summary.rectTransform.offsetMax.y);

            // Grafiek met lijnen op 50, 100 en 150% FTP.
            var area = new GameObject("Grafiekvlak", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            area.SetParent(panel, false);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0f, 1f);
            area.anchoredPosition = new Vector2(30f, -120f);
            area.sizeDelta = new Vector2(ChartWidth, ChartHeight + 20f);
            area.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            _chart = new GameObject("Grafiek", typeof(RectTransform)).GetComponent<RectTransform>();
            _chart.SetParent(area, false);
            _chart.anchorMin = _chart.anchorMax = _chart.pivot = Vector2.zero;
            _chart.anchoredPosition = new Vector2(0f, 10f);
            _chart.sizeDelta = new Vector2(ChartWidth, ChartHeight);
            foreach (double level in new[] { 0.5, 1.0, 1.5 })
            {
                var line = new GameObject($"{level * 100:0}%", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                line.SetParent(_chart, false);
                line.anchorMin = line.anchorMax = line.pivot = Vector2.zero;
                line.anchoredPosition = new Vector2(0f, Height01(level) * ChartHeight);
                line.sizeDelta = new Vector2(ChartWidth, level == 1.0 ? 2f : 1f);
                line.GetComponent<Image>().color = new Color(1f, 1f, 1f, level == 1.0 ? 0.45f : 0.2f);
                line.GetComponent<Image>().raycastTarget = false;
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.rectTransform.SetParent(line, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(-4f, 2f);
                label.rectTransform.sizeDelta = new Vector2(120f, 20f);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 14;
                label.alignment = TextAnchor.LowerRight;
                label.color = new Color(1f, 1f, 1f, 0.6f);
                label.text = level == 1.0 ? "FTP" : $"{level * 100:0}%";
                label.raycastTarget = false;
            }
            _selection = new GameObject("Gekozen", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _selection.SetParent(_chart, false);
            _selection.anchorMin = _selection.anchorMax = _selection.pivot = Vector2.zero;
            _selection.GetComponent<Image>().color = Color.white;
            _selection.GetComponent<Image>().raycastTarget = false;

            _blockInfo = UiFactory.Text(panel, "Blok", 22, -456f, 34f);
            _blockInfo.rectTransform.offsetMin = new Vector2(30f, _blockInfo.rectTransform.offsetMin.y);

            float y = -506f, x = 30f;
            void Btn(string label, float width, UnityEngine.Events.UnityAction action, Color? color = null)
            {
                var (button, text) = UiFactory.Button(panel, label, new Vector2(x, y), width, 44f, action);
                text.text = label;
                if (color.HasValue) button.GetComponent<Image>().color = color.Value;
                x += width + 10f;
            }
            Btn("+ Blok", 120f, () => AddBlock(false));
            Btn("+ Ramp", 120f, () => AddBlock(true));
            Btn("Kopieer blok", 140f, DuplicateSelected);
            Btn("Verwijder blok", 150f, RemoveSelected, new Color(0.55f, 0.2f, 0.2f));
            Btn("Opslaan", 120f, () => Save(), new Color(0.2f, 0.45f, 0.25f));
            Btn("Opslaan en rijden", 180f, SaveAndRide, new Color(0.2f, 0.45f, 0.25f));
            Btn("Map openen", 140f, OpenFolder, new Color(0.35f, 0.35f, 0.4f));
            Btn("Sluiten", 120f, Close, new Color(0.35f, 0.35f, 0.4f));

            _status = UiFactory.Text(panel, "Status", 18, -566f, 40f);
            _status.rectTransform.offsetMin = new Vector2(30f, _status.rectTransform.offsetMin.y);
        }
    }

    /// <summary>Doorgeefluik voor aanwijzen en slepen op een blok of greep in de workout-editor.</summary>
    public sealed class BlockHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action Down, Begin, End;
        public System.Action<PointerEventData> Dragged;

        public void OnPointerDown(PointerEventData e) => Down?.Invoke();
        public void OnBeginDrag(PointerEventData e) => Begin?.Invoke();
        public void OnDrag(PointerEventData e) => Dragged?.Invoke(e);
        public void OnEndDrag(PointerEventData e) => End?.Invoke();
    }

    /// <summary>Een blok als vlak met een schuine bovenkant: links <see cref="StartHeight"/>, rechts <see cref="EndHeight"/>.</summary>
    public sealed class BlockShape : Graphic
    {
        public float StartHeight, EndHeight;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var c = (Color32)color;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMin + StartHeight), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin + EndHeight), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
