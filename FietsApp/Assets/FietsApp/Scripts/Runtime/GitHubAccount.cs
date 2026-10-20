using System;
using System.Collections;
using System.IO;
using System.Text;
using FietsApp.Accounts;
using FietsApp.Training;
using UnityEngine;
using UnityEngine.Networking;

namespace FietsApp
{
    /// <summary>
    /// Account via GitHub (stories 7.1 en 7.2, besluit "Accounts"): inloggen met de device flow (de app toont een code, je
    /// bevestigt die op github.com; wachtwoord en wachtwoordherstel blijven bij GitHub) en het profiel synchroniseren met
    /// een privé-Gist in je eigen account. Synchroniseert bij het starten, na elke profielwijziging (na een paar seconden)
    /// en op verzoek. Het token staat in <c>account.json</c> in de app-map en mag alleen Gists lezen en schrijven.
    /// </summary>
    public sealed class GitHubAccount : MonoBehaviour
    {
        [Tooltip("Client ID van de OAuth-app op GitHub (met Device Flow aan). Niet geheim; een client secret is niet nodig.")]
        [SerializeField] string clientId = "";
        [Tooltip("Zoveel seconden na een profielwijziging synchroniseren, zodat een reeks wijzigingen één upload wordt.")]
        [SerializeField, Min(0.5f)] float syncDelaySeconds = 3f;

        [Serializable]
        sealed class Stored
        {
            public string token = "";
            public string login = "";
            public string gistId = "";
        }

        Stored _account = new Stored();
        bool _busy, _applying;
        float _syncAt = -1f;

        /// <summary>De code om op github.com in te tikken, zolang het inloggen loopt; anders null.</summary>
        public DeviceCode PendingCode { get; private set; }

        /// <summary>Korte toestand voor het accountpaneel.</summary>
        public string Status { get; private set; } = "";

        public bool IsSignedIn => !string.IsNullOrEmpty(_account.token);
        public string Login => _account.login;
        public bool HasClientId => !string.IsNullOrWhiteSpace(clientId);
        public bool IsBusy => _busy;
        public DateTime? LastSync { get; private set; }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "account.json");

