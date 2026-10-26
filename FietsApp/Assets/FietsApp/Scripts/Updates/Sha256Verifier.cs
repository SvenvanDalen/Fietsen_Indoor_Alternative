using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FietsApp.Updates
{
    /// <summary>Controleert een download tegen een <c>.sha256</c>-bestand in <c>sha256sum</c>-formaat (story 13.6).</summary>
    public static class Sha256Verifier
    {
        /// <summary>Waar als het eerste woord van <paramref name="sha256FileContent"/> de SHA-256 van het bestand is.</summary>
        public static bool Matches(string filePath, string sha256FileContent)
        {
            if (string.IsNullOrWhiteSpace(sha256FileContent)) return false;
            string expected = sha256FileContent.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            if (expected.Length != 64) return false;
            foreach (char c in expected)
                if (!Uri.IsHexDigit(c)) return false;

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var sb = new StringBuilder(64);
                foreach (var b in sha.ComputeHash(stream)) sb.Append(b.ToString("x2"));
                return string.Equals(sb.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
