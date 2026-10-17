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

/// <summary>Helling die de trainer moet nabootsen (story 1.2), in procent.</summary>
public sealed record GradeCommand(string Id, float Grade) : Command;

/// <summary>ERG-modus (story 8.2): vast vermogen in watt; 0 of minder zet de trainer terug op de helling.</summary>
public sealed record PowerCommand(string Id, int Watts) : Command;

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
                "grade" => new GradeCommand(root.GetProperty("id").GetString()!, root.GetProperty("grade").GetSingle()),
                "power" => new PowerCommand(root.GetProperty("id").GetString()!, root.GetProperty("watts").GetInt32()),
                _ => null,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
