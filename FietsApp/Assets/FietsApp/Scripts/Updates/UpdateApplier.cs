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

        public static void Launch(UpdateResult ready, UpdatePaths paths, bool restart)
        {
            int pid = Process.GetCurrentProcess().Id;
            var info = new ProcessStartInfo("powershell.exe",
                BuildArguments(ScriptPath(ready.StagingDir), pid, paths.InstallDir, ready.StagingDir, ExeName, paths.LogPath, restart))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(info);
        }
    }
}
