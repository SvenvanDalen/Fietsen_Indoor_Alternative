using System.IO;
using UnityEditor;
using UnityEngine;

namespace FietsApp.Editor
{
    /// <summary>
    /// Vaste opnames van de wereld vanuit de achtervolgcamera, om grafische wijzigingen voor en na te vergelijken.
    /// Werkt in de bewerkmodus: de wereld wordt daar ook opgebouwd.
    /// </summary>
    public static class WorldShots
    {
        /// <summary>Plekken op de route als deel van de lengte: dal, halverwege, boven de boomgrens, onderaan.</summary>
        public static readonly float[] DefaultFractions = { 0.15f, 0.55f, 0.75f, 0.9f };

        /// <summary>Schrijft <c>{prefix}{i}.png</c> in <paramref name="folder"/>, één per plek.</summary>
        public static void Capture(string folder, string prefix, float[] fractions = null, int width = 1600, int height = 900)
        {
            var camera = Camera.main;
            var route = Object.FindAnyObjectByType<RouteView>().Route;
            camera.transform.GetPositionAndRotation(out var oldPosition, out var oldRotation);
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            Directory.CreateDirectory(folder);
            try
            {
                fractions ??= DefaultFractions;
                for (int i = 0; i < fractions.Length; i++)
                {
                    var sample = route.Sample(route.Length * fractions[i]);
                    var forward = new Vector3(sample.HeadingX, 0f, sample.HeadingZ).normalized;
                    var position = new Vector3(sample.X, sample.Elevation, sample.Z) - forward * 7f + Vector3.up * 2.6f;
                    camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward + Vector3.down * 0.08f));
                    camera.targetTexture = target;
                    // De eerste render na een verplaatsing gebruikt nog de oude cameraplek voor het wegsnijden; daarom twee keer.
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"{prefix}{i}.png"), image.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
