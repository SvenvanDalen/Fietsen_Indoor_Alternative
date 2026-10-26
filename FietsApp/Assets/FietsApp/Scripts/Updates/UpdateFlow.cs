using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace FietsApp.Updates
{
    public interface IReleaseSource
    {
        Task<GitHubRelease> GetLatestAsync(CancellationToken ct);
    }

    public interface IDownloader
    {
        Task DownloadFileAsync(string url, string path, CancellationToken ct);
        Task<string> GetStringAsync(string url, CancellationToken ct);
    }

    public enum UpdateStatus { UpToDate, ReadyToApply, ManualDownloadRequired, Failed }

    public sealed class UpdateResult
    {
        public UpdateStatus Status;
        public AppVersion Version;
        public string StagingDir;
        public string ReleasePageUrl;
        public string Error;
    }

    /// <summary>
    /// Kijkt of er een nieuwere release is, downloadt en controleert de zip en pakt hem uit in een staging-map (story 13.6).
    /// Toepassen gebeurt later door <c>apply-update.ps1</c>; deze klasse raakt de installatiemap nooit aan.
    /// </summary>
    public sealed class UpdateFlow
    {
        // Zonder deze bestanden kan de nieuwe versie niet starten of zichzelf niet plaatsen.
        static readonly string[] RequiredFiles = { "FietsApp.exe", "apply-update.ps1" };

        // Geschreven door apply-update.ps1 in de staging-map; bevat het aantal mislukte installaties.
        const string FailedMarker = "apply-failed.txt";
        const int MaxFailedApplies = 3;

        readonly AppVersion _current;
        readonly IReleaseSource _source;
        readonly IDownloader _downloader;
        readonly UpdatePaths _paths;
        readonly bool _installWritable;

        public UpdateFlow(AppVersion current, IReleaseSource source, IDownloader downloader, UpdatePaths paths, bool installWritable)
        {
            _current = current;
            _source = source;
            _downloader = downloader;
            _paths = paths;
            _installWritable = installWritable;
        }

        public async Task<UpdateResult> RunAsync(CancellationToken ct)
        {
            GitHubRelease release;
            try { release = await _source.GetLatestAsync(ct); }
            catch (Exception e) { return Failed(default, e); }

            var candidate = UpdateDecision.Evaluate(_current, release);
            if (candidate == null) return new UpdateResult { Status = UpdateStatus.UpToDate, Version = _current };

            var v = candidate.Version;
            if (!_installWritable)
                return new UpdateResult { Status = UpdateStatus.ManualDownloadRequired, Version = v, ReleasePageUrl = candidate.ReleasePageUrl };

            string staging = _paths.StagingDir(v);
            if (HasRequiredFiles(staging))
            {
                // apply-update.ps1 telt mislukte pogingen; blijft het misgaan, dan liever een downloadknop dan eeuwig "staat klaar".
                if (FailedApplies(staging) >= MaxFailedApplies)
                    return new UpdateResult { Status = UpdateStatus.ManualDownloadRequired, Version = v, ReleasePageUrl = candidate.ReleasePageUrl };
                return Ready(v, staging, candidate);
            }

            string zip = _paths.ZipPath(v);
            string part = zip + ".part";
            string tmp = staging + ".tmp";
            try
            {
                Directory.CreateDirectory(_paths.UpdatesRoot);
                RemoveOtherVersions(staging);

                // De downloader mag de hoofdthread nodig hebben (UnityWebRequest); hashen en uitpakken niet, dat kost seconden.
                await _downloader.DownloadFileAsync(candidate.ZipUrl, part, ct);
                string sha = await _downloader.GetStringAsync(candidate.Sha256Url, ct);
                return await Task.Run(() => VerifyAndExtract(v, candidate, sha, part, zip, tmp, staging), ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                DeleteFile(part);
                DeleteFile(zip);
                DeleteDir(tmp);
                return Failed(v, e);
            }
        }

        static UpdateResult VerifyAndExtract(AppVersion v, UpdateCandidate candidate, string sha, string part, string zip, string tmp,
            string staging)
        {
            if (!Sha256Verifier.Matches(part, sha))
            {
                DeleteFile(part);
                return Failed(v, new InvalidDataException("De sha256 van de download klopt niet."));
            }
            DeleteFile(zip);
            File.Move(part, zip);

            DeleteDir(tmp);
            ZipFile.ExtractToDirectory(zip, tmp);
            if (!HasRequiredFiles(tmp))
            {
                DeleteDir(tmp);
                DeleteFile(zip);
                return Failed(v, new InvalidDataException("De download mist FietsApp.exe of apply-update.ps1."));
            }
            DeleteDir(staging);
            Directory.Move(tmp, staging);
            DeleteFile(zip);
            return Ready(v, staging, candidate);
        }

        static UpdateResult Ready(AppVersion v, string staging, UpdateCandidate c) =>
            new UpdateResult { Status = UpdateStatus.ReadyToApply, Version = v, StagingDir = staging, ReleasePageUrl = c.ReleasePageUrl };

        static UpdateResult Failed(AppVersion v, Exception e)
        {
            // HttpClient verstopt de echte oorzaak (TLS, DNS, verbinding) in de InnerException; die hoort in het log.
            var error = e.GetType().Name + ": " + e.Message;
            for (var inner = e.InnerException; inner != null; inner = inner.InnerException)
                error += " → " + inner.GetType().Name + ": " + inner.Message;
            return new UpdateResult { Status = UpdateStatus.Failed, Version = v, Error = error };
        }

        static int FailedApplies(string staging)
        {
            try
            {
                var marker = Path.Combine(staging, FailedMarker);
                return File.Exists(marker) && int.TryParse(File.ReadAllText(marker).Trim(), out int count) ? count : 0;
            }
            catch (IOException) { return 0; }
        }

        static bool HasRequiredFiles(string dir)
        {
            if (!Directory.Exists(dir)) return false;
            foreach (var f in RequiredFiles)
                if (!File.Exists(Path.Combine(dir, f))) return false;
            return true;
        }

        /// <summary>Resten van eerdere of afgebroken updates weghalen, zodat ze de schijf niet vullen.</summary>
        void RemoveOtherVersions(string keepStaging)
        {
            foreach (var file in Directory.GetFiles(_paths.UpdatesRoot))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("FietsApp-", StringComparison.Ordinal) || name.EndsWith(".part", StringComparison.Ordinal))
                    DeleteFile(file);
            }
            foreach (var dir in Directory.GetDirectories(_paths.UpdatesRoot))
            {
                if (Path.GetFileName(dir).StartsWith("staging-", StringComparison.Ordinal) && dir != keepStaging)
                    DeleteDir(dir);
            }
        }

        static void DeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void DeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
