using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class RampTestTests
    {
        [Test]
        public void TargetRisesTwentyWattsPerMinute()
        {
            var test = new RampTest();
            Assert.That(test.ErgWatts, Is.EqualTo(100));
            test.Advance(61, 100);
            Assert.That(test.ErgWatts, Is.EqualTo(120));
            test.Advance(269, 100); // 5:30
            Assert.That(test.ErgWatts, Is.EqualTo(200));
            Assert.That(test.PhaseRemaining, Is.EqualTo(30).Within(1e-9));
        }

        [Test]
        public void RiderFollowsUntilTenThirtyThenStops()
        {
            var test = new RampTest();
            for (int t = 0; t < 630; t++) test.Advance(1, test.ErgWatts);
            for (int t = 0; t < 10 && !test.Finished; t++) test.Advance(1, 0);

            // Beste minuut 9:30-10:30: 30 s op 280 W en 30 s op 300 W = 290 W; FTP 0,75 × 290 = 217,5 → 218.
            Assert.That(test.Finished, Is.True);
            Assert.That(test.BestMinuteWatts, Is.EqualTo(290).Within(1e-6));
            Assert.That(test.ResultFtp, Is.EqualTo(218));
            Assert.That(test.ErgWatts, Is.EqualTo(0));
        }

        [Test]
        public void StopsAfterFiveSecondsBelowHalfTheTarget()
        {
            var test = new RampTest();
            test.Advance(120, 140); // stap 3 (140 W)
            test.Advance(4, 60);
            Assert.That(test.Finished, Is.False);
            test.Advance(1, 60);
            Assert.That(test.Finished, Is.True);
        }

        [Test]
        public void NoFailureWhileGettingGoingInFirstStep()
        {
            var test = new RampTest();
            test.Advance(30, 0);
            Assert.That(test.Finished, Is.False);
        }

        [Test]
        public void TooShortGivesNoResult()
        {
            var test = new RampTest();
            test.Advance(40, 100);
            test.Stop();
            Assert.That(test.Finished, Is.True);
            Assert.That(test.ResultFtp, Is.Null);
        }
    }

    public class TwentyMinuteTestTests
    {
        [Test]
        public void ErgDuringWarmupAndCooldownOnly()
        {
            var test = new TwentyMinuteTest(200);
            Assert.That(test.PhaseName, Is.EqualTo("Opwarmen"));
            Assert.That(test.ErgWatts, Is.EqualTo(90));      // 45% van 200
            test.Advance(300, 100);
            Assert.That(test.ErgWatts, Is.EqualTo(120));     // 60% halverwege
            test.Advance(300, 100);
            Assert.That(test.PhaseName, Is.EqualTo("Test"));
            Assert.That(test.ErgWatts, Is.EqualTo(0));       // vrij rijden
            test.Advance(1200, 240);
            Assert.That(test.PhaseName, Is.EqualTo("Uitrijden"));
            Assert.That(test.ErgWatts, Is.EqualTo(100));     // 50%
        }

        [Test]
        public void FtpIsNinetyFivePercentOfTwentyMinutes()
        {
            var test = new TwentyMinuteTest(200);
            for (int t = 0; t < 600; t += 10) test.Advance(10, 100);
            for (int t = 0; t < 1200; t += 10) test.Advance(10, 240);
            Assert.That(test.AverageWatts, Is.EqualTo(240).Within(1e-9));
            for (int t = 0; t < 300; t += 10) test.Advance(10, 100);

            Assert.That(test.Finished, Is.True);
            Assert.That(test.ResultFtp, Is.EqualTo(228)); // 0,95 × 240
        }

        [Test]
        public void StepsAcrossPhaseBoundariesCountOnlyTheTestPart()
        {
            var test = new TwentyMinuteTest(200);
            for (int t = 0; t < 2100; t += 7) test.Advance(7, 240);
            Assert.That(test.AverageWatts, Is.EqualTo(240).Within(1e-9));
            Assert.That(test.ResultFtp, Is.EqualTo(228));
        }

        [Test]
        public void StoppingDuringTestGivesNoResultButAfterwardsDoes()
        {
            var during = new TwentyMinuteTest(200);
            during.Advance(1000, 250);
            during.Stop();
            Assert.That(during.ResultFtp, Is.Null);

            var after = new TwentyMinuteTest(200);
            after.Advance(600, 100);
            after.Advance(1200, 250);
            after.Advance(60, 100);
            after.Stop();
            Assert.That(after.ResultFtp, Is.EqualTo(238)); // 0,95 × 250 = 237,5 → 238
        }
    }
}
