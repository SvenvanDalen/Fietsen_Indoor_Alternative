using FietsBridge.Core;

namespace FietsBridge.Tests;

// Testberichten met de hand opgebouwd volgens de Bluetooth GATT Specification Supplement
// (gss/org.bluetooth.characteristic.*.yaml). Getallen zijn little-endian.
public class IndoorBikeDataTests
{
    static Measurement Parse(params byte[] bytes) => IndoorBikeData.Parse(bytes);

    [Fact]
    public void SpeedCadencePower()
    {
        // Flags 0x0044: bit 0 = 0 (snelheid aanwezig), bit 2 cadans, bit 6 vermogen.
        var m = Parse(0x44, 0x00, 0xAC, 0x0D, 0xB4, 0x00, 0xFA, 0x00);

        Assert.Equal(250, m.PowerWatts);
        Assert.Equal(90f, m.CadenceRpm); // 180 × 0,5 rpm
        Assert.Null(m.HeartRateBpm);
    }

    [Fact]
    public void MoreDataBitSet_NoSpeedField()
    {
        // Flags 0x0041: bit 0 = 1 betekent géén snelheid; alleen vermogen.
        var m = Parse(0x41, 0x00, 0xFA, 0x00);

        Assert.Equal(250, m.PowerWatts);
        Assert.Null(m.CadenceRpm);
    }

    [Fact]
    public void AveragesAreSkipped()
    {
        // Flags 0x00CE: snelheid, gem. snelheid, cadans, gem. cadans, vermogen, gem. vermogen.
        var m = Parse(0xCE, 0x00, 0xAC, 0x0D, 0x00, 0x0D, 0xB4, 0x00, 0xA0, 0x00, 0xFA, 0x00, 0xC8, 0x00);

        Assert.Equal(250, m.PowerWatts);
        Assert.Equal(90f, m.CadenceRpm);
    }

    [Fact]
    public void TotalDistanceIsThreeBytes()
    {
        // Flags 0x0054: snelheid, cadans, totale afstand (uint24), vermogen.
        var m = Parse(0x54, 0x00, 0xAC, 0x0D, 0xB4, 0x00, 0x10, 0x27, 0x00, 0xFA, 0x00);

        Assert.Equal(250, m.PowerWatts);
    }

    [Fact]
    public void ResistanceLevel_TwoBytes_AsInFtms10()
    {
        // FTMS 1.0 definieert Resistance Level als sint16.
        var m = Parse(0x64, 0x00, 0xAC, 0x0D, 0xB4, 0x00, 0x0A, 0x00, 0xFA, 0x00);

        Assert.Equal(250, m.PowerWatts);
    }

    [Fact]
    public void ResistanceLevel_OneByte_AsInCurrentGss()
    {
        // De huidige GSS definieert Resistance Level als uint8; de lengte verraadt welke variant het is.
        var m = Parse(0x64, 0x00, 0xAC, 0x0D, 0xB4, 0x00, 0x0A, 0xFA, 0x00);

        Assert.Equal(250, m.PowerWatts);
    }

    [Fact]
    public void HeartRateAfterEnergy()
    {
        // Flags 0x0340: snelheid, vermogen, energie (2+2+1 bytes), hartslag.
        var m = Parse(0x40, 0x03, 0xAC, 0x0D, 0xFA, 0x00, 0x10, 0x00, 0x20, 0x01, 0x05, 0x8C);

        Assert.Equal(250, m.PowerWatts);
        Assert.Equal(140, m.HeartRateBpm);
    }

    [Fact]
    public void TruncatedMessage_ReturnsEmpty()
    {
        var m = Parse(0x44, 0x00, 0xAC);

        Assert.Equal(new Measurement(), m);
    }
}

public class CyclingPowerMeasurementTests
{
    [Fact]
    public void PowerOnly()
    {
        var m = CyclingPowerMeasurement.Parse([0x00, 0x00, 0xFA, 0x00]);

        Assert.Equal(250, m.PowerWatts);
        Assert.Null(m.Crank);
    }

    [Fact]
    public void NegativePowerIsSigned()
    {
        Assert.Equal(-5, CyclingPowerMeasurement.Parse([0x00, 0x00, 0xFB, 0xFF]).PowerWatts);
    }

    [Fact]
    public void CrankDataAfterWheelData()
    {
        // Flags 0x0030: wielomwentelingen (4+2 bytes) en crankomwentelingen (2+2 bytes).
        var m = CyclingPowerMeasurement.Parse([
            0x30, 0x00, 0xFA, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x08,
            0x0A, 0x00, 0x00, 0x04,
        ]);

        Assert.Equal(new CrankData(10, 1024), m.Crank);
    }

    [Fact]
    public void CrankDataAfterBalanceAndTorque()
    {
        // Flags 0x0025: pedaalbalans (1), opgebouwd koppel (2), crank (4).
        var m = CyclingPowerMeasurement.Parse([0x25, 0x00, 0xFA, 0x00, 0x64, 0x00, 0x01, 0x0A, 0x00, 0x00, 0x04]);

        Assert.Equal(new CrankData(10, 1024), m.Crank);
    }
}

public class CrankCadenceTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstSample_NoCadenceYet()
    {
        Assert.Null(new CrankCadence().Update(new CrankData(10, 1024), T0));
    }

    [Fact]
    public void ThreeRevolutionsInTwoSeconds_Is90Rpm()
    {
        var cadence = new CrankCadence();
        cadence.Update(new CrankData(10, 1024), T0);

        Assert.Equal(90f, cadence.Update(new CrankData(13, 1024 + 2048), T0.AddSeconds(2)));
    }

    [Fact]
    public void CountersWrapAround()
    {
        var cadence = new CrankCadence();
        cadence.Update(new CrankData(65535, 65000), T0);

        Assert.Equal(90f, cadence.Update(new CrankData(2, (ushort)((65000 + 2048) % 65536)), T0.AddSeconds(2)));
    }

    [Fact]
    public void RepeatedEvent_KeepsLastCadence()
    {
        var cadence = new CrankCadence();
        cadence.Update(new CrankData(10, 1024), T0);
        cadence.Update(new CrankData(13, 3072), T0.AddSeconds(2));

        Assert.Equal(90f, cadence.Update(new CrankData(13, 3072), T0.AddSeconds(2.5)));
    }

    [Fact]
    public void NoNewRevolutionsForThreeSeconds_IsZero()
    {
        var cadence = new CrankCadence();
        cadence.Update(new CrankData(10, 1024), T0);
        cadence.Update(new CrankData(13, 3072), T0.AddSeconds(2));

        Assert.Equal(0f, cadence.Update(new CrankData(13, 3072), T0.AddSeconds(5.1)));
    }
}

public class HeartRateMeasurementTests
{
    [Fact]
    public void EightBitValue() => Assert.Equal(72, HeartRateMeasurement.Parse([0x00, 0x48]));

    [Fact]
    public void SixteenBitValue() => Assert.Equal(72, HeartRateMeasurement.Parse([0x01, 0x48, 0x00]));

    [Fact]
    public void WithRrIntervals() => Assert.Equal(72, HeartRateMeasurement.Parse([0x10, 0x48, 0x00, 0x04]));

    [Fact]
    public void Truncated_ReturnsNull() => Assert.Null(HeartRateMeasurement.Parse([0x01, 0x48]));
}
