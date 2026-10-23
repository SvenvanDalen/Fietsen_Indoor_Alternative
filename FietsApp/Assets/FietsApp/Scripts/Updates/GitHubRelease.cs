using System.Collections.Generic;
using FietsApp.Accounts;

namespace FietsApp.Updates
{
    public sealed class GitHubAsset
    {
        public string Name;
        public string DownloadUrl;
    }

    /// <summary>De velden van een GitHub-release die de updater nodig heeft (story 13.6).</summary>
    public sealed class GitHubRelease
    {
        public string TagName;
        public string HtmlUrl;
        public bool Draft;
        public bool Prerelease;
        public List<GitHubAsset> Assets = new List<GitHubAsset>();

        /// <summary>Antwoord van <c>releases/latest</c>, of null bij een foutpagina, lege tekst of een object zonder tag.</summary>
        public static GitHubRelease Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            object root;
            try { root = Json.Parse(json); }
            catch (System.FormatException) { return null; }
            string tag = Json.GetString(root, "tag_name");
            if (tag == null) return null;

            var release = new GitHubRelease
            {
                TagName = tag,
                HtmlUrl = Json.GetString(root, "html_url"),
                Draft = Json.Get(root, "draft") is bool draft && draft,
                Prerelease = Json.Get(root, "prerelease") is bool pre && pre,
            };
            if (Json.Get(root, "assets") is List<object> assets)
                foreach (var a in assets)
                    release.Assets.Add(new GitHubAsset { Name = Json.GetString(a, "name"), DownloadUrl = Json.GetString(a, "browser_download_url") });
            return release;
        }
    }
}
