using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FietsApp.Strava;
using UnityEngine;
using UnityEngine.Networking;

namespace FietsApp
{
    /// <summary>
    /// Strava-koppeling (story 5.2; besluit "Strava"): de rijder vult de Client ID en het Client Secret van zijn eigen
    /// API-app in (strava.com/settings/api, callback-domein <c>localhost</c>) en koppelt via de browser. Daarna gaat elke
    /// opgeslagen rit vanzelf naar Strava als virtuele rit: meteen na het opslaan, en bij de volgende start wat bij het
    /// afsluiten is blijven liggen. Ritten van vóór het koppelen gaan alleen op verzoek mee. Alles staat in
    /// <c>strava.json</c> in de app-map, ook welke rit welke activiteit werd, zodat niets twee keer gaat.
    /// </summary>
    public sealed class StravaAccount : MonoBehaviour
    {
        /// <summary>Hierheen stuurt Strava de browser terug na het koppelen; de app luistert er even op.</summary>
        public const string RedirectUri = "http://localhost:8767/strava/";
        const float ConnectTimeoutSeconds = 300f;
        const int ProcessingPolls = 30;

        [Serializable]
        sealed class Upload
        {
            public string file = "";
            public long activityId;
            public string error = "";
        }

        [Serializable]
        sealed class Stored
        {
            public string clientId = "";
            public string clientSecret = "";
            public string accessToken = "";
            public string refreshToken = "";
            public long expiresAt;
            public string athlete = "";
            public string connectedAtUtc = ""; // ISO 8601; vanaf hier uploadt de app vanzelf
            public List<Upload> uploads = new List<Upload>();
        }

        Stored _stored = new Stored();
        RideRecorder _recorder;
        bool _connecting, _uploading, _uploadAgain, _includeOlder;
        HttpListener _listener;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "strava.json");

        /// <summary>Korte toestand voor het startscherm en het Strava-paneel.</summary>
        public string Status { get; private set; } = "";

        public bool IsConnected => !string.IsNullOrEmpty(_stored.refreshToken);
        public bool IsBusy => _connecting || _uploading;
        public bool HasAppKeys => _stored.clientId.Length > 0 && _stored.clientSecret.Length > 0;
        public string ClientId => _stored.clientId;
        public string Athlete => _stored.athlete;

        /// <summary>Ritten die nog automatisch naar Strava gaan.</summary>
        public int PendingCount => UploadQueue.Pending(RideFiles(), ConnectedAt, Handled()).Count;

        /// <summary>Ritten van vóór het koppelen die nog niet op Strava staan.</summary>
        public int OlderCount => UploadQueue.Older(RideFiles(), ConnectedAt, Handled()).Count;

