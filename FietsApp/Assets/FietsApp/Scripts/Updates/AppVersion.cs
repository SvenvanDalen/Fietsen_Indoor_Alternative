using System;
using System.Globalization;

namespace FietsApp.Updates
{
    /// <summary>Versie als major.minor.patch, zoals in de release-tag (<c>v0.1.12</c>) en <c>Application.version</c> (story 13.6).</summary>
    public readonly struct AppVersion : IComparable<AppVersion>
    {
        public readonly int Major, Minor, Patch;

        public AppVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>Leest drie niet-negatieve gehele getallen, met een optionele <c>v</c> ervoor.</summary>
        public static bool TryParse(string text, out AppVersion version)
        {
            version = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (text[0] == 'v' || text[0] == 'V') text = text.Substring(1);
            var parts = text.Split('.');
            if (parts.Length != 3) return false;
            var numbers = new int[3];
            for (int i = 0; i < 3; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
            version = new AppVersion(numbers[0], numbers[1], numbers[2]);
            return true;
        }

        public int CompareTo(AppVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
