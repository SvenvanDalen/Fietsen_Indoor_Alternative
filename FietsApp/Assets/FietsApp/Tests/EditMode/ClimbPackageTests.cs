using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FietsApp.Climbs;
using FietsApp.Updates;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Kleine nagemaakte pakketten, zoals tools/climb_package.py ze maakt.</summary>
    static class FakePackages
    {
        public const string Gpx = "<gpx><trk><trkseg>" +
                                  "<trkpt lat=\"45.0\" lon=\"6.0\"><ele>100</ele></trkpt>" +
                                  "<trkpt lat=\"45.009\" lon=\"6.0\"><ele>150</ele></trkpt>" +
                                  "</trkseg></trk></gpx>";

        public static string Info(string id, int version = 1, int format = 1, string name = null, string extra = "") =>
            $"{{\"format\": {format}, \"version\": {version}, \"id\": \"{id}\", \"name\": \"{name ?? id}\", \"region\": \"Alpen\", " +
            "\"country\": \"Frankrijk\", \"figures\": {\"length_m\": 1000.5, \"average_grade\": 5.0, \"elevation_gain\": 50.0}, " +
            "\"vegetation\": {\"tree_line\": null, \"forest_from\": 700, \"above_tree_line\": \"dark_grass\", \"gravel_verge\": true}, " +
            "\"surfaces\": [{\"from\": 200, \"to\": 400, \"surface\": \"cobbles\"}], \"landmark\": null, " +
            $"\"source\": \"test\", \"license\": \"CC0\", \"route\": \"route.gpx\", \"terrain\": null{extra}}}";

        public static byte[] Zip(params (string name, byte[] data)[] files)
        {
            var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                foreach (var (name, data) in files)
                    using (var stream = zip.CreateEntry(name).Open())
                        stream.Write(data, 0, data.Length);
            return buffer.ToArray();
        }

        public static byte[] Package(string id, int version = 1, int format = 1, string name = null) =>
            Zip(("klim.json", Encoding.UTF8.GetBytes(Info(id, version, format, name))), ("route.gpx", Encoding.UTF8.GetBytes(Gpx)));

        public static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
        }
    }

    public class ClimbPackageTests
    {
        [Test]
        public void ReadsInfoAndRoute()
        {
            var climb = ClimbPackage.Read(FakePackages.Package("Test", version: 3, name: "Testklim"));

            Assert.That(climb.Id, Is.EqualTo("Test"));
            Assert.That(climb.Name, Is.EqualTo("Testklim"));
            Assert.That(climb.Version, Is.EqualTo(3));
            Assert.That(climb.Region, Is.EqualTo("Alpen"));
            Assert.That(climb.LengthMeters, Is.EqualTo(1000.5f));
            Assert.That(climb.TreeLine, Is.EqualTo(Climb.None), "null = geen boomgrens");
            Assert.That(climb.ForestFrom, Is.EqualTo(700f));
            Assert.That(climb.AboveTreeLine, Is.EqualTo(Ground.DarkGrass));
            Assert.That(climb.GravelVergeAboveTreeLine, Is.True);
            Assert.That(climb.SurfaceAt(300f), Is.EqualTo(Surface.Cobbles));
            Assert.That(climb.SurfaceAt(500f), Is.EqualTo(Surface.Asphalt));
            Assert.That(climb.SummitTower, Is.False);
            Assert.That(climb.Terrain, Is.Null);
            Assert.That(Gpx.Parse(climb.Gpx), Has.Count.EqualTo(2));
        }

        [Test]
        public void ReadsTransmitterAndTerrain()
        {
            var info = FakePackages.Info("Ventoux", extra: "").Replace("\"landmark\": null",
                    "\"landmark\": {\"kind\": \"transmitter\", \"latitude\": 44.1739, \"longitude\": 5.27843}")
                .Replace("\"terrain\": null", "\"terrain\": \"terrain.bytes\"");
            var climb = ClimbPackage.Read(FakePackages.Zip(("klim.json", Encoding.UTF8.GetBytes(info)),
                ("route.gpx", Encoding.UTF8.GetBytes(FakePackages.Gpx)), ("terrain.bytes", new byte[] { 1, 2, 3 })));

            Assert.That(climb.SummitTower, Is.True);
            Assert.That(climb.SummitLongitude, Is.EqualTo(5.27843).Within(1e-9));
            Assert.That(climb.Terrain, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void NewerFormat_IsRefusedWithItsFormat()
        {
            var e = Assert.Throws<NewerFormatException>(() => ClimbPackage.Read(FakePackages.Package("Nieuw", format: ClimbPackage.Format + 1)));
            Assert.That(e.Format, Is.EqualTo(ClimbPackage.Format + 1));
        }

        [Test]
        public void BrokenPackages_AreRefused()
        {
            Assert.Throws<ClimbPackageException>(() => ClimbPackage.Read(Encoding.ASCII.GetBytes("geen zip")));
            Assert.Throws<ClimbPackageException>(() => ClimbPackage.Read(FakePackages.Zip(("route.gpx", new byte[1]))), "zonder klim.json");
            Assert.Throws<ClimbPackageException>(() => ClimbPackage.Read(
                FakePackages.Zip(("klim.json", Encoding.UTF8.GetBytes(FakePackages.Info("Test"))))), "zonder GPX");
        }
    }

    public class ClimbShelfTests
    {
        [Test]
        public void HighestVersionWins_WhicheverComesFirst()
        {
            var shelf = new ClimbShelf();
            shelf.Add("meegeleverd", FakePackages.Package("A", version: 2, name: "Oud"));
            shelf.Add("gedownload", FakePackages.Package("A", version: 3, name: "Nieuw"));
            shelf.Add("verouderde download", FakePackages.Package("A", version: 1, name: "Heel oud"));

            Assert.That(shelf.Climbs.Count, Is.EqualTo(1));
            Assert.That(shelf.Find("A").Name, Is.EqualTo("Nieuw"));
            Assert.That(shelf.VersionOf("A"), Is.EqualTo(3));
            Assert.That(shelf.VersionOf("B"), Is.EqualTo(0));
        }

        [Test]
        public void UnreadablePackages_AreSkippedWithANotice()
        {
            var shelf = new ClimbShelf();
            shelf.Add("goed", FakePackages.Package("A"));
            shelf.Add("toekomst.klim.bytes", FakePackages.Package("B", format: ClimbPackage.Format + 1));
            shelf.Add("kapot.klim.bytes", new byte[] { 1, 2, 3 });

            Assert.That(shelf.Climbs.Select(c => c.Id), Is.EqualTo(new[] { "A" }));
            Assert.That(shelf.Problems, Has.Count.EqualTo(2));
            Assert.That(shelf.Problems[0], Does.StartWith("toekomst.klim.bytes"));
        }

        [Test]
        public void SortedByRegionThenName()
        {
            var shelf = new ClimbShelf();
            shelf.Add("1", FakePackages.Package("Z", name: "Zeta"));
            shelf.Add("2", FakePackages.Package("A", name: "Alfa"));

            Assert.That(shelf.Climbs.Select(c => c.Name), Is.EqualTo(new[] { "Alfa", "Zeta" }));
        }
    }

    public class ClimbCatalogTests
    {
        static string Entry(string id, int version, int format, string sha, string file = null) =>
            $"{{\"id\": \"{id}\", \"version\": {version}, \"format\": {format}, \"sha256\": \"{sha}\", \"file\": \"{file ?? id + ".klim.bytes"}\"}}";

        [Test]
        public void ParsesEntries_AndRefusesPaths()
        {
            string sha = new string('a', 64);
            var catalog = ClimbCatalog.Parse("{\"format\": 1, \"climbs\": [" +
                                             Entry("Goed", 2, 1, sha) + "," +
                                             Entry("Pad", 1, 1, sha, "../../evil.klim.bytes") + "," +
                                             Entry("../x", 1, 1, sha, "../x.klim.bytes") + "," +
                                             Entry("GeenSha", 1, 1, "") + "]}");

            Assert.That(catalog.Entries.Select(e => e.Id), Is.EqualTo(new[] { "Goed" }));
            Assert.That(catalog.Entries[0].Version, Is.EqualTo(2));
            Assert.That(catalog.Problems, Has.Count.EqualTo(3));
        }

        [Test]
        public void Garbage_Throws()
        {
            Assert.Throws<ClimbPackageException>(() => ClimbCatalog.Parse("<html>404</html>"));
            Assert.Throws<ClimbPackageException>(() => ClimbCatalog.Parse("{}"));
        }
    }

    public class ClimbSyncTests
    {
        sealed class FakeWeb : IDownloader
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public readonly List<string> Requested = new List<string>();
            public bool Offline;

            public Task DownloadFileAsync(string url, string path, CancellationToken ct)
            {
                Requested.Add(url);
                if (Offline || !Files.TryGetValue(url, out var data)) throw new IOException(url + " gaf 404");
                File.WriteAllBytes(path, data);
                return Task.CompletedTask;
            }

            public Task<string> GetStringAsync(string url, CancellationToken ct)
            {
                Requested.Add(url);
                if (Offline || !Files.TryGetValue(url, out var data)) throw new IOException("geen verbinding");
                return Task.FromResult(Encoding.UTF8.GetString(data));
            }
        }

        const string Base = "https://example.test/klimmen/";
        string _folder;
        FakeWeb _web;
        readonly List<string> _catalog = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "FietsAppKlimTest-" + System.Guid.NewGuid().ToString("N"));
            _web = new FakeWeb();
            _catalog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        void Publish(string id, int version, int format = 1, byte[] data = null, string sha = null)
        {
            data = data ?? FakePackages.Package(id, version, format);
            _web.Files[Base + id + ".klim.bytes"] = data;
            _catalog.Add($"{{\"id\": \"{id}\", \"version\": {version}, \"format\": {format}, \"sha256\": \"{sha ?? FakePackages.Sha256(data)}\", \"file\": \"{id}.klim.bytes\"}}");
            _web.Files[Base + "catalogus.json"] = Encoding.UTF8.GetBytes("{\"format\": 1, \"climbs\": [" + string.Join(",", _catalog) + "]}");
        }

        ClimbSyncResult Run(Dictionary<string, int> have = null) =>
            new ClimbSync(_web, Base, _folder, id => have != null && have.TryGetValue(id, out var v) ? v : 0)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();

        string Downloaded(string id) => Path.Combine(_folder, id + ".klim.bytes");

        [Test]
        public void DownloadsNewAndChangedClimbs_SkipsWhatTheAppHas()
        {
            Publish("Nieuw", 1);
            Publish("Gewijzigd", 3);
            Publish("Bekend", 2);

            var result = Run(new Dictionary<string, int> { ["Gewijzigd"] = 2, ["Bekend"] = 2 });

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Downloaded, Is.EquivalentTo(new[] { "Nieuw", "Gewijzigd" }));
            Assert.That(File.Exists(Downloaded("Nieuw")) && File.Exists(Downloaded("Gewijzigd")), Is.True);
            Assert.That(File.Exists(Downloaded("Bekend")), Is.False);
            Assert.That(_web.Requested, Has.None.EqualTo(Base + "Bekend.klim.bytes"));
            Assert.That(ClimbPackage.Read(File.ReadAllBytes(Downloaded("Gewijzigd"))).Version, Is.EqualTo(3));
            Assert.That(Directory.GetFiles(_folder, "*.part"), Is.Empty);
        }

        [Test]
        public void NewerFormat_IsSkippedWithoutDownloading()
        {
            Publish("Toekomst", 1, format: ClimbPackage.Format + 1);

            var result = Run();

            Assert.That(result.Downloaded, Is.Empty);
            Assert.That(result.NewerFormat, Is.EqualTo(1));
            Assert.That(result.Skipped.Single(), Does.StartWith("Toekomst overgeslagen").And.Contain("werk de app bij"));
            Assert.That(_web.Requested, Has.None.EqualTo(Base + "Toekomst.klim.bytes"));
        }

        [Test]
        public void WrongSha256_IsNotKept()
        {
            Publish("Kapot", 1, sha: new string('0', 64));

            var result = Run();

            Assert.That(result.Downloaded, Is.Empty);
            Assert.That(result.Skipped.Single(), Does.Contain("sha256"));
            Assert.That(Directory.Exists(_folder) ? Directory.GetFiles(_folder) : new string[0], Is.Empty);
        }

        [Test]
        public void PackageThatDisagreesWithTheCatalog_IsNotKept()
        {
            Publish("Anders", 2, data: FakePackages.Package("Anders", version: 1));

            var result = Run();

            Assert.That(result.Downloaded, Is.Empty);
            Assert.That(File.Exists(Downloaded("Anders")), Is.False);
        }

        [Test]
        public void Offline_ChangesNothing()
        {
            Publish("Nieuw", 1);
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(Downloaded("Eerder"), FakePackages.Package("Eerder"));
            _web.Offline = true;

            var result = Run();

            Assert.That(result.Error, Does.Contain("geen verbinding"));
            Assert.That(result.Downloaded, Is.Empty);
            Assert.That(Directory.GetFiles(_folder).Select(Path.GetFileName), Is.EqualTo(new[] { "Eerder.klim.bytes" }));
        }

        [Test]
        public void OneFailedDownload_DoesNotStopTheRest()
        {
            Publish("Weg", 1);
            Publish("Goed", 1);
            _web.Files.Remove(Base + "Weg.klim.bytes");

            var result = Run();

            Assert.That(result.Downloaded, Is.EqualTo(new[] { "Goed" }));
            Assert.That(result.Skipped.Single(), Does.StartWith("Weg overgeslagen"));
        }
    }
}
