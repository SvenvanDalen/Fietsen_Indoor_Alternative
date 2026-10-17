using FietsApp.Training;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// HUD (story 6.1): vermogen, cadans, snelheid en hartslag, altijd zichtbaar en schaalbaar.
    /// De vermogenstegel kleurt mee met de zone van het 3-secondenvermogen (story 8.5).
    /// Bouwt zijn eigen canvas op, zodat de scène alleen dit component nodig heeft.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RideRecorder recorder;
        [SerializeField, Range(0.5f, 2f)] float scale = 1f;

        const float TileWidth = 220f;
        const float TileHeight = 120f;
        const float ZoneSeconds = 3f;
        static readonly Color TileColor = new Color(0f, 0f, 0f, 0.55f);

        RectTransform _root;
        Text _power, _cadence, _speed, _heartRate, _status, _powerCaption;
        Image _powerTile, _zoneBar;
        readonly RollingAverage _zonePower = new RollingAverage(ZoneSeconds);

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (recorder == null) recorder = FindAnyObjectByType<RideRecorder>();
            BuildCanvas();
        }

        void OnValidate() => ApplyScale();

        void Update()
        {
            if (ride == null || ride.Trainer == null) return;
            var t = ride.Trainer;

            _power.text = $"{t.PowerWatts:0}";
            ShowZone(t.PowerWatts);
            _cadence.text = $"{t.CadenceRpm:0}";
            _speed.text = $"{ride.SpeedMs * 3.6f:0.0}";
            _heartRate.text = t.HeartRateBpm > 0f ? $"{t.HeartRateBpm:0}" : "--";

            int seconds = (int)ride.ElapsedSeconds;
            _status.text =
                $"{t.Name}{(t.IsConnected ? "" : " (niet verbonden)")}   " +
                $"helling {ride.GradePercent:0.0}% (trainer {t.GradePercent:0.0}%)   " +
                $"{ride.DistanceM / 1000f:0.00} km   {seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" +
                (ride.Brake > 0f ? "   <color=#ff6060>remmen</color>" : "") +
                (recorder != null && recorder.Status.Length > 0 ? $"   <color=#ff9090>●</color> {recorder.Status}" : "");
        }

        /// <summary>Kleur en naam van de zone; stilstaan (0 W) laat de tegel neutraal.</summary>
        void ShowZone(float watts)
        {
            _zonePower.Add(watts, Time.deltaTime);
            int ftp = ProfileStore.Current.ftpWatts;
            if (_zonePower.Value < 1.0)
            {
                _powerTile.color = TileColor;
                _zoneBar.enabled = false;
                _powerCaption.text = $"VERMOGEN (W)  <color=#bbb>FTP {ftp}</color>";
                return;
            }
            var zone = PowerZones.Of(_zonePower.Value, ftp);
            var color = ZoneColor(zone);
            _powerTile.color = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.8f);
            _zoneBar.enabled = true;
            _zoneBar.color = color;
            _powerCaption.text = $"<b>{zone.Label.ToUpperInvariant()}</b> (W)";
        }

        public static Color ZoneColor(PowerZone zone) => new Color(
            ((zone.Rgb >> 16) & 0xFF) / 255f, ((zone.Rgb >> 8) & 0xFF) / 255f, (zone.Rgb & 0xFF) / 255f, 1f);

        void ApplyScale()
        {
            if (_root != null) _root.localScale = Vector3.one * scale;
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Tiles", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(canvasGo.transform, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -24f);
            ApplyScale();

            _power = AddTile("VERMOGEN", "W", 0);
            _powerTile = _power.transform.parent.GetComponent<Image>();
            _powerCaption = _powerTile.transform.Find("Label").GetComponent<Text>();
            _powerCaption.supportRichText = true;
            _zoneBar = new GameObject("Zone", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _zoneBar.transform.SetParent(_powerTile.transform, false);
            var bar = _zoneBar.rectTransform;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 8f);
            _zoneBar.raycastTarget = false;
            _zoneBar.enabled = false;
            _cadence = AddTile("CADANS", "rpm", 1);
            _speed = AddTile("SNELHEID", "km/u", 2);
            _heartRate = AddTile("HARTSLAG", "bpm", 3);

            _status = AddText(_root, "Status", 22, TextAnchor.MiddleCenter);
            var rt = _status.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(4 * TileWidth + 60f, 36f);
            rt.anchoredPosition = new Vector2(0f, -TileHeight - 12f);
        }

        Text AddTile(string label, string unit, int index)
        {
            var tile = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            tile.SetParent(_root, false);
            tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(0.5f, 1f);
            tile.sizeDelta = new Vector2(TileWidth, TileHeight);
            tile.anchoredPosition = new Vector2((index - 1.5f) * (TileWidth + 20f), 0f);
            tile.GetComponent<Image>().color = TileColor;

            var caption = AddText(tile, "Label", 18, TextAnchor.UpperCenter);
            caption.text = $"{label} ({unit})";
            Stretch(caption.rectTransform, top: 8f);

            var value = AddText(tile, "Waarde", 56, TextAnchor.MiddleCenter);
            value.fontStyle = FontStyle.Bold;
            value.text = "--";
            Stretch(value.rectTransform, top: 26f);
            return value;
        }

        static Text AddText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rt, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -top);
        }
    }
}
