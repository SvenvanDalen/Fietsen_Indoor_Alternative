using System.IO;
using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Laadt en bewaart het rijdersprofiel als <c>profiel.json</c> in <see cref="Application.persistentDataPath"/>.
    /// Eén gedeeld exemplaar voor de hele app, zodat HUD, FTP-test en workouts dezelfde FTP zien.
    /// </summary>
    public static class ProfileStore
    {
        static RiderProfile _current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "profiel.json");

        public static RiderProfile Current => _current ??= Load();

        /// <summary>Bijvoorbeeld na een FTP-test; meteen opslaan.</summary>
        public static event System.Action Changed;

        public static void Save()
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Current, prettyPrint: true));
            Changed?.Invoke();
        }

        static RiderProfile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var profile = JsonUtility.FromJson<RiderProfile>(File.ReadAllText(FilePath));
                    if (profile != null && profile.ftpWatts > 0) return profile;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Profiel niet te lezen, standaardwaarden gebruikt: {e.Message}");
            }
            return new RiderProfile();
        }
    }
}
