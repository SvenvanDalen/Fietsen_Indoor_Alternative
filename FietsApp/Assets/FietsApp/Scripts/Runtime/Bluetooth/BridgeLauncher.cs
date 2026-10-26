using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Start de meegeleverde FietsBridge (<c>Bridge\FietsBridge.exe</c> naast de app) en stopt hem weer bij afsluiten (story 13.6).
    /// Draait er al een brug, bijvoorbeeld via <c>dotnet run</c>, dan blijft die gewoon in gebruik.
    /// </summary>
    public sealed class BridgeLauncher : MonoBehaviour
    {
        const string ProcessName = "FietsBridge";

        Process _bridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Bridge");
            string exe = Path.Combine(dir, ProcessName + ".exe");
            if (!File.Exists(exe) || Process.GetProcessesByName(ProcessName).Length > 0) return;

            try
            {
                var bridge = Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = dir,
                });
                var go = new GameObject("BridgeLauncher");
                DontDestroyOnLoad(go);
                go.AddComponent<BridgeLauncher>()._bridge = bridge;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Brug] Starten mislukt: " + e.Message);
            }
        }

        void OnApplicationQuit()
        {
            try
            {
                if (_bridge != null && !_bridge.HasExited) _bridge.Kill();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Brug] Stoppen mislukt: " + e.Message);
            }
        }
    }
}
