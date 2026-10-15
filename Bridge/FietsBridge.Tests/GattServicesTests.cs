using FietsBridge.Core;

namespace FietsBridge.Tests;

public class GattServicesTests
{
    [Theory]
    [InlineData(0x1826, DeviceKind.Trainer)]
    [InlineData(0x1818, DeviceKind.Power)]
    [InlineData(0x180D, DeviceKind.HeartRate)]
    [InlineData(0x1816, DeviceKind.SpeedCadence)]
    public void Classify_KnownServices(int shortUuid, DeviceKind expected)
    {
        var kinds = GattServices.Classify([GattServices.FromShort((ushort)shortUuid)]);

        Assert.Equal([expected], kinds);
    }

    [Fact]
    public void Classify_IgnoresUnknownServicesAndDuplicates()
    {
        var kinds = GattServices.Classify([
            GattServices.FromShort(0x180F), // Battery
            GattServices.FitnessMachine,
            GattServices.CyclingPower,
            GattServices.FitnessMachine,
        ]);

        Assert.Equal([DeviceKind.Trainer, DeviceKind.Power], kinds);
    }

    [Fact]
    public void FromShort_UsesBluetoothBaseUuid()
    {
        Assert.Equal(Guid.Parse("00001826-0000-1000-8000-00805f9b34fb"), GattServices.FromShort(0x1826));
    }
}
