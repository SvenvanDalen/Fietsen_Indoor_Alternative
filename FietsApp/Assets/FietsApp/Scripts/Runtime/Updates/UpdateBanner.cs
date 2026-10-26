using FietsApp.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>Versienummer rechtsonder en, als er een update is, een melding met knop (story 13.6).</summary>
    public sealed class UpdateBanner : MonoBehaviour
    {
        RectTransform _panel;
        Text _message;
        Text _buttonLabel;
        Button _button;

        void Start()
        {
            var canvas = UiFactory.Canvas(transform, "UpdateCanvas", 1000);

            var version = new GameObject("Versie", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rt = version.rectTransform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-12f, 6f);
            rt.sizeDelta = new Vector2(300f, 24f);
            version.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            version.fontSize = 14;
            version.color = new Color(1f, 1f, 1f, 0.45f);
            version.alignment = TextAnchor.LowerRight;
            version.raycastTarget = false;
            version.text = "FietsApp " + Application.version;

            _panel = UiFactory.Panel(canvas, "UpdateMelding", new Vector2(1f, 0f), new Vector2(-12f, 34f), new Vector2(560f, 96f));
            _message = UiFactory.Text(_panel, "Tekst", 18, -8f, 40f);
            _message.horizontalOverflow = HorizontalWrapMode.Wrap;
            (_button, _buttonLabel) = UiFactory.Button(_panel, "Knop", new Vector2(16f, -50f), 200f, 36f, OnClick);
            _panel.gameObject.SetActive(false);

            var service = UpdateService.Instance;
            if (service != null)
            {
                service.Changed += Refresh;
                Refresh();
            }
        }

        void OnDestroy()
        {
            if (UpdateService.Instance != null) UpdateService.Instance.Changed -= Refresh;
        }

        void Refresh()
        {
            var result = UpdateService.Instance != null ? UpdateService.Instance.Result : null;
            if (result == null || _panel == null) return;
            switch (result.Status)
            {
                case UpdateStatus.ReadyToApply:
                    _message.text = $"Update {result.Version} staat klaar en wordt geïnstalleerd bij afsluiten.";
                    _buttonLabel.text = "Nu herstarten";
                    break;
                case UpdateStatus.ManualDownloadRequired:
                    _message.text = $"Nieuwe versie {result.Version} beschikbaar.";
                    _buttonLabel.text = "Downloaden";
                    break;
                default:
                    _panel.gameObject.SetActive(false);
                    return;
            }
            UiFactory.EnsureEventSystem();
            _panel.gameObject.SetActive(true);
        }

        void OnClick()
        {
            var result = UpdateService.Instance != null ? UpdateService.Instance.Result : null;
            if (result == null) return;
            if (result.Status == UpdateStatus.ReadyToApply) UpdateService.Instance.RestartNow();
            else if (result.Status == UpdateStatus.ManualDownloadRequired) Application.OpenURL(result.ReleasePageUrl);
        }
    }
}
