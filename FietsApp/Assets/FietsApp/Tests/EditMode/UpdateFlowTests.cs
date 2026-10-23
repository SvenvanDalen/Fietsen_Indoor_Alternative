using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FietsApp.Updates;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Update ophalen, controleren en klaarzetten zonder netwerk (story 13.6).</summary>
    public class UpdateFlowTests
    {
        const string ZipUrl = "https://example.org/FietsApp-Windows.zip";
        const string ShaUrl = "https://example.org/FietsApp-Windows.zip.sha256";

        sealed class FakeReleaseSource : IReleaseSource
        {
            readonly GitHubRelease _release;
            readonly Exception _error;
            public FakeReleaseSource(GitHubRelease release) => _release = release;
            public FakeReleaseSource(Exception error) => _error = error;

            public Task<GitHubRelease> GetLatestAsync(CancellationToken ct)
            {
                if (_error != null) throw _error;
                return Task.FromResult(_release);
            }
        }

        sealed class FakeDownloader : IDownloader
        {
            public byte[] Zip;
            public string Sha;
            public bool CancelAfterWrite;
            public int Calls;

            public Task DownloadFileAsync(string url, string path, CancellationToken ct)
            {
                Calls++;
                Assert.AreEqual(ZipUrl, url);
                File.WriteAllBytes(path, CancelAfterWrite ? new byte[] { 1, 2, 3 } : Zip);
                if (CancelAfterWrite) throw new OperationCanceledException();
                return Task.CompletedTask;
            }

            public Task<string> GetStringAsync(string url, CancellationToken ct)
            {
                Calls++;
                Assert.AreEqual(ShaUrl, url);
                return Task.FromResult(Sha);
            }
        }

        string _root;
        UpdatePaths _paths;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "fietsapp-updateflow-" + Guid.NewGuid().ToString("N"));
            var install = Path.Combine(_root, "install");
            Directory.CreateDirectory(install);
            _paths = new UpdatePaths(install, Path.Combine(_root, "LocalAppData", "FietsApp", "updates"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        static AppVersion V(string text)
        {
            Assert.IsTrue(AppVersion.TryParse(text, out var v));
            return v;
        }

        static GitHubRelease Release(string tag) => new GitHubRelease
        {
            TagName = tag,
            HtmlUrl = "https://example.org/release/" + tag,
            Assets = new List<GitHubAsset>
            {
                new GitHubAsset { Name = UpdateDecision.ZipAssetName, DownloadUrl = ZipUrl },
                new GitHubAsset { Name = UpdateDecision.ShaAssetName, DownloadUrl = ShaUrl },
            },
        };

        static byte[] BuildZip(params string[] entries)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var name in entries)
                    {
                        using (var w = new StreamWriter(zip.CreateEntry(name).Open()))
                            w.Write("inhoud van " + name);
                    }
                }
                return ms.ToArray();
            }
        }

        static string Sha(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (var b in sha.ComputeHash(data)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static FakeDownloader ValidDownloader()
        {
            var zip = BuildZip("FietsApp.exe", "apply-update.ps1", "Bridge/FietsBridge.exe");
            return new FakeDownloader { Zip = zip, Sha = Sha(zip) + "  FietsApp-Windows.zip\n" };
        }

        UpdateResult Run(IReleaseSource source, IDownloader downloader, bool writable = true) =>
            new UpdateFlow(V("0.1.5"), source, downloader, _paths, writable).RunAsync(CancellationToken.None).GetAwaiter().GetResult();

        string WriteTemp(string name, string content)
        {
            var path = Path.Combine(_root, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void Matches_SumFormatUppercase_True()
        {
            var path = WriteTemp("a.bin", "hallo");
            var hash = Sha(File.ReadAllBytes(path)).ToUpperInvariant();
            Assert.IsTrue(Sha256Verifier.Matches(path, hash + "  FietsApp-Windows.zip\n"));
        }

        [Test]
        public void Matches_BareHashWithCrlf_True()
        {
            var path = WriteTemp("a.bin", "hallo");
            Assert.IsTrue(Sha256Verifier.Matches(path, Sha(File.ReadAllBytes(path)) + "\r\n"));
        }

        [Test]
        public void Matches_WrongHash_False()
        {
            var path = WriteTemp("a.bin", "hallo");
            Assert.IsFalse(Sha256Verifier.Matches(path, new string('0', 64)));
        }

        [TestCase("Not Found")]
        [TestCase("")]
        [TestCase(null)]
        public void Matches_GarbageContent_False(string content)
        {
            var path = WriteTemp("a.bin", "hallo");
            Assert.IsFalse(Sha256Verifier.Matches(path, content));
        }

        [Test]
        public void Paths_UseVersionInName()
        {
            StringAssert.EndsWith(Path.Combine("updates", "FietsApp-0.1.6.zip"), _paths.ZipPath(V("0.1.6")));
            StringAssert.EndsWith(Path.Combine("updates", "staging-0.1.6"), _paths.StagingDir(V("0.1.6")));
            Assert.AreEqual(Path.Combine(_root, "LocalAppData", "FietsApp", "update.log"), _paths.LogPath);
        }

        [Test]
        public void IsWritable_MissingDir_False() => Assert.IsFalse(UpdatePaths.IsWritable(Path.Combine(_root, "bestaat-niet")));

        [Test]
        public void IsWritable_TempDir_True() => Assert.IsTrue(UpdatePaths.IsWritable(_paths.InstallDir));

        [Test]
        public void RunAsync_NoNewerRelease_ReturnsUpToDate()
        {
            var downloader = ValidDownloader();
            var result = Run(new FakeReleaseSource(Release("v0.1.5")), downloader);
            Assert.AreEqual(UpdateStatus.UpToDate, result.Status);
            Assert.AreEqual(0, downloader.Calls);
        }

        [Test]
        public void RunAsync_SourceThrows_ReturnsFailedAndWritesNothing()
        {
            var result = Run(new FakeReleaseSource(new HttpRequestException("403 rate limit")), ValidDownloader());
            Assert.AreEqual(UpdateStatus.Failed, result.Status);
            StringAssert.Contains("403", result.Error);
            Assert.IsFalse(Directory.Exists(_paths.UpdatesRoot) && Directory.GetFileSystemEntries(_paths.UpdatesRoot).Length > 0);
        }

        [Test]
        public void RunAsync_InstallNotWritable_ReturnsManualWithoutDownloading()
        {
            var downloader = ValidDownloader();
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), downloader, writable: false);
            Assert.AreEqual(UpdateStatus.ManualDownloadRequired, result.Status);
            Assert.AreEqual("0.1.6", result.Version.ToString());
            Assert.AreEqual("https://example.org/release/v0.1.6", result.ReleasePageUrl);
            Assert.AreEqual(0, downloader.Calls);
        }

        [Test]
        public void RunAsync_ValidUpdate_ExtractsToStaging()
        {
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), ValidDownloader());
            Assert.AreEqual(UpdateStatus.ReadyToApply, result.Status, result.Error);
            Assert.AreEqual(_paths.StagingDir(V("0.1.6")), result.StagingDir);
            Assert.IsTrue(File.Exists(Path.Combine(result.StagingDir, "FietsApp.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(result.StagingDir, "Bridge", "FietsBridge.exe")));
            Assert.IsFalse(File.Exists(_paths.ZipPath(V("0.1.6"))));
            Assert.IsFalse(Directory.Exists(result.StagingDir + ".tmp"));
        }

        [Test]
        public void RunAsync_HashMismatch_DeletesZipAndNoStaging()
        {
            var downloader = ValidDownloader();
            downloader.Sha = new string('a', 64) + "  FietsApp-Windows.zip";
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), downloader);
            Assert.AreEqual(UpdateStatus.Failed, result.Status);
            Assert.IsFalse(File.Exists(_paths.ZipPath(V("0.1.6"))));
            Assert.IsFalse(File.Exists(_paths.ZipPath(V("0.1.6")) + ".part"));
            Assert.IsFalse(Directory.Exists(_paths.StagingDir(V("0.1.6"))));
        }

        [Test]
        public void RunAsync_Cancelled_LeavesNoStaging()
        {
            var downloader = ValidDownloader();
            downloader.CancelAfterWrite = true;
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), downloader);
            Assert.AreEqual(UpdateStatus.Failed, result.Status);
            Assert.IsFalse(File.Exists(_paths.ZipPath(V("0.1.6")) + ".part"));
            Assert.IsFalse(Directory.Exists(_paths.StagingDir(V("0.1.6")) + ".tmp"));
            Assert.IsFalse(Directory.Exists(_paths.StagingDir(V("0.1.6"))));
        }

        [Test]
        public void RunAsync_ZipWithoutExe_ReturnsFailed()
        {
            var zip = BuildZip("apply-update.ps1");
            var downloader = new FakeDownloader { Zip = zip, Sha = Sha(zip) };
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), downloader);
            Assert.AreEqual(UpdateStatus.Failed, result.Status);
            Assert.IsFalse(Directory.Exists(_paths.StagingDir(V("0.1.6"))));
            Assert.IsFalse(Directory.Exists(_paths.StagingDir(V("0.1.6")) + ".tmp"));
        }

        [Test]
        public void RunAsync_StagingAlreadyPresent_SkipsDownload()
        {
            var staging = _paths.StagingDir(V("0.1.6"));
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, "FietsApp.exe"), "x");
            File.WriteAllText(Path.Combine(staging, "apply-update.ps1"), "x");
            var downloader = ValidDownloader();
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), downloader);
            Assert.AreEqual(UpdateStatus.ReadyToApply, result.Status);
            Assert.AreEqual(0, downloader.Calls);
        }

        [Test]
        public void RunAsync_RemovesStagingOfOtherVersions()
        {
            var oldStaging = _paths.StagingDir(V("0.1.4"));
            Directory.CreateDirectory(oldStaging);
            File.WriteAllText(_paths.ZipPath(V("0.1.4")) + ".part", "x");
            var result = Run(new FakeReleaseSource(Release("v0.1.6")), ValidDownloader());
            Assert.AreEqual(UpdateStatus.ReadyToApply, result.Status, result.Error);
            Assert.IsFalse(Directory.Exists(oldStaging));
            Assert.IsFalse(File.Exists(_paths.ZipPath(V("0.1.4")) + ".part"));
        }
    }
}
