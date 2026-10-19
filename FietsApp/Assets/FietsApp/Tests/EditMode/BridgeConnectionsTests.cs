using System.Linq;
using FietsApp.Trainers;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class BridgeConnectionsTests
    {
        const string Id = "AA:BB:CC:DD:EE:FF";

        static BridgeConnections Connected()
        {
            var c = new BridgeConnections();
            c.Connecting(Id, "Smart Drive", 0);
            Assert.That(c.Apply(Id, "Smart Drive", "connected", "", 1), Is.True);
            return c;
        }

        [Test]
        public void DeviceDropsAndComesBack()
        {
            var c = Connected();
            Assert.That(c.Apply(Id, "Smart Drive", "reconnecting", "", 10), Is.False);
            Assert.That(c.AnyConnected, Is.False, "geen data nu");
            Assert.That(c.HoldsTrainer, Is.True, "rit blijft op de trainer");
            Assert.That(c.IsActive(Id), Is.True, "knop ontkoppelt");
            Assert.That(c.Apply(Id, "", "connected", "", 14), Is.True, "weer verbonden: helling opnieuw sturen");
            Assert.That(c.TrainerName, Is.EqualTo("Smart Drive"), "naam blijft bekend");
        }

        [Test]
        public void BridgeRestartAsksForTheSameDevicesAgain()
        {
            var c = Connected();
            c.Apply("11:22:33:44:55:66", "Band", "failed", "niet gevonden", 2);
            c.BridgeLost(5);
            Assert.That(c.HoldsTrainer, Is.True);
            Assert.That(c.BridgeBack(), Is.EqualTo(new[] { Id }), "mislukte apparaten niet opnieuw");
            Assert.That(c.All.Count(), Is.EqualTo(1));
        }

        [Test]
        public void GivesUpAfterAMinute()
        {
            var c = Connected();
            c.Apply(Id, "Smart Drive", "reconnecting", "", 10);
            c.Apply(Id, "Smart Drive", "reconnecting", "", 30); // herhaald bericht verschuift het begin niet
            Assert.That(c.Expire(69.9), Is.False);
            Assert.That(c.Expire(70.0), Is.True);
            Assert.That(c.HoldsTrainer, Is.False, "terug naar de simulator");
            Assert.That(c.All.Single().Message, Is.EqualTo("verbinding niet hersteld"));
            Assert.That(c.IsActive(Id), Is.False, "knop verbindt opnieuw");
        }

        [Test]
        public void DeliberateDisconnectRemoves()
        {
            var c = Connected();
            c.Apply(Id, "Smart Drive", "disconnected", "", 3);
            Assert.That(c.All, Is.Empty);
            Assert.That(c.HoldsTrainer, Is.False);
        }
    }
}
