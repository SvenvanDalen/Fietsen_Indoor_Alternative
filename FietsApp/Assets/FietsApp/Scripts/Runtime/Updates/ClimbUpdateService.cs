using System;
using System.Collections;
using System.Linq;
using System.Threading;
using FietsApp.Climbs;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Haalt bij het opstarten nieuwe en gewijzigde klimmen op uit de online catalogus (story 4.16), op de achtergrond.
    /// Wat binnenkomt telt mee vanaf de volgende start, zodat de wereld en het ritmenu niet onder een rit veranderen.
    /// Alleen in de speler (niet in de Editor) en niet met <c>--no-update</c>.
    /// </summary>
    public sealed class ClimbUpdateService : MonoBehaviour
    {
        const float NoticeSeconds = 12f;

        public static ClimbUpdateService Instance { get; private set; }

        /// <summary>Uitkomst van de laatste controle; null zolang die loopt.</summary>
        public ClimbSyncResult Result { get; private set; }

        CancellationTokenSource _cts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--no-update") >= 0) return;
            var go = new GameObject("ClimbUpdateService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ClimbUpdateService>();
            Instance.Check();
        }

        async void Check()
        {
            _cts = new CancellationTokenSource();
            var sync = new ClimbSync(new UnityDownloader(), ClimbCatalog.BaseUrl, ClimbLibrary.DownloadFolder, ClimbLibrary.Shelf.VersionOf);
            try
            {
                // Op de hoofdthread (UnityWebRequest); de pakketten zijn klein genoeg om hier te controleren.
                Result = await sync.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Result = new ClimbSyncResult { Error = e.Message };
            }
            if (this == null) return;

            if (Result.Error != null) Debug.Log("[Klimmen] Catalogus niet opgehaald (de bestaande klimmen blijven werken): " + Result.Error);
            foreach (var name in Result.Downloaded) Debug.Log("[Klimmen] Gedownload: " + name);
            foreach (var skipped in Result.Skipped) Debug.LogWarning("[Klimmen] " + skipped);
            string notice = Notice(Result);
            if (notice != null) StartCoroutine(Show(notice));
        }

        /// <summary>Tekst voor de rijder, of null als er niets te melden is.</summary>
        static string Notice(ClimbSyncResult result)
        {
            string text = null;
            if (result.Downloaded.Count > 0)
            {
                var names = result.Downloaded.Take(3).ToList();
                if (result.Downloaded.Count > 3) names.Add($"en {result.Downloaded.Count - 3} meer");
                text = $"Nieuwe klimmen binnen: {string.Join(", ", names)}. Ze staan in het ritmenu na herstarten.";
            }
            int newer = result.NewerFormat;
            if (newer > 0)
                text = (text != null ? text + "\n" : "") +
                       $"{newer} {(newer == 1 ? "klim vraagt" : "klimmen vragen")} een nieuwere versie van de app en {(newer == 1 ? "is" : "zijn")} overgeslagen.";
            return text;
        }

        IEnumerator Show(string text)
        {
            var canvas = UiFactory.Canvas(transform, "KlimmenCanvas", 1000);
            var panel = UiFactory.Panel(canvas, "KlimmenMelding", new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(560f, 84f));
            var label = UiFactory.Text(panel, "Tekst", 17, -8f, 70f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = text;
            yield return new WaitForSecondsRealtime(NoticeSeconds);
            Destroy(canvas.gameObject);
        }

        void OnApplicationQuit() => _cts?.Cancel();
    }
}
