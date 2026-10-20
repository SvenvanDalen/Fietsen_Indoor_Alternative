using System;
using FietsApp.Recording;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Etappe van meerdere klimmen (story 4.7).</summary>
    public class StageTests
    {
        [Test]
        public void ClimbsFromOneRegion_InOrder()
        {
            var stage = new Stage();
            Assert.IsFalse(stage.CanStart);
            Assert.AreEqual(StageChoice.Added, stage.Toggle("AlpeDHuez", "Alpen"));
            Assert.IsFalse(stage.CanStart, "één klim is nog geen etappe");
            Assert.AreEqual(StageChoice.Added, stage.Toggle("Galibier", "Alpen"));
            Assert.IsTrue(stage.CanStart);
            Assert.AreEqual(StageChoice.OtherRegion, stage.Toggle("Tourmalet", "Pyreneeën"));
            CollectionAssert.AreEqual(new[] { "AlpeDHuez", "Galibier" }, stage.Climbs);
            Assert.AreEqual("Alpen", stage.Region);
        }

        [Test]
        public void AtMostFourClimbs()
        {
            var stage = new Stage();
            foreach (var id in new[] { "a", "b", "c", "d" })
                Assert.AreEqual(StageChoice.Added, stage.Toggle(id, "Alpen"));
            Assert.AreEqual(StageChoice.Full, stage.Toggle("e", "Alpen"));
            Assert.AreEqual(4, stage.Count);
        }

        [Test]
        public void ChoosingAgainRemoves_LastOneFreesTheRegion()
        {
            var stage = new Stage();
            stage.Toggle("a", "Alpen");
            stage.Toggle("b", "Alpen");
            Assert.AreEqual(StageChoice.Removed, stage.Toggle("a", "Alpen"));
            CollectionAssert.AreEqual(new[] { "b" }, stage.Climbs);
            stage.Toggle("b", "Alpen");
            Assert.IsNull(stage.Region);
            Assert.AreEqual(StageChoice.Added, stage.Toggle("Tourmalet", "Pyreneeën"));
        }

        /// <summary><paramref name="seconds"/> metingen op <paramref name="watts"/>, elke seconde <paramref name="step"/> m verder en <paramref name="climb"/> m hoger.</summary>
        static RideLog Log(int seconds, float watts, float step, float climb)
        {
            var log = new RideLog();
            var start = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < seconds; i++)
                log.Add(new RideSample(start.AddSeconds(i), watts, 85f, 0f, step, i * step, 500f + i * climb));
            return log;
        }

        [Test]
        public void SummaryTotalsOverAllClimbs()
        {
            var legs = new[]
            {
                new StageLeg("Alpe d'Huez", Log(60, 200f, 10f, 1f), 45.0),
                new StageLeg("Col du Galibier", Log(120, 300f, 5f, 2f), null),
            };
            string text = StageSummaryText.Of(legs);
            // 60 + 120 s; 590 + 595 m; (200·60 + 300·120) / 180 = 266,7 W; 59 + 238 m.
            StringAssert.Contains("Tijd:</color>  <b>0:03:00", text);
            StringAssert.Contains("Afstand:</color>  <b>1.2 km", text);
            StringAssert.Contains("Gemiddeld vermogen:</color>  <b>267 W", text);
            StringAssert.Contains("Hoogtemeters:</color>  <b>297 m", text);
            StringAssert.Contains("1. Alpe d'Huez:</color>  <b>0:00:45", text);
            StringAssert.Contains("2. Col du Galibier:</color>  <b><color=#bbb>top niet gehaald", text);
        }
    }
}
