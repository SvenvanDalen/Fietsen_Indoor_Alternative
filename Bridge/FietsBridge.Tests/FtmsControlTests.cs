using FietsBridge.Core;

namespace FietsBridge.Tests;

// Berichtindeling van het Fitness Machine Control Point (0x2AD9). De FTMS-specificatie zelf is niet vrij
// te downloaden; gecontroleerd tegen GoldenCheetah (src/Train/Ftms.h) en Auuki (src/ble/ftms/control-point.js).
public class FtmsControlTests
{
    [Fact]
    public void RequestControlAndStart()
    {
        Assert.Equal([0x00], FtmsControl.RequestControl());
        Assert.Equal([0x07], FtmsControl.StartOrResume());
    }

    [Fact]
    public void SetSimulation_EncodesGradeInHundredthsOfAPercent()
    {
        // 8,5% = 850 = 0x0352; Crr 0,004 = 40; windweerstand 0,51 kg/m = 51; geen wind.
        Assert.Equal([0x11, 0x00, 0x00, 0x52, 0x03, 40, 51], FtmsControl.SetSimulation(8.5f));
    }

    [Fact]
    public void SetSimulation_NegativeGrade()
    {
        // -3,25% = -325 = 0xFEBB (little-endian BB FE).
        Assert.Equal([0x11, 0x00, 0x00, 0xBB, 0xFE, 40, 51], FtmsControl.SetSimulation(-3.25f));
    }

    [Fact]
    public void SetSimulation_ClampsToProtocolRange()
    {
        var bytes = FtmsControl.SetSimulation(55f);
        Assert.Equal(4000, (short)(bytes[3] | bytes[4] << 8)); // ±40% is het maximum
    }

    [Theory]
    [InlineData(new byte[] { 0x80, 0x11, 0x01 }, 0x11, FtmsResult.Success)]
    [InlineData(new byte[] { 0x80, 0x00, 0x05 }, 0x00, FtmsResult.ControlNotPermitted)]
    [InlineData(new byte[] { 0x80, 0x11, 0x02 }, 0x11, FtmsResult.NotSupported)]
    public void ParseResponse(byte[] data, byte opCode, FtmsResult result)
    {
        var response = FtmsControl.ParseResponse(data);

        Assert.NotNull(response);
        Assert.Equal(opCode, response.Value.RequestOpCode);
        Assert.Equal(result, response.Value.Result);
    }

    [Fact]
    public void ParseResponse_IgnoresOtherMessages()
    {
        Assert.Null(FtmsControl.ParseResponse([0x11, 0x00]));
        Assert.Null(FtmsControl.ParseResponse([0x80]));
    }

    [Fact]
    public void SupportsSimulation_ReadsTargetSettingBit13()
    {
        // 4 bytes machine-features, dan 4 bytes target-settings; bit 13 = indoor bike simulation.
        Assert.True(FtmsControl.SupportsSimulation([0, 0, 0, 0, 0x00, 0x20, 0, 0]));
        Assert.False(FtmsControl.SupportsSimulation([0, 0, 0, 0, 0x08, 0x00, 0, 0])); // alleen vermogen
        Assert.False(FtmsControl.SupportsSimulation([0, 0, 0]));
    }

    [Fact]
    public void SupportsPower_ReadsTargetSettingBit3()
    {
        Assert.True(FtmsControl.SupportsPower([0, 0, 0, 0, 0x08, 0x00, 0, 0]));
        Assert.True(FtmsControl.SupportsPower([0, 0, 0, 0, 0x08, 0x20, 0, 0])); // vermogen én simulatie
        Assert.False(FtmsControl.SupportsPower([0, 0, 0, 0, 0x00, 0x20, 0, 0])); // alleen simulatie
        Assert.False(FtmsControl.SupportsPower([0, 0, 0]));
    }

    [Fact]
    public void SetTargetPower_EncodesWattsAsSint16()
    {
        // 250 W = 0x00FA (little-endian FA 00); 1200 W = 0x04B0.
        Assert.Equal([0x05, 0xFA, 0x00], FtmsControl.SetTargetPower(250));
        Assert.Equal([0x05, 0xB0, 0x04], FtmsControl.SetTargetPower(1200));
        Assert.Equal([0x05, 0x00, 0x00], FtmsControl.SetTargetPower(-10));
    }
}

public class PowerSenderTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SendsNewTargetOnceAndImmediately()
    {
        var sender = new PowerSender();
        Assert.Equal(200, sender.Next(200, T0));
        Assert.Null(sender.Next(200, T0.AddSeconds(5)));
    }

    [Fact]
    public void StepWaitsForMinimumInterval()
    {
        var sender = new PowerSender();
        sender.Next(200, T0);
        Assert.Null(sender.Next(300, T0.AddMilliseconds(250)));
        Assert.Equal(300, sender.Next(300, T0.AddMilliseconds(500)));
    }

    [Fact]
    public void ResetSendsAgain()
    {
        var sender = new PowerSender();
        sender.Next(200, T0);
        sender.Reset();
        Assert.Equal(200, sender.Next(200, T0.AddMilliseconds(10)));
    }
}

public class GradeSenderTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SendsFirstGradeImmediately()
    {
        var sender = new GradeSender();

        Assert.Equal(0f, sender.Next(0f, T0));
    }

    [Fact]
    public void WaitsForMinimumInterval()
    {
        var sender = new GradeSender();
        sender.Next(0f, T0);

        Assert.Null(sender.Next(1f, T0.AddMilliseconds(300)));
        Assert.NotNull(sender.Next(1f, T0.AddMilliseconds(500)));
    }

    [Fact]
    public void IgnoresTinyChanges()
    {
        var sender = new GradeSender();
        sender.Next(5f, T0);

        Assert.Null(sender.Next(5.05f, T0.AddSeconds(2)));
    }

    [Fact]
    public void RampsLargeJumps_InSmallSteps()
    {
        // Van 0 naar 10%: per halve seconde hoogstens 2% (4% per seconde), geen klap op de pedalen.
        var sender = new GradeSender();
        sender.Next(0f, T0);

        Assert.Equal(2f, sender.Next(10f, T0.AddSeconds(0.5)));
        Assert.Equal(4f, sender.Next(10f, T0.AddSeconds(1.0)));
    }

    [Fact]
    public void ReachesTarget_WithinAFewSeconds()
    {
        var sender = new GradeSender();
        sender.Next(0f, T0);
        float? last = null;
        for (int i = 1; i <= 8; i++) last = sender.Next(10f, T0.AddSeconds(0.5 * i)) ?? last;

        Assert.Equal(10f, last);
    }

    [Fact]
    public void SmallChange_IsSentWithinOneSecond()
    {
        // Story 1.2: vertraging onder 1 seconde.
        var sender = new GradeSender();
        sender.Next(6f, T0);

        Assert.Equal(6.8f, sender.Next(6.8f, T0.AddSeconds(0.5)));
    }
}
