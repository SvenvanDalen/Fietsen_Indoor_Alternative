using System.Collections.Generic;
using System.IO;
using System.Linq;
using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Eigen workouts (story 8.3) als <c>.zwo</c> in <c>Workouts/</c> in de app-map. Wat je daar neerzet (een gedeelde
    /// workout, of een export uit Zwift of Intervals.icu) staat daarna in de workoutlijst.
    /// </summary>
    public static class WorkoutStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Workouts");

        /// <summary>Alle leesbare workouts in de map, op naam; onleesbare bestanden worden overgeslagen met een waarschuwing.</summary>
        public static List<(Workout workout, string path)> LoadAll()
        {
            var result = new List<(Workout, string)>();
            if (!Directory.Exists(Folder)) return result;
            foreach (var path in Directory.GetFiles(Folder, "*.zwo").OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    result.Add((ZwoFormat.Parse(File.ReadAllText(path)), path));
                }
                catch (System.Exception e) when (e is IOException || e is System.FormatException)
                {
                    Debug.LogWarning($"Workout {Path.GetFileName(path)} niet te lezen: {e.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Bewaart een workout onder zijn naam; met <paramref name="replaces"/> (het bestand dat je bewerkte) wordt dat
        /// bestand vervangen, ook als de naam veranderd is. Geeft het pad terug, of null als opslaan mislukte.
        /// </summary>
        public static string Save(Workout workout, string replaces = null)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string safe = new string(workout.Name.Select(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_').ToArray()).Trim();
                string path = Path.Combine(Folder, (safe.Length > 0 ? safe : "workout") + ".zwo");
                File.WriteAllText(path, ZwoFormat.Write(workout));
                if (replaces != null && !string.Equals(Path.GetFullPath(replaces), Path.GetFullPath(path), System.StringComparison.OrdinalIgnoreCase)
                    && File.Exists(replaces))
                    File.Delete(replaces);
                return path;
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                Debug.LogError($"Workout opslaan mislukt: {e.Message}");
                return null;
            }
        }
    }
}
