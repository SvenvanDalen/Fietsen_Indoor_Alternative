using System;
using System.Collections.Generic;
using FietsApp.Trainers;
using UnityEngine;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Houdt de verbonden Bluetooth-apparaten bij en voedt daarmee een <see cref="BridgeTrainer"/>.
    /// Zodra er een apparaat verbonden is, rijdt de rit op die data; zonder apparaten weer op de simulator.
    /// Valt een apparaat of de brug even weg, dan blijft de rit op de trainer staan tot de verbinding hersteld is
    /// (story 1.5), of tot het herstel na een minuut opgegeven wordt.
    /// </summary>
    public sealed class BridgeTrainerLink : MonoBehaviour
    {
        [SerializeField] BridgeClient bridge;
        [SerializeField] RideController ride;

        readonly BridgeConnections _connections = new BridgeConnections();

        public BridgeTrainer Trainer { get; } = new BridgeTrainer();
        public IEnumerable<DeviceConnection> Connections => _connections.All;
        public event Action Changed;

        static double Now => Time.realtimeSinceStartupAsDouble;

        void Awake()
        {
            if (bridge == null) bridge = FindAnyObjectByType<BridgeClient>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
        }

        void OnEnable()
        {
            Trainer.GradeChanged += SendGrade;
            Trainer.ErgChanged += SendErg;
            bridge.ConnectionChanged += OnBridgeConnectionChanged;
            bridge.ConnectionReceived += OnConnection;
            bridge.DataReceived += OnData;
        }

        void OnDisable()
        {
            Trainer.GradeChanged -= SendGrade;
            Trainer.ErgChanged -= SendErg;
            bridge.ConnectionChanged -= OnBridgeConnectionChanged;
            bridge.ConnectionReceived -= OnConnection;
            bridge.DataReceived -= OnData;
        }

        void Update()
        {
            Trainer.Tick(Now);
            if (_connections.Expire(Now)) Apply();
        }

        public bool IsActive(string id) => _connections.IsActive(id);

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
                _connections.Connecting(id, name, Now);
            }
            Apply();
        }

        void OnBridgeConnectionChanged(bool connected)
        {
            // Zonder Unity-client geeft de brug alle apparaten vrij: onthouden en na herverbinden opnieuw aanvragen.
            if (!connected) _connections.BridgeLost(Now);
            else foreach (var id in _connections.BridgeBack()) bridge.Connect(id);
            Apply();
        }

        void OnConnection(ConnectionMessage message)
        {
            // Een (weer) verbonden trainer krijgt meteen de huidige helling, en het ERG-doel als er een workout loopt.
            if (_connections.Apply(message.id, message.name, message.state, message.message, Now))
            {
                bridge.SetGrade(message.id, Trainer.GradePercent);
                if (Trainer.ErgWatts > 0f) bridge.SetTargetPower(message.id, (int)Trainer.ErgWatts);
            }
            Apply();
        }

        /// <summary>
        /// Helling naar alle verbonden apparaten; de brug stuurt hem alleen door naar trainers met
        /// FTMS-besturing en zorgt zelf voor geleidelijke stappen.
        /// </summary>
        void SendGrade(float gradePercent)
        {
            foreach (var c in _connections.All)
                if (c.State == DeviceConnection.Connected) bridge.SetGrade(c.Id, gradePercent);
        }

        /// <summary>ERG-vermogen naar alle verbonden apparaten; alleen trainers met FTMS-vermogensdoel doen er iets mee.</summary>
        void SendErg(int watts)
        {
            foreach (var c in _connections.All)
                if (c.State == DeviceConnection.Connected) bridge.SetTargetPower(c.Id, watts);
        }

        void OnData(DataMessage data) => Trainer.Apply(data.power, data.cadence, data.heartRate, Now);

        void Apply()
        {
            Trainer.IsConnected = _connections.AnyConnected;
            Trainer.Name = _connections.TrainerName ?? "Bluetooth";

            // Tijdens herstel blijft de rit op de trainer (vermogen valt vanzelf naar 0), niet op de simulator.
            bool hold = _connections.HoldsTrainer;
            if (hold && ride.Trainer != Trainer) ride.UseTrainer(Trainer);
            else if (!hold && ride.Trainer == Trainer) ride.UseTrainer(ride.Simulator);

            Changed?.Invoke();
        }
    }
}
