using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Alles wat één klim eigen is (story 4.4): de weg, het landschap, de begroeiing en een eventueel
    /// herkenningspunt op de top. <see cref="RouteView"/>, <see cref="TerrainView"/> en <see cref="SummitTower"/>
    /// lezen hieruit, zodat een andere klim kiezen één veld is.
    /// </summary>
    [CreateAssetMenu(menuName = "FietsApp/Klim", fileName = "Klim")]
    public sealed class Climb : ScriptableObject
    {
        [Tooltip("Naam zoals de rijder hem ziet, bijv. \"Mont Ventoux (Bédoin)\".")]
        public string displayName;
        [Tooltip("GPX van de klim (via tools/route_from_osm.py).")]
        public TextAsset gpx;
        [Tooltip("Echt landschap rond de klim (.bytes via tools/terrain_from_tiles.py). Leeg = verzonnen heuvels.")]
        public TextAsset terrain;
        [Tooltip("Gebied voor het filter in het ritmenu, bijv. \"Alpen\" of \"Pyreneeën\".")]
        public string region;
        [Tooltip("Land van de voet, voor zoeken in het ritmenu (story 4.6).")]
        public string country;

        [Header("Kerncijfers (FietsApp > Klimmen bijwerken)")]
        [Tooltip("Uit de GPX berekend en hier bewaard, zodat het ritmenu niet elke klim hoeft in te lezen.")]
        public float lengthMeters;
        public float averageGradePercent;
        public float elevationGain;

        [Header("Begroeiing")]
        [Tooltip("Boomgrens in meter boven zeeniveau; erboven geen bomen.")]
        public float treeLine = 100000f;
        [Tooltip("Vanaf deze hoogte (tot de boomgrens) bosbodem in plaats van gras.")]
        public float forestFrom = 100000f;
        [Tooltip("Grond boven de boomgrens. Leeg = de kale grond van TerrainView (witte kalksteen); gras voor een alpenweide.")]
        public Material aboveTreeLine;
        [Tooltip("Grindberm boven de boomgrens, zoals op de kale top van de Ventoux.")]
        public bool gravelVergeAboveTreeLine = true;

        [Header("Ondergrond (story 4.13)")]
        [Tooltip("Stukken met grind, kasseien of sneeuw, in meter vanaf de voet (FietsApp > Klimmen bijwerken, uit tools/climbs.json). De rest is asfalt.")]
        public FietsApp.World.SurfaceSection[] surfaces = new FietsApp.World.SurfaceSection[0];

        /// <summary>De ondergrond op <paramref name="distance"/> m van de voet.</summary>
        public FietsApp.World.Surface SurfaceAt(float distance) => FietsApp.World.Surfaces.At(surfaces, distance);

        [Header("Herkenningspunt")]
        [Tooltip("Zendmast op de top, zoals op de Mont Ventoux.")]
        public bool summitTower;
        public double summitLatitude;
        public double summitLongitude;
    }
}
