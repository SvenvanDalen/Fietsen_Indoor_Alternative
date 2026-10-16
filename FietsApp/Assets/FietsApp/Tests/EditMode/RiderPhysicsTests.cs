using FietsApp.Physics;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class RiderPhysicsTests
    {
        // Referentiewaarden los berekend (bisectie op de vermogensformule uit backlog sectie 3),
        // met Crr = 0.004, CdA = 0.32 m², rho = 1.225 kg/m³, g = 9.80665 m/s².
        [TestCase(250f, 80f, 0f, 37.272f)]
        [TestCase(200f, 75f, 0f, 34.456f)]
        [TestCase(300f, 85f, 5f, 20.936f)]
        [TestCase(250f, 75f, 8f, 13.948f)]
        [TestCase(400f, 70f, 10f, 18.846f)]
        [TestCase(150f, 90f, 3f, 15.955f)]
        [TestCase(100f, 80f, -2f, 38.132f)]
        public void SteadySpeed_MatchesReferenceWithin1Percent(float power, float mass, float grade, float expectedKmh)
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = mass });

            float kmh = physics.SteadySpeed(power, grade) * 3.6f;

            Assert.That(kmh, Is.EqualTo(expectedKmh).Within(1).Percent);
        }

        [Test]
        public void ResistancePower_AtSteadySpeed_EqualsInputPower()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });

            float v = physics.SteadySpeed(300f, 6f);

            Assert.That(physics.ResistancePower(v, 6f), Is.EqualTo(300f).Within(0.5f));
        }

        [Test]
        public void Step_WithConstantPower_ConvergesToSteadySpeed()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 0f;

            for (int i = 0; i < 120 * 50; i++)
                v = physics.Step(v, 250f, 4f, 0.02f);

            Assert.That(v, Is.EqualTo(physics.SteadySpeed(250f, 4f)).Within(1).Percent);
        }

        [Test]
        public void Step_WithoutPowerOnDescent_Accelerates()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });

            float v = physics.Step(5f, 0f, -8f, 1f);

            Assert.That(v, Is.GreaterThan(5f));
        }

        [Test]
        public void Step_WithoutPowerOnFlat_SlowsDownButNeverReverses()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 10f;

            for (int i = 0; i < 1000; i++)
                v = physics.Step(v, 0f, 0f, 0.1f);

            Assert.That(v, Is.LessThan(10f));
            Assert.That(v, Is.GreaterThanOrEqualTo(0f));
        }

        // Referenties los uitgerekend (Python): eindsnelheid waar zwaartekracht = rolweerstand + luchtweerstand (+ rem),
        // 80 kg, Crr 0,004, CdA 0,32, rho 1,225.
        [TestCase(-5f, 13.561f)]
        [TestCase(-8f, 17.414f)]
        [TestCase(-10f, 19.554f)]
        public void Freewheeling_ReachesTerminalSpeedOnDescent(float grade, float expectedMs)
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 0f;

            for (int i = 0; i < 300 * 50; i++)
                v = physics.Step(v, 0f, grade, 0.02f);

            Assert.That(v, Is.EqualTo(expectedMs).Within(1).Percent);
        }

        [Test]
        public void LightBraking_LowersTerminalSpeed()
        {
            // 10% remmen = 0,4 m/s² extra; op -8% levert dat 11,831 m/s in plaats van 17,4.
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 0f;

            for (int i = 0; i < 300 * 50; i++)
                v = physics.Step(v, 0f, -8f, 0.02f, brake: 0.1f);

            Assert.That(v, Is.EqualTo(11.831f).Within(1).Percent);
        }

        [Test]
        public void FullBraking_StopsFromFiftyFourKmhWithinFourSeconds()
        {
            // Vlak, vanaf 15 m/s: stil na 3,56 s (los nagerekend).
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 15f, t = 0f;

            while (v > 0f && t < 10f)
            {
                v = physics.Step(v, 0f, 0f, 0.01f, brake: 1f);
                t += 0.01f;
            }

            Assert.That(t, Is.EqualTo(3.56f).Within(0.05f));
        }

        [Test]
        public void FullBraking_HoldsStillOnSteepDescent()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 0f;

            for (int i = 0; i < 100; i++)
                v = physics.Step(v, 0f, -10f, 0.02f, brake: 1f);

            Assert.That(v, Is.EqualTo(0f));
        }

        [Test]
        public void Step_WithoutPowerUphill_StopsAtZero()
        {
            var physics = new RiderPhysics(new RiderParameters { TotalMassKg = 80f });
            float v = 3f;

            for (int i = 0; i < 100; i++)
                v = physics.Step(v, 0f, 10f, 0.1f);

            Assert.That(v, Is.EqualTo(0f));
        }
    }
}
