using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FietsApp.Updates;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Kijkt bij het opstarten op GitHub of er een nieuwere versie is en zet die op de achtergrond klaar (story 13.6).
    /// De update gaat erin bij afsluiten of via "Nu herstarten"; nooit midden in een rit.
    /// </summary>
    public sealed class UpdateService : MonoBehaviour
    {
        public static UpdateService Instance { get; private set; }

        /// <summary>Uitkomst van de controle; null zolang die loopt.</summary>
        public UpdateResult Result { get; private set; }

        public event Action Changed;

        UpdatePaths _paths;
        CancellationTokenSource _cts;
        bool _launched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            var go = new GameObject("UpdateService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<UpdateService>();
            go.AddComponent<UpdateBanner>();

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--no-update") >= 0) return;
            if (!AppVersion.TryParse(Application.version, out var current)) return;
            Instance.Check(current);
        }

        async void Check(AppVersion current)
        {
            _paths = new UpdatePaths(Path.GetDirectoryName(Application.dataPath), UpdatePaths.DefaultUpdatesRoot());
            _cts = new CancellationTokenSource();
            var flow = new UpdateFlow(current, new GitHubReleaseSource(), new HttpDownloader(), _paths,
                UpdatePaths.IsWritable(_paths.InstallDir));
            try
            {
                // Uitpakken is synchroon werk; buiten de hoofdthread blijft de app vloeiend.
                Result = await Task.Run(() => flow.RunAsync(_cts.Token));
            }
            catch (Exception e)
            {
                Result = new UpdateResult { Status = UpdateStatus.Failed, Error = e.Message };
            }
            if (this == null) return;
            Log($"Controle vanaf {current}: {Result.Status} {Result.Version} {Result.Error}");
            Changed?.Invoke();
        }

        /// <summary>Sluit de app; het script zet de update neer en start de nieuwe versie.</summary>
        public void RestartNow()
        {
            if (Result?.Status != UpdateStatus.ReadyToApply) return;
            if (TryLaunch(restart: true)) Application.Quit();
        }

        void OnApplicationQuit()
        {
            _cts?.Cancel();
            if (Result?.Status == UpdateStatus.ReadyToApply) TryLaunch(restart: false);
        }

        bool TryLaunch(bool restart)
        {
            if (_launched) return true;
            try
            {
                UpdateApplier.Launch(Result, _paths, restart);
                _launched = true;
                Log($"Update {Result.Version} wordt geïnstalleerd{(restart ? " en de app herstart" : "")}.");
                return true;
            }
            catch (Exception e)
            {
                Log("Update starten mislukt: " + e.Message);
                return false;
            }
        }

        void Log(string message)
        {
            Debug.Log("[Update] " + message);
            if (_paths == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_paths.LogPath));
                File.AppendAllText(_paths.LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Update] Log schrijven mislukt: " + e.Message);
            }
        }
    }
}
