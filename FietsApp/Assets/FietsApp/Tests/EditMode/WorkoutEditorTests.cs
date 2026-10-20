using System;
using System.Linq;
using FietsApp.Training;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Workout-editor en .zwo-bestanden (story 8.3).</summary>
    public class WorkoutEditorTests
    {
        static Workout SweetSpot => WorkoutLibrary.All.First(w => w.Name == "Sweet spot 3×10");

        [Test]
        public void Zwo_RoundTripKeepsEveryBlock()
        {
            var original = SweetSpot;
            var back = ZwoFormat.Parse(ZwoFormat.Write(original));
            Assert.AreEqual(original.Name, back.Name);
            Assert.AreEqual(original.Description, back.Description);
            Assert.AreEqual(original.Steps.Count, back.Steps.Count);
            for (int i = 0; i < original.Steps.Count; i++)
            {
                Assert.AreEqual(original.Steps[i].Name, back.Steps[i].Name);
                Assert.AreEqual(original.Steps[i].Seconds, back.Steps[i].Seconds, 1e-9);
                Assert.AreEqual(original.Steps[i].StartFtp, back.Steps[i].StartFtp, 1e-9);
                Assert.AreEqual(original.Steps[i].EndFtp, back.Steps[i].EndFtp, 1e-9);
            }
        }

        [Test]
        public void Zwo_WritesZwiftElements()
        {
            string xml = ZwoFormat.Write(SweetSpot);
            StringAssert.Contains("<sportType>bike</sportType>", xml);
            StringAssert.Contains("<Warmup Duration=\"600\" PowerLow=\"0.4\" PowerHigh=\"0.7\">", xml);
            StringAssert.Contains("<SteadyState Duration=\"600\" Power=\"0.9\">", xml);
            StringAssert.Contains("<Cooldown Duration=\"300\" PowerLow=\"0.55\" PowerHigh=\"0.4\">", xml);
            StringAssert.Contains("<textevent timeoffset=\"0\" message=\"Sweet spot\" />", xml);
        }

        [Test]
        public void Zwo_ReadsAWorkoutFromElsewhere()
        {
            const string xml = @"<workout_file>
  <author>Iemand</author><name>Zwift test</name><description>gedeeld</description><sportType>bike</sportType>
  <workout>
    <Warmup Duration=""300"" PowerLow=""0.25"" PowerHigh=""0.75""/>
    <IntervalsT Repeat=""2"" OnDuration=""60"" OffDuration=""30"" OnPower=""1.2"" OffPower=""0.5""/>
    <SteadyState Duration=""120"" PowerLow=""0.8"" PowerHigh=""0.9""/>
    <FreeRide Duration=""60""/>
    <Cooldown Duration=""240"" PowerLow=""0.6"" PowerHigh=""0.3""/>
  </workout>
</workout_file>";
            var w = ZwoFormat.Parse(xml);
            Assert.AreEqual("Zwift test", w.Name);
            Assert.AreEqual(8, w.Steps.Count); // opwarmen, 2 × (interval + herstel), blok, vrij, uitrijden
            Assert.AreEqual(900, w.TotalSeconds, 1e-9);
            Assert.AreEqual("Opwarmen", w.Steps[0].Name);
            Assert.AreEqual(0.25, w.Steps[0].StartFtp, 1e-9);
            Assert.AreEqual(0.75, w.Steps[0].EndFtp, 1e-9);
            Assert.AreEqual("Interval", w.Steps[1].Name);
            Assert.AreEqual(1.2, w.Steps[3].StartFtp, 1e-9);
            Assert.AreEqual("Herstel", w.Steps[4].Name);
            Assert.AreEqual(0.85, w.Steps[5].StartFtp, 1e-9, "zonder Power het midden van PowerLow en PowerHigh");
            Assert.AreEqual("Tempo", w.Steps[5].Name);
            Assert.AreEqual("Uitrijden", w.Steps[7].Name);
            Assert.AreEqual(0.3, w.Steps[7].EndFtp, 1e-9);
        }

        [Test]
        public void Zwo_RejectsOtherFiles()
        {
            Assert.Throws<FormatException>(() => ZwoFormat.Parse("geen xml"));
            Assert.Throws<FormatException>(() => ZwoFormat.Parse("<gpx></gpx>"));
            Assert.Throws<FormatException>(() => ZwoFormat.Parse("<workout_file><workout/></workout_file>"));
        }

        [Test]
        public void Draft_DragReordersBlocks()
        {
            var draft = WorkoutDraft.From(SweetSpot); // opwarmen 10, (10 + 5) × 3, uitrijden 5 min
            Assert.AreEqual(600, draft.StartOf(1), 1e-9);
            Assert.AreEqual(1, draft.IndexAt(600));
            Assert.AreEqual(0, draft.IndexAt(-50));
            Assert.AreEqual(draft.Blocks.Count - 1, draft.IndexAt(1e6));

            var last = draft.Blocks[^1];
            draft.Move(draft.Blocks.Count - 1, 0);
            Assert.AreSame(last, draft.Blocks[0]);
            draft.Move(0, 99); // voorbij het eind: achteraan
            Assert.AreSame(last, draft.Blocks[^1]);
        }

        [Test]
        public void Draft_SnapsAndClamps()
        {
            var draft = new WorkoutDraft();
            int i = draft.Add(ramp: false);
            draft.SetSeconds(i, 95);
            Assert.AreEqual(90, draft.Blocks[i].Seconds);
            draft.SetSeconds(i, 10);
            Assert.AreEqual(30, draft.Blocks[i].Seconds);
            draft.SetPower(i, 0.87);
            Assert.AreEqual(0.85, draft.Blocks[i].StartFtp, 1e-9);
            Assert.AreEqual(0.85, draft.Blocks[i].EndFtp, 1e-9);
            Assert.AreEqual("Tempo", draft.Blocks[i].Name);
            draft.SetPower(i, 5.0);
            Assert.AreEqual(2.0, draft.Blocks[i].StartFtp, 1e-9);
        }

        [Test]
        public void Draft_RampKeepsItsSlopeWhenShifted()
        {
            var draft = new WorkoutDraft();
            int i = draft.Add(ramp: true); // 50 → 80%
            draft.Blocks[i].Name = "Opwarmen";
            draft.SetPower(i, 0.60);
            Assert.AreEqual(0.60, draft.Blocks[i].StartFtp, 1e-9);
            Assert.AreEqual(0.90, draft.Blocks[i].EndFtp, 1e-9);
            Assert.AreEqual("Opwarmen", draft.Blocks[i].Name, "zelfde richting: naam blijft");
            draft.SetPower(i, 0.40, end: true);
            Assert.AreEqual(0.60, draft.Blocks[i].StartFtp, 1e-9);
            Assert.AreEqual(0.40, draft.Blocks[i].EndFtp, 1e-9);
            Assert.AreEqual("Afbouw", draft.Blocks[i].Name);
        }

        [Test]
        public void Draft_BecomesARideableWorkout()
        {
            var draft = new WorkoutDraft { Name = "  Klimblokken " };
            draft.Add(ramp: true);
            int b = draft.Add(ramp: false);
            draft.SetPower(b, 1.0);
            draft.Duplicate(b);
            Assert.AreEqual(3, draft.Blocks.Count);
            var w = draft.ToWorkout();
            Assert.AreEqual("Klimblokken", w.Name);
            Assert.AreEqual(900, w.TotalSeconds, 1e-9);
            // (0,65·300 + 1,0·300 + 1,0·300) / 900 = 88%.
            Assert.AreEqual("eigen workout, gemiddeld 88% FTP", w.Description);
            Assert.Throws<InvalidOperationException>(() => new WorkoutDraft().ToWorkout());
        }
    }
}
