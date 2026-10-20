using System;
using System.Collections.Generic;

namespace FietsApp.Accounts
{
    /// <summary>De code die de rijder op github.com/login/device intikt (story 7.1).</summary>
    public sealed class DeviceCode
    {
        public string Code;            // device_code, voor het opvragen van het token
        public string UserCode;        // wat de rijder intikt, bijvoorbeeld "WDJB-MJHT"
        public string VerificationUri; // https://github.com/login/device
        public int IntervalSeconds;    // zo vaak mag de app vragen of het al bevestigd is
        public int ExpiresInSeconds;
    }

    public enum TokenState { Pending, SlowDown, Expired, Denied, Granted, Failed }

    /// <summary>Antwoord op de vraag om een token: nog wachten, sneller niet, verlopen, geweigerd of gelukt.</summary>
    public readonly struct TokenPoll
    {
        public readonly TokenState State;
        public readonly string Token;   // bij Granted
        public readonly string Message; // bij Failed

        public TokenPoll(TokenState state, string token = null, string message = null)
        {
            State = state;
            Token = token;
            Message = message;
        }
    }

    /// <summary>
    /// Inloggen met GitHub via de device flow en het profiel als privé-Gist (stories 7.1 en 7.2; besluit "Accounts").
    /// De device flow heeft alleen de openbare Client ID van de OAuth-app nodig, geen geheim, en de rijder tikt zijn
    /// wachtwoord nooit in de app: hij bevestigt een code op github.com. Het token mag alleen Gists lezen en schrijven.
    /// Hier staan de berichten en hun uitleg; het versturen doet de Unity-kant.
    /// </summary>
    public static class GitHub
    {
        public const string DeviceCodeUrl = "https://github.com/login/device/code";
        public const string TokenUrl = "https://github.com/login/oauth/access_token";
        public const string UserUrl = "https://api.github.com/user";
        public const string GistsUrl = "https://api.github.com/gists";
        public const string Scope = "gist";

        /// <summary>Naam van het bestand in de Gist en de beschrijving waaraan de app hem herkent.</summary>
        public const string ProfileFile = "fietsapp-profiel.json";
        public const string ProfileDescription = "FietsApp-profiel (niet met de hand wijzigen)";

        public static string DeviceCodeForm(string clientId) =>
            $"client_id={Uri.EscapeDataString(clientId)}&scope={Scope}";

        public static string TokenForm(string clientId, string deviceCode) =>
            $"client_id={Uri.EscapeDataString(clientId)}&device_code={Uri.EscapeDataString(deviceCode)}" +
            "&grant_type=urn:ietf:params:oauth:grant-type:device_code";

        public static DeviceCode ParseDeviceCode(string json)
        {
            var o = Json.Parse(json);
            string error = Json.GetString(o, "error");
            if (error != null)
                throw new FormatException(error == "device_flow_disabled"
                    ? "Device Flow staat uit in de OAuth-app op GitHub"
                    : Json.GetString(o, "error_description") ?? error);
            return new DeviceCode
            {
                Code = Json.GetString(o, "device_code") ?? throw new FormatException("device_code ontbreekt"),
                UserCode = Json.GetString(o, "user_code"),
                VerificationUri = Json.GetString(o, "verification_uri") ?? "https://github.com/login/device",
                IntervalSeconds = (int)(Json.GetNumber(o, "interval") ?? 5),
                ExpiresInSeconds = (int)(Json.GetNumber(o, "expires_in") ?? 900),
            };
        }

        public static TokenPoll ParseToken(string json)
        {
            object o;
            try { o = Json.Parse(json); }
            catch (FormatException e) { return new TokenPoll(TokenState.Failed, message: e.Message); }
            string token = Json.GetString(o, "access_token");
            if (!string.IsNullOrEmpty(token)) return new TokenPoll(TokenState.Granted, token);
            switch (Json.GetString(o, "error"))
            {
                case "authorization_pending": return new TokenPoll(TokenState.Pending);
                case "slow_down": return new TokenPoll(TokenState.SlowDown);
                case "expired_token": return new TokenPoll(TokenState.Expired);
                case "access_denied": return new TokenPoll(TokenState.Denied);
                default: return new TokenPoll(TokenState.Failed, message: Json.GetString(o, "error_description") ?? Json.GetString(o, "error") ?? "onbekend antwoord");
            }
        }

        /// <summary>Gebruikersnaam uit het antwoord van /user.</summary>
        public static string ParseLogin(string json) => Json.GetString(Json.Parse(json), "login");

        /// <summary>De id van de profiel-Gist in een lijst van /gists, of null als die er (nog) niet is.</summary>
        public static string FindProfileGist(string listJson)
        {
            if (!(Json.Parse(listJson) is List<object> gists)) return null;
            foreach (var g in gists)
                if (Json.Get(Json.Get(g, "files"), ProfileFile) != null) return Json.GetString(g, "id");
            return null;
        }

        /// <summary>De inhoud van het profielbestand uit een Gist, of null. Bij een te groot bestand kapt GitHub af (truncated).</summary>
        public static string ReadProfile(string gistJson)
        {
            var file = Json.Get(Json.Get(Json.Parse(gistJson), "files"), ProfileFile);
            if (file == null) return null;
            if (Json.Get(file, "truncated") is bool truncated && truncated)
                throw new FormatException("Profiel in de Gist is te groot om in één keer te lezen.");
            return Json.GetString(file, "content");
        }

        /// <summary>Body om de privé-Gist aan te maken (POST /gists).</summary>
        public static string CreateGistBody(string profileJson) =>
            "{\"description\":" + Json.Quote(ProfileDescription) + ",\"public\":false,\"files\":{" +
            Json.Quote(ProfileFile) + ":{\"content\":" + Json.Quote(profileJson) + "}}}";

        /// <summary>Body om het profiel in de Gist te vervangen (PATCH /gists/{id}).</summary>
        public static string UpdateGistBody(string profileJson) =>
            "{\"files\":{" + Json.Quote(ProfileFile) + ":{\"content\":" + Json.Quote(profileJson) + "}}}";
    }
}
