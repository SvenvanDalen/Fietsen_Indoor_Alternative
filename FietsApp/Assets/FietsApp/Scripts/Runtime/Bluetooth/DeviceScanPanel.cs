using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Paneel om Bluetooth-apparaten te zoeken en te verbinden (stories 1.1 en 1.3): lijst met
    /// trainers en sensoren en hun signaalsterkte; klik op een apparaat om te verbinden of te
    /// ontkoppelen. Toets B toont of verbergt het paneel.
    /// </summary>
    public sealed class DeviceScanPanel : MonoBehaviour
    {
        [SerializeField] BridgeClient bridge;
        [SerializeField] BridgeTrainerLink link;
        [SerializeField] bool visibleAtStart = true;

        const int MaxRows = 12;
        const float Width = 620f;
        const float RowHeight = 34f;
        const float HeaderHeight = 140f;

        GameObject _panel;
        Text _status, _scanLabel, _allLabel;
        Row[] _rows;
        DeviceEntry[] _scanned = new DeviceEntry[0];
        bool _scanWanted, _showAll;

        sealed class Row
        {
            public Button Button;
            public Image Background;
            public Text Label;
            public string Id, Name;
        }

        void Awake()
        {
            if (bridge == null) bridge = FindAnyObjectByType<BridgeClient>();
            if (link == null) link = FindAnyObjectByType<BridgeTrainerLink>();
            UiFactory.EnsureEventSystem();
            Build();
            _panel.SetActive(visibleAtStart);
        }

        void OnEnable()
        {
            bridge.ConnectionChanged += OnConnectionChanged;
            bridge.StatusReceived += OnStatus;
            bridge.DevicesReceived += OnDevices;
            link.Changed += ShowRows;
            Refresh();
        }

        void OnDisable()
        {
            bridge.ConnectionChanged -= OnConnectionChanged;
            bridge.StatusReceived -= OnStatus;
            bridge.DevicesReceived -= OnDevices;
            link.Changed -= ShowRows;
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current; // niet tijdens typen in een tekstveld
            if (kb != null && kb.bKey.wasPressedThisFrame) _panel.SetActive(!_panel.activeSelf);
        }

        /// <summary>Toont het paneel (vanaf het startscherm).</summary>
        public void Show() => _panel.SetActive(true);

        void OnConnectionChanged(bool connected)
        {
            // Na herverbinden het zoeken hervatten als dat de bedoeling was.
            if (connected && _scanWanted) bridge.SetScanning(true, _showAll);
            if (!connected) _scanned = new DeviceEntry[0];
            Refresh();
        }

        void OnStatus(StatusMessage status) => Refresh();

        void OnDevices(DevicesMessage message)
        {
            _scanned = message.devices ?? new DeviceEntry[0];
            ShowRows();
        }

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

        void OnRowClicked(Row row)
        {
            if (!string.IsNullOrEmpty(row.Id)) link.Toggle(row.Id, row.Name);
        }

        void Refresh()
        {
            var status = bridge.LastStatus;
            _status.text =
                !bridge.IsConnected ? "Brug niet verbonden. Start Bridge/FietsBridge." :
                status == null ? "Verbonden met brug..." :
                status.bluetooth == "unavailable" ? "Geen Bluetooth LE op deze pc." :
                status.bluetooth == "off" ? "Bluetooth staat uit." :
                status.scanning ? "Zoeken... Klik op een apparaat om te verbinden." : "Klaar om te zoeken.";

            _scanLabel.text = _scanWanted ? "Stoppen" : "Zoeken";
            _allLabel.text = _showAll ? "Alle apparaten: aan" : "Alle apparaten: uit";
            ShowRows();
        }

        /// <summary>Verbonden apparaten bovenaan (die adverteren vaak niet meer), daarna de zoekresultaten.</summary>
        void ShowRows()
        {
            var lines = new List<(string id, string name, string text, Color color)>();

            foreach (var c in link.Connections)
            {
                string state =
                    c.State == "connected" ? "<color=#7d7>verbonden</color>" +
                        (string.IsNullOrEmpty(c.Message) ? "" : $"  <color=#bbb>{c.Message}</color>") :
                    c.State == "connecting" ? "<color=#fd6>verbinden...</color>" :
                    c.State == "reconnecting" ? "<color=#fd6>verbinding weg, herstellen...</color>" :
                    $"<color=#f77>mislukt: {c.Message}</color>";
                lines.Add((c.Id, c.Name, $"{DisplayName(c.Name, c.Id)}  {state}", new Color(0.15f, 0.3f, 0.15f, 0.9f)));
            }

            foreach (var d in _scanned.Where(d => !link.Connections.Any(c => c.Id == d.id)))
            {
                string kinds = d.kinds != null && d.kinds.Length > 0
                    ? string.Join(", ", d.kinds.Select(BridgeMessages.KindLabel))
                    : "onbekend";
                lines.Add((d.id, d.name,
                    $"{DisplayName(d.name, d.id)}  <color=#9ad>{kinds}</color>  <color=#bbb>{d.rssi} dBm ({BridgeMessages.SignalLabel(d.rssi)})</color>",
                    new Color(1f, 1f, 1f, 0.08f)));
            }

            if (lines.Count == 0 && _scanWanted && bridge.IsConnected)
                lines.Add((null, null, "Nog geen apparaten gevonden.", Color.clear));

            // Paneel groeit mee met het aantal regels, zodat het leeg niet de weg bedekt.
            int shown = Mathf.Min(lines.Count, _rows.Length);
            var panel = (RectTransform)_panel.transform;
            panel.sizeDelta = new Vector2(Width, HeaderHeight + shown * (RowHeight + 4f) + 8f);

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                bool used = i < lines.Count;
                row.Button.gameObject.SetActive(used);
                if (!used) continue;
                (row.Id, row.Name, row.Label.text, row.Background.color) = lines[i];
                row.Button.interactable = row.Id != null;
            }
        }

        static string DisplayName(string name, string id) => string.IsNullOrEmpty(name) ? $"(naamloos) {id}" : name;

        void Build()
        {
            var canvas = UiFactory.Canvas(transform, "Apparaten Canvas", 10);

            float height = HeaderHeight;
            var panel = new GameObject("Paneel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvas, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(24f, 24f);
            panel.sizeDelta = new Vector2(Width, height);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            _panel = panel.gameObject;

            float y = -12f;
            var title = UiFactory.Text(panel, "Titel", 26, y, 34f);
            title.text = "Bluetooth-apparaten  <size=16><color=#bbb>(B verbergt)</color></size>";
            y -= 38f;

            _status = UiFactory.Text(panel, "Status", 18, y, 26f);
            y -= 34f;

            _scanLabel = UiFactory.Button(panel, "Zoeken", new Vector2(16f, y), 160f, RowHeight + 6f, ToggleScan).label;
            _allLabel = UiFactory.Button(panel, "Alle", new Vector2(188f, y), 240f, RowHeight + 6f, ToggleAll).label;
            y -= 56f;

            _rows = new Row[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                var (button, label) = UiFactory.Button(panel, $"Rij {i}", new Vector2(16f, y), Width - 32f, RowHeight + 6f, null);
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.rectTransform.offsetMin = new Vector2(10f, 0f);
                var row = new Row { Button = button, Background = button.GetComponent<Image>(), Label = label };
                button.onClick.AddListener(() => OnRowClicked(row));
                button.gameObject.SetActive(false);
                _rows[i] = row;
                y -= RowHeight + 4f;
            }
        }
    }
}