        void Awake()
        {
            try
            {
                if (File.Exists(FilePath)) _account = JsonUtility.FromJson<Stored>(File.ReadAllText(FilePath)) ?? new Stored();
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"Account niet te lezen: {e.Message}");
            }
            Status = IsSignedIn ? $"ingelogd als {_account.login}" : "niet ingelogd";
        }

        void OnEnable() => ProfileStore.Changed += OnProfileChanged;

        void OnDisable() => ProfileStore.Changed -= OnProfileChanged;

        void Start()
        {
            if (IsSignedIn) SyncNow();
        }

        void Update()
        {
            if (_syncAt >= 0f && Time.unscaledTime >= _syncAt && !_busy)
            {
                _syncAt = -1f;
                SyncNow();
            }
        }

        void OnProfileChanged()
        {
            if (_applying || !IsSignedIn) return;
            _syncAt = Time.unscaledTime + syncDelaySeconds;
        }

        // ---- Inloggen ----

        public void SignIn()
        {
            if (_busy || IsSignedIn) return;
            if (!HasClientId)
            {
                Status = "nog geen GitHub Client ID ingesteld (zie GitHubAccount in de scène)";
                return;
            }
            StartCoroutine(SignInRoutine());
        }

        public void CancelSignIn()
        {
            if (PendingCode == null) return;
            StopAllCoroutines();
            PendingCode = null;
            _busy = false;
            Status = "inloggen afgebroken";
        }

        IEnumerator SignInRoutine()
        {
            _busy = true;
            Status = "code opvragen bij GitHub...";
            string body = null, error = null;
            long status = 0;
            yield return Post(GitHub.DeviceCodeUrl, GitHub.DeviceCodeForm(clientId), (b, e, c) => { body = b; error = e; status = c; });
            if (status == 404) { Fail("GitHub kent deze Client ID niet; controleer hem bij GitHubAccount in de scène"); yield break; }
            if (error != null) { Fail($"GitHub niet bereikbaar: {error}"); yield break; }
            DeviceCode code;
            try { code = GitHub.ParseDeviceCode(body); }
            catch (FormatException e) { Fail(e.Message); yield break; }

            PendingCode = code;
            Status = "bevestig de code op github.com";
            Application.OpenURL(code.VerificationUri);

            float interval = Mathf.Max(1, code.IntervalSeconds);
            float deadline = Time.unscaledTime + code.ExpiresInSeconds;
            while (Time.unscaledTime < deadline)
            {
                yield return new WaitForSecondsRealtime(interval);
                yield return Post(GitHub.TokenUrl, GitHub.TokenForm(clientId, code.Code), (b, e, c) => { body = b; error = e; });
                if (error != null) continue; // even geen netwerk: blijven proberen tot de code verloopt
                var poll = GitHub.ParseToken(body);
                if (poll.State == TokenState.Pending) continue;
                if (poll.State == TokenState.SlowDown) { interval += 5; continue; }
                PendingCode = null;
                if (poll.State == TokenState.Granted)
                {
                    _account = new Stored { token = poll.Token };
                    yield return Api("GET", GitHub.UserUrl, null, (b, e, _) => { body = b; error = e; });
                    if (error != null) { Fail($"gebruiker opvragen mislukt: {error}"); _account = new Stored(); yield break; }
                    _account.login = GitHub.ParseLogin(body) ?? "";
                    StoreAccount();
                    _busy = false;
                    Status = $"ingelogd als {_account.login}";
                    SyncNow();
                    yield break;
                }
                Fail(poll.State == TokenState.Denied ? "inloggen geweigerd op GitHub"
                    : poll.State == TokenState.Expired ? "de code is verlopen, probeer opnieuw"
                    : $"inloggen mislukt: {poll.Message}");
                yield break;
            }
            PendingCode = null;
            Fail("de code is verlopen, probeer opnieuw");
        }

        /// <summary>Vergeet het token op dit apparaat. Het profiel blijft lokaal en in de Gist staan.</summary>
        public void SignOut()
        {
            StopAllCoroutines();
            _busy = false;
            PendingCode = null;
            _account = new Stored();
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (IOException e) { Debug.LogWarning($"Account niet te verwijderen: {e.Message}"); }
            Status = "uitgelogd";
        }

        // ---- Synchroniseren ----

        public void SyncNow()
        {
            if (_busy || !IsSignedIn) return;
            StartCoroutine(SyncRoutine());
        }

        IEnumerator SyncRoutine()
        {
            _busy = true;
            Status = "synchroniseren...";
            string body = null, error = null;
            long code = 0;
            void Got(string b, string e, long c) { body = b; error = e; code = c; }

            // De Gist zoeken als we zijn id nog niet weten (eerste keer op dit apparaat).
            if (string.IsNullOrEmpty(_account.gistId))
            {
                yield return Api("GET", GitHub.GistsUrl + "?per_page=100", null, Got);
                if (!Check(error, code)) yield break;
                _account.gistId = GitHub.FindProfileGist(body) ?? "";
                StoreAccount();
            }

            string local = ProfileStore.ToJson(ProfileStore.Current);
            if (string.IsNullOrEmpty(_account.gistId))
            {
                yield return Api("POST", GitHub.GistsUrl, GitHub.CreateGistBody(local), Got);
                if (!Check(error, code)) yield break;
                _account.gistId = Json.GetString(Json.Parse(body), "id") ?? "";
                StoreAccount();
                Done("profiel in je GitHub-account gezet");
                yield break;
            }

            yield return Api("GET", $"{GitHub.GistsUrl}/{_account.gistId}", null, Got);
            if (code == 404)
            {
                // Gist weggehaald: volgende keer opnieuw aanmaken.
                _account.gistId = "";
                StoreAccount();
                _busy = false;
                SyncNow();
                yield break;
            }
            if (!Check(error, code)) yield break;

            string remoteJson;
            try { remoteJson = GitHub.ReadProfile(body); }
            catch (FormatException e) { Fail(e.Message); yield break; }
            var remote = remoteJson != null ? ProfileStore.FromJson(remoteJson) : null;
            var merged = ProfileMerge.Merge(ProfileStore.Current, remote);
            string mergedJson = ProfileStore.ToJson(merged);
            if (mergedJson != local)
            {
                _applying = true;
                ProfileStore.Replace(merged);
                _applying = false;
            }
            if (mergedJson != remoteJson)
            {
                yield return Api("PATCH", $"{GitHub.GistsUrl}/{_account.gistId}", GitHub.UpdateGistBody(mergedJson), Got);
                if (!Check(error, code)) yield break;
            }
            Done(mergedJson != local ? "profiel van een ander apparaat binnengehaald" : "gesynchroniseerd");
        }

        bool Check(string error, long code)
        {
            if (code == 401)
            {
                // Token ingetrokken (bijvoorbeeld op github.com): opnieuw inloggen.
                SignOut();
                Fail("GitHub accepteert de inlog niet meer, log opnieuw in");
                return false;
            }
            if (error == null) return true;
            Fail($"synchroniseren mislukt: {error}");
            return false;
        }

        void Done(string message)
        {
            _busy = false;
            LastSync = DateTime.Now;
            Status = $"ingelogd als {_account.login} · {message} ({LastSync:HH:mm})";
        }

        void Fail(string message)
        {
            _busy = false;
            Status = message;
            Debug.LogWarning($"GitHub-account: {message}");
        }

        void StoreAccount()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(_account)); }
            catch (IOException e) { Debug.LogError($"Account opslaan mislukt: {e.Message}"); }
        }

        // ---- HTTP ----

        static IEnumerator Post(string url, string form, Action<string, string, long> done)
        {
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(form));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                done(request.downloadHandler.text, ok ? null : request.error, request.responseCode);
            }
        }

        IEnumerator Api(string method, string url, string json, Action<string, string, long> done)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + _account.token);
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                if (json != null) request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                done(request.downloadHandler.text, ok ? null : request.error, request.responseCode);
            }
        }
    }
}
