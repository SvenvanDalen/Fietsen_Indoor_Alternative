using FietsApp.Recording;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Ritsamenvatting (story 5.3): op de top een melding met de klimtijd, en aan het eind van de route een paneel met
    /// tijd, afstand, gemiddeld vermogen, klimtijd en hoogtemeters. Enter, Esc of de knop sluit het paneel.
    /// </summary>
    public sealed class RideSummaryView : MonoBehaviour, IPanel
    {
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [SerializeField] RideRecorder recorder;
        [Tooltip("Hoe lang de melding op de top blijft staan (seconden).")]
        [SerializeField, Min(1f)] float summitSeconds = 10f;

        GameObject _panel;
        Text _title, _body;
        GameObject _toast;
        Text _toastText;
        float _toastUntil;
        bool _summitShown;
        string _recordLine; // uitkomst van de klimtijd tegen het record (9.2), voor de samenvatting
        StageController _stage;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (recorder == null) recorder = FindAnyObjectByType<RideRecorder>();
            _stage = FindAnyObjectByType<StageController>();
            UiFactory.EnsureEventSystem();
            BuildUi();
        }

        void OnEnable()
        {
            recorder.Finished += Show;
            ride.RideStarting += OnRideStarting;
        }

        void OnDisable()
        {
            recorder.Finished -= Show;
            ride.RideStarting -= OnRideStarting;
        }

        void OnRideStarting()
        {
            _summitShown = false;
            _recordLine = null;
            _toast.SetActive(false);
            Close();
        }

        void Update()
        {
            // De klimtijd staat vast zodra de opname een meting voorbij de top heeft.
            if (!_summitShown && recorder.Log.SecondsBetween(routeView.ClimbStart, routeView.ClimbEnd) is double climb)
            {
                _summitShown = true;
                float watts = recorder.Log.AveragePowerBetween(routeView.ClimbStart, routeView.ClimbEnd);
                _recordLine = RegisterClimbTime(climb);
                _toastText.text = $"<b>Top bereikt!</b>   klimtijd {Clock(climb)}   gemiddeld {watts:0} W" +
                                  (_recordLine != null ? $"\n<size=22>{_recordLine}</size>" : "");
                _toast.SetActive(true);
                _toastUntil = Time.unscaledTime + summitSeconds;
            }
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) _toast.SetActive(false);

            var kb = UiFactory.IsTyping ? null : Keyboard.current; // niet tijdens typen in een tekstveld
            if (IsOpen && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                Close();
        }

        /// <summary>Toont de samenvatting van een afgelopen rit.</summary>
        public void Show(RideLog log)
        {
            if (log.Count == 0) return;
            if (_stage != null && _stage.IsRunning) return; // etappe: één samenvatting na de laatste klim (ShowStage)
            string name = recorder.ClimbName ?? "Rit";
            var climb = log.SecondsBetween(routeView.ClimbStart, routeView.ClimbEnd);
            _title.text = $"<b>{name}</b>  <color=#bbb>ritsamenvatting</color>";
            _body.text = RideSummaryText.Of(log, climb, log.AveragePowerBetween(routeView.ClimbStart, routeView.ClimbEnd)) +
                         (_recordLine != null ? $"\n<color=#bbb>Record:</color>  <b>{_recordLine}</b>" : "");
            _panel.SetActive(true);
        }

        /// <summary>Toont de samenvatting van een hele etappe (story 4.7).</summary>
        public void ShowStage(string title, string body)
        {
            _title.text = title;
            _body.text = body;
            _panel.SetActive(true);
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        static string Clock(double seconds) => RideSummaryText.Clock(seconds);

        /// <summary>Legt de klimtijd vast in het profiel (alleen voor een echte klim) en beschrijft hoe hij zich tot het record verhoudt.</summary>
        string RegisterClimbTime(double seconds)
        {
            var climb = routeView.Climb;
            if (climb == null) return null; // oefenklim
            string today = System.DateTime.Now.ToString("yyyy-MM-dd");
            var result = ProfileStore.Current.AddClimbTime(climb.displayName, seconds, today);
            ProfileStore.Save();
            // Eerste keer of nieuw record: dit wordt de ghost voor de volgende keer (9.4).
            if (result.IsFirst || result.IsRecord)
            {
                var trace = GhostTrace.FromLog(recorder.Log, routeView.ClimbStart, routeView.ClimbEnd, climb.displayName, today);
                if (trace != null) GhostStore.Save(climb.name, trace);
            }
            return result.IsRecord ? $"<color=#ffd060>{result.Describe()}</color>" : result.Describe();
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Ritsamenvatting", 35);

            var toast = UiFactory.Panel(canvas, "Top", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(860f, 92f));
            _toast = toast.gameObject;
            _toastText = UiFactory.Text(toast, "Tekst", 28, -4f, 84f);
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = new Color(1f, 0.85f, 0.4f);
            _toast.SetActive(false);

            var panel = UiFactory.Panel(canvas, "Samenvatting", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 420f));
            _panel = panel.gameObject;
            _title = UiFactory.Text(panel, "Titel", 32, -16f, 50f);
            _body = UiFactory.Text(panel, "Cijfers", 26, -76f, 270f);
            _body.alignment = TextAnchor.UpperLeft;
            _body.lineSpacing = 1.25f;
            UiFactory.Button(panel, "Sluiten", new Vector2(240f, -350f), 160f, 48f, Close).label.text = "Sluiten (Enter)";
            _panel.SetActive(false);
        }
    }
}
