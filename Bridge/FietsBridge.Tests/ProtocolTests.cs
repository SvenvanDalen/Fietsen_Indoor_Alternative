using FietsBridge.Core;

namespace FietsBridge.Tests;

public class ProtocolTests
{
    [Fact]
    public void Devices_SerializesForUnityJsonUtility()
    {
        // Unity's JsonUtility leest alleen velden met exact deze namen; enums als tekst.
        var message = Protocol.Devices([
            new DeviceInfo("AA:BB:CC:DD:EE:FF", "Smart Drive", -60, [DeviceKind.Trainer, DeviceKind.Power]),
        ]);

        Assert.Equal(
            """{"type":"devices","devices":[{"id":"AA:BB:CC:DD:EE:FF","name":"Smart Drive","rssi":-60,"kinds":["trainer","power"]}]}""",
            message);
    }

    [Fact]
    public void Status_Serializes()
    {
        Assert.Equal(
            """{"type":"status","bluetooth":"on","scanning":true}""",
            Protocol.Status(BluetoothState.On, scanning: true));
    }

    [Theory]
    [InlineData("""{"type":"scan","enabled":true,"all":false}""", true, false)]
    [InlineData("""{"type":"scan","enabled":true,"all":true}""", true, true)]
    [InlineData("""{"type":"scan","enabled":false}""", false, false)]
    public void Parse_ScanCommand(string json, bool enabled, bool all)
    {
        var command = Assert.IsType<ScanCommand>(Protocol.Parse(json));

        Assert.Equal(enabled, command.Enabled);
        Assert.Equal(all, command.All);
    }

    [Fact]
    public void Data_UsesMinusOneForMissingValues()
    {
        // JsonUtility kent geen null; -1 betekent "niet meegestuurd".
        Assert.Equal(
            """{"type":"data","id":"AA:BB:CC:DD:EE:FF","power":250,"cadence":90.5,"heartRate":-1}""",
            Protocol.Data("AA:BB:CC:DD:EE:FF", new Measurement(250, 90.5f, null)));
    }

    [Fact]
    public void Connection_Serializes()
    {
        Assert.Equal(
            """{"type":"connection","id":"AA:BB:CC:DD:EE:FF","name":"Smart Drive","state":"connected","message":""}""",
            Protocol.Connection("AA:BB:CC:DD:EE:FF", "Smart Drive", ConnectionState.Connected));
    }

    [Fact]
    public void Parse_ConnectAndDisconnect()
    {
        Assert.Equal(new ConnectCommand("AA:BB:CC:DD:EE:FF"),
            Protocol.Parse("""{"type":"connect","id":"AA:BB:CC:DD:EE:FF"}"""));
        Assert.Equal(new DisconnectCommand("AA:BB:CC:DD:EE:FF"),
            Protocol.Parse("""{"type":"disconnect","id":"AA:BB:CC:DD:EE:FF"}"""));
    }

    [Fact]
    public void Parse_GradeCommand()
    {
        Assert.Equal(new GradeCommand("AA:BB:CC:DD:EE:FF", 8.5f),
            Protocol.Parse("""{"type":"grade","id":"AA:BB:CC:DD:EE:FF","grade":8.5}"""));
    }

    [Fact]
    public void Parse_PowerCommand()
    {
        Assert.Equal(new PowerCommand("AA:BB:CC:DD:EE:FF", 250),
            Protocol.Parse("""{"type":"power","id":"AA:BB:CC:DD:EE:FF","watts":250}"""));
        Assert.Equal(new PowerCommand("AA:BB:CC:DD:EE:FF", 0),
            Protocol.Parse("""{"type":"power","id":"AA:BB:CC:DD:EE:FF","watts":0}"""));
    }

    [Theory]
    [InlineData("""{"type":"onbekend"}""")]
    [InlineData("""{"type":"connect"}""")]
    [InlineData("geen json")]
    [InlineData("")]
    public void Parse_InvalidInput_ReturnsNull(string json)
    {
        Assert.Null(Protocol.Parse(json));
    }
}
