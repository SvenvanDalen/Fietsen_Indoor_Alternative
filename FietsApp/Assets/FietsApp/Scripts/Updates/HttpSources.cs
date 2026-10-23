using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FietsApp.Updates
{
    static class Http
    {
        // Eén client voor de hele app; de timeout regelen we per verzoek, want de zip kan lang duren.
        public static readonly HttpClient Client = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FietsApp-Updater");
            return client;
        }

        public static CancellationTokenSource WithTimeout(CancellationToken ct, int seconds)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(seconds));
            return cts;
        }

        public static void EnsureSuccess(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{response.RequestMessage?.RequestUri} gaf {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }

    /// <summary>Nieuwste release van de FietsApp-repo via de GitHub API (story 13.6).</summary>
    public sealed class GitHubReleaseSource : IReleaseSource
    {
        public const string LatestUrl = "https://api.github.com/repos/SvenvanDalen/Fietsen_Indoor_Alternative/releases/latest";

        public async Task<GitHubRelease> GetLatestAsync(CancellationToken ct)
        {
            using (var cts = Http.WithTimeout(ct, 30))
            using (var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await Http.Client.SendAsync(request, cts.Token))
                {
                    Http.EnsureSuccess(response);
                    var release = GitHubRelease.Parse(await response.Content.ReadAsStringAsync());
                    if (release == null) throw new InvalidDataException("GitHub gaf geen leesbare release.");
                    return release;
                }
            }
        }
    }

    /// <summary>Downloadt release-bestanden (story 13.6).</summary>
    public sealed class HttpDownloader : IDownloader
    {
        public async Task DownloadFileAsync(string url, string path, CancellationToken ct)
        {
            using (var response = await Http.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                Http.EnsureSuccess(response);
                using (var source = await response.Content.ReadAsStreamAsync())
                using (var target = File.Create(path))
                    await source.CopyToAsync(target, 81920, ct);
            }
        }

        public async Task<string> GetStringAsync(string url, CancellationToken ct)
        {
            using (var cts = Http.WithTimeout(ct, 30))
            using (var response = await Http.Client.GetAsync(url, cts.Token))
            {
                Http.EnsureSuccess(response);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
