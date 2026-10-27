using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FietsApp.Accounts;

namespace FietsApp.Climbs
{
    /// <summary>Eén klim in de online catalogus: welk pakket, welke versie en in welk formaat.</summary>
    public sealed class CatalogEntry
    {
        public string Id;
        public int Version;
        public int Format;
        public string Sha256;
        public string File;
    }

    /// <summary>
    /// De catalogus van klimpakketten (story 4.16): <c>catalogus.json</c> naast de pakketten, bijgehouden door
    /// <c>tools/climb_package.py</c>. De app haalt hem van GitHub (de map op <c>main</c>), dus een klim publiceren is
    /// het pakket en de catalogus pushen, zonder app-release.
    /// </summary>
    public sealed class ClimbCatalog
    {
        /// <summary>De map met pakketten en catalogus op GitHub; raw.githubusercontent.com is gratis en zonder API-limiet.</summary>
        public const string BaseUrl =
            "https://raw.githubusercontent.com/SvenvanDalen/Fietsen_Indoor_Alternative/main/FietsApp/Assets/FietsApp/Resources/Klimmen/";

        public const string FileName = "catalogus.json";

        static readonly Regex SafeId = new Regex("^[A-Za-z0-9_]+$");

        public readonly List<CatalogEntry> Entries = new List<CatalogEntry>();

        /// <summary>Regels die niet te gebruiken zijn (geen id, vreemde bestandsnaam), met de reden.</summary>
        public readonly List<string> Problems = new List<string>();

        /// <exception cref="ClimbPackageException">Geen leesbare catalogus.</exception>
        public static ClimbCatalog Parse(string json)
        {
            object root;
            try { root = Json.Parse(json ?? ""); }
            catch (FormatException e) { throw new ClimbPackageException("catalogus niet te lezen: " + e.Message); }
            if (!(Json.Get(root, "climbs") is List<object> climbs)) throw new ClimbPackageException("catalogus zonder klimmen");

            var catalog = new ClimbCatalog();
            foreach (var item in climbs)
            {
                var entry = new CatalogEntry
                {
                    Id = Json.GetString(item, "id"),
                    Version = (int)(Json.GetNumber(item, "version") ?? 0.0),
                    Format = (int)(Json.GetNumber(item, "format") ?? 0.0),
                    Sha256 = Json.GetString(item, "sha256"),
                    File = Json.GetString(item, "file"),
                };
                // De bestandsnaam komt van internet: alleen "<id>.klim.bytes", nooit een pad.
                if (entry.Id == null || !SafeId.IsMatch(entry.Id) || entry.File != entry.Id + ClimbPackage.Extension)
                    catalog.Problems.Add($"regel {entry.Id ?? "zonder id"} overgeslagen: ongeldige id of bestandsnaam");
                else if (entry.Version < 1 || entry.Sha256 == null || entry.Sha256.Length != 64)
                    catalog.Problems.Add($"{entry.Id} overgeslagen: versie of sha256 ontbreekt");
                else
                    catalog.Entries.Add(entry);
            }
            return catalog;
        }
    }
}