        void Awake()
        {
            try
            {
                if (File.Exists(FilePath)) _stored = JsonUtility.FromJson<Stored>(File.ReadAllText(FilePath)) ?? new Stored();
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"Strava-gegevens niet te lezen: {e.Message}");
            }
            if (_stored.uploads == null) _stored.uploads = new List<Upload>();
            Status = IsConnected ? $"gekoppeld als {_stored.athlete}" : "niet gekoppeld";
        }

        void OnEnable()
        {
            _recorder = FindAnyObjectByType<RideRecorder>();
            if (_recorder != null) _recorder.Saved += OnRideSaved;
        }

        void OnDisable()
        {
            if (_recorder != null) _recorder.Saved -= OnRideSaved;
            StopListening();
        }

        void Start()
        {
            // Wat bij het afsluiten is opgeslagen (of toen geen netwerk had) gaat nu alsnog.
            if (IsConnected) UploadNow();
        }

        void OnRideSaved(string path)
        {
            if (IsConnected && isActiveAndEnabled) UploadNow();
        }

        /// <summary>Wat er met een rit op Strava gebeurde: de activiteit, een fout, of (false) nog niets.</summary>
        public bool TryGetUpload(string fileName, out long activityId, out string error)
        {
            var u = _stored.uploads.FirstOrDefault(x => x.file == fileName);
            activityId = u?.activityId ?? 0;
            error = u?.error ?? "";
            return u != null;
        }

        // ---- Koppelen ----

        /// <summary>Bewaart de gegevens van de Strava-API-app; een <paramref name="clientSecret"/> van null houdt het oude.</summary>
        public void SetAppKeys(string clientId, string clientSecret)
        {
            _stored.clientId = (clientId ?? "").Trim();
            if (clientSecret != null) _stored.clientSecret = clientSecret.Trim();
            Store();
        }

        public void Connect()
        {
            if (IsBusy) return;
            if (!HasAppKeys) { Status = "vul eerst de Client ID en het Client Secret in"; return; }
            StartCoroutine(ConnectRoutine());
        }

        public void CancelConnect()
        {
            if (!_connecting) return;
            StopAllCoroutines();
            StopListening();
            _connecting = _uploading = false;
            Status = "koppelen afgebroken";
        }

        /// <summary>Vergeet de tokens; de API-gegevens en welke ritten al op Strava staan blijven bewaard.</summary>
        public void Disconnect()
        {
            StopAllCoroutines();
            StopListening();
            _connecting = _uploading = false;
            _stored.accessToken = _stored.refreshToken = _stored.athlete = _stored.connectedAtUtc = "";
            _stored.expiresAt = 0;
            Store();
            Status = "ontkoppeld";
        }

        IEnumerator ConnectRoutine()
        {
            _connecting = true;
            string state = Guid.NewGuid().ToString("N");
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(RedirectUri);
                _listener.Start();
            }
            catch (Exception e) when (e is HttpListenerException || e is InvalidOperationException || e is System.Net.Sockets.SocketException)
            {
                StopListening();
                Fail($"kan niet luisteren op {RedirectUri}: {e.Message}");
                yield break;
            }

            Status = "bevestig het koppelen in je browser...";
            Application.OpenURL(StravaApi.AuthorizeUrl(_stored.clientId, RedirectUri, state));

            OAuthCallback callback = null;
            float deadline = Time.unscaledTime + ConnectTimeoutSeconds;
            while (callback == null)
            {
                var wait = _listener.GetContextAsync();
                while (!wait.IsCompleted && Time.unscaledTime < deadline) yield return null;
                if (!wait.IsCompleted || wait.IsFaulted)
                {
                    StopListening();
                    Fail(wait.IsCompleted ? "koppelen mislukt" : "geen antwoord van Strava, probeer opnieuw");
                    yield break;
                }
                var context = wait.Result;
                // De browser vraagt soms ook /favicon.ico; alleen de terugkeer zelf telt.
                if (!context.Request.Url.AbsolutePath.StartsWith("/strava", StringComparison.Ordinal))
                {
                    Respond(context, 404, "");
                    continue;
                }
                callback = StravaApi.ParseCallback(context.Request.Url.Query, state);
                Respond(context, 200, callback.Error == null
                    ? "FietsApp is gekoppeld met Strava. Je kunt dit tabblad sluiten."
                    : $"Koppelen mislukt: {callback.Error}");
            }
            StopListening();
            if (callback.Error != null) { Fail(callback.Error); yield break; }

            Status = "tokens ophalen bij Strava...";
            string body = null, error = null;
            yield return Post(StravaApi.TokenUrl, StravaApi.TokenForm(_stored.clientId, _stored.clientSecret, callback.Code),
                (b, e, _) => { body = b; error = e; });
            StravaToken token;
            try { token = StravaApi.ParseToken(body ?? ""); }
            catch (FormatException e) { Fail($"koppelen mislukt: {(error != null ? $"{error} · " : "")}{e.Message}"); yield break; }

            _stored.accessToken = token.AccessToken;
            _stored.refreshToken = token.RefreshToken;
            _stored.expiresAt = token.ExpiresAt;
            _stored.athlete = token.AthleteName ?? "";
            _stored.connectedAtUtc = DateTime.UtcNow.ToString("o");
            Store();
            _connecting = false;
            Status = $"gekoppeld als {_stored.athlete}";
            UploadNow();
        }

        static void Respond(HttpListenerContext context, int status, string message)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(
                    $"<!doctype html><meta charset=\"utf-8\"><title>FietsApp</title>" +
                    $"<body style=\"font-family:sans-serif;padding:3em\"><h2>{WebUtility.HtmlEncode(message)}</h2></body>");
                context.Response.StatusCode = status;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
            catch (Exception e) when (e is HttpListenerException || e is IOException || e is ObjectDisposedException)
            {
                Debug.LogWarning($"Strava: antwoord aan de browser mislukt: {e.Message}");
            }
        }

        void StopListening()
        {
            if (_listener == null) return;
            try { _listener.Close(); }
            catch (ObjectDisposedException) { }
            _listener = null;
        }

        // ---- Uploaden ----

        /// <summary>Upload de ritten sinds het koppelen die nog niet op Strava staan.</summary>
        public void UploadNow()
        {
            if (!IsConnected || _connecting) return;
            if (_uploading) { _uploadAgain = true; return; }
            StartCoroutine(UploadRoutine());
        }

        /// <summary>Upload ook de ritten van vóór het koppelen.</summary>
        public void UploadOlder()
        {
            _includeOlder = true;
            UploadNow();
        }

        IEnumerator UploadRoutine()
        {
            _uploading = true;
            do
            {
                _uploadAgain = false;
                var files = RideFiles();
                var queue = UploadQueue.Pending(files, ConnectedAt, Handled());
                if (_includeOlder) queue.InsertRange(0, UploadQueue.Older(files, ConnectedAt, Handled()));
                _includeOlder = false;
                int done = 0;
                foreach (var name in queue)
                {
                    Status = $"rit naar Strava: {RideFileName.ActivityName(name)} ({done + 1}/{queue.Count})";
                    bool keepGoing = true;
                    yield return UploadOne(name, ok => keepGoing = ok);
                    if (!keepGoing) { _uploading = false; yield break; } // netwerk of token: later opnieuw
                    done++;
                }
                if (queue.Count > 0) Status = $"gekoppeld als {_stored.athlete} · {done} rit{(done == 1 ? "" : "ten")} op Strava gezet ({DateTime.Now:HH:mm})";
            } while (_uploadAgain);
            _uploading = false;
        }

        IEnumerator UploadOne(string name, Action<bool> keepGoing)
        {
            bool tokenOk = false;
            yield return EnsureToken(ok => tokenOk = ok);
            if (!tokenOk) { keepGoing(false); yield break; }

            byte[] bytes;
            try { bytes = File.ReadAllBytes(Path.Combine(RideRecorder.Folder, name)); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Record(name, 0, $"bestand niet te lezen: {e.Message}");
                keepGoing(true);
                yield break;
            }

            var form = new WWWForm();
            form.AddBinaryData("file", bytes, name, "application/octet-stream");
            form.AddField("data_type", "fit");
            form.AddField("external_id", name);
            form.AddField("name", RideFileName.ActivityName(name));
            // Geen trainer=1: dan toont Strava de rit als binnentraining zonder kaart. Het type (virtuele rit) en de
            // GPS-punten van de echte klim komen uit de .fit zelf, zoals bij Zwift en MyWhoosh.

            string body = null, error = null;
            long code = 0;
            using (var request = UnityWebRequest.Post(StravaApi.UploadsUrl, form))
            {
                request.SetRequestHeader("Authorization", "Bearer " + _stored.accessToken);
                yield return request.SendWebRequest();
                body = request.downloadHandler.text;
                code = request.responseCode;
                error = request.result == UnityWebRequest.Result.Success ? null : request.error;
            }
            if (!Usable(code, error, body)) { keepGoing(false); yield break; }

            UploadStatus upload;
            try { upload = StravaApi.ParseUpload(body); }
            catch (FormatException e) { Fail($"onverwacht antwoord van Strava: {e.Message}"); keepGoing(false); yield break; }

            // Strava verwerkt de rit op de achtergrond; even navragen tot er een activiteit is.
            for (int i = 0; i < ProcessingPolls && upload.State == UploadState.Processing; i++)
            {
                yield return new WaitForSecondsRealtime(2f);
                yield return Get($"{StravaApi.UploadsUrl}/{upload.UploadId}", (b, e, c) => { body = b; error = e; code = c; });
                if (error != null) break; // de upload is binnen; de activiteit komt vanzelf
                try { upload = StravaApi.ParseUpload(body); }
                catch (FormatException) { break; }
            }

            switch (upload.State)
            {
                case UploadState.Done:
                case UploadState.Duplicate:
                    Record(name, upload.ActivityId, "");
                    Debug.Log($"Strava: {name} → {StravaApi.ActivityUrl(upload.ActivityId)}");
                    break;
                case UploadState.Failed:
                    Record(name, 0, upload.Error);
                    Debug.LogWarning($"Strava weigerde {name}: {upload.Error}");
                    break;
                default:
                    Record(name, 0, ""); // nog in verwerking: staat erop zodra Strava klaar is
                    break;
            }
            keepGoing(true);
        }

        /// <summary>Ververst het toegangstoken als het (bijna) verlopen is.</summary>
        IEnumerator EnsureToken(Action<bool> done)
        {
            if (!StravaApi.NeedsRefresh(_stored.expiresAt, DateTime.UtcNow)) { done(true); yield break; }
            string body = null, error = null;
            long code = 0;
            yield return Post(StravaApi.TokenUrl, StravaApi.RefreshForm(_stored.clientId, _stored.clientSecret, _stored.refreshToken),
                (b, e, c) => { body = b; error = e; code = c; });
            if (code == 400 || code == 401)
            {
                // Toegang ingetrokken op strava.com of ander secret: opnieuw koppelen.
                Disconnect();
                Fail("Strava accepteert de koppeling niet meer, koppel opnieuw");
                done(false);
                yield break;
            }
            if (error != null) { Fail($"Strava niet bereikbaar ({error}); later opnieuw"); done(false); yield break; }
            try
            {
                var token = StravaApi.ParseToken(body);
                _stored.accessToken = token.AccessToken;
                _stored.refreshToken = token.RefreshToken;
                _stored.expiresAt = token.ExpiresAt;
                Store();
                done(true);
            }
            catch (FormatException e) { Fail($"token verversen mislukt: {e.Message}"); done(false); }
        }

        bool Usable(long code, string error, string body)
        {
            if (code == 401) { _stored.expiresAt = 0; Store(); Fail("Strava wil een nieuw token; volgende keer opnieuw"); return false; }
            if (code == 429) { Fail("even te veel verzoeken aan Strava; later opnieuw"); return false; }
            // Een 400 met een upload-antwoord (bijvoorbeeld een dubbele rit) is bruikbaar; geen antwoord betekent geen netwerk.
            if (error != null && (code == 0 || code >= 500 || string.IsNullOrEmpty(body))) { Fail($"uploaden mislukt ({error}); later opnieuw"); return false; }
            return true;
        }

        void Record(string file, long activityId, string error)
        {
            _stored.uploads.RemoveAll(u => u.file == file);
            _stored.uploads.Add(new Upload { file = file, activityId = activityId, error = error ?? "" });
            Store();
        }

        // ---- Hulp ----

        DateTime? ConnectedAt =>
            DateTime.TryParse(_stored.connectedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t.ToUniversalTime() : (DateTime?)null;

        HashSet<string> Handled() => new HashSet<string>(_stored.uploads.Select(u => u.file));

        /// <summary>De opgeslagen ritten met het moment van schrijven.</summary>
        public static List<RideFile> RideFiles()
        {
            try
            {
                if (!Directory.Exists(RideRecorder.Folder)) return new List<RideFile>();
                return new DirectoryInfo(RideRecorder.Folder).GetFiles("*.fit")
                    .Select(f => new RideFile(f.Name, f.LastWriteTimeUtc)).ToList();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Ritten niet te lezen: {e.Message}");
                return new List<RideFile>();
            }
        }

        void Fail(string message)
        {
            _connecting = false;
            Status = message;
            Debug.LogWarning($"Strava: {message}");
        }

        void Store()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(_stored)); }
            catch (IOException e) { Debug.LogError($"Strava-gegevens opslaan mislukt: {e.Message}"); }
        }

        static IEnumerator Post(string url, string form, Action<string, string, long> done)
        {
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(form));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                done(request.downloadHandler.text, ok ? null : request.error, request.responseCode);
            }
        }

        IEnumerator Get(string url, Action<string, string, long> done)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + _stored.accessToken);
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                done(request.downloadHandler.text, ok ? null : request.error, request.responseCode);
            }
        }
    }
}
