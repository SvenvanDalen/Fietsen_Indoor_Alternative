using FietsApp.Training;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    public class RiderProfileTests
    {
        [Test]
        public void UnknownClimbRidesAtFullGrade()
        {
            Assert.That(new RiderProfile().GradeScale("Mont Ventoux"), Is.EqualTo(100));
        }

        [Test]
        public void GradeScaleIsStoredPerClimb()
        {
            var profile = new RiderProfile();
            profile.SetGradeScale("Mont Ventoux", 75);
            profile.SetGradeScale("Alpe d'Huez", 125);
            Assert.That(profile.GradeScale("Mont Ventoux"), Is.EqualTo(75));
            Assert.That(profile.GradeScale("Alpe d'Huez"), Is.EqualTo(125));

            profile.SetGradeScale("Mont Ventoux", 50);
            Assert.That(profile.GradeScale("Mont Ventoux"), Is.EqualTo(50));
            Assert.That(profile.climbs, Has.Count.EqualTo(2));
        }

        [Test]
        public void GradeScaleIsClampedAndDefaultIsNotStored()
        {
            var profile = new RiderProfile();
            profile.SetGradeScale("A", 10);
            profile.SetGradeScale("B", 300);
            Assert.That(profile.GradeScale("A"), Is.EqualTo(50));
            Assert.That(profile.GradeScale("B"), Is.EqualTo(150));

            profile.SetGradeScale("A", 100);
            Assert.That(profile.climbs, Has.Count.EqualTo(1));
        }

        [Test]
        public void SurvivesJsonRoundTrip()
        {
            var profile = new RiderProfile { ftpWatts = 245, ftpTestedOn = "2026-10-17" };
            profile.SetGradeScale("Mont Ventoux (Bédoin)", 75);

            var copy = JsonUtility.FromJson<RiderProfile>(JsonUtility.ToJson(profile));

            Assert.That(copy.ftpWatts, Is.EqualTo(245));
            Assert.That(copy.ftpTestedOn, Is.EqualTo("2026-10-17"));
            Assert.That(copy.GradeScale("Mont Ventoux (Bédoin)"), Is.EqualTo(75));
        }

        [Test]
        public void OldProfileWithoutClimbsStillLoads()
        {
            var copy = JsonUtility.FromJson<RiderProfile>("{\"ftpWatts\":230,\"ftpTestedOn\":\"\"}");
            Assert.That(copy.ftpWatts, Is.EqualTo(230));
            Assert.That(copy.GradeScale("X"), Is.EqualTo(100));
        }
    }
}
