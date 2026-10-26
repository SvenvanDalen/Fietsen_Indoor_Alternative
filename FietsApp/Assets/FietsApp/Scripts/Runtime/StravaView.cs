using FietsApp.Strava;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Strava-paneel (story 5.2): S opent het. Client ID en Client Secret van je eigen Strava-API-app invullen, koppelen
    /// via de browser, en zien wat er naar Strava gaat. De rit staat stil zolang het paneel open is.
    /// </summary>
    public sealed class StravaView : MonoBehaviour
    {
        [SerializeField] StravaAccount strava;
        [SerializeField] RideController ride;

        const string SecretMask = "********";

        GameObject _panel;
        Text _status, _connectLabel, _uploadLabel, _olderLabel;
        InputField _clientId, _secret;
        Button _upload, _older;
        float _countsAt = -1f;
        int _pending, _olderCount;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (strava == null) strava = FindAnyObjectByType<StravaAccount>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current;
            if (kb != null && kb.sKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            Refresh();
        }

        public void Open()
        {
            _panel.SetActive(true);
            ride.Hold(this);
            _clientId.text = strava.ClientId;
            _secret.text = strava.HasAppKeys ? SecretMask : "";
            _countsAt = -1f;
            Refresh();
        }

        public void Close()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            _panel.SetActive(false);
            ride.Release(this);
        }

        void Refresh()
        {
            // De ritten-map niet elk frame doorzoeken.
            if (Time.unscaledTime >= _countsAt)
            {
                _pending = strava.PendingCount;
                _olderCount = strava.OlderCount;
                _countsAt = Time.unscaledTime + 1f;
            }
            _status.text = strava.Status;
            _connectLabel.text = strava.IsConnected ? "Ontkoppelen" : strava.IsBusy ? "Annuleren" : "Koppelen met Strava";
            _upload.gameObject.SetActive(strava.IsConnected);
            _upload.interactable = !strava.IsBusy && _pending > 0;
            _uploadLabel.text = _pending > 0 ? $"Nu uploaden ({_pending})" : "Alles staat op Strava";
            _older.gameObject.SetActive(strava.IsConnected && _olderCount > 0);
            _older.interactable = !strava.IsBusy;
            _olderLabel.text = $"Oudere ritten ook uploaden ({_olderCount})";
        }

        void ConnectButton()
        {
            if (strava.IsConnected) { strava.Disconnect(); return; }
            if (strava.IsBusy) { strava.CancelConnect(); return; }
            // Het bewaarde secret staat gemaskeerd in het veld; alleen een nieuw ingetikt secret vervangt het.
            strava.SetAppKeys(_clientId.text, _secret.text == SecretMask ? null : _secret.text);
            strava.Connect();
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Strava Canvas", 46);
            var panel = UiFactory.Panel(canvas, "Strava", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 600f));
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
            _panel = panel.gameObject;

            UiFactory.Text(panel, "Titel", 30, -14f, 40f).text = "Strava  <size=16><color=#bbb>(S of Esc sluit)</color></size>";
            _status = UiFactory.Text(panel, "Status", 20, -60f, 30f);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            var help = UiFactory.Text(panel, "Uitleg", 17, -100f, 150f);
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            help.alignment = TextAnchor.UpperLeft;
            help.color = new Color(1f, 1f, 1f, 0.8f);
            help.text =
                "Eenmalig:\n" +
                "1. Maak een API-app op <b>strava.com/settings/api</b>. Vul bij <i>Authorization Callback Domain</i> in: <b>localhost</b>.\n" +
                "2. Neem hieronder de <i>Client ID</i> en het <i>Client Secret</i> over. Ze blijven alleen op deze pc.\n" +
                "3. Klik op Koppelen en bevestig in je browser.\n" +
                "Daarna gaat elke rit vanzelf naar Strava, als virtuele rit.";
            UiFactory.Button(panel, "Strava-instellingen", new Vector2(560f, -100f), 244f, 36f,
                () => Application.OpenURL(StravaApi.SettingsUrl)).label.text = "strava.com/settings/api";

            UiFactory.Text(panel, "Client ID", 18, -264f, 40f).text = "Client ID";
            _clientId = UiFactory.TextInput(panel, "Client ID veld", new Vector2(200f, -264f), 360f, 40f, "bijvoorbeeld 123456");
            _clientId.contentType = InputField.ContentType.IntegerNumber;
            UiFactory.Text(panel, "Client Secret", 18, -316f, 40f).text = "Client Secret";
            _secret = UiFactory.TextInput(panel, "Client Secret veld", new Vector2(200f, -316f), 600f, 40f, "40 tekens van strava.com/settings/api");
            _secret.contentType = InputField.ContentType.Password;

            var (connect, connectLabel) = UiFactory.Button(panel, "Koppelen", new Vector2(16f, -380f), 300f, 50f, ConnectButton);
            connect.GetComponent<Image>().color = new Color(0.99f, 0.32f, 0.0f, 1f); // Strava-oranje
            _connectLabel = connectLabel;

            (_upload, _uploadLabel) = UiFactory.Button(panel, "Nu uploaden", new Vector2(330f, -380f), 220f, 50f, () => strava.UploadNow());
            (_older, _olderLabel) = UiFactory.Button(panel, "Oudere ritten", new Vector2(16f, -444f), 534f, 44f, () => strava.UploadOlder());
            _older.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

            var note = UiFactory.Text(panel, "Let op", 15, -510f, 70f);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.alignment = TextAnchor.UpperLeft;
            note.color = new Color(1f, 1f, 1f, 0.6f);
            note.text = "Ritten van vóór het koppelen gaan alleen mee als je daarom vraagt. Staat een rit al op Strava, " +
                        "dan herkent Strava hem en komt hij er niet dubbel op. Een rit die bij het afsluiten wordt bewaard, " +
                        "gaat bij de volgende start.";
        }
    }
}
