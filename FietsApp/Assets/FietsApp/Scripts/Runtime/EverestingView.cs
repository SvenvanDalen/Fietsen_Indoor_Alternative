using FietsApp.Recording;
using FietsApp.Training;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Everesting-uitdaging (story 9.3): linksonder een voortgangsbalk naar 8848 m zolang de uitdaging loopt, met de
    /// hoogtemeters van de huidige rit erbij. Elke afgesloten rit telt mee in het profiel. Starten en stoppen gaat via het
    /// ritmenu (<see cref="Toggle"/>).
    /// </summary>
    public sealed class EverestingView : MonoBehaviour
    {
        [SerializeField] RideRecorder recorder;
        [Tooltip("Hoe lang de melding blijft staan als de 8848 m gehaald is (seconden).")]
        [SerializeField, Min(1f)] float doneSeconds = 15f;

        const float Width = 560f;

        GameObject _bar, _toast;
        RectTransform _fill;
        Text _label, _toastText;
        float _toastUntil, _sinceRefresh;
        RideLog _counted;     // de rit die al is meegeteld; telt daarna niet meer als "huidige rit"
        RideLog _startLog;    // de rit waarin de uitdaging gestart is
        float _ascentAtStart; // en de hoogtemeters daarvan van vóór de start, die niet meetellen

        static EverestingChallenge Challenge => ProfileStore.Current.everesting;
        static string Today => System.DateTime.Now.ToString("yyyy-MM-dd");

        void Awake()
        {
            if (recorder == null) recorder = FindAnyObjectByType<RideRecorder>();
            BuildUi();
        }

        void OnEnable() => recorder.Closed += OnRideClosed;

        void OnDisable() => recorder.Closed -= OnRideClosed;

        void Update()
        {
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) _toast.SetActive(false);
            _bar.SetActive(Challenge.active);
            if (!Challenge.active) return;
            // Hoogtemeters optellen kost een lus over de rit; twee keer per seconde is genoeg.
            _sinceRefresh -= Time.unscaledDeltaTime;
            if (_sinceRefresh > 0f) return;
            _sinceRefresh = 0.5f;
            Refresh();
        }

        /// <summary>Hoogtemeters van de rit die nog loopt en nog niet is meegeteld.</summary>
        float CurrentAscent =>
            recorder.Log == _counted ? 0f : AscentSinceStart(recorder.Log);

        float AscentSinceStart(RideLog log) => Mathf.Max(0f, log.Ascent - (log == _startLog ? _ascentAtStart : 0f));

        void Refresh()
        {
            float current = CurrentAscent;
            _label.text = Challenge.Describe(current);
            _fill.anchorMax = new Vector2(Challenge.Progress(current), 1f);
        }

        void OnRideClosed(RideLog log)
        {
            float ascent = AscentSinceStart(log);
            _counted = log;
            if (!Challenge.active) return;
            bool done = Challenge.AddRide(ascent, Today);
            ProfileStore.Save();
            Refresh();
            if (!done) return;
            _toastText.text = $"<b>Everest gehaald!</b>   {Challenge.Describe()}";
            _toast.SetActive(true);
            _toastUntil = Time.unscaledTime + doneSeconds;
        }

        /// <summary>Start de uitdaging opnieuw bij nul, of stopt hem; de rit die nu loopt telt vanaf hier mee.</summary>
        public void Toggle()
        {
            if (Challenge.active) Challenge.Stop();
            else
            {
                Challenge.Start(Today);
                _startLog = recorder.Log;
                _ascentAtStart = recorder.Log.Ascent;
            }
            ProfileStore.Save();
            _sinceRefresh = 0f;
        }

        public bool IsActive => Challenge.active;

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Everesting Canvas", 12);

            var bar = UiFactory.Panel(canvas, "Everesting", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(Width, 64f));
            _bar = bar.gameObject;
            _label = UiFactory.Text(bar, "Tekst", 20, -4f, 28f);
            _label.alignment = TextAnchor.MiddleCenter;

            var track = new GameObject("Balk", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            track.SetParent(bar, false);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(12f, 10f);
            track.offsetMax = new Vector2(-12f, 28f);
            track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            track.GetComponent<Image>().raycastTarget = false;

            _fill = new GameObject("Voortgang", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _fill.SetParent(track, false);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fill.GetComponent<Image>().color = new Color(1f, 0.82f, 0.3f, 1f);
            _fill.GetComponent<Image>().raycastTarget = false;
            _bar.SetActive(false);

            var toast = UiFactory.Panel(canvas, "Everest gehaald", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(900f, 80f));
            _toast = toast.gameObject;
            _toastText = UiFactory.Text(toast, "Tekst", 26, -4f, 72f);
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = new Color(1f, 0.85f, 0.4f);
            _toast.SetActive(false);
        }
    }
}
