namespace FietsBridge.Core;

/// <summary>Soort apparaat, afgeleid uit de Bluetooth-services die het adverteert.</summary>
public enum DeviceKind
{
    Trainer,      // Fitness Machine Service (FTMS)
    Power,        // Cycling Power Service
    HeartRate,    // Heart Rate Service
    SpeedCadence, // Cycling Speed and Cadence Service
}

/// <summary>
/// Bluetooth-service-UUID's die de app gebruikt.
/// Gecontroleerd tegen de Bluetooth SIG assigned numbers (service_uuids.yaml) op 2026-10-15.
/// </summary>
public static class GattServices
{
    public static readonly Guid FitnessMachine = FromShort(0x1826);
    public static readonly Guid CyclingPower = FromShort(0x1818);
    public static readonly Guid HeartRate = FromShort(0x180D);
    public static readonly Guid CyclingSpeedAndCadence = FromShort(0x1816);

    // Volgorde bepaalt ook de volgorde van Kinds in de apparatenlijst.
    static readonly (Guid Service, DeviceKind Kind)[] Known =
    [
        (FitnessMachine, DeviceKind.Trainer),
        (CyclingPower, DeviceKind.Power),
        (HeartRate, DeviceKind.HeartRate),
        (CyclingSpeedAndCadence, DeviceKind.SpeedCadence),
    ];

    /// <summary>Zet een 16-bits UUID om naar een volledige UUID op basis van de Bluetooth Base UUID.</summary>
    public static Guid FromShort(ushort shortUuid) =>
        new(shortUuid, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);

    public static IReadOnlyList<DeviceKind> Classify(IEnumerable<Guid> services)
    {
        var set = services.ToHashSet();
        return Known.Where(k => set.Contains(k.Service)).Select(k => k.Kind).ToArray();
    }
}
