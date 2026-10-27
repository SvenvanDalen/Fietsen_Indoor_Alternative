using FietsApp.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Account en profiel (stories 7.1 en 7.2): A opent het paneel. Inloggen met GitHub, synchroniseren, en gewicht, max
    /// hartslag en FTP instellen. Elke wijziging gaat via <see cref="ProfileStore.Save"/> en daarmee na een paar
    /// seconden naar de Gist. De rit staat stil zolang het paneel open is.
    /// </summary>
    public sealed class AccountView : MonoBehaviour, IPanel
    {
        [SerializeField] GitHubAccount account;
        [SerializeField] RideController ride;

        GameObject _panel, _codeBox;
        Text _status, _code, _weight, _heartRate, _ftp, _signLabel;
        Button _syncButton;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (account == null) account = FindAnyObjectByType<GitHubAccount>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current;
            if (kb != null && kb.aKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            Refresh();
        }

        public void Open()
        {
            _panel.SetActive(true);
            ride.Hold(this);
            Refresh();
        }

        public void Close()
        {
            _panel.SetActive(false);
            ride.Release(this);
        }

        void Refresh()
        {
            var p = ProfileStore.Current;
            _weight.text = $"{p.weightKg:0} kg";
            _heartRate.text = p.maxHeartRateBpm > 0 ? $"{p.maxHeartRateBpm} bpm" : "onbekend";
            _ftp.text = $"{p.ftpWatts} W" + (p.ftpTestedOn.Length > 0 ? $"  <size=15><color=#bbb>getest {p.ftpTestedOn}</color></size>" : "");

            var code = account.PendingCode;
            _codeBox.SetActive(code != null);
            if (code != null) _code.text = $"Ga naar <b>{code.VerificationUri}</b> en tik in:\n<size=44><b>{code.UserCode}</b></size>";
            _signLabel.text = account.IsSignedIn ? "Uitloggen" : code != null ? "Annuleren" : "Inloggen met GitHub";
            _syncButton.gameObject.SetActive(account.IsSignedIn);
            _syncButton.interactable = !account.IsBusy;
            _status.text = account.IsSignedIn || code != null || account.Status.Length > 0
                ? account.Status
                : "niet ingelogd";
        }

        void SignButton()
        {
            if (account.IsSignedIn) account.SignOut();
            else if (account.PendingCode != null) account.CancelSignIn();
            else account.SignIn();
        }

        static void Change(System.Action<RiderProfile> change)
        {
            change(ProfileStore.Current);
            ProfileStore.Save();
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Account Canvas", 26);
            var panel = UiFactory.Panel(canvas, "Account", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 520f));
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            _panel = panel.gameObject;

            UiFactory.Text(panel, "Titel", 30, -14f, 40f).text = "Account en profiel  <size=16><color=#bbb>(A of Esc sluit)</color></size>";
            _status = UiFactory.Text(panel, "Status", 20, -60f, 30f);

            var (signButton, signLabel) = UiFactory.Button(panel, "Inloggen", new Vector2(16f, -100f), 260f, 46f, SignButton);
            signButton.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);
            _signLabel = signLabel;
            var (sync, syncLabel) = UiFactory.Button(panel, "Synchroniseren", new Vector2(290f, -100f), 220f, 46f, () => account.SyncNow());
            syncLabel.text = "Nu synchroniseren";
            _syncButton = sync;
            UiFactory.Button(panel, "Avatar", new Vector2(524f, -100f), 240f, 46f, () =>
            {
                Close();
                FindAnyObjectByType<AvatarView>()?.Open();
            }).label.text = "Avatar en fiets (V)";

            // Code voor github.com/login/device, alleen tijdens het inloggen.
            var box = new GameObject("Code", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            box.SetParent(panel, false);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(16f, -160f);
            box.sizeDelta = new Vector2(748f, 110f);
            box.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.25f, 0.6f);
            _codeBox = box.gameObject;
            _code = UiFactory.Text(box, "Tekst", 20, -6f, 98f);
            _code.alignment = TextAnchor.MiddleCenter;
            UiFactory.Button(box, "Opnieuw openen", new Vector2(600f, -66f), 136f, 34f,
                () => { if (account.PendingCode != null) Application.OpenURL(account.PendingCode.VerificationUri); }).label.text = "pagina openen";
            _codeBox.SetActive(false);

            float y = -290f;
            _weight = Row(panel, "Gewicht", ref y,
                () => Change(p => p.weightKg = Mathf.Max(RiderProfile.MinWeightKg, Mathf.Round(p.weightKg) - 1f)),
                () => Change(p => p.weightKg = Mathf.Min(RiderProfile.MaxWeightKg, Mathf.Round(p.weightKg) + 1f)));
            _heartRate = Row(panel, "Max hartslag", ref y,
                () => Change(p => p.maxHeartRateBpm = p.maxHeartRateBpm <= 100 ? 0 : p.maxHeartRateBpm - 1),
                () => Change(p => p.maxHeartRateBpm = p.maxHeartRateBpm <= 0 ? 185 : Mathf.Min(230, p.maxHeartRateBpm + 1)));
            _ftp = Row(panel, "FTP", ref y,
                () => Change(p => p.ftpWatts = Mathf.Max(50, p.ftpWatts - 5)),
                () => Change(p => p.ftpWatts = Mathf.Min(600, p.ftpWatts + 5)));

            var note = UiFactory.Text(panel, "Uitleg", 15, -470f, 40f);
            note.color = new Color(1f, 1f, 1f, 0.65f);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.text = "Je profiel staat als privé-Gist in je eigen GitHub-account en gaat zo mee naar je andere apparaten. " +
                        "Wachtwoord vergeten? Dat herstel je op github.com.";
        }

        static Text Row(RectTransform panel, string label, ref float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            var name = UiFactory.Text(panel, label, 22, y, 44f);
            name.text = label;
            UiFactory.Button(panel, label + " min", new Vector2(260f, y), 50f, 44f, minus).label.text = "−";
            var value = UiFactory.Text(panel, label + " waarde", 24, y, 44f);
            value.alignment = TextAnchor.MiddleCenter;
            value.rectTransform.offsetMin = new Vector2(320f, value.rectTransform.offsetMin.y);
            value.rectTransform.offsetMax = new Vector2(-260f, value.rectTransform.offsetMax.y);
            UiFactory.Button(panel, label + " plus", new Vector2(530f, y), 50f, 44f, plus).label.text = "+";
            y -= 56f;
            return value;
        }
    }
}
