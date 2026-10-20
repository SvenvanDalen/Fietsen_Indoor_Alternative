using System.Collections.Generic;

namespace FietsApp.Training
{
    /// <summary>
    /// Ingebouwde workouts (story 8.2), van rustig tot zwaar. Doelen als deel van de FTP, zodat ze voor iedereen
    /// passen; eigen workouts maak je in de workout-editor (8.3).
    /// </summary>
    public static class WorkoutLibrary
    {
        const double Min = 60;

        public static readonly Workout[] All =
        {
            new Workout("Herstelrit", "30 min rustig in zone 1-2", new[]
            {
                new WorkoutStep("Inrijden", 5 * Min, 0.40, 0.50),
                new WorkoutStep("Rustig", 20 * Min, 0.52),
                new WorkoutStep("Uitrijden", 5 * Min, 0.50, 0.40),
            }),
            new Workout("Duur 60", "60 min duurtempo in zone 2", new[]
            {
                new WorkoutStep("Opwarmen", 10 * Min, 0.40, 0.65),
                new WorkoutStep("Duur", 45 * Min, 0.68),
                new WorkoutStep("Uitrijden", 5 * Min, 0.55, 0.40),
            }),
            new Workout("Sweet spot 3×10", "3 × 10 min op 90% FTP", Repeat(
                new[] { new WorkoutStep("Opwarmen", 10 * Min, 0.40, 0.70) },
                3, new[] { new WorkoutStep("Sweet spot", 10 * Min, 0.90), new WorkoutStep("Herstel", 5 * Min, 0.50) },
                new[] { new WorkoutStep("Uitrijden", 5 * Min, 0.55, 0.40) })),
            new Workout("Drempel 2×20", "2 × 20 min net onder de FTP", new[]
            {
                new WorkoutStep("Opwarmen", 10 * Min, 0.40, 0.75),
                new WorkoutStep("Drempel", 20 * Min, 0.97),
                new WorkoutStep("Herstel", 5 * Min, 0.50),
                new WorkoutStep("Drempel", 20 * Min, 0.97),
                new WorkoutStep("Uitrijden", 10 * Min, 0.55, 0.40),
            }),
            new Workout("Over-unders 3×9", "3 × 9 min wisselend 95% en 108% FTP", Repeat(
                new[] { new WorkoutStep("Opwarmen", 12 * Min, 0.40, 0.75) },
                3, OverUnderSet(),
                new[] { new WorkoutStep("Uitrijden", 8 * Min, 0.55, 0.40) })),
            new Workout("VO2max 5×3", "5 × 3 min op 115% FTP", Repeat(
                new[] { new WorkoutStep("Opwarmen", 12 * Min, 0.40, 0.75), new WorkoutStep("Herstel", 3 * Min, 0.50) },
                5, new[] { new WorkoutStep("VO2max", 3 * Min, 1.15), new WorkoutStep("Herstel", 3 * Min, 0.50) },
                new[] { new WorkoutStep("Uitrijden", 8 * Min, 0.55, 0.40) })),
            new Workout("Piramide", "1-2-3-4-3-2-1 min op 105% FTP, even lang herstel", Pyramid()),
        };

        static IEnumerable<WorkoutStep> Repeat(WorkoutStep[] before, int times, WorkoutStep[] block, WorkoutStep[] after)
        {
            foreach (var s in before) yield return s;
            for (int i = 0; i < times; i++)
                foreach (var s in block)
                    yield return s;
            foreach (var s in after) yield return s;
        }

        /// <summary>Drie keer 2 min onder en 1 min boven de FTP, dan 5 min herstel.</summary>
        static WorkoutStep[] OverUnderSet()
        {
            var set = new List<WorkoutStep>();
            for (int i = 0; i < 3; i++)
            {
                set.Add(new WorkoutStep("Onder", 2 * Min, 0.95));
                set.Add(new WorkoutStep("Over", 1 * Min, 1.08));
            }
            set.Add(new WorkoutStep("Herstel", 5 * Min, 0.50));
            return set.ToArray();
        }

        static IEnumerable<WorkoutStep> Pyramid()
        {
            yield return new WorkoutStep("Opwarmen", 10 * Min, 0.40, 0.75);
            foreach (int minutes in new[] { 1, 2, 3, 4, 3, 2, 1 })
            {
                yield return new WorkoutStep("Blok", minutes * Min, 1.05);
                yield return new WorkoutStep("Herstel", minutes * Min, 0.50);
            }
            yield return new WorkoutStep("Uitrijden", 5 * Min, 0.55, 0.40);
        }
    }
}
