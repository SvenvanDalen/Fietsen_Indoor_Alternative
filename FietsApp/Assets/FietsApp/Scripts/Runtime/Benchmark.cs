using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Prestatietest (story 3.1e): start de speler met <c>-benchmark</c> (en eventueel <c>-benchmark-climb AlpeDHuez</c>)
    /// en hij rijdt vanzelf vijf stukken van de klim, meet de beeldjes per seconde zonder vsync en sluit af. De uitslag
    /// staat in het log als <c>[Benchmark]</c>. Er wordt niets opgeslagen of naar Strava gestuurd.
    /// </summary>
    public sealed class Benchmark : MonoBehaviour
    {
        const float WarmUpSeconds = 2f, MeasureSeconds = 10f, PowerWatts = 350f;
        static readonly float[] Spots = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-benchmark") < 0) return;
            if (FindAnyObjectByType<RideController>() == null) return;
            new GameObject("Benchmark").AddComponent<Benchmark>();
        }

        static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        IEnumerator Start()
        {
            yield return null; // de andere bootstraps (startscherm, Strava) eerst
            foreach (var recorder in FindObjectsByType<RideRecorder>(FindObjectsInactive.Include)) Destroy(recorder);
            foreach (var strava in FindObjectsByType<StravaAccount>(FindObjectsInactive.Include)) Destroy(strava);
            var home = FindAnyObjectByType<HomeScreen>();
            if (home != null && home.IsOpen) home.Close();

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var ride = FindAnyObjectByType<RideController>();
            var route = FindAnyObjectByType<RouteView>();
            var menu = FindAnyObjectByType<RideMenu>();
            string id = Argument("-benchmark-climb", "AlpeDHuez");
            var climb = menu != null ? menu.Climbs.FirstOrDefault(c => c.name == id) : null;
            if (climb == null)
            {
                Debug.LogError($"[Benchmark] klim {id} niet gevonden");
                Application.Quit(1);
                yield break;
            }
            menu.Ride(climb);
            yield return null;

            float climbStart = ClimbStart(route);
            float length = route.ClimbRoute.Length;
            ride.Simulator.TargetPowerWatts = PowerWatts;

            var all = new List<float>();
            foreach (float spot in Spots)
            {
                ride.StartAt(climbStart + spot * length);
                for (float t = 0f; t < WarmUpSeconds; t += Time.unscaledDeltaTime) yield return null;
                var frames = new List<float>();
                for (float t = 0f; t < MeasureSeconds; t += Time.unscaledDeltaTime)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime);
                }
                Debug.Log($"[Benchmark] {spot * 100f:0}% van de klim: {Report(frames)}");
                all.AddRange(frames);
            }

            Debug.Log($"[Benchmark] {climb.displayName}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, " +
                      $"kwaliteit {QualitySettings.names[QualitySettings.GetQualityLevel()]}: {Report(all)}");
            Application.Quit();
        }

        /// <summary>Afstand langs de rit waar de klim zelf begint (na de dalweg vanaf het knooppunt).</summary>
        static float ClimbStart(RouteView route)
        {
            for (float d = 0f; d < route.Route.Length; d += 5f)
            {
                var onClimb = route.ClimbDistanceAt(d);
                if (onClimb.HasValue && onClimb.Value >= 0f) return d - onClimb.Value;
            }
            return 0f;
        }

        /// <summary>Gemiddelde fps, de 1%-traagste beeldjes als fps, en het traagste beeldje.</summary>
        static string Report(List<float> frames)
        {
            var sorted = frames.OrderByDescending(f => f).ToList();
            int slowest = Mathf.Max(1, sorted.Count / 100);
            float average = frames.Count / frames.Sum();
            float low = slowest / sorted.Take(slowest).Sum();
            return $"gemiddeld {average:0} fps, 1% laag {low:0} fps, traagste {sorted[0] * 1000f:0} ms ({frames.Count} beeldjes)";
        }
    }
}
