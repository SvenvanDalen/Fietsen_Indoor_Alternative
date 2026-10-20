using System;
using System.Collections.Generic;
using System.Linq;

namespace FietsApp.Training
{
    /// <summary>Eén blok in de workout-editor; een ramp heeft een ander eind dan begin.</summary>
    public sealed class DraftBlock
    {
        public string Name;
        public double Seconds;
        public double StartFtp, EndFtp;

        public bool IsRamp => Math.Abs(EndFtp - StartFtp) > 1e-9;
    }

    /// <summary>
    /// De workout die je in de editor bouwt (story 8.3): blokken toevoegen, verschuiven, langer of zwaarder maken.
    /// Slepen en pijltjes ronden af op 30 s en 5% FTP; de grenzen houden een workout rijdbaar.
    /// </summary>
    public sealed class WorkoutDraft
    {
        public const double SecondsStep = 30, MinSeconds = 30, MaxSeconds = 2 * 3600;
        public const double FtpStep = 0.05, MinFtp = 0.30, MaxFtp = 2.00;

        public string Name = "Mijn workout";
        public readonly List<DraftBlock> Blocks = new List<DraftBlock>();

        public double TotalSeconds => Blocks.Sum(b => b.Seconds);

        /// <summary>Begintijd van blok <paramref name="index"/> in seconden vanaf de start.</summary>
        public double StartOf(int index) => Blocks.Take(index).Sum(b => b.Seconds);

        /// <summary>Het blok op tijdstip <paramref name="seconds"/>; ervoor is het eerste, erna het laatste.</summary>
        public int IndexAt(double seconds)
        {
            double t = 0;
            for (int i = 0; i < Blocks.Count; i++)
            {
                t += Blocks[i].Seconds;
                if (seconds < t) return i;
            }
            return Math.Max(0, Blocks.Count - 1);
        }

        public static WorkoutDraft From(Workout workout)
        {
            var draft = new WorkoutDraft { Name = workout.Name };
            foreach (var s in workout.Steps)
                draft.Blocks.Add(new DraftBlock { Name = s.Name, Seconds = s.Seconds, StartFtp = s.StartFtp, EndFtp = s.EndFtp });
            return draft;
        }

        /// <summary>De workout om te rijden of op te slaan, met een korte beschrijving van duur en gemiddelde.</summary>
        public Workout ToWorkout()
        {
            if (Blocks.Count == 0) throw new InvalidOperationException("Een workout heeft minstens één blok.");
            var steps = Blocks.Select(b => new WorkoutStep(b.Name, b.Seconds, b.StartFtp, b.EndFtp)).ToList();
            var probe = new Workout(Name, "", steps);
            string name = string.IsNullOrWhiteSpace(Name) ? "Mijn workout" : Name.Trim();
            return new Workout(name, $"eigen workout, {Math.Round(probe.TotalSeconds / 60)} min, gemiddeld {probe.AverageFtp * 100:0}% FTP", steps);
        }

        /// <summary>
        /// Voegt een blok in na <paramref name="after"/> (of achteraan): 5 min duurtempo, of een ramp van 50 naar 80% FTP.
        /// Geeft de plek van het nieuwe blok terug.
        /// </summary>
        public int Add(bool ramp, int after = -1)
        {
            var block = ramp
                ? new DraftBlock { Name = "Ramp", Seconds = 300, StartFtp = 0.50, EndFtp = 0.80 }
                : new DraftBlock { Seconds = 300, StartFtp = 0.70, EndFtp = 0.70 };
            if (!ramp) block.Name = ZwoFormat.StepName(block.StartFtp);
            int index = after >= 0 && after < Blocks.Count ? after + 1 : Blocks.Count;
            Blocks.Insert(index, block);
            return index;
        }

        /// <summary>Kopie van een blok er direct achter, voor herhalingen.</summary>
        public int Duplicate(int index)
        {
            var b = Blocks[index];
            Blocks.Insert(index + 1, new DraftBlock { Name = b.Name, Seconds = b.Seconds, StartFtp = b.StartFtp, EndFtp = b.EndFtp });
            return index + 1;
        }

        public void Remove(int index) => Blocks.RemoveAt(index);

        /// <summary>Verplaatst een blok naar plek <paramref name="to"/> (slepen); de andere schuiven op.</summary>
        public void Move(int from, int to)
        {
            to = Math.Max(0, Math.Min(Blocks.Count - 1, to));
            if (from == to) return;
            var b = Blocks[from];
            Blocks.RemoveAt(from);
            Blocks.Insert(to, b);
        }

        /// <summary>Nieuwe duur, afgerond op 30 s en tussen 30 s en 2 uur.</summary>
        public void SetSeconds(int index, double seconds) =>
            Blocks[index].Seconds = Clamp(Math.Round(seconds / SecondsStep) * SecondsStep, MinSeconds, MaxSeconds);

        /// <summary>
        /// Nieuw doel (deel van de FTP), afgerond op 5%. Bij een ramp kies je met <paramref name="end"/> het begin of het
        /// eind; <c>null</c> zet het hele blok (een ramp blijft dan even steil). De naam volgt de nieuwe zone.
        /// </summary>
        public void SetPower(int index, double ftp, bool? end = null)
        {
            var b = Blocks[index];
            ftp = Clamp(Math.Round(ftp / FtpStep) * FtpStep, MinFtp, MaxFtp);
            if (end == true) b.EndFtp = ftp;
            else if (end == false) b.StartFtp = ftp;
            else
            {
                double shift = ftp - b.StartFtp;
                b.StartFtp = ftp;
                b.EndFtp = Clamp(b.EndFtp + shift, MinFtp, MaxFtp);
            }
            b.Name = b.IsRamp ? (b.EndFtp > b.StartFtp ? "Opbouw" : "Afbouw") : ZwoFormat.StepName(b.StartFtp);
        }

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
