namespace FietsApp.Updates
{
    public sealed class UpdateCandidate
    {
        public AppVersion Version;
        public string ZipUrl;
        public string Sha256Url;
        public string ReleasePageUrl;
    }

    /// <summary>Beslist of de nieuwste release een update is die de app kan installeren (story 13.6).</summary>
    public static class UpdateDecision
    {
        public const string ZipAssetName = "FietsApp-Windows.zip";
        public const string ShaAssetName = "FietsApp-Windows.zip.sha256";

        /// <summary>Kandidaat als de release nieuwer, definitief en compleet is; anders null.</summary>
        public static UpdateCandidate Evaluate(AppVersion current, GitHubRelease latest)
        {
            if (latest == null || latest.Draft || latest.Prerelease) return null;
            if (!AppVersion.TryParse(latest.TagName, out var version) || version.CompareTo(current) <= 0) return null;
            string zip = null, sha = null;
            foreach (var asset in latest.Assets)
            {
                if (asset.Name == ZipAssetName) zip = asset.DownloadUrl;
                else if (asset.Name == ShaAssetName) sha = asset.DownloadUrl;
            }
            if (string.IsNullOrEmpty(zip) || string.IsNullOrEmpty(sha)) return null;
            return new UpdateCandidate { Version = version, ZipUrl = zip, Sha256Url = sha, ReleasePageUrl = latest.HtmlUrl };
        }
    }
}
