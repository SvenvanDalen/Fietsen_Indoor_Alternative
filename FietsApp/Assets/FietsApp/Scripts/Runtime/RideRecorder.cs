using System;
using System.IO;
using System.Linq;
using FietsApp.Recording;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Neemt de rit op (story 5.1): elke seconde vermogen, cadans, hartslag, snelheid, afstand, hoogte en de echte
    /// plek op de klim. Slaat de rit op als .fit in <c>Ritten/</c> onder <see cref="Application.persistentDataPath"/>
    /// zodra je de top haalt, een nieuwe rit begint of de app sluit. Pauzes (rit uit, bijvoorbeeld in het menu) tellen niet.
    /// </summary>
    public sealed class RideRecorder : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [Tooltip("Kortere ritten worden niet bewaard (seconden).")]
        [SerializeField, Min(1)] int minimumSeconds = 30;

        RideLog _log = new RideLog();
        string _climbName; // bij de eerste meting vastgelegd: bij een nieuwe klim is de route al gewisseld vóór het opslaan
        float _sinceSample;
        bool _finished;
        string _savedMessage;
        float _savedUntil;

        /// <summary>Waar de ritten staan.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Ritten");

        /// <summary>De metingen van de rit die nu loopt (of net klaar is).</summary>
        public RideLog Log => _log;

        /// <summary>De naam van de klim van deze rit.</summary>
        public string ClimbName => _climbName;

        /// <summary>Aan het eind van de route: de rit is klaar en bewaard (ritsamenvatting, story 5.3).</summary>
        public event Action<RideLog> Finished;

        /// <summary>Een rit is afgesloten en telt mee (lang genoeg); precies één keer per rit, ook als het schrijven mislukt (Everesting, story 9.3).</summary>
        public event Action<RideLog> Closed;

        /// <summary>Het laatst opgeslagen bestand, of null.</summary>
        public string LastSavedPath { get; private set; }

        /// <summary>Korte toestand voor de HUD.</summary>
        public string Status =>
            Time.unscaledTime < _savedUntil ? _savedMessage
            : _log.Count > 0 ? $"opname {_log.Count / 60}:{_log.Count % 60:00}"
            : "";

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
        }

        void OnEnable() => ride.RideStarting += OnRideStarting;

        void OnDisable() => ride.RideStarting -= OnRideStarting;

        void OnApplicationQuit() => Save();

        void Update()
        {
            if (!ride.enabled || _finished) return;
            _sinceSample += Time.deltaTime;
            if (_sinceSample < 1f) return;
            _sinceSample -= 1f;

            var route = routeView.Route;
            float distance = Mathf.Min(ride.DistanceM, route.Length);
            var here = route.Sample(distance);
            double lat = double.NaN, lon = double.NaN;
            var geo = routeView.ToGeo(here.X, here.Z);
            if (geo.HasValue) (lat, lon) = geo.Value;
            var t = ride.Trainer;
            if (_log.Count == 0) _climbName = routeView.Climb != null ? routeView.Climb.displayName : "Oefenklim";
            // Echte hoogte en echte ligging, ook als de klim in de doorlopende wereld verschoven ligt.
            _log.Add(new RideSample(DateTime.UtcNow, t.PowerWatts, t.CadenceRpm, t.HeartRateBpm, ride.SpeedMs,
                distance, routeView.RealElevation(here.Elevation), lat, lon));

            // Aan het eind van de route (de top, of in de wereld terug in het dal) is de rit klaar: bewaren.
            if (ride.DistanceM >= route.Length)
            {
                Save();
                _finished = true;
                Finished?.Invoke(_log);
            }
        }

        void OnRideStarting()
        {
            Save();
            _log = new RideLog();
            _sinceSample = 0f;
            _finished = false;
        }

        /// <summary>Bewaart de rit tot nu toe als .fit; doet niets bij een te korte rit of als hij al bewaard is.</summary>
        public void Save()
        {
            if (_finished || _log.Count < minimumSeconds) return;
            Closed?.Invoke(_log);
            try
            {
                Directory.CreateDirectory(Folder);
                string safe = new string(_climbName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
                string path = Path.Combine(Folder, $"{_log.StartUtc.ToLocalTime():yyyy-MM-dd_HHmm}_{safe}.fit");
                File.WriteAllBytes(path, FitWriter.Write(_log));
                LastSavedPath = path;
                _savedMessage = $"rit opgeslagen: {Path.GetFileName(path)}";
                Debug.Log($"Rit opgeslagen: {path} ({_log.Count} s, {_log.DistanceM / 1000f:0.00} km)");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _savedMessage = "rit opslaan mislukt";
                Debug.LogError($"Rit opslaan mislukt: {e.Message}");
            }
            _savedUntil = Time.unscaledTime + 8f;
        }
    }
}
