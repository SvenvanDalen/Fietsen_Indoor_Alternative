using System.Text.Json;
using System.Text.Json.Serialization;

namespace FietsBridge.Core;

public enum BluetoothState { On, Off, Unavailable }

public enum ConnectionState { Connecting, Connected, Disconnected, Failed }

public abstract record Command;

/// <summary>Start of stop met zoeken; <see cref="All"/> toont ook apparaten zonder fietsservice.</summary>
public sealed record ScanCommand(bool Enabled, bool All) : Command;

public sealed record ConnectCommand(string Id) : Command;

public sealed record DisconnectCommand(string Id) : Command;

/// <summary>
/// JSON-berichten tussen brug en Unity, één bericht per WebSocket-tekstframe.
/// Veldnamen in camelCase en enums als tekst, zodat Unity's JsonUtility ze direct leest.
/// </summary>
public static class Protocol
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Devices(IReadOnlyList<DeviceInfo> devices) =>
        JsonSerializer.Serialize(new { type = "devices", devices }, Options);

    public static string Status(BluetoothState bluetooth, bool scanning) =>
        JsonSerializer.Serialize(new { type = "status", bluetooth, scanning }, Options);

    /// <summary>Meetwaarden van één apparaat; -1 = niet meegestuurd (JsonUtility kent geen null).</summary>
    public static string Data(string id, Measurement m) =>
        JsonSerializer.Serialize(new
        {
            type = "data",
            id,
            power = m.PowerWatts ?? -1,
            cadence = m.CadenceRpm ?? -1f,
            heartRate = m.HeartRateBpm ?? -1,
        }, Options);

    public static string Connection(string id, string name, ConnectionState state, string message = "") =>
        JsonSerializer.Serialize(new { type = "connection", id, name, state, message }, Options);

    /// <summary>Leest een opdracht van Unity; null bij onbekende of ongeldige invoer.</summary>
    public static Command? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return root.GetProperty("type").GetString() switch
            {
                "scan" => new ScanCommand(
                    root.GetProperty("enabled").GetBoolean(),
                    root.TryGetProperty("all", out var all) && all.GetBoolean()),
                "connect" => new ConnectCommand(root.GetProperty("id").GetString()!),
                "disconnect" => new DisconnectCommand(root.GetProperty("id").GetString()!),
                _ => null,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
