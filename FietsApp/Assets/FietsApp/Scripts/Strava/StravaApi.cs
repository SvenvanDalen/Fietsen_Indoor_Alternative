using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using FietsApp.Accounts;

namespace FietsApp.Strava
{
    /// <summary>Tokens van Strava: het toegangstoken verloopt na zes uur, het verversingstoken niet.</summary>
    public sealed class StravaToken
    {
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAt;      // Unix-seconden
        public string AthleteName;  // alleen bij het eerste token, niet bij verversen
    }

    /// <summary>Wat Strava na het koppelen naar de app terugstuurt: een code, of waarom niet.</summary>
    public sealed class OAuthCallback
    {
        public string Code;
        public string Error;
    }

    public enum UploadState { Processing, Done, Duplicate, Failed }

    /// <summary>Stand van een upload; bij <see cref="UploadState.Duplicate"/> is <see cref="ActivityId"/> de bestaande activiteit.</summary>
    public sealed class UploadStatus
    {
        public long UploadId;
        public long ActivityId;
        public string Error;
        public UploadState State;
    }

    /// <summary>
    /// Strava-koppeling (story 5.2; besluit "Strava"). Koppelen gaat met OAuth: de app opent strava.com in de browser en
    /// Strava stuurt de rijder terug naar <c>http://localhost</c>, waar de app een code ophaalt en die met de Client ID en
    /// het Client Secret van de eigen API-app van de rijder ruilt voor tokens. Uploaden is een .fit naar <c>/uploads</c>;
    /// Strava verwerkt die op de achtergrond. Hier staan de berichten en hun uitleg; het versturen doet de Unity-kant.
    /// </summary>
    public static class StravaApi
    {
        public const string AuthorizeBaseUrl = "https://www.strava.com/oauth/authorize";
        public const string TokenUrl = "https://www.strava.com/oauth/token";
        public const string UploadsUrl = "https://www.strava.com/api/v3/uploads";
        public const string SettingsUrl = "https://www.strava.com/settings/api";
        public const string Scope = "activity:write";

        /// <summary>Zo lang voor het verlopen ververst de app het token al.</summary>
        public const int RefreshMarginSeconds = 300;

        public static string AuthorizeUrl(string clientId, string redirectUri, string state) =>
            $"{AuthorizeBaseUrl}?client_id={Escape(clientId)}&redirect_uri={Escape(redirectUri)}&response_type=code" +
            $"&approval_prompt=auto&scope={Escape(Scope)}&state={Escape(state)}";

        public static string TokenForm(string clientId, string secret, string code) =>
            $"client_id={Escape(clientId)}&client_secret={Escape(secret)}&code={Escape(code)}&grant_type=authorization_code";

        public static string RefreshForm(string clientId, string secret, string refreshToken) =>
            $"client_id={Escape(clientId)}&client_secret={Escape(secret)}&refresh_token={Escape(refreshToken)}&grant_type=refresh_token";

        public static string ActivityUrl(long activityId) => "https://www.strava.com/activities/" + activityId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Leest de query van de terugkeer-URL; de state moet kloppen en uploaden moet zijn toegestaan.</summary>
        public static OAuthCallback ParseCallback(string query, string expectedState)
        {
            var q = ParseQuery(query);
            q.TryGetValue("state", out var state);
            if (state != expectedState) return new OAuthCallback { Error = "onbekend antwoord van Strava, probeer opnieuw" };
            if (q.TryGetValue("error", out var error))
                return new OAuthCallback { Error = error == "access_denied" ? "koppelen geweigerd op Strava" : $"Strava: {error}" };
            q.TryGetValue("scope", out var scope);
            if (Array.IndexOf((scope ?? "").Split(','), Scope) < 0)
                return new OAuthCallback { Error = "geen toestemming om ritten te uploaden; zet dat vinkje aan op Strava" };
            if (!q.TryGetValue("code", out var code) || code.Length == 0)
                return new OAuthCallback { Error = "Strava stuurde geen code" };
            return new OAuthCallback { Code = code };
        }

        public static StravaToken ParseToken(string json)
        {
            var o = Json.Parse(json);
            string access = Json.GetString(o, "access_token");
            if (access == null) throw new FormatException(ErrorMessage(o) ?? "Strava gaf geen token");
            var athlete = Json.Get(o, "athlete");
            string name = athlete == null ? null
                : $"{Json.GetString(athlete, "firstname")} {Json.GetString(athlete, "lastname")}".Trim();
            return new StravaToken
            {
                AccessToken = access,
                RefreshToken = Json.GetString(o, "refresh_token") ?? throw new FormatException("refresh_token ontbreekt"),
                ExpiresAt = (long)(Json.GetNumber(o, "expires_at") ?? 0),
                AthleteName = string.IsNullOrEmpty(name) ? null : name,
            };
        }

