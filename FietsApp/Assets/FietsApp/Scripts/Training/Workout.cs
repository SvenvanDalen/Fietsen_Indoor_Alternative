using System;
using System.Collections.Generic;
using System.Linq;

namespace FietsApp.Training
{
    /// <summary>
    /// Eén blok van een workout (story 8.2): een duur en een doel als deel van de FTP. Met een ander eind dan
    /// begin loopt het doel geleidelijk op of af (opwarmen, uitrijden, ramp).
    /// </summary>
    public sealed class WorkoutStep
    {
        public string Name { get; }
        public double Seconds { get; }
        public double StartFtp { get; }
        public double EndFtp { get; }

        public WorkoutStep(string name, double seconds, double startFtp, double? endFtp = null)
        {
            if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Name = name;
            Seconds = seconds;
            StartFtp = startFtp;
            EndFtp = endFtp ?? startFtp;
        }

        public bool IsRamp => Math.Abs(EndFtp - StartFtp) > 1e-9;

        /// <summary>Doel (deel van de FTP) na <paramref name="elapsed"/> seconden in dit blok.</summary>
        public double FtpAt(double elapsed) =>
            StartFtp + (EndFtp - StartFtp) * Math.Max(0, Math.Min(1, elapsed / Seconds));
    }

    public sealed class Workout
    {
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<WorkoutStep> Steps { get; }
        public double TotalSeconds { get; }

        public Workout(string name, string description, IEnumerable<WorkoutStep> steps)
        {
            Name = name;
            Description = description;
            Steps = steps.ToArray();
            if (Steps.Count == 0) throw new ArgumentException("Een workout heeft minstens één blok.", nameof(steps));
            TotalSeconds = Steps.Sum(s => s.Seconds);
        }

        /// <summary>Gemiddeld doel als deel van de FTP, gewogen naar tijd (ramps tellen voor hun gemiddelde).</summary>
        public double AverageFtp => Steps.Sum(s => (s.StartFtp + s.EndFtp) / 2 * s.Seconds) / TotalSeconds;
    }

    /// <summary>
    /// Speelt een workout af (story 8.2): houdt bij welk blok bezig is en wat het doelvermogen nu is.
    /// De tijd loopt alleen als <see cref="Advance"/> wordt aangeroepen, dus pauzeren is niet aanroepen.
    /// </summary>
    public sealed class WorkoutPlayer
    {
        public WorkoutPlayer(Workout workout) => Workout = workout;

        public Workout Workout { get; }
        public double Elapsed { get; private set; }
        public int StepIndex { get; private set; }
        public double StepElapsed { get; private set; }
        public bool Finished { get; private set; }

        public WorkoutStep Current => Workout.Steps[StepIndex];
        public WorkoutStep Next => StepIndex + 1 < Workout.Steps.Count ? Workout.Steps[StepIndex + 1] : null;
        public double StepRemaining => Finished ? 0 : Current.Seconds - StepElapsed;
        public double Remaining => Math.Max(0, Workout.TotalSeconds - Elapsed);

        /// <summary>Doel nu, als deel van de FTP; 0 als de workout klaar is.</summary>
        public double TargetFtp => Finished ? 0 : Current.FtpAt(StepElapsed);

        /// <summary>Doelvermogen in hele watts bij deze FTP; 0 als de workout klaar is.</summary>
        public int TargetWatts(int ftpWatts) => (int)Math.Round(TargetFtp * ftpWatts);

        /// <summary>Laat <paramref name="dt"/> seconden verstrijken; geeft true als er een nieuw blok begon.</summary>
        public bool Advance(double dt)
        {
            if (Finished || dt <= 0) return false;
            Elapsed += dt;
            StepElapsed += dt;
            bool changed = false;
            while (StepElapsed >= Current.Seconds)
            {
                if (StepIndex + 1 >= Workout.Steps.Count)
                {
                    Finished = true;
                    StepElapsed = Current.Seconds;
                    Elapsed = Workout.TotalSeconds;
                    return true;
                }
                StepElapsed -= Current.Seconds;
                StepIndex++;
                changed = true;
            }
            return changed;
        }

        /// <summary>Spring naar het volgende blok (bijvoorbeeld een opwarming overslaan).</summary>
        public void Skip()
        {
            if (Finished) return;
            Advance(StepRemaining);
        }
    }
}
