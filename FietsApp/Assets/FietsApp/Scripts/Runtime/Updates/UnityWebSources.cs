using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FietsApp.Updates;
using UnityEngine.Networking;

namespace FietsApp
{
    // UnityWebRequest in plaats van HttpClient: in de Windows-speler kan Mono's eigen TLS de certificaatketen van GitHub
    // niet opbouwen ("TrustFailure: Chain instance is empty"); UnityWebRequest gebruikt de TLS van het platform (story 13.6).
    // Alleen aanroepen vanaf de hoofdthread.
    static class UnityWeb
    {
        public static async Task<UnityWebRequest> SendAsync(UnityWebRequest request, CancellationToken ct)
        {
            var done = new TaskCompletionSource<bool>();
            request.SetRequestHeader("User-Agent", "FietsApp-Updater");
            request.SendWebRequest().completed += _ => done.TrySetResult(true);
            using (ct.Register(request.Abort))
                await done.Task;
            ct.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"{request.url} gaf {request.responseCode}: {request.error}");
            return request;
        }
    }

    /// <summary>Nieuwste release van de FietsApp-repo via de GitHub API.</summary>
    public sealed class UnityReleaseSource : IReleaseSource
    {
        public async Task<GitHubRelease> GetLatestAsync(CancellationToken ct)
        {
            using (var request = UnityWebRequest.Get(GitHubRelease.LatestUrl))
            {
                request.timeout = 30;
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                await UnityWeb.SendAsync(request, ct);
                var release = GitHubRelease.Parse(request.downloadHandler.text);
                if (release == null) throw new InvalidDataException("GitHub gaf geen leesbare release.");
                return release;
            }
        }
    }

    /// <summary>Downloadt release-bestanden; de zip gaat rechtstreeks naar schijf.</summary>
    public sealed class UnityDownloader : IDownloader
    {
        public async Task DownloadFileAsync(string url, string path, CancellationToken ct)
        {
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
                await UnityWeb.SendAsync(request, ct);
            }
        }

        public async Task<string> GetStringAsync(string url, CancellationToken ct)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                await UnityWeb.SendAsync(request, ct);
                return request.downloadHandler.text;
            }
        }
    }
}
