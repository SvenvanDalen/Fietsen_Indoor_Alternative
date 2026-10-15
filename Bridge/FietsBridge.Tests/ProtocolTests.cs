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

    [Theory]
    [InlineData("""{"type":"onbekend"}""")]
    [InlineData("geen json")]
    [InlineData("")]
    public void Parse_InvalidInput_ReturnsNull(string json)
    {
        Assert.Null(Protocol.Parse(json));
    }
}
