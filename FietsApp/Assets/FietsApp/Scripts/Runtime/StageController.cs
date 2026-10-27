using System.Collections.Generic;
using FietsApp.Climbs;
using FietsApp.Recording;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Etappe (story 4.7): rijdt 2-4 klimmen uit één gebied na elkaar. Elke klim is een eigen rit door de wereld
    /// (knooppunt → klim → afdaling → knooppunt); terug in het dal begint meteen de volgende vanaf hetzelfde knooppunt.
    /// Rechtsboven staat welke klim je rijdt; na de laatste toont de ritsamenvatting de hele etappe.
    /// Kies je zelf een andere klim, dan stopt de etappe.
    /// </summary>
    public sealed class StageController : MonoBehaviour
    {
        [SerializeField] RideMenu rideMenu;
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [SerializeField] RideRecorder recorder;
        [SerializeField] RideSummaryView summary;
        [Tooltip("Hoe lang de melding van de volgende klim blijft staan (seconden).")]
        [SerializeField, Min(1f)] float nextSeconds = 8f;

        readonly List<Climb> _climbs = new List<Climb>();
        readonly List<StageLeg> _legs = new List<StageLeg>();
        int _index = -1;
        bool _legDone, _switching;
        GameObject _status, _toast;
        Text _statusText, _toastText;
        float _toastUntil;

        /// <summary>Of er een etappe loopt; de ritsamenvatting wacht dan tot de laatste klim.</summary>
        public bool IsRunning => _index >= 0;

        void Awake()
        {
            if (rideMenu == null) rideMenu = FindAnyObjectByType<RideMenu>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (recorder == null) recorder = FindAnyObjectByType<RideRecorder>();
            if (summary == null) summary = FindAnyObjectByType<RideSummaryView>();
            BuildUi();
        }

        void OnEnable()
        {
            recorder.Finished += OnLegFinished;
            ride.RideStarting += OnRideStarting;
        }

        void OnDisable()
        {
            recorder.Finished -= OnLegFinished;
            ride.RideStarting -= OnRideStarting;
        }

        /// <summary>Begint een etappe over <paramref name="climbs"/>, in deze volgorde.</summary>
        public void Begin(IReadOnlyList<Climb> climbs)
        {
            _climbs.Clear();
            _climbs.AddRange(climbs);
            _legs.Clear();
            _index = 0;
            StartLeg();
        }

        void StartLeg()
        {
            _switching = true;
            rideMenu.Ride(_climbs[_index]);
            _switching = false;
            ShowStatus();
            _toastText.text = _index == 0
                ? $"<b>Etappe</b>: {_climbs.Count} klimmen, eerst {_climbs[0].Name}"
                : $"<b>Klim {_index + 1}/{_climbs.Count}</b>: op naar {_climbs[_index].Name}";
            _toast.SetActive(true);
            _toastUntil = Time.unscaledTime + nextSeconds;
        }

        void OnRideStarting()
        {
            // Zelf een andere klim gekozen: de etappe stopt. Dezelfde klim opnieuw beginnen mag.
            if (!_switching && IsRunning && routeView.Climb != _climbs[_index]) Stop();
        }

        void OnLegFinished(RideLog log)
        {
            if (!IsRunning) return;
            _legs.Add(new StageLeg(_climbs[_index].Name, log, log.SecondsBetween(routeView.ClimbStart, routeView.ClimbEnd)));
            _legDone = true; // pas in Update verder, zodat iedereen die op Finished wacht deze klim nog afhandelt
        }

        void Update()
        {
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) _toast.SetActive(false);
            if (!_legDone) return;
            _legDone = false;
            if (_index + 1 < _climbs.Count)
            {
                _index++;
                StartLeg();
                return;
            }
            var legs = new List<StageLeg>(_legs);
            Stop();
            _toast.SetActive(false); // de samenvatting komt op dezelfde plek
            summary.ShowStage($"<b>Etappe</b>  <color=#bbb>{legs.Count} klimmen</color>", StageSummaryText.Of(legs));
        }

        void Stop()
        {
            _index = -1;
            _legDone = false;
            _status.SetActive(false);
        }

        void ShowStatus()
        {
            _statusText.text = $"Etappe {_index + 1}/{_climbs.Count}: <b>{_climbs[_index].Name}</b>" +
                               (_index + 1 < _climbs.Count ? $"\n<size=16><color=#bbb>daarna {_climbs[_index + 1].Name}</color></size>" : "");
            _status.SetActive(true);
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Etappe Canvas", 13);

            // Rechtsboven, onder de pauzeknop.
            var status = UiFactory.Panel(canvas, "Etappe", new Vector2(1f, 1f), new Vector2(-24f, -84f), new Vector2(440f, 64f));
            _status = status.gameObject;
            _statusText = UiFactory.Text(status, "Tekst", 20, -4f, 56f);
            _statusText.alignment = TextAnchor.MiddleRight;
            _status.SetActive(false);

            var toast = UiFactory.Panel(canvas, "Volgende klim", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(860f, 70f));
            _toast = toast.gameObject;
            _toastText = UiFactory.Text(toast, "Tekst", 26, -4f, 62f);
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = new Color(0.7f, 1f, 0.75f);
            _toast.SetActive(false);
        }
    }
}
