using System.Globalization;

namespace FietsBridge.Core;

/// <summary>Bluetooth-adres als tekst ("AA:BB:CC:DD:EE:FF"), zoals Unity het ziet.</summary>
public static class DeviceAddress
{
    public static string Format(ulong address) =>
        string.Join(":", Enumerable.Range(0, 6).Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2")));

    public static ulong? Parse(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 6) return null;
        ulong address = 0;
        foreach (var part in parts)
        {
            if (part.Length != 2 || !byte.TryParse(part, NumberStyles.HexNumber, null, out var b)) return null;
            address = (address << 8) | b;
        }
        return address;
    }
}
