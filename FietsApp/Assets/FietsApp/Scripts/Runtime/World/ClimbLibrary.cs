using System.Collections.Generic;
using System.IO;
using FietsApp.Climbs;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Alle klimmen die het ritmenu toont (stories 4.5 en 4.15): de pakketten die met de app zijn meegeleverd
    /// (Resources/Klimmen) en de pakketten die de app later heeft gedownload (story 4.16), eenmalig ingelezen.
    /// </summary>
    public static class ClimbLibrary
    {
        /// <summary>Map onder Resources met de meegeleverde pakketten en catalogus.json.</summary>
        public const string ResourcesFolder = "Klimmen";

        static ClimbShelf _shelf;

        /// <summary>Waar gedownloade pakketten staan; ze tellen mee vanaf de volgende start.</summary>
        public static string DownloadFolder => Path.Combine(Application.persistentDataPath, "Klimmen");

        public static ClimbShelf Shelf => _shelf ?? (_shelf = Load());

        /// <summary>Op volgorde van gebied en naam.</summary>
        public static IReadOnlyList<Climb> All => Shelf.Climbs;

        public static Climb Find(string id) => Shelf.Find(id);

        static ClimbShelf Load()
        {
            var shelf = new ClimbShelf();
            int bundled = 0, downloaded = 0;
            foreach (var asset in Resources.LoadAll<TextAsset>(ResourcesFolder))
            {
                if (!asset.name.EndsWith(".klim")) continue; // catalogus.json
                shelf.Add(asset.name, asset.bytes);
                bundled++;
            }
            if (Directory.Exists(DownloadFolder))
            {
                foreach (var file in Directory.GetFiles(DownloadFolder, "*" + ClimbPackage.Extension))
                {
                    try { shelf.Add(Path.GetFileName(file), File.ReadAllBytes(file)); }
                    catch (IOException e) { shelf.Problems.Add($"{Path.GetFileName(file)} niet te lezen: {e.Message}"); }
                    downloaded++;
                }
            }
            foreach (var problem in shelf.Problems) Debug.LogWarning("[Klimmen] " + problem);
            Debug.Log($"[Klimmen] {shelf.Climbs.Count} klimmen uit {bundled} meegeleverde en {downloaded} gedownloade pakketten.");
            return shelf;
        }
    }
}
