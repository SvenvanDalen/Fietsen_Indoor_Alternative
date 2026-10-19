using FietsApp.Physics;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class SurfaceTests
    {
        static readonly SurfaceSection[] Finestre =
        {
            new SurfaceSection { from = 10480f, to = 100000f, surface = Surface.Gravel },
        };

        [Test]
        public void SectionsDecideTheSurface()
        {
            Assert.That(Surfaces.At(Finestre, 5000f), Is.EqualTo(Surface.Asphalt));
            Assert.That(Surfaces.At(Finestre, 10480f), Is.EqualTo(Surface.Gravel));
            Assert.That(Surfaces.At(Finestre, 17000f), Is.EqualTo(Surface.Gravel));
            Assert.That(Surfaces.At(null, 100f), Is.EqualTo(Surface.Asphalt));
        }

        [TestCase("gravel", Surface.Gravel)]
        [TestCase("cobbles", Surface.Cobbles)]
        [TestCase("snow", Surface.Snow)]
        [TestCase("onbekend", Surface.Asphalt)]
        public void ParsesTheCatalog(string name, Surface expected) => Assert.That(Surfaces.Parse(name), Is.EqualTo(expected));

        [Test]
        public void RougherIsHeavier()
        {
            Assert.That(Surfaces.RollingResistance(Surface.Cobbles), Is.GreaterThan(Surfaces.RollingResistance(Surface.Gravel)));
            Assert.That(Surfaces.RollingResistance(Surface.Gravel), Is.GreaterThan(Surfaces.RollingResistance(Surface.Asphalt)));
            // Kasseien: (0,012 − 0,004) · 100 = 0,8 procentpunt extra helling op de trainer.
            Assert.That(Surfaces.TrainerGradeBonus(Surface.Cobbles), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(Surfaces.TrainerGradeBonus(Surface.Asphalt), Is.EqualTo(0f));
            Assert.That(Surfaces.Shake(Surface.Asphalt), Is.EqualTo(0f));
            Assert.That(Surfaces.Shake(Surface.Cobbles), Is.GreaterThan(Surfaces.Shake(Surface.Gravel)));
        }

        [Test]
        public void CobblesCostSpeedAtTheSamePower()
        {
            // 300 W op 11,6% (Koppenberg), 80 kg, CdA 0,32. Evenwicht los uitgerekend (bisectie op
            // P = v·(m·g·sinθ + Crr·m·g·cosθ + ½ρ·CdA·v²)): asfalt 3,143 m/s, kasseien 2,957 m/s.
            var asphalt = new RiderPhysics(new RiderParameters { RollingResistance = Surfaces.RollingResistance(Surface.Asphalt) });
            var cobbles = new RiderPhysics(new RiderParameters { RollingResistance = Surfaces.RollingResistance(Surface.Cobbles) });
            Assert.That(asphalt.SteadySpeed(300f, 11.6f), Is.EqualTo(3.143f).Within(0.005f));
            Assert.That(cobbles.SteadySpeed(300f, 11.6f), Is.EqualTo(2.957f).Within(0.005f));
        }
    }
}
