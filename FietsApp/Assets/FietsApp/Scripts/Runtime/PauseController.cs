using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Pauzeren (story 6.4): P, de Pauze-toets of de knop rechtsboven zet de rit stil en weer door. Tijdens de pauze
    /// tellen tijd en afstand niet, loopt een workout of FTP-test niet verder, en geeft de trainer geen weerstand.
    /// Opslaan (O, of de knop eronder) bewaart de rit tot hier, zodat hij meteen op Strava komt; daarna neemt de app
    /// verder op als een nieuwe rit.
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RideMenu rideMenu;
        [SerializeField] RideRecorder recorder;

        GameObject _overlay;
        Text _details;
        Text _buttonLabel;

        public bool IsPaused => ride != null && ride.IsHeldBy(this);

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (rideMenu == null) rideMenu = FindAnyObjectByType<RideMenu>();
            if (recorder == null) recorder = FindAnyObjectByType<RideRecorder>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            Refresh();
        }

        void OnEnable() => ride.RideStarting += OnRideStarting;

        void OnDisable() => ride.RideStarting -= OnRideStarting;

        /// <summary>Een nieuwe rit kiezen (menu, wereldkaart) heft de pauze op.</summary>
        void OnRideStarting()
        {
            if (!IsPaused) return;
            ride.Release(this);
            Refresh();
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current; // niet tijdens typen in een tekstveld
            if (kb != null && (kb.pKey.wasPressedThisFrame || kb.pauseKey.wasPressedThisFrame)) Toggle();
            if (kb != null && kb.oKey.wasPressedThisFrame) SaveRide();
            if (IsPaused) Refresh();
        }

        /// <summary>Pauzeren of verder rijden.</summary>
        public void Toggle()
        {
            if (IsPaused) ride.Release(this);
            else ride.Hold(this);
            Refresh();
        }

        /// <summary>De rit tot hier opslaan (en naar Strava); daarna rijd je verder en telt het als een nieuwe rit.</summary>
        public void SaveRide() => recorder?.SaveNow();

        void Refresh()
        {
            bool paused = IsPaused;
            _overlay.SetActive(paused && (rideMenu == null || !rideMenu.IsOpen));
            _buttonLabel.text = paused ? "Verder (P)" : "Pauze (P)";
            if (!paused) return;
            int s = (int)ride.ElapsedSeconds;
            _details.text = $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}   {ride.DistanceM / 1000f:0.00} km\n" +
                            "<size=20><color=#ccc>Tijd en afstand staan stil. Druk op P om verder te rijden,\n" +
                            "of op O om de rit tot hier op te slaan (en naar Strava).</color></size>";
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Pauze", 30);

            var corner = new GameObject("Knop", typeof(RectTransform)).GetComponent<RectTransform>();
            corner.SetParent(canvas, false);
            corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(1f, 1f);
            corner.anchoredPosition = new Vector2(-24f, -24f);
            corner.sizeDelta = new Vector2(150f, 100f);
            corner.gameObject.AddComponent<ScreenFit>(); // groter op een telefoon (story 14.2)
            (_, _buttonLabel) = UiFactory.Button(corner, "Pauzeknop", Vector2.zero, 150f, 44f, Toggle);
            UiFactory.Button(corner, "Opslaanknop", new Vector2(0f, -54f), 150f, 44f, SaveRide).label.text = "Opslaan (O)";

            var panel = UiFactory.Panel(canvas, "Gepauzeerd", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 210f));
            _overlay = panel.gameObject;
            var title = UiFactory.Text(panel, "Titel", 44, -20f, 60f);
            title.alignment = TextAnchor.MiddleCenter;
            title.text = "<b>GEPAUZEERD</b>";
            _details = UiFactory.Text(panel, "Details", 30, -86f, 110f);
            _details.alignment = TextAnchor.MiddleCenter;
        }
    }
}
