using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Paneel om Bluetooth-apparaten te zoeken (story 1.1): lijst met trainers en sensoren
    /// en hun signaalsterkte. Toets B toont of verbergt het paneel.
    /// </summary>
    public sealed class DeviceScanPanel : MonoBehaviour
    {
        [SerializeField] BridgeClient bridge;
        [SerializeField] bool visibleAtStart = true;

        const int MaxRows = 12;
        const float Width = 520f;
        const float RowHeight = 30f;

        GameObject _panel;
        Text _status, _scanLabel, _allLabel;
        Text[] _rows;
        bool _scanWanted, _showAll;

        void Awake()
        {
            if (bridge == null) bridge = FindAnyObjectByType<BridgeClient>();
            EnsureEventSystem();
            Build();
            _panel.SetActive(visibleAtStart);
        }

        void OnEnable()
        {
            bridge.ConnectionChanged += OnConnectionChanged;
            bridge.StatusReceived += OnStatus;
            bridge.DevicesReceived += OnDevices;
            Refresh();
        }

        void OnDisable()
        {
            bridge.ConnectionChanged -= OnConnectionChanged;
            bridge.StatusReceived -= OnStatus;
            bridge.DevicesReceived -= OnDevices;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) _panel.SetActive(!_panel.activeSelf);
        }

        void OnConnectionChanged(bool connected)
        {
            // Na herverbinden het zoeken hervatten als dat de bedoeling was.
            if (connected && _scanWanted) bridge.SetScanning(true, _showAll);
            if (!connected) ShowDevices(new DeviceEntry[0]);
            Refresh();
        }

        void OnStatus(StatusMessage status) => Refresh();

        void OnDevices(DevicesMessage message) => ShowDevices(message.devices ?? new DeviceEntry[0]);

        void ToggleScan()
        {
            _scanWanted = !_scanWanted;
            bridge.SetScanning(_scanWanted, _showAll);
            Refresh();
        }

        void ToggleAll()
        {
            _showAll = !_showAll;
            if (_scanWanted) bridge.SetScanning(true, _showAll);
            Refresh();
        }

        void Refresh()
        {
            var status = bridge.LastStatus;
            _status.text =
                !bridge.IsConnected ? "Brug niet verbonden. Start Bridge/FietsBridge." :
                status == null ? "Verbonden met brug..." :
                status.bluetooth == "unavailable" ? "Geen Bluetooth LE op deze pc." :
                status.bluetooth == "off" ? "Bluetooth staat uit." :
                status.scanning ? "Zoeken..." : "Klaar om te zoeken.";

            _scanLabel.text = _scanWanted ? "Stoppen" : "Zoeken";
            _allLabel.text = _showAll ? "Alle apparaten: aan" : "Alle apparaten: uit";
        }

        void ShowDevices(DeviceEntry[] devices)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (i >= devices.Length)
                {
                    _rows[i].text = i == 0 && _scanWanted && bridge.IsConnected ? "Nog geen apparaten gevonden." : "";
                    continue;
                }
                var d = devices[i];
                string name = string.IsNullOrEmpty(d.name) ? $"(naamloos) {d.id}" : d.name;
                string kinds = d.kinds != null && d.kinds.Length > 0
                    ? string.Join(", ", d.kinds.Select(BridgeMessages.KindLabel))
                    : "onbekend";
                _rows[i].text = $"{name}  <color=#9ad>{kinds}</color>  <color=#bbb>{d.rssi} dBm ({BridgeMessages.SignalLabel(d.rssi)})</color>";
            }
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void Build()
        {
            var canvasGo = new GameObject("Apparaten Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            float height = 140f + MaxRows * RowHeight;
            var panel = new GameObject("Paneel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(24f, 24f);
            panel.sizeDelta = new Vector2(Width, height);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            _panel = panel.gameObject;

            float y = -12f;
            var title = AddText(panel, "Titel", 26, y, 34f);
            title.text = "Bluetooth-apparaten  <size=16><color=#bbb>(B verbergt)</color></size>";
            y -= 38f;

            _status = AddText(panel, "Status", 18, y, 26f);
            y -= 34f;

            _scanLabel = AddButton(panel, "Zoeken", new Vector2(16f, y), 160f, ToggleScan);
            _allLabel = AddButton(panel, "Alle", new Vector2(188f, y), 240f, ToggleAll);
            y -= 52f;

            _rows = new Text[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                _rows[i] = AddText(panel, $"Rij {i}", 18, y, RowHeight);
                y -= RowHeight;
            }
        }

        static Text AddText(RectTransform parent, string name, int size, float y, float height)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rt = text.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(16f, y - height);
            rt.offsetMax = new Vector2(-16f, y);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        static Text AddButton(RectTransform parent, string name, Vector2 topLeft, float width, UnityEngine.Events.UnityAction onClick)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = new Vector2(width, 40f);
            rt.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.8f, 1f);
            rt.GetComponent<Button>().onClick.AddListener(onClick);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(rt, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return label;
        }
    }
}
