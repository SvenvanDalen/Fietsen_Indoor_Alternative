using System;
using UnityEngine;

namespace FietsApp.Bluetooth
{
    // Berichten van FietsBridge (Bridge/FietsBridge.Core/Protocol.cs). Veldnamen moeten
    // exact overeenkomen met de JSON, want JsonUtility koppelt op veldnaam.

    [Serializable]
    public class BridgeMessage
    {
        public string type;
    }

    [Serializable]
    public class StatusMessage : BridgeMessage
    {
        public string bluetooth; // "on", "off" of "unavailable"
        public bool scanning;
    }

    [Serializable]
    public class DevicesMessage : BridgeMessage
    {
        public DeviceEntry[] devices;
    }

    [Serializable]
    public class DeviceEntry
    {
        public string id;
        public string name;
        public int rssi;
        public string[] kinds; // "trainer", "power", "heartRate", "speedCadence"
    }

    public static class BridgeMessages
    {
        /// <summary>Leest een bericht van de brug; null bij onbekende of ongeldige invoer.</summary>
        public static BridgeMessage Parse(string json)
        {
            try
            {
                switch (JsonUtility.FromJson<BridgeMessage>(json)?.type)
                {
                    case "status": return JsonUtility.FromJson<StatusMessage>(json);
                    case "devices": return JsonUtility.FromJson<DevicesMessage>(json);
                    default: return null;
                }
            }
            catch (ArgumentException)
            {
                return null; // geen geldige JSON
            }
        }

        public static string Scan(bool enabled, bool all) =>
            $"{{\"type\":\"scan\",\"enabled\":{Bool(enabled)},\"all\":{Bool(all)}}}";

        /// <summary>Signaalsterkte in woorden; grenzen zijn vuistregels voor Bluetooth LE binnenshuis.</summary>
        public static string SignalLabel(int rssi) =>
            rssi >= -60 ? "sterk" : rssi >= -80 ? "goed" : "zwak";

        public static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "trainer": return "trainer (FTMS)";
                case "power": return "vermogen";
                case "heartRate": return "hartslag";
                case "speedCadence": return "snelheid/cadans";
                default: return kind;
            }
        }

        static string Bool(bool b) => b ? "true" : "false";
    }
}
