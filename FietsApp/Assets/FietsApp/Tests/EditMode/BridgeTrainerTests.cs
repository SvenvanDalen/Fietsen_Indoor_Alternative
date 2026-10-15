using FietsApp.Trainers;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class BridgeTrainerTests
    {
        [Test]
        public void Apply_SetsValues_AndIgnoresMissingOnes()
        {
            var trainer = new BridgeTrainer();

            trainer.Apply(power: 250f, cadence: 90f, heartRate: -1f, time: 0);
            trainer.Apply(power: -1f, cadence: -1f, heartRate: 140f, time: 0.5);

            Assert.That(trainer.PowerWatts, Is.EqualTo(250f));
            Assert.That(trainer.CadenceRpm, Is.EqualTo(90f));
            Assert.That(trainer.HeartRateBpm, Is.EqualTo(140f));
        }

        [Test]
        public void NegativePower_IsReportedAsZero()
        {
            var trainer = new BridgeTrainer();

            trainer.Apply(power: -5f, cadence: -1f, heartRate: -1f, time: 0);

            // -5 W is een geldige meting (sint16) maar kan de rijder niet voortstuwen;
            // -1 is "niet meegestuurd" en verandert niets.
            Assert.That(trainer.PowerWatts, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithoutNewPower_FallsBackToZero()
        {
            var trainer = new BridgeTrainer();
            trainer.Apply(250f, 90f, 140f, time: 0);

            trainer.Tick(2.9);
            Assert.That(trainer.PowerWatts, Is.EqualTo(250f));

            trainer.Tick(3.1);
            Assert.That(trainer.PowerWatts, Is.EqualTo(0f));
            Assert.That(trainer.CadenceRpm, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithoutNewHeartRate_ShowsNoData()
        {
            var trainer = new BridgeTrainer();
            trainer.Apply(-1f, -1f, 140f, time: 0);

            trainer.Tick(5.1);

            Assert.That(trainer.HeartRateBpm, Is.EqualTo(0f));
        }

        [Test]
        public void SetGrade_IsClampedToTrainerMaximum()
        {
            var trainer = new BridgeTrainer(maxGradePercent: 20f);

            trainer.SetGrade(22f);

            Assert.That(trainer.GradePercent, Is.EqualTo(20f));
        }
    }
}
