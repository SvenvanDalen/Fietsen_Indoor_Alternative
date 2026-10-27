using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FietsApp.Accounts;
using FietsApp.World;

namespace FietsApp.Climbs
{
    /// <summary>Een pakket dat de app niet kan lezen: kapot, onvolledig of van een nieuwer formaat.</summary>
    public class ClimbPackageException : IOException
    {
        public ClimbPackageException(string message) : base(message) { }
    }

    /// <summary>Een pakket in een nieuwer formaat dan deze versie van de app kent (story 4.16).</summary>
    public sealed class NewerFormatException : ClimbPackageException
    {
        public readonly int Format;

        public NewerFormatException(int format)
            : base($"formaat {format}, deze app kent tot en met {ClimbPackage.Format}; werk de app bij") => Format = format;
    }

    /// <summary>
    /// Leest een klimpakket (story 4.15): een zip met <c>klim.json</c>, de GPX en eventueel het landschap, gemaakt door
    /// <c>tools/climb_package.py</c>. Zie daar voor de indeling.
    /// </summary>
    public static class ClimbPackage
    {
        /// <summary>Het nieuwste formaat dat deze app kent; samen ophogen met FORMAT in tools/climb_package.py.</summary>
        public const int Format = 1;

        /// <summary>Extensie van een pakket; .bytes zodat Unity het als TextAsset meeneemt.</summary>
        public const string Extension = ".klim.bytes";

        const string InfoEntry = "klim.json";

        public static Climb Read(byte[] data)
        {
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                    return Read(zip);
            }
            catch (ClimbPackageException) { throw; }
            catch (Exception e) when (e is InvalidDataException || e is FormatException || e is IOException)
            {
                throw new ClimbPackageException("geen leesbaar klimpakket: " + e.Message);
            }
        }

        static Climb Read(ZipArchive zip)
        {
            var info = Json.Parse(Text(zip, InfoEntry) ?? throw new ClimbPackageException(InfoEntry + " ontbreekt"));
            int format = Int(info, "format");
            if (format > Format) throw new NewerFormatException(format);
            if (format < 1) throw new ClimbPackageException("geen formaat in " + InfoEntry);

            var climb = new Climb
            {
                Id = Required(info, "id"),
                Name = Required(info, "name"),
                Region = Required(info, "region"),
                Country = Json.GetString(info, "country") ?? "",
                Version = Int(info, "version"),
                Source = Json.GetString(info, "source"),
                License = Json.GetString(info, "license"),
            };

            var figures = Json.Get(info, "figures");
            climb.LengthMeters = Float(figures, "length_m", 0f);
            climb.AverageGradePercent = Float(figures, "average_grade", 0f);
            climb.ElevationGain = Float(figures, "elevation_gain", 0f);

            var vegetation = Json.Get(info, "vegetation");
            climb.TreeLine = Float(vegetation, "tree_line", Climb.None);
            climb.ForestFrom = Float(vegetation, "forest_from", Climb.None);
            climb.AboveTreeLine = ParseGround(Json.GetString(vegetation, "above_tree_line"));
            climb.GravelVergeAboveTreeLine = Json.Get(vegetation, "gravel_verge") is bool gravel && gravel;

            if (Json.Get(info, "surfaces") is List<object> surfaces)
                climb.Surfaces = surfaces.Select(s => new SurfaceSection
                {
                    from = Float(s, "from", 0f),
                    to = Float(s, "to", 0f),
                    surface = World.Surfaces.Parse(Json.GetString(s, "surface")),
                }).ToArray();

            var landmark = Json.Get(info, "landmark");
            if (Json.GetString(landmark, "kind") == "transmitter")
            {
                climb.SummitTower = true;
                climb.SummitLatitude = Json.GetNumber(landmark, "latitude") ?? 0.0;
                climb.SummitLongitude = Json.GetNumber(landmark, "longitude") ?? 0.0;
            }

            string route = Required(info, "route");
            climb.Gpx = Text(zip, route) ?? throw new ClimbPackageException(route + " ontbreekt");
            string terrain = Json.GetString(info, "terrain");
            if (terrain != null) climb.Terrain = Bytes(zip, terrain) ?? throw new ClimbPackageException(terrain + " ontbreekt");
            return climb;
        }

        static Ground ParseGround(string name) => name switch
        {
            "grass" => Ground.Grass,
            "dark_grass" => Ground.DarkGrass,
            _ => Ground.Bare,
        };

        static string Required(object obj, string key) =>
            Json.GetString(obj, key) is string s && s.Length > 0 ? s : throw new ClimbPackageException($"{key} ontbreekt in {InfoEntry}");

        static int Int(object obj, string key) => (int)(Json.GetNumber(obj, key) ?? 0.0);

        // null in klim.json (geen boomgrens of bos) wordt de standaardwaarde.
        static float Float(object obj, string key, float fallback) => (float)(Json.GetNumber(obj, key) ?? fallback);

        static byte[] Bytes(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry == null) return null;
            using (var stream = entry.Open())
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        static string Text(ZipArchive zip, string name) => Bytes(zip, name) is byte[] b ? Encoding.UTF8.GetString(b) : null;
    }

    /// <summary>
    /// Alle klimmen die de app kent (stories 4.15 en 4.16): de meegeleverde pakketten en de gedownloade. Van een klim
    /// die op beide plekken staat telt de hoogste versie. Pakketten die niet te lezen zijn worden overgeslagen, met een
    /// melding in <see cref="Problems"/>.
    /// </summary>
    public sealed class ClimbShelf
    {
        readonly Dictionary<string, Climb> _byId = new Dictionary<string, Climb>();
        Climb[] _sorted;

        /// <summary>Op volgorde van gebied en naam, zoals het ritmenu ze toont.</summary>
        public IReadOnlyList<Climb> Climbs => _sorted ?? (_sorted = _byId.Values
            .OrderBy(c => c.Region, StringComparer.InvariantCulture)
            .ThenBy(c => c.Name, StringComparer.InvariantCulture)
            .ToArray());
        public List<string> Problems { get; } = new List<string>();

        public Climb Find(string id) => id != null && _byId.TryGetValue(id, out var climb) ? climb : null;

        /// <summary>Versie van de klim die de app heeft, of 0 als hij er niet is.</summary>
        public int VersionOf(string id) => Find(id)?.Version ?? 0;

        /// <summary>Voegt een pakket toe; <paramref name="origin"/> (bestandsnaam) staat in de melding als het mislukt.</summary>
        public void Add(string origin, byte[] data)
        {
            Climb climb;
            try { climb = ClimbPackage.Read(data); }
            catch (ClimbPackageException e)
            {
                Problems.Add($"{origin} overgeslagen: {e.Message}");
                return;
            }
            if (_byId.TryGetValue(climb.Id, out var have) && have.Version >= climb.Version) return;
            _byId[climb.Id] = climb;
            _sorted = null;
        }
    }
}
