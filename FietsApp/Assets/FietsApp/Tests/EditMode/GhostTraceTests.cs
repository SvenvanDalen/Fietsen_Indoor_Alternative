using System;
using FietsApp.Recording;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class GhostTraceTests
    {
        /// <summary>Elke seconde 10 m verder: voet op 100 m (t = 10 s), top op 305 m (t = 30,5 s).</summary>
        static RideLog Log(int seconds)
        {
            var log = new RideLog();
            var start = new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < seconds; i++)
                log.Add(new RideSample(start.AddSeconds(i), 250f, 85f, 0f, 10f, i * 10f, 500f));
            return log;
        }

        [Test]
        public void TraceFromFootToSummit()
        {
            var trace = GhostTrace.FromLog(Log(50), 100f, 305f, "Proef", "2026-10-19");
            Assert.That(trace.seconds, Is.EqualTo(20.5).Within(1e-9));
            Assert.That(trace.length, Is.EqualTo(205f));
            // 22 punten: 0, 10, ..., 200 m en de top (205 m).
            Assert.That(trace.distances, Has.Length.EqualTo(22));
            Assert.That(trace.distances[3], Is.EqualTo(30f).Within(1e-4f));
            Assert.That(trace.distances[21], Is.EqualTo(205f));
        }

        [Test]
        public void PositionAndTimeAreEachOthersInverse()
        {
            var trace = GhostTrace.FromLog(Log(50), 100f, 305f, "Proef", "2026-10-19");
            Assert.That(trace.DistanceAt(-3), Is.EqualTo(0f), "wacht aan de voet");
            Assert.That(trace.DistanceAt(5.5), Is.EqualTo(55f).Within(1e-4f));
            Assert.That(trace.DistanceAt(25), Is.EqualTo(205f), "blijft op de top");
            Assert.That(trace.TimeAt(55f), Is.EqualTo(5.5).Within(1e-6));
            Assert.That(trace.TimeAt(205f), Is.EqualTo(20.5).Within(1e-9));
            Assert.That(trace.TimeAt(203f), Is.LessThanOrEqualTo(20.5));
        }

        [Test]
        public void NoTraceWithoutTheWholeClimb()
        {
            Assert.That(GhostTrace.FromLog(Log(20), 100f, 305f, "Proef", ""), Is.Null, "top niet gehaald");
            Assert.That(GhostTrace.FromLog(Log(50), -100f, 305f, "Proef", ""), Is.Null, "halverwege begonnen");
        }

        [Test]
        public void SurvivesJson()
        {
            var trace = GhostTrace.FromLog(Log(50), 100f, 305f, "Proef", "2026-10-19");
            var copy = UnityEngine.JsonUtility.FromJson<GhostTrace>(UnityEngine.JsonUtility.ToJson(trace));
            Assert.That(copy.distances, Is.EqualTo(trace.distances));
            Assert.That(copy.DistanceAt(5.5), Is.EqualTo(55f).Within(1e-4f));
        }
    }
}
