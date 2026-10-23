using System.Collections.Generic;
using FietsApp.Updates;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Versies en de keuze of een GitHub-release een bruikbare update is (story 13.6).</summary>
    public class UpdateDecisionTests
    {
        static AppVersion V(string text)
        {
            Assert.IsTrue(AppVersion.TryParse(text, out var v), text);
            return v;
        }

        static GitHubRelease Release(string tag, params string[] assetNames)
        {
            var assets = new List<GitHubAsset>();
            foreach (var name in assetNames)
                assets.Add(new GitHubAsset { Name = name, DownloadUrl = "https://example.org/" + name });
            return new GitHubRelease { TagName = tag, HtmlUrl = "https://example.org/release/" + tag, Assets = assets };
        }

        static GitHubRelease Complete(string tag) => Release(tag, UpdateDecision.ZipAssetName, UpdateDecision.ShaAssetName);

        [Test]
        public void TryParse_WithVPrefix_Parses()
        {
            Assert.IsTrue(AppVersion.TryParse("v0.1.12", out var v));
            Assert.AreEqual("0.1.12", v.ToString());
            Assert.AreEqual("2.3.4", V("V2.3.4").ToString());
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("0.1")]
        [TestCase("0.1.x")]
        [TestCase("0.1.2.3")]
        [TestCase("-1.0.0")]
        [TestCase("v")]
        public void TryParse_Invalid_ReturnsFalse(string text)
        {
            Assert.IsFalse(AppVersion.TryParse(text, out _));
        }

        [Test]
        public void CompareTo_IsNumericNotLexical()
        {
            Assert.Greater(V("0.1.10").CompareTo(V("0.1.9")), 0);
            Assert.Less(V("0.9.0").CompareTo(V("1.0.0")), 0);
            Assert.AreEqual(0, V("v0.1.5").CompareTo(V("0.1.5")));
        }

        [Test]
        public void Parse_RealApiShape_ReadsTagAndAssets()
        {
            // Ingekort antwoord van GET /repos/{owner}/{repo}/releases/latest.
            const string json = "{\"url\":\"https://api.github.com/repos/o/r/releases/1\",\"html_url\":\"https://github.com/o/r/releases/tag/v0.1.7\"," +
                                "\"id\":1,\"tag_name\":\"v0.1.7\",\"name\":\"FietsApp 0.1.7\",\"draft\":false,\"prerelease\":false," +
                                "\"assets\":[{\"name\":\"FietsApp-Windows.zip\",\"size\":123456,\"browser_download_url\":\"https://github.com/o/r/releases/download/v0.1.7/FietsApp-Windows.zip\"}," +
                                "{\"name\":\"FietsApp-Windows.zip.sha256\",\"size\":87,\"browser_download_url\":\"https://github.com/o/r/releases/download/v0.1.7/FietsApp-Windows.zip.sha256\"}]}";
            var release = GitHubRelease.Parse(json);
            Assert.AreEqual("v0.1.7", release.TagName);
            Assert.AreEqual("https://github.com/o/r/releases/tag/v0.1.7", release.HtmlUrl);
            Assert.IsFalse(release.Draft);
            Assert.IsFalse(release.Prerelease);
            Assert.AreEqual(2, release.Assets.Count);
            Assert.AreEqual("FietsApp-Windows.zip", release.Assets[0].Name);
            Assert.AreEqual("https://github.com/o/r/releases/download/v0.1.7/FietsApp-Windows.zip.sha256", release.Assets[1].DownloadUrl);
        }

        [TestCase("<html>rate limited</html>")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("{\"message\":\"API rate limit exceeded\"}")]
        [TestCase("[1,2]")]
        public void Parse_NotJson_ReturnsNull(string text)
        {
            Assert.IsNull(GitHubRelease.Parse(text));
        }

        [Test]
        public void Evaluate_NewerWithBothAssets_ReturnsCandidate()
        {
            var candidate = UpdateDecision.Evaluate(V("0.1.5"), Complete("v0.1.6"));
            Assert.IsNotNull(candidate);
            Assert.AreEqual("0.1.6", candidate.Version.ToString());
            Assert.AreEqual("https://example.org/FietsApp-Windows.zip", candidate.ZipUrl);
            Assert.AreEqual("https://example.org/FietsApp-Windows.zip.sha256", candidate.Sha256Url);
            Assert.AreEqual("https://example.org/release/v0.1.6", candidate.ReleasePageUrl);
        }

        [Test]
        public void Evaluate_SameVersion_ReturnsNull() => Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), Complete("v0.1.5")));

        [Test]
        public void Evaluate_OlderVersion_ReturnsNull() => Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), Complete("v0.1.4")));

        [Test]
        public void Evaluate_Draft_ReturnsNull()
        {
            var release = Complete("v0.1.6");
            release.Draft = true;
            Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), release));
        }

        [Test]
        public void Evaluate_Prerelease_ReturnsNull()
        {
            var release = Complete("v0.1.6");
            release.Prerelease = true;
            Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), release));
        }

        [Test]
        public void Evaluate_MissingZip_ReturnsNull() =>
            Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), Release("v0.1.6", UpdateDecision.ShaAssetName)));

        [Test]
        public void Evaluate_MissingSha_ReturnsNull() =>
            Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), Release("v0.1.6", UpdateDecision.ZipAssetName)));

        [Test]
        public void Evaluate_UnparsableTag_ReturnsNull() => Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), Complete("nightly")));

        [Test]
        public void Evaluate_NullRelease_ReturnsNull() => Assert.IsNull(UpdateDecision.Evaluate(V("0.1.5"), null));
    }
}
