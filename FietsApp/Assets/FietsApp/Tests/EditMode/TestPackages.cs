using System.Collections.Generic;
using System.IO;
using System.Linq;
using FietsApp.Climbs;
using FietsApp.World;

namespace FietsApp.Tests
{
    /// <summary>De meegeleverde klimpakketten (story 4.15); tests draaien met de projectmap als werkmap.</summary>
    static class TestPackages
    {
        public const string Folder = "Assets/FietsApp/Resources/Klimmen/";

        public static IEnumerable<string> Files() =>
            Directory.GetFiles(Folder, "*" + ClimbPackage.Extension).OrderBy(f => f, System.StringComparer.Ordinal);

        public static Climb Load(string id) => ClimbPackage.Read(File.ReadAllBytes(Folder + id + ClimbPackage.Extension));

        public static Route Route(string id) => ClimbImport.FromGpx(Gpx.Parse(Load(id).Gpx));
    }
}
