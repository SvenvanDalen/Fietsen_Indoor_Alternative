using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class PowerZonesTests
    {
        // FTP 250 W: grenzen op 137,5 / 187,5 / 225 / 262,5 / 300 / 375 W (55/75/90/105/120/150%).
        [TestCase(0, 1)]
        [TestCase(137.5, 1)]
        [TestCase(138, 2)]
        [TestCase(187.5, 2)]
        [TestCase(188, 3)]
        [TestCase(225, 3)]
        [TestCase(226, 4)]
        [TestCase(250, 4)]
        [TestCase(262.5, 4)]
        [TestCase(263, 5)]
        [TestCase(300, 5)]
        [TestCase(301, 6)]
        [TestCase(375, 6)]
        [TestCase(376, 7)]
        [TestCase(1200, 7)]
        public void ZoneFollowsCogganBounds(double watts, int zone)
        {
            Assert.That(PowerZones.Of(watts, 250).Number, Is.EqualTo(zone));
        }

        [Test]
        public void ZonesHaveNamesAndDistinctColours()
        {
            Assert.That(PowerZones.All, Has.Length.EqualTo(7));
            Assert.That(PowerZones.All, Has.All.Matches<PowerZone>(z => z.Name.Length > 0));
            Assert.That(System.Array.ConvertAll(PowerZones.All, z => z.Rgb), Is.Unique);
            Assert.That(PowerZones.Of(240, 250).Label, Is.EqualTo("Z4 Drempel"));
        }

        [Test]
        public void LowerWattsMatchesBounds()
        {
            Assert.That(PowerZones.LowerWatts(1, 250), Is.EqualTo(0));
            Assert.That(PowerZones.LowerWatts(4, 250), Is.EqualTo(225).Within(1e-9));
            Assert.That(PowerZones.LowerWatts(7, 250), Is.EqualTo(375).Within(1e-9));
        }

        [Test]
        public void WithoutFtpEverythingIsZoneOne()
        {
            Assert.That(PowerZones.Of(300, 0).Number, Is.EqualTo(1));
        }
    }

    public class RollingAverageTests
    {
        [Test]
        public void AveragesOverWindowOnly()
        {
            var avg = new RollingAverage(3);
            avg.Add(100, 2);
            avg.Add(400, 2);
            // Laatste 3 s: 1 s op 100 W en 2 s op 400 W → 900 / 3 = 300 W.
            Assert.That(avg.Value, Is.EqualTo(300).Within(1e-9));
        }

        [Test]
        public void ShortHistoryAveragesWhatThereIs()
        {
            var avg = new RollingAverage(3);
            avg.Add(200, 0.5);
            avg.Add(100, 0.5);
            Assert.That(avg.Value, Is.EqualTo(150).Within(1e-9));
        }

        [Test]
        public void ManySmallFramesStayStable()
        {
            var avg = new RollingAverage(3);
            for (int i = 0; i < 1000; i++) avg.Add(i < 500 ? 100 : 300, 0.02);
            // Laatste 3 s (150 frames) allemaal op 300 W.
            Assert.That(avg.Value, Is.EqualTo(300).Within(1e-6));
            avg.Clear();
            Assert.That(avg.Value, Is.EqualTo(0));
        }
    }
}
