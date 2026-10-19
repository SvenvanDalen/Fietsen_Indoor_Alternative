using System.IO;
using FietsApp.Recording;
using UnityEngine;

namespace FietsApp
{
    /// <summary>Bewaart per klim het spoor van je beste poging (story 9.4) als <c>Ghosts/&lt;klim&gt;.json</c> in de app-map.</summary>
    public static class GhostStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Ghosts");

        static string PathOf(string climbId) => Path.Combine(Folder, climbId + ".json");

        public static GhostTrace Load(string climbId)
        {
            try
            {
                string path = PathOf(climbId);
                if (!File.Exists(path)) return null;
                var trace = JsonUtility.FromJson<GhostTrace>(File.ReadAllText(path));
                return trace != null && trace.distances != null && trace.distances.Length > 1 ? trace : null;
            }
            catch (System.Exception e) when (e is IOException || e is System.ArgumentException)
            {
                Debug.LogWarning($"Ghost van {climbId} niet te lezen: {e.Message}");
                return null;
            }
        }

        public static void Save(string climbId, GhostTrace trace)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathOf(climbId), JsonUtility.ToJson(trace));
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                Debug.LogError($"Ghost van {climbId} opslaan mislukt: {e.Message}");
            }
        }
    }
}
