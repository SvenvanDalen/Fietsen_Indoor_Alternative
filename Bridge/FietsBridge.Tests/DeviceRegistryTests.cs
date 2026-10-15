using FietsBridge.Core;

namespace FietsBridge.Tests;

public class DeviceRegistryTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Snapshot_ShowsTrainersBeforeSensors_ThenStrongestSignal()
    {
        var registry = new DeviceRegistry();
        registry.Report(1, "HRM", -50, [GattServices.HeartRate], T0);
        registry.Report(2, "Trainer ver", -80, [GattServices.FitnessMachine], T0);
        registry.Report(3, "Trainer dichtbij", -55, [GattServices.FitnessMachine], T0);

        var names = registry.Snapshot(T0, includeUnknown: false).Select(d => d.Name);

        Assert.Equal(["Trainer dichtbij", "Trainer ver", "HRM"], names);
    }

    [Fact]
    public void Report_MergesNameAndServicesFromSeparatePackets()
    {
        // Naam en services komen vaak in verschillende pakketten (advertentie en scan-antwoord).
        var registry = new DeviceRegistry();
        registry.Report(1, null, -60, [GattServices.FitnessMachine], T0);
        registry.Report(1, "Smart Drive", -62, [GattServices.CyclingPower], T0.AddMilliseconds(100));

        var device = Assert.Single(registry.Snapshot(T0.AddSeconds(1), includeUnknown: false));

        Assert.Equal("Smart Drive", device.Name);
        Assert.Equal(-62, device.Rssi);
        Assert.Equal([DeviceKind.Trainer, DeviceKind.Power], device.Kinds);
    }

    [Fact]
    public void Report_KeepsKnownNameWhenLaterPacketHasNone()
    {
        var registry = new DeviceRegistry();
        registry.Report(1, "HRM", -60, [GattServices.HeartRate], T0);
        registry.Report(1, "", -61, [], T0);

        Assert.Equal("HRM", registry.Snapshot(T0, includeUnknown: false)[0].Name);
    }

    [Fact]
    public void Snapshot_HidesUnknownDevicesUnlessAsked()
    {
        var registry = new DeviceRegistry();
        registry.Report(1, "Koptelefoon", -40, [], T0);

        Assert.Empty(registry.Snapshot(T0, includeUnknown: false));
        Assert.Single(registry.Snapshot(T0, includeUnknown: true));
    }

    [Fact]
    public void Snapshot_DropsDevicesNotSeenRecently()
    {
        var registry = new DeviceRegistry(expiry: TimeSpan.FromSeconds(10));
        registry.Report(1, "HRM", -60, [GattServices.HeartRate], T0);

        Assert.Single(registry.Snapshot(T0.AddSeconds(9), includeUnknown: false));
        Assert.Empty(registry.Snapshot(T0.AddSeconds(11), includeUnknown: false));
    }

    [Fact]
    public void Snapshot_FormatsAddressAsId()
    {
        var registry = new DeviceRegistry();
        registry.Report(0xAABBCCDDEEFF, "HRM", -60, [GattServices.HeartRate], T0);

        Assert.Equal("AA:BB:CC:DD:EE:FF", registry.Snapshot(T0, includeUnknown: false)[0].Id);
    }
}