        public static bool NeedsRefresh(long expiresAt, DateTime nowUtc) =>
            expiresAt - new DateTimeOffset(nowUtc, TimeSpan.Zero).ToUnixTimeSeconds() < RefreshMarginSeconds;

        static readonly Regex DuplicateOf = new Regex(@"duplicate of\D*(\d+)", RegexOptions.IgnoreCase);

        public static UploadStatus ParseUpload(string json)
        {
            var o = Json.Parse(json);
            var status = new UploadStatus
            {
                UploadId = (long)(Json.GetNumber(o, "id") ?? 0),
                ActivityId = (long)(Json.GetNumber(o, "activity_id") ?? 0),
                Error = Json.GetString(o, "error"),
            };
            if (string.IsNullOrEmpty(status.Error))
            {
                status.State = status.ActivityId > 0 ? UploadState.Done : UploadState.Processing;
                if (status.UploadId == 0 && status.ActivityId == 0)
                {
                    // Geen upload-antwoord maar een foutbericht, bijvoorbeeld een verlopen token.
                    status.State = UploadState.Failed;
                    status.Error = ErrorMessage(o) ?? "onverwacht antwoord van Strava";
                }
                return status;
            }
            var duplicate = DuplicateOf.Match(status.Error);
            if (duplicate.Success && long.TryParse(duplicate.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                status.State = UploadState.Duplicate;
                status.ActivityId = id;
            }
            else status.State = UploadState.Failed;
            return status;
        }

        /// <summary>"Bad Request (client_secret invalid)" uit een foutantwoord van Strava, of null.</summary>
        static string ErrorMessage(object o)
        {
            string message = Json.GetString(o, "message");
            if (message == null) return null;
            if (Json.Get(o, "errors") is List<object> errors && errors.Count > 0)
            {
                var first = errors[0];
                message += $" ({Json.GetString(first, "field")} {Json.GetString(first, "code")})";
            }
            return message;
        }

        static string Escape(string s) => Uri.EscapeDataString(s ?? "");

        static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (var part in (query ?? "").TrimStart('?').Split('&'))
            {
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                string key = eq < 0 ? part : part.Substring(0, eq);
                string value = eq < 0 ? "" : part.Substring(eq + 1);
                result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }
            return result;
        }
    }

    /// <summary>Een opgeslagen rit: bestandsnaam en wanneer hij geschreven is.</summary>
    public readonly struct RideFile
    {
        public readonly string Name;
        public readonly DateTime WrittenUtc;

        public RideFile(string name, DateTime writtenUtc)
        {
            Name = name;
            WrittenUtc = writtenUtc;
        }
    }

    /// <summary>
    /// Welke ritten nog naar Strava moeten. Automatisch alleen ritten vanaf het koppelen, zodat oude proefritten niet
    /// ongevraagd op Strava belanden; oudere ritten upload de rijder met één knop.
    /// </summary>
    public static class UploadQueue
    {
        public static List<string> Pending(IEnumerable<RideFile> files, DateTime? connectedUtc, ICollection<string> handled) =>
            connectedUtc.HasValue ? Select(files, f => f.WrittenUtc >= connectedUtc.Value, handled) : new List<string>();

        public static List<string> Older(IEnumerable<RideFile> files, DateTime? connectedUtc, ICollection<string> handled) =>
            connectedUtc.HasValue ? Select(files, f => f.WrittenUtc < connectedUtc.Value, handled) : new List<string>();

        static List<string> Select(IEnumerable<RideFile> files, Func<RideFile, bool> when, ICollection<string> handled)
        {
            var chosen = new List<RideFile>();
            foreach (var f in files)
                if (when(f) && !handled.Contains(f.Name)) chosen.Add(f);
            chosen.Sort((a, b) => a.WrittenUtc.CompareTo(b.WrittenUtc));
            return chosen.ConvertAll(f => f.Name);
        }
    }

    /// <summary>Ritbestanden heten <c>jjjj-MM-dd_UUmm_Klim.fit</c> (zie <c>RideRecorder</c>): datum en klim terug uit de naam.</summary>
    public static class RideFileName
    {
        public static bool TryParse(string fileName, out DateTime local, out string climb)
        {
            string stem = fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase) ? fileName.Substring(0, fileName.Length - 4) : fileName;
            if (stem.Length > 16 && stem[10] == '_' && stem[15] == '_' &&
                DateTime.TryParseExact(stem.Substring(0, 15), "yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                climb = stem.Substring(16).Replace('_', ' ');
                return true;
            }
            local = default;
            climb = stem;
            return false;
        }

        /// <summary>Titel van de activiteit op Strava.</summary>
        public static string ActivityName(string fileName)
        {
            TryParse(fileName, out _, out var climb);
            return $"{climb} (FietsApp)";
        }
    }
}
