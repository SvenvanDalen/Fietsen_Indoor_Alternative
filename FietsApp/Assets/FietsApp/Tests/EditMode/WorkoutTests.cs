using System.Linq;
using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class WorkoutTests
    {
        static Workout Sample() => new Workout("Test", "", new[]
        {
            new WorkoutStep("Opwarmen", 100, 0.40, 0.60),
            new WorkoutStep("Blok", 60, 1.00),
            new WorkoutStep("Uitrijden", 40, 0.50),
        });

        [Test]
        public void TotalAndAverage()
        {
            var w = Sample();
            Assert.That(w.TotalSeconds, Is.EqualTo(200));
            // (0,50·100 + 1,00·60 + 0,50·40) / 200 = 130 / 200 = 0,65.
            Assert.That(w.AverageFtp, Is.EqualTo(0.65).Within(1e-9));
        }

        [Test]
        public void RampInterpolatesWithinStep()
        {
            var player = new WorkoutPlayer(Sample());
            Assert.That(player.TargetWatts(250), Is.EqualTo(100)); // 0,40 × 250
            player.Advance(50);
            Assert.That(player.TargetFtp, Is.EqualTo(0.50).Within(1e-9));
            Assert.That(player.TargetWatts(250), Is.EqualTo(125));
        }

        [Test]
        public void AdvanceCrossesSteps()
        {
            var player = new WorkoutPlayer(Sample());
            Assert.That(player.Advance(99), Is.False);
            Assert.That(player.Advance(11), Is.True); // 110 s: 10 s in het blok
            Assert.That(player.StepIndex, Is.EqualTo(1));
            Assert.That(player.StepElapsed, Is.EqualTo(10).Within(1e-9));
            Assert.That(player.StepRemaining, Is.EqualTo(50).Within(1e-9));
            Assert.That(player.TargetWatts(250), Is.EqualTo(250));
            Assert.That(player.Next.Name, Is.EqualTo("Uitrijden"));
            Assert.That(player.Remaining, Is.EqualTo(90).Within(1e-9));
        }

        [Test]
        public void BigStepSkipsSeveralBlocks()
        {
            var player = new WorkoutPlayer(Sample());
            player.Advance(170);
            Assert.That(player.StepIndex, Is.EqualTo(2));
            Assert.That(player.StepElapsed, Is.EqualTo(10).Within(1e-9));
            Assert.That(player.Next, Is.Null);
        }

        [Test]
        public void FinishesAndStopsTarget()
        {
            var player = new WorkoutPlayer(Sample());
            Assert.That(player.Advance(250), Is.True);
            Assert.That(player.Finished, Is.True);
            Assert.That(player.Elapsed, Is.EqualTo(200));
            Assert.That(player.TargetWatts(250), Is.EqualTo(0));
            Assert.That(player.Advance(10), Is.False);
        }

        [Test]
        public void SkipJumpsToNextStep()
        {
            var player = new WorkoutPlayer(Sample());
            player.Advance(30);
            player.Skip();
            Assert.That(player.StepIndex, Is.EqualTo(1));
            Assert.That(player.StepElapsed, Is.EqualTo(0).Within(1e-9));
            Assert.That(player.Elapsed, Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void NoTimePassesWithoutAdvance()
        {
            var player = new WorkoutPlayer(Sample());
            Assert.That(player.Advance(0), Is.False);
            Assert.That(player.Advance(-5), Is.False);
            Assert.That(player.Elapsed, Is.EqualTo(0));
        }

        // Duur met de hand opgeteld uit de opbouw in WorkoutLibrary (minuten).
        [TestCase("Herstelrit", 30)]
        [TestCase("Duur 60", 60)]
        [TestCase("Sweet spot 3×10", 60)]   // 10 + 3 × (10 + 5) + 5
        [TestCase("Drempel 2×20", 65)]      // 10 + 20 + 5 + 20 + 10
        [TestCase("Over-unders 3×9", 62)]   // 12 + 3 × (9 + 5) + 8
        [TestCase("VO2max 5×3", 53)]        // 12 + 3 + 5 × 6 + 8
        [TestCase("Piramide", 47)]          // 10 + 2 × 16 + 5
        public void LibraryDurations(string name, int minutes)
        {
            var workout = WorkoutLibrary.All.Single(w => w.Name == name);
            Assert.That(workout.TotalSeconds, Is.EqualTo(minutes * 60));
        }

        [Test]
        public void LibraryIsSane()
        {
            Assert.That(WorkoutLibrary.All.Select(w => w.Name), Is.Unique);
            Assert.That(WorkoutLibrary.All.Length, Is.LessThanOrEqualTo(9), "cijfertoetsen 1-9 in de lijst");
            foreach (var w in WorkoutLibrary.All)
            {
                Assert.That(w.AverageFtp, Is.InRange(0.4, 1.0), w.Name);
                Assert.That(w.Steps.SelectMany(s => new[] { s.StartFtp, s.EndFtp }), Has.All.InRange(0.3, 1.5), w.Name);
            }
        }
    }

    public class ErgTests
    {
        [Test]
        public void SimulatorDeliversErgPower()
        {
            var sim = new FietsApp.Trainers.SimulatorTrainer { TargetPowerWatts = 150f };
            sim.SetErgWatts(230f);
            sim.Tick(0.1f);
            Assert.That(sim.PowerWatts, Is.EqualTo(230f));
            sim.SetErgWatts(0f);
            sim.Tick(0.1f);
            Assert.That(sim.PowerWatts, Is.EqualTo(150f));
        }

        [Test]
        public void BridgeTrainerRoundsAndReportsOnlyChanges()
        {
            var trainer = new FietsApp.Trainers.BridgeTrainer();
            var sent = new System.Collections.Generic.List<int>();
            trainer.ErgChanged += sent.Add;
            trainer.SetErgWatts(200.4f);
            trainer.SetErgWatts(199.6f);
            trainer.SetErgWatts(250f);
            trainer.SetErgWatts(-3f);
            Assert.That(sent, Is.EqualTo(new[] { 200, 250, 0 }));
            Assert.That(trainer.ErgWatts, Is.EqualTo(0f));
        }

        [Test]
        public void PowerMessageMatchesBridgeSample()
        {
            // Zelfde voorbeeld als Bridge/FietsBridge.Tests/ProtocolTests.cs (Parse_PowerCommand).
            Assert.That(FietsApp.Bluetooth.BridgeMessages.Power("AA:BB:CC:DD:EE:FF", 250),
                Is.EqualTo("{\"type\":\"power\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"watts\":250}"));
            Assert.That(FietsApp.Bluetooth.BridgeMessages.Power("AA:BB:CC:DD:EE:FF", -5),
                Is.EqualTo("{\"type\":\"power\",\"id\":\"AA:BB:CC:DD:EE:FF\",\"watts\":0}"));
        }
    }
}
