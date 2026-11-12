using System;
using System.Collections.Generic;
using System.Linq;
using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class RiderNumbersTests
    {
        [Test]
        public void VamIsMetersPerHour() => Assert.That(RiderNumbers.Vam(300, 1200), Is.EqualTo(900).Within(1e-9)); // 300 m in 20 min

        [Test]
        public void WattsPerKg() => Assert.That(RiderNumbers.WattsPerKg(252, 72), Is.EqualTo(3.5).Within(1e-9));

        [Test]
        public void UnknownInputsGiveZero()
        {
            Assert.That(RiderNumbers.Vam(100, 0), Is.EqualTo(0));
            Assert.That(RiderNumbers.WattsPerKg(200, 0), Is.EqualTo(0));
        }
    }

    public class WPrimeBalanceTests
    {
        [Test]
        public void SpendsTheExcessOverCriticalPower()
        {
            var w = new WPrimeBalance(250);
            for (int i = 0; i < 100; i++) w.Add(300, 1); // 50 W te veel, 100 s: 5 kJ
            Assert.That(w.Balance, Is.EqualTo(15000).Within(1e-6));
            Assert.That(w.Fraction, Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void RecoversWithSkibaTimeConstant()
        {
            var w = new WPrimeBalance(250);
            for (int i = 0; i < 500; i++) w.Add(300, 1); // leeg
            Assert.That(w.Balance, Is.EqualTo(0).Within(1e-6));
            w.Add(150, 60); // DCP 100 W: tau = 546·e^-1 + 316 = 516,86 s; 20000·(1 − e^(−60/516,86)) = 2192,01 J
            Assert.That(w.Balance, Is.EqualTo(2192.011).Within(0.01));
        }

        [Test]
        public void KeepsTheLowestPointAndNeverExceedsFull()
        {
            var w = new WPrimeBalance(250);
            w.Add(350, 100); // 10 kJ weg
            w.Add(100, 10000);
            Assert.That(w.Lowest, Is.EqualTo(10000).Within(1e-6));
            Assert.That(w.Balance, Is.LessThanOrEqualTo(w.WPrime));
            Assert.That(w.LowestFraction, Is.EqualTo(0.5).Within(1e-9));
        }
    }

    public class PowerCurveTests
    {
        [Test]
        public void BestAverageFindsTheBestWindow()
        {
            var p = new float[] { 100, 100, 300, 300, 100, 100 };
            Assert.That(PowerCurve.BestAverage(p, 2), Is.EqualTo(300).Within(1e-9));
            Assert.That(PowerCurve.BestAverage(p, 4), Is.EqualTo(200).Within(1e-9)); // 100+300+300+100 = 800 / 4
            Assert.That(PowerCurve.BestAverage(p, 7), Is.Null);
        }

        [Test]
        public void RecordKeepsEveryRideAndTheBestWins()
        {
            var bests = new List<PowerBest>();
            PowerCurve.Record(bests, Enumerable.Repeat(200f, 400).ToArray(), "2026-01-01");
            PowerCurve.Record(bests, Enumerable.Repeat(250f, 400).ToArray(), "2026-02-01");
            Assert.That(PowerCurve.Of(bests, 300), Is.EqualTo(250));
            Assert.That(PowerCurve.Of(bests, 1200), Is.EqualTo(0)); // geen rit van 20 minuten
            var curve = PowerCurve.Curve(bests);
            Assert.That(curve.Select(c => c.seconds), Is.EqualTo(new[] { 5, 60, 300 }));
            Assert.That(curve.Last().on, Is.EqualTo("2026-02-01"));
        }

        [Test]
        public void SinceLeavesOutOldRides()
        {
            var bests = new List<PowerBest>
            {
                new PowerBest { seconds = 300, watts = 400, on = "2025-01-01" },
                new PowerBest { seconds = 300, watts = 300, on = "2026-10-20" },
            };
            var recent = PowerCurve.Since(bests, new DateTime(2026, 11, 12), 90);
            Assert.That(PowerCurve.Of(recent, 300), Is.EqualTo(300));
            Assert.That(PowerCurve.Of(bests, 300), Is.EqualTo(400));
        }

        [Test]
        public void EftpIs95PercentOfBestTwentyMinutesAndSuggestsOnlyBeyondTheMargin()
        {
            var bests = new List<PowerBest> { new PowerBest { seconds = 1200, watts = 263, on = "2026-11-01" } };
            Assert.That(PowerCurve.Eftp(bests), Is.EqualTo(250)); // 0,95 × 263 = 249,85
            Assert.That(PowerCurve.SuggestedFtp(bests, 200), Is.EqualTo(250));
            Assert.That(PowerCurve.SuggestedFtp(bests, 248), Is.Null);
            Assert.That(PowerCurve.SuggestedFtp(new List<PowerBest>(), 200), Is.Null);
        }
    }

    public class SteepAheadTests
    {
        // 0-500 m vlak, 500-1300 m 11%, daarna vlak.
        static readonly float[] D = { 0, 500, 1300, 2000 };
        static readonly float[] E = { 0, 0, 88, 88 };

        [Test]
        public void FindsTheSteepStretchAhead()
        {
            var s = SteepAhead.Next(D, E, 200f, 10f);
            Assert.That(s, Is.Not.Null);
            Assert.That(s.Value.StartM, Is.EqualTo(300).Within(1));
            Assert.That(s.Value.LengthM, Is.EqualTo(800).Within(1));
            Assert.That(s.Value.AverageGrade, Is.EqualTo(11).Within(0.01));
            Assert.That(s.Value.Describe(s.Value.StartM), Is.EqualTo("over 300 m: 800 m aan 11%"));
        }

        [Test]
        public void SaysNowWhenYouAreOnIt()
        {
            var s = SteepAhead.Next(D, E, 700f, 10f);
            Assert.That(s.Value.StartM, Is.EqualTo(0).Within(1));
            Assert.That(s.Value.Describe(s.Value.StartM), Does.StartWith("nu:"));
        }

        [Test]
        public void NothingBeyondTheLookAheadOrBelowTheThreshold()
        {
            Assert.That(SteepAhead.Next(D, E, 0f, 10f, 300f), Is.Null);   // begint pas na 500 m
            Assert.That(SteepAhead.Next(D, E, 200f, 12f), Is.Null);       // 11% haalt de drempel niet
        }

        [Test]
        public void SteepestFindsTheSteepestWindow()
        {
            var s = SteepAhead.Steepest(D, E);
            Assert.That(s.Value.AverageGrade, Is.EqualTo(11).Within(0.01));
            Assert.That(s.Value.StartM, Is.InRange(500f, 1100f));
        }
    }
}
