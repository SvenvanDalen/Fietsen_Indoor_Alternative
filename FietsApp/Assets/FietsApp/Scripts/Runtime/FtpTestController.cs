using FietsApp.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// FTP-test (story 8.1). F opent de keuze: 1 = ramptest, 2 = 20-minutentest. Tijdens de test zet dit component
    /// het ERG-doel op de trainer en toont fase, doel en tijd; X stopt (bij de ramptest het normale einde). Na
    /// afloop: Enter bewaart de nieuwe FTP in het profiel, Esc niet. Niet tegelijk met een workout.
    /// </summary>
    public sealed class FtpTestController : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RideMenu rideMenu;
        [SerializeField] WorkoutController workouts;

        const float PanelWidth = 460f;

        IFtpTest _test;
        GameObject _choice, _panel;
        Text _choiceTitle, _title, _phase, _instruction;
        bool _awaitingSave;

        public bool IsRunning => _test != null;
        public IFtpTest Test => _test;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (rideMenu == null) rideMenu = FindAnyObjectByType<RideMenu>();
            if (workouts == null) workouts = FindAnyObjectByType<WorkoutController>();
            BuildUi();
        }

        void Update()
        {
            HandleKeys();
            if (_test == null || _awaitingSave) return;

            if (ride.enabled) _test.Advance(Time.deltaTime, ride.Trainer.PowerWatts);
            if (_test.Finished)
            {
                Finish();
                return;
            }
            ride.Trainer.SetErgWatts(_test.ErgWatts);
            int s = (int)System.Math.Ceiling(_test.PhaseRemaining);
            string erg = _test.ErgWatts > 0 ? $"<b>{_test.ErgWatts} W</b>" : "<b>vrij</b>";
            _phase.text = $"{_test.PhaseName}  {erg}   nog {s / 60}:{s % 60:00}";
            _instruction.text = $"<color=#ddd>{_test.Instruction}</color>";
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null || (rideMenu != null && rideMenu.IsOpen)) return;

            if (_awaitingSave)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Close(save: true);
                else if (kb.escapeKey.wasPressedThisFrame) Close(save: false);
                return;
            }
            if (_test != null)
            {
                if (kb.xKey.wasPressedThisFrame) _test.Stop();
                return;
            }
            bool busy = workouts != null && (workouts.IsRunning || workouts.IsListOpen);
            if (kb.fKey.wasPressedThisFrame && !busy) ShowChoice(!_choice.activeSelf);
            if (!_choice.activeSelf) return;
            if (kb.escapeKey.wasPressedThisFrame) ShowChoice(false);
            if (kb.digit1Key.wasPressedThisFrame) Begin(new RampTest());
            if (kb.digit2Key.wasPressedThisFrame) Begin(new TwentyMinuteTest(ProfileStore.Current.ftpWatts));
        }

        void ShowChoice(bool show)
        {
            var profile = ProfileStore.Current;
            string tested = profile.ftpTestedOn.Length > 0 ? $"getest op {profile.ftpTestedOn}" : "nog niet getest";
            _choiceTitle.text = $"FTP-test  <size=16><color=#bbb>(nu {profile.ftpWatts} W, {tested}; F sluit)</color></size>";
            _choice.SetActive(show);
        }

        public void Begin(IFtpTest test)
        {
            _test = test;
            _awaitingSave = false;
            _choice.SetActive(false);
            _panel.SetActive(true);
            _title.text = $"<b>{test.Name}</b>   <color=#bbb>X stoppen</color>";
        }

        void Finish()
        {
            ride.Trainer.SetErgWatts(0f);
            _title.text = $"<b>{_test.Name}</b>   <color=#bbb>klaar</color>";
            int old = ProfileStore.Current.ftpWatts;
            if (_test.ResultFtp is int ftp)
            {
                _awaitingSave = true;
                _phase.text = $"Nieuwe FTP: <b>{ftp} W</b>  <color=#bbb>(was {old} W)</color>";
                _instruction.text = $"<color=#ddd>{_test.Instruction}.</color>  Enter = opslaan, Esc = niet opslaan";
            }
            else
            {
                _awaitingSave = true;
                _phase.text = "Geen uitslag";
                _instruction.text = "<color=#ddd>De test was te kort voor een FTP.</color>  Esc = sluiten";
            }
        }

        void Close(bool save)
        {
            if (save && _test?.ResultFtp is int ftp)
            {
                ProfileStore.Current.ftpWatts = ftp;
                ProfileStore.Current.ftpTestedOn = System.DateTime.Today.ToString("yyyy-MM-dd");
                ProfileStore.Save();
            }
            _test = null;
            _awaitingSave = false;
            _panel.SetActive(false);
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "FTP-test Canvas", 16);

            var choice = UiFactory.Panel(canvas, "Keuze", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 196f));
            _choice = choice.gameObject;
            _choiceTitle = UiFactory.Text(choice, "Titel", 28, -14f, 40f);
            AddOption(choice, -64f, "1.  <b>Ramptest</b>   ±20 min   <color=#ddd>elke minuut 20 W zwaarder tot je stopt; FTP = 75% van je beste minuut</color>",
                () => Begin(new RampTest()));
            AddOption(choice, -110f, "2.  <b>20-minutentest</b>   35 min   <color=#ddd>opwarmen, 20 min voluit, uitrijden; FTP = 95% van het gemiddelde</color>",
                () => Begin(new TwentyMinuteTest(ProfileStore.Current.ftpWatts)));
            UiFactory.Text(choice, "Uitleg", 15, -152f, 28f).text =
                "<color=#bbb>Tijdens opwarmen en de ramp regelt de trainer het vermogen (ERG); bij de 20 minuten rijd je vrij.</color>";
            _choice.SetActive(false);

            var panel = UiFactory.Panel(canvas, "Test", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(PanelWidth, 100f));
            _panel = panel.gameObject;
            _title = UiFactory.Text(panel, "Naam", 18, -8f, 26f);
            _phase = UiFactory.Text(panel, "Fase", 24, -34f, 32f);
            _instruction = UiFactory.Text(panel, "Uitleg", 16, -66f, 26f);
            _panel.SetActive(false);
        }

        static void AddOption(RectTransform parent, float y, string text, UnityEngine.Events.UnityAction onClick)
        {
            var (button, label) = UiFactory.Button(parent, "Optie", new Vector2(16f, y), 728f, 38f, onClick);
            button.GetComponent<Image>().color = new Color(0.16f, 0.3f, 0.5f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.text = text;
        }

    }
}
