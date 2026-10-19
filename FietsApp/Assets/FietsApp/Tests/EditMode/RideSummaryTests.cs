using System;
using FietsApp.Recording;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class RideSummaryTests
    {
        /// <summary>Rit van <paramref name="seconds"/> metingen, elke seconde 10 m verder en 1 m hoger, op 200 W (op de klim 300 W).</summary>
        static RideLog Log(int seconds)
        {
            var log = new RideLog();
            var start = new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < seconds; i++)
            {
                float d = i * 10f;
                log.Add(new RideSample(start.AddSeconds(i), d >= 100f && d <= 300f ? 300f : 200f, 85f, 0f, 10f, d, 500f + i));
            }
            return log;
        }

        [Test]
        public void TimerAtDistanceInterpolates()
        {
            var log = Log(50);
            Assert.That(log.TimerAtDistance(15f), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(log.TimerAtDistance(-5f), Is.EqualTo(0.0), "rit begon er al voorbij");
            Assert.That(log.TimerAtDistance(10000f), Is.Null, "nog niet gehaald");
        }

        [Test]
        public void ClimbTimeAndPower()
        {
            var log = Log(50);
            // Voet op 100 m (seconde 10), top op 305 m (seconde 30,5).
            Assert.That(log.SecondsBetween(100f, 305f), Is.EqualTo(20.5).Within(1e-9));
            // Metingen op 100..300 m: 21 stuks, allemaal 300 W.
            Assert.That(log.AveragePowerBetween(100f, 305f), Is.EqualTo(300f));
            Assert.That(log.SecondsBetween(100f, 5000f), Is.Null);
        }

        [Test]
        public void NoClimbTimeWhenStartedOnTheClimb()
        {
            var log = Log(50);
            // Eerste meting op 0 m: een voet 20 m daarvoor telt nog (binnen 25 m), 30 m daarvoor betekent dat de rit op de klim begon.
            Assert.That(log.SecondsBetween(-20f, 100f), Is.EqualTo(10.0).Within(1e-9));
            Assert.That(log.SecondsBetween(-30f, 100f), Is.Null);
        }

        [Test]
        public void ClockFormat()
        {
            Assert.That(RideSummaryText.Clock(3725.4), Is.EqualTo("1:02:05"));
            Assert.That(RideSummaryText.Clock(59.6), Is.EqualTo("0:01:00"));
        }

        [Test]
        public void SummaryLines()
        {
            var log = Log(50);
            string text = RideSummaryText.Of(log, log.SecondsBetween(100f, 305f), 300f);
            StringAssert.Contains("Tijd:</color>  <b>0:00:50", text);
            StringAssert.Contains("Klimtijd:</color>  <b>0:00:21", text);
            StringAssert.Contains("Hoogtemeters:</color>  <b>49 m", text);
            StringAssert.DoesNotContain("Hartslag", text, "geen hartslagmeter");

            StringAssert.Contains("top niet gehaald", RideSummaryText.Of(log, null, 0f));
        }
    }
}
