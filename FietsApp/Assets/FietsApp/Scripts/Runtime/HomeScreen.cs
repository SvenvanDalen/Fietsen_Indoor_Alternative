using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FietsApp.Bluetooth;
using FietsApp.Strava;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Startscherm: opent bij het starten (in plaats van het ritmenu) en met T. Knoppen naar rijden, workouts, de FTP-test,
    /// de wereldkaart, de trainer, je profiel en Strava; rechts je profiel, de Strava-stand en je laatste ritten. Een keuze
    /// sluit het scherm. Zolang het open is staat de rit stil. Maakt zichzelf (met de Strava-koppeling) aan in elke scène
    /// met een <see cref="RideController"/>, zodat er niets in de scène hoeft te worden gezet.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour, IPanel
    {
        const int RecentRides = 7;
        static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
        static readonly Color Green = new Color(0.35f, 0.8f, 0.4f), Orange = new Color(0.99f, 0.32f, 0f);

        RideController _ride;
        StravaAccount _strava;
        GameObject _panel;
        Text _profile, _stravaStatus, _noRides;
        readonly List<(Button button, Text label)> _rows = new List<(Button, Text)>();
        readonly List<long> _rowActivities = new List<long>();
        float _refreshAt;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<RideController>() == null || FindAnyObjectByType<HomeScreen>() != null) return;
            var go = new GameObject("Startscherm");
            go.AddComponent<StravaAccount>();
            go.AddComponent<StravaView>();
            go.AddComponent<HomeScreen>();
        }

        void Awake()
        {
            _ride = FindAnyObjectByType<RideController>();
            _strava = FindAnyObjectByType<StravaAccount>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        void Start() => Open();

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || AnyShortcut(kb))) { Close(); return; }
            if (Time.unscaledTime >= _refreshAt) Refresh();
        }

        /// <summary>De sneltoetsen van de knoppen: hun eigen paneel opent dan, het startscherm gaat dicht.</summary>
        static bool AnyShortcut(Keyboard kb)
        {
            foreach (var key in new[] { Key.M, Key.W, Key.F, Key.K, Key.B, Key.A, Key.S })
                if (kb[key].wasPressedThisFrame) return true;
            return false;
        }

        public void Open()
        {
            _panel.SetActive(true);
            _ride.Hold(this);
            Refresh();
        }

        public void Close()
        {
            _panel.SetActive(false);
            _ride.Release(this);
        }

        /// <summary>Sluit het startscherm en opent iets anders.</summary>
        void Go<T>(System.Action<T> open) where T : Object
        {
            var target = FindAnyObjectByType<T>();
            Close();
            if (target != null) open(target);
        }

        void Refresh()
        {
            _refreshAt = Time.unscaledTime + 2f;
            var p = ProfileStore.Current;
            _profile.text = $"FTP <b>{p.ftpWatts} W</b>   ·   gewicht <b>{p.weightKg:0} kg</b>" +
                            (p.maxHeartRateBpm > 0 ? $"   ·   max hartslag <b>{p.maxHeartRateBpm}</b>" : "");

            _stravaStatus.text = _strava.IsConnected
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(Orange)}>Strava</color>  {_strava.Status}"
                : "<color=#bbb>Strava niet gekoppeld; ritten blijven op deze pc. Koppel via de knop Strava (S).</color>";

            var rides = StravaAccount.RideFiles().OrderByDescending(f => f.WrittenUtc).Take(RecentRides).ToList();
            _noRides.gameObject.SetActive(rides.Count == 0);
            _rowActivities.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                var (button, label) = _rows[i];
                button.gameObject.SetActive(i < rides.Count);
                if (i >= rides.Count) continue;
                string name = rides[i].Name;
                string when = RideFileName.TryParse(name, out var local, out var climb)
                    ? local.ToString("ddd d MMM  HH:mm", Dutch)
                    : rides[i].WrittenUtc.ToLocalTime().ToString("ddd d MMM  HH:mm", Dutch);
                long activity = 0;
                string mark = "";
                if (_strava.TryGetUpload(name, out activity, out var error))
                    mark = activity > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(Green)}>op Strava ›</color>"
                         : error.Length > 0 ? "<color=#e66>Strava weigerde</color>"
                         : "<color=#bbb>Strava verwerkt...</color>";
                else if (_strava.IsConnected && _strava.IsBusy) mark = "<color=#bbb>uploaden...</color>";
                _rowActivities.Add(activity);
                button.interactable = activity > 0;
                label.text = $"<color=#bbb>{when}</color>   {climb}    {mark}";
            }
        }

        void OpenActivity(int row)
        {
            if (row < _rowActivities.Count && _rowActivities[row] > 0)
                Application.OpenURL(StravaApi.ActivityUrl(_rowActivities[row]));
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Startscherm Canvas", 45);
            var panel = UiFactory.Panel(canvas, "Startscherm", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 760f));
            panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.92f);
            _panel = panel.gameObject;

            UiFactory.Text(panel, "Titel", 60, -24f, 76f).text = "<b>FietsApp</b>";
            var sub = UiFactory.Text(panel, "Ondertitel", 20, -100f, 30f);
            sub.color = new Color(1f, 1f, 1f, 0.7f);
            sub.text = $"Klimmen op je trainer   <size=15>versie {Application.version}</size>";

            // Links: wat je kunt doen.
            var actions = new (string label, UnityEngine.Events.UnityAction action)[]
            {
                ("Rijden: kies een klim  (M)", () => Go<RideMenu>(m => m.Open())),
                ("Workout  (W)", () => Go<WorkoutController>(w => w.ShowList(true))),
                ("FTP-test  (F)", () => Go<FtpTestController>(f => f.ShowChoice(true))),
                ("Wereldkaart  (K)", () => Go<WorldMapView>(m => m.Open())),
                ("Trainer verbinden  (B)", () => Go<DeviceScanPanel>(d => d.Show())),
                ("Account en profiel  (A)", () => Go<AccountView>(a => a.Open())),
                ("Strava  (S)", () => Go<StravaView>(s => s.Open())),
            };
            float y = -160f;
            for (int i = 0; i < actions.Length; i++)
            {
                var (button, label) = UiFactory.Button(panel, actions[i].label, new Vector2(32f, y), 420f, i == 0 ? 72f : 58f, actions[i].action);
                label.text = actions[i].label;
                label.fontSize = i == 0 ? 24 : 20;
                if (i > 0) button.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.28f, 1f);
                if (i == actions.Length - 1) button.GetComponent<Image>().color = Orange;
                y -= (i == 0 ? 72f : 58f) + 12f;
            }

            // Rechts: profiel, Strava en de laatste ritten.
            var right = new GameObject("Rechts", typeof(RectTransform)).GetComponent<RectTransform>();
            right.SetParent(panel, false);
            right.anchorMin = right.anchorMax = right.pivot = new Vector2(0f, 1f);
            right.anchoredPosition = new Vector2(490f, -150f);
            right.sizeDelta = new Vector2(730f, 580f);

            UiFactory.Text(right, "Profiel kop", 22, 0f, 32f).text = "<b>Jouw profiel</b>";
            _profile = UiFactory.Text(right, "Profiel", 19, -34f, 30f);
            _stravaStatus = UiFactory.Text(right, "Strava", 18, -80f, 56f);
            _stravaStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Text(right, "Ritten kop", 22, -150f, 32f).text = "<b>Laatste ritten</b>";
            _noRides = UiFactory.Text(right, "Geen ritten", 18, -190f, 30f);
            _noRides.color = new Color(1f, 1f, 1f, 0.6f);
            _noRides.text = "Nog geen ritten. Kies links Rijden om te beginnen.";
            for (int i = 0; i < RecentRides; i++)
            {
                int row = i;
                var (button, label) = UiFactory.Button(right, $"Rit {i + 1}", new Vector2(16f, -188f - i * 46f), 698f, 40f, () => OpenActivity(row));
                button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
                var colors = button.colors;
                colors.disabledColor = Color.white; // een rit zonder activiteit is niet klikbaar maar wel goed leesbaar
                button.colors = colors;
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(14f, 0f);
                label.supportRichText = true;
                _rows.Add((button, label));
            }
            UiFactory.Button(right, "Ritten-map", new Vector2(16f, -520f), 240f, 38f,
                () => Application.OpenURL("file:///" + RideRecorder.Folder.Replace('\\', '/'))).label.text = "Ritten-map openen";

            var footer = UiFactory.Text(panel, "Voet", 15, -720f, 30f);
            footer.color = new Color(1f, 1f, 1f, 0.5f);
            footer.text = "T opent dit scherm   ·   Esc sluit het en je rijdt verder";
        }
    }
}
