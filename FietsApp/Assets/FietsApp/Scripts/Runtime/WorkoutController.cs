using System.Collections.Generic;
using FietsApp.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Gestructureerde workouts in ERG-modus (story 8.2). W opent de lijst (cijfertoetsen kiezen); tijdens de
    /// workout zet dit component elk frame het doelvermogen op de trainer en toont het blok, de resterende tijd
    /// en een blokkengrafiek. N slaat een blok over, X stopt. De tijd loopt alleen zolang je trapt, dus even
    /// stoppen pauzeert de workout. Je rijdt ondertussen gewoon over de gekozen klim.
    /// De lijst toont ook je eigen workouts uit <see cref="WorkoutStore"/>; die bewerk en maak je in de
    /// <see cref="WorkoutEditor"/> (story 8.3).
    /// </summary>
    public sealed class WorkoutController : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RideMenu rideMenu;
        [SerializeField] FtpTestController ftpTest;
        [SerializeField] WorkoutEditor editor;

        const float PanelWidth = 420f, ChartHeight = 70f;
        const float FinishedMessageSeconds = 8f;

        WorkoutPlayer _player;
        GameObject _list, _panel, _donePanel;
        RectTransform _listRect;
        readonly List<GameObject> _listRows = new List<GameObject>();
        readonly List<Workout> _choices = new List<Workout>(); // ingebouwd, dan eigen; cijfertoetsen kiezen hieruit
        Text _listTitle, _title, _step, _next, _done;
        RectTransform _chart, _marker;
        float _doneUntil;

        public bool IsRunning => _player != null && !_player.Finished;
        public bool IsListOpen => _list != null && _list.activeSelf;
        public WorkoutPlayer Player => _player;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (rideMenu == null) rideMenu = FindAnyObjectByType<RideMenu>();
            if (ftpTest == null) ftpTest = FindAnyObjectByType<FtpTestController>();
            if (editor == null) editor = FindAnyObjectByType<WorkoutEditor>();
            UiFactory.EnsureEventSystem();
            BuildUi();
        }

        void Update()
        {
            HandleKeys();
            if (_player == null) return;

            // Alleen tellen als er getrapt wordt en de rit loopt (niet in een menu).
            if (ride.enabled && ride.Trainer.PowerWatts > 0f) _player.Advance(Time.deltaTime);

            if (_player.Finished)
            {
                Stop($"Workout klaar: {_player.Workout.Name}");
                return;
            }
            ride.Trainer.SetErgWatts(ride.enabled ? _player.TargetWatts(ProfileStore.Current.ftpWatts) : 0f); // stil: geen weerstand
            Refresh();
        }

        void HandleKeys()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current; // niet tijdens typen in een tekstveld
            if (kb == null) return;
            if (rideMenu != null && rideMenu.IsOpen) return;
            if (editor != null && editor.IsOpen) return;

            if (kb.wKey.wasPressedThisFrame && (ftpTest == null || !ftpTest.IsRunning)) ShowList(!IsListOpen);
            if (IsListOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame) ShowList(false);
                for (int i = 0; i < _choices.Count && i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) Begin(_choices[i]);
                return;
            }
            if (_player == null) return;
            if (kb.nKey.wasPressedThisFrame) _player.Skip();
            if (kb.xKey.wasPressedThisFrame) Stop($"Workout gestopt: {_player.Workout.Name}");
        }

        public void ShowList(bool show)
        {
            _listTitle.text = "Kies een workout  <size=16><color=#bbb>(W sluit, cijfer kiest; " +
                              $"doelen naar je FTP van {ProfileStore.Current.ftpWatts} W)</color></size>";
            if (show) FillList();
            _list.SetActive(show);
        }

        public void Begin(Workout workout)
        {
            _player = new WorkoutPlayer(workout);
            _list.SetActive(false);
            _panel.SetActive(true);
            _donePanel.SetActive(false);
            _title.text = $"<b>{workout.Name}</b>   <color=#bbb>N volgende blok · X stoppen</color>";
            DrawChart(workout);
        }

        public void Stop(string message)
        {
            _player = null;
            ride.Trainer.SetErgWatts(0f);
            _panel.SetActive(false);
            _done.text = message;
            _donePanel.SetActive(true);
            _doneUntil = Time.time + FinishedMessageSeconds;
        }

        void LateUpdate()
        {
            if (_donePanel.activeSelf && Time.time > _doneUntil) _donePanel.SetActive(false);
        }

        void Refresh()
        {
            int ftp = ProfileStore.Current.ftpWatts;
            var step = _player.Current;
            int watts = _player.TargetWatts(ftp);
            var zone = PowerZones.Of(watts, ftp);
            string paused = ride.Trainer.PowerWatts > 0f ? "" : "   <color=#ff9090>pauze: trap om verder te gaan</color>";
            _step.text = $"{step.Name}  <b><color=#{zone.Rgb:X6}>{watts} W</color></b>   nog {Clock(_player.StepRemaining)}{paused}";
            var next = _player.Next;
            _next.text = next != null
                ? $"<color=#bbb>daarna {next.Name} {(int)System.Math.Round(next.StartFtp * ftp)} W   ·   totaal nog {Clock(_player.Remaining)}</color>"
                : $"<color=#bbb>laatste blok   ·   totaal nog {Clock(_player.Remaining)}</color>";
            float x = (float)(_player.Elapsed / _player.Workout.TotalSeconds) * (PanelWidth - 32f);
            _marker.anchoredPosition = new Vector2(x, 0f);
        }

        static string Clock(double seconds)
        {
            int s = (int)System.Math.Ceiling(seconds);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Workout Canvas", 15);

            // Lijst met workouts, midden in beeld; de rijen komen bij het openen (FillList).
            _listRect = UiFactory.Panel(canvas, "Workoutlijst", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 200f));
            _list = _listRect.gameObject;
            _listTitle = UiFactory.Text(_listRect, "Titel", 28, -14f, 40f);
            _list.SetActive(false);

            // Paneel tijdens de workout, linksboven.
            var panel = UiFactory.Panel(canvas, "Workout", new Vector2(0f, 1f), new Vector2(24f, -24f),
                new Vector2(PanelWidth, 110f + ChartHeight));
            _panel = panel.gameObject;
            _title = UiFactory.Text(panel, "Naam", 18, -8f, 26f);
            _step = UiFactory.Text(panel, "Blok", 24, -34f, 32f);
            _next = UiFactory.Text(panel, "Volgende", 16, -66f, 24f);

            _chart = new GameObject("Grafiek", typeof(RectTransform)).GetComponent<RectTransform>();
            _chart.SetParent(panel, false);
            _chart.anchorMin = _chart.anchorMax = _chart.pivot = new Vector2(0f, 0f);
            _chart.anchoredPosition = new Vector2(16f, 12f);
            _chart.sizeDelta = new Vector2(PanelWidth - 32f, ChartHeight);

            _marker = new GameObject("Nu", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _marker.SetParent(_chart, false);
            _marker.anchorMin = _marker.anchorMax = _marker.pivot = new Vector2(0f, 0f);
            _marker.sizeDelta = new Vector2(3f, ChartHeight + 6f);
            _marker.GetComponent<Image>().color = Color.white;
            _panel.SetActive(false);

            // Melding na afloop, op dezelfde plek.
            var done = UiFactory.Panel(canvas, "Klaar", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(PanelWidth, 44f));
            _donePanel = done.gameObject;
            _done = UiFactory.Text(done, "Tekst", 20, -6f, 32f);
            _donePanel.SetActive(false);
        }

        /// <summary>Vult de lijst: ingebouwde workouts, dan je eigen (met bewerken), dan een knop voor een nieuwe.</summary>
        void FillList()
        {
            foreach (var row in _listRows) Destroy(row);
            _listRows.Clear();
            var own = WorkoutStore.LoadAll();
            _choices.Clear();
            _choices.AddRange(WorkoutLibrary.All);
            foreach (var (w, _) in own) _choices.Add(w);

            float y = -64f;
            void Row(Workout w, string path, int number)
            {
                bool editable = path != null && editor != null;
                var (button, label) = UiFactory.Button(_listRect, w.Name, new Vector2(16f, y), editable ? 600f : 728f, 34f, () => Begin(w));
                button.GetComponent<Image>().color = path != null ? new Color(0.2f, 0.38f, 0.26f, 1f) : new Color(0.16f, 0.3f, 0.5f, 1f);
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                string key = number < 9 ? $"{number + 1}." : "   ";
                label.text = $"{key}  <b>{w.Name}</b>   {Clock(w.TotalSeconds)}   <color=#ddd>{w.Description}</color>";
                _listRows.Add(button.gameObject);
                if (editable)
                {
                    var (edit, editLabel) = UiFactory.Button(_listRect, "Bewerken", new Vector2(626f, y), 118f, 34f, () => Edit(w, path));
                    editLabel.text = "bewerken";
                    edit.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.4f, 1f);
                    _listRows.Add(edit.gameObject);
                }
                y -= 40f;
            }
            for (int i = 0; i < WorkoutLibrary.All.Length; i++) Row(WorkoutLibrary.All[i], null, i);
            for (int i = 0; i < own.Count; i++) Row(own[i].workout, own[i].path, WorkoutLibrary.All.Length + i);

            if (editor != null)
            {
                y -= 6f;
                var (create, createLabel) = UiFactory.Button(_listRect, "Nieuw", new Vector2(16f, y), 728f, 38f, () => Edit(null, null));
                createLabel.text = own.Count == 0
                    ? "+ Nieuwe workout  <color=#ddd>(eigen workouts staan als .zwo in de map Workouts, om te delen)</color>"
                    : "+ Nieuwe workout";
                createLabel.supportRichText = true;
                create.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.25f, 1f);
                _listRows.Add(create.gameObject);
                y -= 44f;
            }
            _listRect.sizeDelta = new Vector2(760f, -y + 16f);
        }

        void Edit(Workout workout, string path)
        {
            _list.SetActive(false);
            editor.Open(workout, path);
        }

        /// <summary>Blokken als staafjes: hoogte naar het doel (tot 130% FTP), kleur naar de zone; ramps in stapjes.</summary>
        void DrawChart(Workout workout)
        {
            var old = new List<GameObject>();
            foreach (Transform child in _chart) if (child != _marker) old.Add(child.gameObject);
            foreach (var go in old) Destroy(go);

            float width = _chart.sizeDelta.x;
            double t = 0;
            foreach (var step in workout.Steps)
            {
                int slices = step.IsRamp ? 6 : 1;
                for (int i = 0; i < slices; i++)
                {
                    double ftp = step.FtpAt((i + 0.5) / slices * step.Seconds);
                    double from = t + step.Seconds * i / slices, to = t + step.Seconds * (i + 1) / slices;
                    var bar = new GameObject("Blok", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    bar.SetParent(_chart, false);
                    bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
                    bar.anchoredPosition = new Vector2((float)(from / workout.TotalSeconds) * width, 0f);
                    bar.sizeDelta = new Vector2(Mathf.Max(1f, (float)((to - from) / workout.TotalSeconds) * width - 1f),
                        Mathf.Clamp01((float)ftp / 1.3f) * ChartHeight);
                    bar.GetComponent<Image>().color = Hud.ZoneColor(PowerZones.Of(ftp, 1.0));
                    bar.GetComponent<Image>().raycastTarget = false;
                }
                t += step.Seconds;
            }
            _marker.SetAsLastSibling();
        }

    }
}
