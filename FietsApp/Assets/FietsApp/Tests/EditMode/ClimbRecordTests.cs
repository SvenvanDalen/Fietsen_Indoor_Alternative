using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class ClimbRecordTests
    {
        [Test]
        public void FirstAttemptIsTheRecord()
        {
            var profile = new RiderProfile();
            var result = profile.AddClimbTime("Alpe d'Huez", 3725, "2026-10-19");
            Assert.That(result.IsFirst, Is.True);
            Assert.That(result.Describe(), Is.EqualTo("eerste keer: je record"));
            Assert.That(profile.Record("Alpe d'Huez").bestSeconds, Is.EqualTo(3725));
            Assert.That(profile.Record("Mont Ventoux"), Is.Null);
        }

        [Test]
        public void FasterIsANewRecord()
        {
            var profile = new RiderProfile();
            profile.AddClimbTime("Alpe d'Huez", 3725, "2026-10-19");
            var result = profile.AddClimbTime("Alpe d'Huez", 3693, "2026-10-20");
            Assert.That(result.IsRecord, Is.True);
            // 3725 − 3693 = 32 s; de vorige poging was het record zelf, dus die wordt niet apart genoemd.
            Assert.That(result.Describe(), Is.EqualTo("nieuw record, 0:32 sneller"));
            var record = profile.Record("Alpe d'Huez");
            Assert.That(record.bestSeconds, Is.EqualTo(3693));
            Assert.That(record.bestOn, Is.EqualTo("2026-10-20"));
            Assert.That(record.attempts, Is.EqualTo(2));
        }

        [Test]
        public void SlowerKeepsTheRecordAndComparesWithLastTime()
        {
            var profile = new RiderProfile();
            profile.AddClimbTime("Stelvio", 5400, "2026-10-01"); // 1:30:00
            profile.AddClimbTime("Stelvio", 5600, "2026-10-08"); // 1:33:20
            var result = profile.AddClimbTime("Stelvio", 5520, "2026-10-15"); // 1:32:00
            Assert.That(result.IsRecord, Is.False);
            // +120 s op het record, 80 s sneller dan vorige keer.
            Assert.That(result.Describe(), Is.EqualTo("+2:00 op je record (1:30:00), 1:20 sneller dan vorige keer"));
            Assert.That(profile.Record("Stelvio").bestSeconds, Is.EqualTo(5400));
            Assert.That(profile.Record("Stelvio").lastSeconds, Is.EqualTo(5520));
        }

        [Test]
        public void ClockUnderAndOverAnHour()
        {
            Assert.That(ClimbResult.Clock(59.5), Is.EqualTo("1:00"));
            Assert.That(ClimbResult.Clock(3599), Is.EqualTo("59:59"));
            Assert.That(ClimbResult.Clock(3725), Is.EqualTo("1:02:05"));
        }

        [Test]
        public void SurvivesJsonRoundTrip()
        {
            var profile = new RiderProfile();
            profile.AddClimbTime("Koppenberg", 95.4, "2026-10-19");
            var copy = UnityEngine.JsonUtility.FromJson<RiderProfile>(UnityEngine.JsonUtility.ToJson(profile));
            Assert.That(copy.Record("Koppenberg").bestSeconds, Is.EqualTo(95.4).Within(1e-9));
        }
    }
}
