using FietsApp.Bluetooth;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class BridgeMessagesTests
    {
        // Voorbeeldberichten zoals FietsBridge ze verstuurt (zie Bridge/FietsBridge.Tests/ProtocolTests.cs).
        [Test]
        public void Parse_DevicesMessage()
        {
            var message = BridgeMessages.Parse(
                "{\"type\":\"devices\",\"devices\":[{\"id\":\"AA:BB:CC:DD:EE:FF\",\"name\":\"Smart Drive\",\"rssi\":-60,\"kinds\":[\"trainer\",\"power\"]}]}");

            var devices = message as DevicesMessage;
            Assert.That(devices, Is.Not.Null);
            Assert.That(devices.devices, Has.Length.EqualTo(1));
            Assert.That(devices.devices[0].name, Is.EqualTo("Smart Drive"));
            Assert.That(devices.devices[0].rssi, Is.EqualTo(-60));
            Assert.That(devices.devices[0].kinds, Is.EqualTo(new[] { "trainer", "power" }));
        }

        [Test]
        public void Parse_StatusMessage()
        {
            var status = BridgeMessages.Parse("{\"type\":\"status\",\"bluetooth\":\"off\",\"scanning\":false}") as StatusMessage;

            Assert.That(status, Is.Not.Null);
            Assert.That(status.bluetooth, Is.EqualTo("off"));
            Assert.That(status.scanning, Is.False);
        }

        [Test]
        public void Parse_DataMessage()
        {
            var data = BridgeMessages.Parse(
                "{\"type\":\"data\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"power\":250,\"cadence\":90.5,\"heartRate\":-1}") as DataMessage;

            Assert.That(data, Is.Not.Null);
            Assert.That(data.id, Is.EqualTo("AA:BB:CC:DD:EE:FF"));
            Assert.That(data.power, Is.EqualTo(250f));
            Assert.That(data.cadence, Is.EqualTo(90.5f));
            Assert.That(data.heartRate, Is.EqualTo(-1f));
        }

        [Test]
        public void Parse_ConnectionMessage()
        {
            var connection = BridgeMessages.Parse(
                "{\"type\":\"connection\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"name\":\"Smart Drive\",\"state\":\"connected\",\"message\":\"\"}") as ConnectionMessage;

            Assert.That(connection, Is.Not.Null);
            Assert.That(connection.name, Is.EqualTo("Smart Drive"));
            Assert.That(connection.state, Is.EqualTo("connected"));
        }

        [Test]
        public void Parse_ReconnectingMessage()
        {
            // Zelfde voorbeeld als ReconnectScheduleTests.ReconnectingState_Serializes in de brug.
            var connection = BridgeMessages.Parse(
                "{\"type\":\"connection\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"name\":\"Smart Drive\",\"state\":\"reconnecting\",\"message\":\"\"}") as ConnectionMessage;

            Assert.That(connection, Is.Not.Null);
            Assert.That(connection.state, Is.EqualTo("reconnecting"));
        }

        [Test]
        public void ConnectCommands_MatchBridgeFormat()
        {
            Assert.That(BridgeMessages.Connect("AA:BB:CC:DD:EE:FF"),
                Is.EqualTo("{\"type\":\"connect\",\"id\":\"AA:BB:CC:DD:EE:FF\"}"));
            Assert.That(BridgeMessages.Disconnect("AA:BB:CC:DD:EE:FF"),
                Is.EqualTo("{\"type\":\"disconnect\",\"id\":\"AA:BB:CC:DD:EE:FF\"}"));
        }

        [Test]
        public void GradeCommand_UsesDecimalPointEvenInDutchCulture()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
            try
            {
                Assert.That(BridgeMessages.Grade("AA:BB:CC:DD:EE:FF", 8.5f),
                    Is.EqualTo("{\"type\":\"grade\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"grade\":8.5}"));
                Assert.That(BridgeMessages.Grade("X", -3.25f), Does.Contain("\"grade\":-3.25}"));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestCase("{\"type\":\"onbekend\"}")]
        [TestCase("geen json")]
        public void Parse_UnknownOrInvalid_ReturnsNull(string json)
        {
            Assert.That(BridgeMessages.Parse(json), Is.Null);
        }

        [Test]
        public void ScanCommand_MatchesBridgeFormat()
        {
            Assert.That(BridgeMessages.Scan(enabled: true, all: false),
                Is.EqualTo("{\"type\":\"scan\",\"enabled\":true,\"all\":false}"));
        }

        [TestCase(-45, "sterk")]
        [TestCase(-70, "goed")]
        [TestCase(-85, "zwak")]
        public void SignalLabel(int rssi, string expected)
        {
            Assert.That(BridgeMessages.SignalLabel(rssi), Is.EqualTo(expected));
        }

        [Test]
        public void KindLabels_InDutch()
        {
            Assert.That(BridgeMessages.KindLabel("trainer"), Is.EqualTo("trainer (FTMS)"));
            Assert.That(BridgeMessages.KindLabel("heartRate"), Is.EqualTo("hartslag"));
        }
    }
}
