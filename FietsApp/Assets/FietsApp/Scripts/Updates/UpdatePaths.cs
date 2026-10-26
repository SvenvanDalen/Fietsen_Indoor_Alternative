using System;
using System.IO;

namespace FietsApp.Updates
{
    /// <summary>Waar de updater downloadt, klaarzet en logt (story 13.6).</summary>
    public sealed class UpdatePaths
    {
        public UpdatePaths(string installDir, string updatesRoot)
        {
            InstallDir = installDir;
            UpdatesRoot = updatesRoot;
        }

        /// <summary>Map met <c>FietsApp.exe</c>.</summary>
        public string InstallDir { get; }

        /// <summary>Map voor downloads en staging, buiten de installatiemap.</summary>
        public string UpdatesRoot { get; }

        public string LogPath => Path.Combine(Path.GetDirectoryName(UpdatesRoot), "update.log");

        public string ZipPath(AppVersion v) => Path.Combine(UpdatesRoot, $"FietsApp-{v}.zip");

        public string StagingDir(AppVersion v) => Path.Combine(UpdatesRoot, $"staging-{v}");

        /// <summary><c>%LOCALAPPDATA%\FietsApp\updates</c>.</summary>
        public static string DefaultUpdatesRoot() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FietsApp", "updates");

        /// <summary>
        /// Of de updater de installatie mag vervangen: hij schrijft in de map zelf en hernoemt die in de bovenliggende map.
        /// Onder <c>C:\Program Files</c> mag dat zonder adminrechten niet.
        /// </summary>
        public static bool IsWritable(string installDir)
        {
            if (!Directory.Exists(installDir)) return false;
            var parent = Path.GetDirectoryName(Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar));
            return parent != null && CanCreateFile(installDir) && CanCreateFile(parent);
        }

        static bool CanCreateFile(string dir)
        {
            var probe = Path.Combine(dir, ".fietsapp-schrijftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
