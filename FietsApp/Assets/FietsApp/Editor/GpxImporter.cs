using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FietsApp.Editor
{
    /// <summary>
    /// Laat Unity .gpx-bestanden importeren als <see cref="TextAsset"/>, zodat een klim als bestand
    /// aan <see cref="RouteView"/> gekoppeld kan worden. Unity kent de extensie zelf niet.
    /// </summary>
    [ScriptedImporter(1, "gpx")]
    public sealed class GpxImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("gpx", text);
            ctx.SetMainObject(text);
        }
    }
}
