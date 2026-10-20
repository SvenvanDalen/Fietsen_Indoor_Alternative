using System.IO;
using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Laadt en bewaart het rijdersprofiel als <c>profiel.json</c> in <see cref="Application.persistentDataPath"/>.
    /// Eén gedeeld exemplaar voor de hele app, zodat HUD, FTP-test en workouts dezelfde FTP zien. Met een GitHub-account
    /// gaat het ook naar een privé-Gist (<see cref="GitHubAccount"/>, story 7.2); lees het daarom altijd via
    /// <see cref="Current"/>, want na het samenvoegen is het een nieuw exemplaar.
    /// </summary>
    public static class ProfileStore
    {
        static RiderProfile _current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "profiel.json");

        public static RiderProfile Current => _current ??= Load();

        /// <summary>Bijvoorbeeld na een FTP-test; meteen opslaan.</summary>
        public static event System.Action Changed;

        /// <summary>Bewaart een wijziging; die krijgt de tijd van nu, zodat de nieuwste versie wint bij het samenvoegen.</summary>
        public static void Save()
        {
            Current.updatedAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            Write();
        }

        /// <summary>Vervangt het profiel door een samengevoegde versie (uit de Gist), zonder de wijzigtijd aan te passen.</summary>
        public static void Replace(RiderProfile profile)
        {
            _current = profile;
            Write();
        }

        public static string ToJson(RiderProfile profile) => JsonUtility.ToJson(profile, prettyPrint: true);

        /// <summary>Profiel uit JSON, of null als het geen bruikbaar profiel is.</summary>
        public static RiderProfile FromJson(string json)
        {
            try
            {
                var profile = JsonUtility.FromJson<RiderProfile>(json);
                return profile != null && profile.ftpWatts > 0 ? profile : null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        static void Write()
        {
            File.WriteAllText(FilePath, ToJson(Current));
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
