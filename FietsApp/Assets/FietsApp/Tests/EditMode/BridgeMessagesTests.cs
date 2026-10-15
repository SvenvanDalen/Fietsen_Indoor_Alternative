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
