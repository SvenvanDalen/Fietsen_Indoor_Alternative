using System.Collections.Generic;
using System.IO;
using System.Linq;
using FietsApp.World;
using UnityEditor;
using UnityEngine;

namespace FietsApp.Editor
{
    /// <summary>
    /// FietsApp &gt; Klimmen bijwerken (story 4.5): maakt voor elke klim uit tools/climbs.json met een GPX een
    /// <see cref="Climb"/>-asset (begroeiing volgens het biome), werkt naam, gebied en kerncijfers bij en zet alles in
    /// Routes/Bibliotheek.asset. Bestaande assets houden hun eigen instellingen (zoals de zendmast of het grind).
    /// </summary>
    public static class ClimbLibraryBuilder
    {
        const string Routes = "Assets/FietsApp/Routes/";
        const string Materials = "Assets/FietsApp/Materials/";

        [System.Serializable] sealed class Catalog { public Entry[] climbs; }
        [System.Serializable] sealed class Entry { public string id, name, region, country, biome; public SurfaceEntry[] surfaces; }
        [System.Serializable] sealed class SurfaceEntry { public float from, to; public string surface; }

        [MenuItem("FietsApp/Klimmen bijwerken")]
        public static void Run() => Debug.Log(Build());

        public static string Build()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "climbs.json"));
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
            var climbs = new List<Climb>();
            int created = 0;

            foreach (var entry in catalog.climbs)
            {
                var gpx = AssetDatabase.LoadAssetAtPath<TextAsset>(Routes + entry.id + ".gpx");
                if (gpx == null) continue; // nog niet gebouwd met tools/build_climbs.py

                string assetPath = Routes + entry.id + ".asset";
                var climb = AssetDatabase.LoadAssetAtPath<Climb>(assetPath);
                if (climb == null)
                {
                    climb = ScriptableObject.CreateInstance<Climb>();
                    ApplyBiome(climb, entry.biome);
                    AssetDatabase.CreateAsset(climb, assetPath);
                    created++;
                }

                climb.displayName = entry.name;
                climb.region = entry.region;
                climb.country = entry.country;
                climb.surfaces = (entry.surfaces ?? new SurfaceEntry[0])
                    .Select(s => new SurfaceSection { from = s.from, to = s.to, surface = Surfaces.Parse(s.surface) }).ToArray();
                climb.gpx = gpx;
                climb.terrain = AssetDatabase.LoadAssetAtPath<TextAsset>(Routes + entry.id + ".terrain.bytes");
                var stats = ClimbStats.Of(ClimbImport.FromGpx(Gpx.Parse(gpx.text)));
                climb.lengthMeters = stats.LengthMeters;
                climb.averageGradePercent = stats.AverageGradePercent;
                climb.elevationGain = stats.ElevationGain;
                EditorUtility.SetDirty(climb);
                climbs.Add(climb);
            }

            var library = AssetDatabase.LoadAssetAtPath<ClimbLibrary>(Routes + "Bibliotheek.asset");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ClimbLibrary>();
                AssetDatabase.CreateAsset(library, Routes + "Bibliotheek.asset");
            }
            library.climbs = climbs.OrderBy(c => c.region).ThenBy(c => c.displayName).ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return $"Klimbibliotheek: {library.climbs.Length} klimmen, {created} nieuw.";
        }

        /// <summary>Begroeiing per soort gebied: boomgrens, bos en de grond erboven.</summary>
        static void ApplyBiome(Climb climb, string biome)
        {
            var darkGrass = AssetDatabase.LoadAssetAtPath<Material>(Materials + "GrasDonker.mat");
            var grass = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Gras.mat");
            switch (biome)
            {
                case "alpen":
                case "dolomieten":
                    climb.treeLine = 2100f;
                    climb.forestFrom = biome == "alpen" ? 800f : 1000f;
                    climb.aboveTreeLine = darkGrass;
                    climb.gravelVergeAboveTreeLine = true;
                    break;
                case "pyreneeen":
                    climb.treeLine = 1900f;
                    climb.forestFrom = 700f;
                    climb.aboveTreeLine = darkGrass;
                    climb.gravelVergeAboveTreeLine = true;
                    break;
                case "jura":
                    climb.treeLine = 1350f;
                    climb.forestFrom = 400f;
                    climb.aboveTreeLine = grass;
                    climb.gravelVergeAboveTreeLine = false;
                    break;
                case "ventoux":
                    climb.treeLine = 1450f;
                    climb.forestFrom = 550f;
                    climb.gravelVergeAboveTreeLine = true;
                    climb.summitTower = true;
                    climb.summitLatitude = 44.17390;
                    climb.summitLongitude = 5.27843;
                    break;
                case "heuvels_bos":
                    climb.forestFrom = 250f;
                    climb.gravelVergeAboveTreeLine = false;
                    break;
                default: // heuvels: gras met losse bomen
                    climb.gravelVergeAboveTreeLine = false;
                    break;
            }
        }
    }
}
