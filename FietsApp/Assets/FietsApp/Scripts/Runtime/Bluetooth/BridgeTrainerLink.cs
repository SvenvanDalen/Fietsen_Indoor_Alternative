using System;
using System.Collections.Generic;
using System.Linq;
using FietsApp.Trainers;
using UnityEngine;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Houdt de verbonden Bluetooth-apparaten bij en voedt daarmee een <see cref="BridgeTrainer"/>.
    /// Zodra er een apparaat verbonden is, rijdt de rit op die data; zonder apparaten weer op de simulator.
    /// </summary>
    public sealed class BridgeTrainerLink : MonoBehaviour
    {
        [SerializeField] BridgeClient bridge;
        [SerializeField] RideController ride;

        public sealed class DeviceConnection
        {
            public string Id;
            public string Name;
            public string State;   // "connecting", "connected" of "failed"
            public string Message; // uitleg bij "failed"
        }

        readonly Dictionary<string, DeviceConnection> _connections = new Dictionary<string, DeviceConnection>();

        public BridgeTrainer Trainer { get; } = new BridgeTrainer();
        public IEnumerable<DeviceConnection> Connections => _connections.Values;
        public event Action Changed;

        static double Now => Time.realtimeSinceStartupAsDouble;

        void Awake()
        {
            if (bridge == null) bridge = FindAnyObjectByType<BridgeClient>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
        }

        void OnEnable()
        {
            bridge.ConnectionChanged += OnBridgeConnectionChanged;
            bridge.ConnectionReceived += OnConnection;
            bridge.DataReceived += OnData;
        }

        void OnDisable()
        {
            bridge.ConnectionChanged -= OnBridgeConnectionChanged;
            bridge.ConnectionReceived -= OnConnection;
            bridge.DataReceived -= OnData;
        }

        void Update() => Trainer.Tick(Now);

        public bool IsActive(string id) =>
            _connections.TryGetValue(id, out var c) && (c.State == "connected" || c.State == "connecting");

        /// <summary>Verbind met het apparaat, of ontkoppel als het al (bijna) verbonden is.</summary>
        public void Toggle(string id, string name)
        {
            if (IsActive(id))
            {
                bridge.Disconnect(id);
                _connections.Remove(id);
            }
            else
            {
                bridge.Connect(id);
                _connections[id] = new DeviceConnection { Id = id, Name = name, State = "connecting" };
            }
            Apply();
        }

        void OnBridgeConnectionChanged(bool connected)
        {
            // Zonder Unity-client geeft de brug alle apparaten vrij; na herverbinden opnieuw kiezen.
            if (!connected) _connections.Clear();
            Apply();
        }

        void OnConnection(ConnectionMessage message)
        {
            if (message.state == "disconnected")
            {
                _connections.Remove(message.id);
            }
            else
            {
                _connections.TryGetValue(message.id, out var known);
                _connections[message.id] = new DeviceConnection
                {
                    Id = message.id,
                    Name = string.IsNullOrEmpty(message.name) ? known?.Name : message.name,
                    State = message.state,
                    Message = message.message,
                };
            }
            Apply();
        }

        void OnData(DataMessage data) => Trainer.Apply(data.power, data.cadence, data.heartRate, Now);

        void Apply()
        {
            var connected = _connections.Values.Where(c => c.State == "connected").ToList();
            Trainer.IsConnected = connected.Count > 0;
            Trainer.Name = connected.Count > 0
                ? string.Join(" + ", connected.Select(c => string.IsNullOrEmpty(c.Name) ? c.Id : c.Name))
                : "Bluetooth";

            if (Trainer.IsConnected && ride.Trainer != Trainer) ride.UseTrainer(Trainer);
            else if (!Trainer.IsConnected && ride.Trainer == Trainer) ride.UseTrainer(ride.Simulator);

            Changed?.Invoke();
        }
    }
}
