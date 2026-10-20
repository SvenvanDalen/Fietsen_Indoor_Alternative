using FietsApp.Training;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    /// <summary>Everesting over meerdere ritten (story 9.3).</summary>
    public class EverestingTests
    {
        [Test]
        public void NotActive_RidesDoNotCount()
        {
            var e = new EverestingChallenge();
            Assert.IsFalse(e.AddRide(1200f, "2026-10-20"));
            Assert.AreEqual(0f, e.climbedMeters);
            Assert.AreEqual(0, e.rides);
        }

        [Test]
        public void Rides_AddUpUntilEverest()
        {
            var e = new EverestingChallenge();
            e.Start("2026-10-01");
            Assert.IsFalse(e.AddRide(1610f, "2026-10-01")); // Ventoux
            Assert.IsFalse(e.AddRide(0f, "2026-10-02"));    // vlakke rit telt niet als rit
            Assert.IsFalse(e.AddRide(1530f, "2026-10-03"));
            Assert.AreEqual(3140f, e.climbedMeters, 0.01f);
            Assert.AreEqual(2, e.rides);
            Assert.AreEqual(3140f / 8848f, e.Progress(), 1e-5f);
            Assert.AreEqual("", e.completedOn);
        }

        [Test]
        public void PassingTarget_CompletesOnce()
        {
            var e = new EverestingChallenge();
            e.Start("2026-10-01");
            e.AddRide(8000f, "2026-10-01");
            Assert.IsTrue(e.AddRide(900f, "2026-10-05"));
            Assert.IsTrue(e.IsComplete);
            Assert.AreEqual("2026-10-05", e.completedOn);
            Assert.IsFalse(e.AddRide(500f, "2026-10-06")); // daarna geen tweede keer "gehaald"
            Assert.AreEqual("2026-10-05", e.completedOn);
            Assert.AreEqual(1f, e.Progress());
        }

        [Test]
        public void Start_ResetsAndStopKeepsTotal()
        {
            var e = new EverestingChallenge();
            e.Start("2026-10-01");
            e.AddRide(9000f, "2026-10-01");
            e.Stop();
            Assert.IsFalse(e.active);
            Assert.AreEqual(9000f, e.climbedMeters);
            e.Start("2026-10-20");
            Assert.IsTrue(e.active);
            Assert.AreEqual(0f, e.climbedMeters);
            Assert.AreEqual(0, e.rides);
            Assert.AreEqual("", e.completedOn);
            Assert.AreEqual("2026-10-20", e.startedOn);
        }

        [Test]
        public void Describe_IncludesCurrentRide()
        {
            var e = new EverestingChallenge();
            e.Start("2026-10-01");
            for (int i = 0; i < 4; i++) e.AddRide(780.175f, "2026-10-01"); // 3120,7 m
            Assert.AreEqual("Everesting 3.120 / 8.848 m (35%), 4 ritten", e.Describe());
            Assert.AreEqual("Everesting 3.620 / 8.848 m (41%), 5 ritten", e.Describe(500f));
        }

        [Test]
        public void Describe_WhenDone()
        {
            var e = new EverestingChallenge();
            e.Start("2026-10-01");
            e.AddRide(8900f, "2026-10-02");
            Assert.AreEqual("Everest gehaald op 2026-10-02: 8.900 m in 1 rit", e.Describe());
        }

        [Test]
        public void Profile_SavesChallenge()
        {
            var profile = new RiderProfile();
            Assert.IsFalse(profile.everesting.active);
            profile.everesting.Start("2026-10-01");
            profile.everesting.AddRide(1234f, "2026-10-01");
            var back = JsonUtility.FromJson<RiderProfile>(JsonUtility.ToJson(profile));
            Assert.IsTrue(back.everesting.active);
            Assert.AreEqual(1234f, back.everesting.climbedMeters);
            Assert.AreEqual(1, back.everesting.rides);
        }

        [Test]
        public void OldProfile_WithoutChallenge_LoadsInactive()
        {
            var back = JsonUtility.FromJson<RiderProfile>("{\"ftpWatts\": 250}");
            Assert.IsNotNull(back.everesting);
            Assert.IsFalse(back.everesting.active);
        }
    }
}
