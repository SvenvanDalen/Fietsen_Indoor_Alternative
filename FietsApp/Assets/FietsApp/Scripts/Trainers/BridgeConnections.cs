using System.Collections.Generic;
using System.Linq;

namespace FietsApp.Trainers
{
    /// <summary>Eén apparaat waarmee de rijder via de brug verbonden is of wil zijn.</summary>
    public sealed class DeviceConnection
    {
        public const string Connecting = "connecting", Connected = "connected", Reconnecting = "reconnecting", Failed = "failed";

        public string Id;
        public string Name;
        public string State;   // een van de constanten hierboven
        public string Message; // uitleg bij "failed", of de status van de hellingsbesturing
        public double Since;   // sinds wanneer in deze toestand (s)
    }

    /// <summary>
    /// Welke apparaten verbonden zijn, ook als de verbinding even wegvalt (story 1.5). Valt een apparaat weg, dan
    /// probeert de brug het opnieuw ("reconnecting"); valt de brug zelf weg, dan onthoudt Unity de apparaten en vraagt
    /// ze opnieuw aan zodra de brug terug is. Zolang een apparaat terugkomt, blijft de rit op de trainer staan.
    /// </summary>
    public sealed class BridgeConnections
    {
        /// <summary>Na zo lang herstellen geeft Unity het op (de brug doet dat na een minuut ook).</summary>
        public const double GiveUpSeconds = 60.0;

        readonly Dictionary<string, DeviceConnection> _all = new Dictionary<string, DeviceConnection>();

        public IEnumerable<DeviceConnection> All => _all.Values;

        /// <summary>Verbonden, verbindend of aan het herstellen: de knop ontkoppelt dan.</summary>
        public bool IsActive(string id) => _all.TryGetValue(id, out var c) && c.State != DeviceConnection.Failed;

        /// <summary>Er komt nu echt data binnen van minstens één apparaat.</summary>
        public bool AnyConnected => _all.Values.Any(c => c.State == DeviceConnection.Connected);

        /// <summary>De rit hoort op de Bluetooth-trainer: verbonden, of even weg maar aan het terugkomen.</summary>
        public bool HoldsTrainer => _all.Values.Any(c => c.State == DeviceConnection.Connected || c.State == DeviceConnection.Reconnecting);

        /// <summary>Namen van de apparaten van de rit, of null als er geen zijn.</summary>
        public string TrainerName
        {
            get
            {
                var names = _all.Values
                    .Where(c => c.State == DeviceConnection.Connected || c.State == DeviceConnection.Reconnecting)
                    .Select(c => string.IsNullOrEmpty(c.Name) ? c.Id : c.Name)
                    .ToList();
                return names.Count > 0 ? string.Join(" + ", names) : null;
            }
        }

        public void Connecting(string id, string name, double now) =>
            _all[id] = new DeviceConnection { Id = id, Name = name, State = DeviceConnection.Connecting, Since = now };

        public void Remove(string id) => _all.Remove(id);

        /// <summary>Verwerkt een connection-bericht van de brug; true als het apparaat nu (weer) verbonden is.</summary>
        public bool Apply(string id, string name, string state, string message, double now)
        {
            if (state == "disconnected")
            {
                _all.Remove(id);
                return false;
            }
            _all.TryGetValue(id, out var known);
            bool newlyConnected = state == DeviceConnection.Connected && known?.State != DeviceConnection.Connected;
            _all[id] = new DeviceConnection
            {
                Id = id,
                Name = string.IsNullOrEmpty(name) ? known?.Name : name,
                State = state,
                Message = message,
                Since = known != null && known.State == state ? known.Since : now,
            };
            return newlyConnected;
        }

        /// <summary>De brug zelf is weg: actieve apparaten wachten op herstel, mislukte vallen af.</summary>
        public void BridgeLost(double now)
        {
            foreach (var c in _all.Values.ToList())
            {
                if (c.State == DeviceConnection.Failed) { _all.Remove(c.Id); continue; }
                if (c.State != DeviceConnection.Reconnecting) c.Since = now;
                c.State = DeviceConnection.Reconnecting;
            }
        }

        /// <summary>De brug is terug: deze apparaten moeten opnieuw aangevraagd worden.</summary>
        public IReadOnlyList<string> BridgeBack() =>
            _all.Values.Where(c => c.State == DeviceConnection.Reconnecting).Select(c => c.Id).ToList();

        /// <summary>Geeft herstel op dat te lang duurt; true als er iets veranderde.</summary>
        public bool Expire(double now)
        {
            bool changed = false;
            foreach (var c in _all.Values)
            {
                if (c.State != DeviceConnection.Reconnecting || now - c.Since < GiveUpSeconds) continue;
                c.State = DeviceConnection.Failed;
                c.Message = "verbinding niet hersteld";
                c.Since = now;
                changed = true;
            }
            return changed;
        }
    }
}
