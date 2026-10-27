using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FietsApp.Updates;

namespace FietsApp.Climbs
{
    public sealed class ClimbSyncResult
    {
        /// <summary>Namen van de klimmen die nieuw of bijgewerkt zijn binnengekomen.</summary>
        public readonly List<string> Downloaded = new List<string>();
        /// <summary>Meldingen over klimmen die niet binnenkwamen (nieuwer formaat, verkeerde sha256, mislukte download).</summary>
        public readonly List<string> Skipped = new List<string>();
        /// <summary>Hoeveel klimmen een nieuwere app vragen (staan ook in <see cref="Skipped"/>).</summary>
        public int NewerFormat;
        /// <summary>Waarom de catalogus niet op te halen was (bijvoorbeeld geen internet), of null.</summary>
        public string Error;
    }

    /// <summary>
    /// Haalt nieuwe en gewijzigde klimmen op (story 4.16): leest de online catalogus, downloadt elk pakket waarvan de
    /// app een lagere versie (of niets) heeft naar de downloadmap, en houdt het alleen als de sha256 klopt en
    /// het pakket te lezen is. Pakketten in een nieuwer formaat dan de app kent worden overgeslagen, met een melding.
    /// Zonder internet verandert er niets: de meegeleverde en eerder gedownloade klimmen blijven werken.
    /// </summary>
    public sealed class ClimbSync
    {
        readonly IDownloader _downloader;
        readonly string _baseUrl;
        readonly string _folder;
        readonly Func<string, int> _versionOf;

        /// <param name="versionOf">Welke versie van een klim de app al heeft (0 = geen).</param>
        public ClimbSync(IDownloader downloader, string baseUrl, string folder, Func<string, int> versionOf)
        {
            _downloader = downloader;
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _folder = folder;
            _versionOf = versionOf;
        }

        public async Task<ClimbSyncResult> RunAsync(CancellationToken ct)
        {
            var result = new ClimbSyncResult();
            ClimbCatalog catalog;
            try { catalog = ClimbCatalog.Parse(await _downloader.GetStringAsync(_baseUrl + ClimbCatalog.FileName, ct)); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                result.Error = e.Message;
                return result;
            }
            result.Skipped.AddRange(catalog.Problems);

            foreach (var entry in catalog.Entries)
            {
                if (entry.Format > ClimbPackage.Format)
                {
                    result.Skipped.Add($"{entry.Id} overgeslagen: {new NewerFormatException(entry.Format).Message}");
                    result.NewerFormat++;
                    continue;
                }
                if (_versionOf(entry.Id) >= entry.Version) continue;
                try
                {
                    var climb = await DownloadAsync(entry, ct);
                    result.Downloaded.Add(climb.Name);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    result.Skipped.Add($"{entry.Id} overgeslagen: {e.Message}");
                }
            }
            return result;
        }

        async Task<Climb> DownloadAsync(CatalogEntry entry, CancellationToken ct)
        {
            Directory.CreateDirectory(_folder);
            string target = Path.Combine(_folder, entry.File);
            string part = target + ".part";
            try
            {
                await _downloader.DownloadFileAsync(_baseUrl + entry.File, part, ct);
                if (!Sha256Verifier.Matches(part, entry.Sha256))
                    throw new InvalidDataException("de sha256 van de download klopt niet");
                var climb = ClimbPackage.Read(File.ReadAllBytes(part));
                if (climb.Id != entry.Id || climb.Version != entry.Version)
                    throw new InvalidDataException($"pakket is {climb.Id} versie {climb.Version}, catalogus zegt {entry.Id} versie {entry.Version}");
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
                return climb;
            }
            finally
            {
                if (File.Exists(part)) File.Delete(part);
            }
        }
    }
}
