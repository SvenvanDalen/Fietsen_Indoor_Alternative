using System.Text.Json;
using System.Text.Json.Serialization;

namespace FietsBridge.Core;

public enum BluetoothState { On, Off, Unavailable }

public abstract record Command;

/// <summary>Start of stop met zoeken; <see cref="All"/> toont ook apparaten zonder fietsservice.</summary>
public sealed record ScanCommand(bool Enabled, bool All) : Command;

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
                _ => null,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
