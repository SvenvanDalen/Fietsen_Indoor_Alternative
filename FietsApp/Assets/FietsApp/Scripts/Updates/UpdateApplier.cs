using System.Diagnostics;
using System.IO;

namespace FietsApp.Updates
{
    /// <summary>Start <c>apply-update.ps1</c> uit de klaargezette versie; dat zet de update neer zodra de app dicht is (story 13.6).</summary>
    public static class UpdateApplier
    {
        public const string ExeName = "FietsApp.exe";
        public const string ScriptName = "apply-update.ps1";

        /// <summary>Het script van de nieuwe versie, zodat een verbeterd script meteen meedoet.</summary>
        public static string ScriptPath(string stagingDir) => Path.Combine(stagingDir, ScriptName);

        public static string BuildArguments(string scriptPath, int processId, string installDir, string stagingDir, string exeName,
            string logPath, bool restart)
        {
            string args = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {processId} " +
                          $"-InstallDir \"{installDir}\" -StagingDir \"{stagingDir}\" -ExeName \"{exeName}\" -LogPath \"{logPath}\"";
            return restart ? args + " -Restart" : args;
        }

        public static ProcessStartInfo StartInfo(UpdateResult ready, UpdatePaths paths, int processId, bool restart) =>
            new ProcessStartInfo("powershell.exe",
                BuildArguments(ScriptPath(ready.StagingDir), processId, paths.InstallDir, ready.StagingDir, ExeName, paths.LogPath, restart))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // Niet de werkmap van de app erven: die ligt vaak in de installatiemap en blokkeert dan het hernoemen.
                WorkingDirectory = paths.UpdatesRoot,
            };

        public static void Launch(UpdateResult ready, UpdatePaths paths, bool restart) =>
            Process.Start(StartInfo(ready, paths, Process.GetCurrentProcess().Id, restart));
    }
}
