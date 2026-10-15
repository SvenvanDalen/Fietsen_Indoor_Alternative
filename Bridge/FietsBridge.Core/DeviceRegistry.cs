namespace FietsBridge.Core;

public sealed record DeviceInfo(string Id, string Name, int Rssi, IReadOnlyList<DeviceKind> Kinds);

/// <summary>
/// Houdt bij welke apparaten tijdens het zoeken gezien zijn. Bluetooth-advertenties komen in
/// losse pakketten (naam en services soms apart), dus elke melding wordt samengevoegd.
/// Thread-safe: meldingen komen binnen op de thread van de Bluetooth-API.
/// </summary>
public sealed class DeviceRegistry(TimeSpan? expiry = null)
{
    readonly TimeSpan _expiry = expiry ?? TimeSpan.FromSeconds(10);
    readonly Dictionary<ulong, Entry> _devices = new();
    readonly Lock _lock = new();

    sealed class Entry
    {
        public string Name = "";
        public int Rssi;
        public readonly HashSet<Guid> Services = [];
        public DateTimeOffset LastSeen;
    }

    public void Report(ulong address, string? name, int rssi, IEnumerable<Guid> services, DateTimeOffset now)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(address, out var entry))
                _devices[address] = entry = new Entry();

            if (!string.IsNullOrWhiteSpace(name)) entry.Name = name;
            entry.Rssi = rssi;
            entry.Services.UnionWith(services);
            entry.LastSeen = now;
        }
    }

    public void Clear()
    {
        lock (_lock) _devices.Clear();
    }

    /// <summary>
    /// Recent geziene apparaten: trainers eerst, daarna sensoren, binnen een groep sterkste signaal eerst.
    /// Met <paramref name="includeUnknown"/> ook apparaten zonder bekende fietsservice (handig bij story 1.8).
    /// </summary>
    public IReadOnlyList<DeviceInfo> Snapshot(DateTimeOffset now, bool includeUnknown)
    {
        lock (_lock)
        {
            return _devices
                .Where(d => now - d.Value.LastSeen <= _expiry)
                .Select(d => new DeviceInfo(
                    DeviceAddress.Format(d.Key),
                    d.Value.Name,
                    d.Value.Rssi,
                    GattServices.Classify(d.Value.Services)))
                .Where(d => includeUnknown || d.Kinds.Count > 0)
                .OrderBy(d => d.Kinds.Count > 0 ? (int)d.Kinds[0] : int.MaxValue)
                .ThenByDescending(d => d.Rssi)
                .ToList();
        }
    }
}
