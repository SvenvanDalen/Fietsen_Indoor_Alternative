using FietsApp.Trainers;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class SimulatorTrainerTests
    {
        [Test]
        public void SetGrade_IsClampedToTrainerMaximum()
        {
            var trainer = new SimulatorTrainer(maxGradePercent: 20f);

            trainer.SetGrade(25f);
            Assert.That(trainer.GradePercent, Is.EqualTo(20f));

            trainer.SetGrade(-30f);
            Assert.That(trainer.GradePercent, Is.EqualTo(-20f));
        }

        [Test]
        public void TargetPower_IsClampedBetweenZeroAndMaximum()
        {
            var trainer = new SimulatorTrainer(maxPowerWatts: 1200f);

            trainer.TargetPowerWatts = 5000f;
            Assert.That(trainer.TargetPowerWatts, Is.EqualTo(1200f));

            trainer.TargetPowerWatts = -10f;
            Assert.That(trainer.TargetPowerWatts, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_ReportsPowerAndCadence()
        {
            var trainer = new SimulatorTrainer { TargetPowerWatts = 200f };

            trainer.Tick(0.1f);

            Assert.That(trainer.PowerWatts, Is.EqualTo(200f));
            Assert.That(trainer.CadenceRpm, Is.GreaterThan(0f));
        }

        [Test]
        public void Tick_NoPower_NoCadence()
        {
            var trainer = new SimulatorTrainer();

            trainer.Tick(0.1f);

            Assert.That(trainer.CadenceRpm, Is.EqualTo(0f));
        }

        [Test]
        public void HeartRate_RisesGraduallyWithPower()
        {
            var trainer = new SimulatorTrainer { TargetPowerWatts = 250f };
            float start = trainer.HeartRateBpm;

            trainer.Tick(1f);
            float afterOneSecond = trainer.HeartRateBpm;
            for (int i = 0; i < 300; i++) trainer.Tick(1f);

            Assert.That(afterOneSecond, Is.GreaterThan(start));
            Assert.That(afterOneSecond, Is.LessThan(trainer.HeartRateBpm));
            Assert.That(trainer.HeartRateBpm, Is.EqualTo(60f + 0.3f * 250f).Within(1f));
        }
    }
}
