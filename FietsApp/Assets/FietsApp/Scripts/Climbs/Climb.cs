using FietsApp.World;

namespace FietsApp.Climbs
{
    /// <summary>Grond boven de boomgrens: kale kalksteen (zoals de top van de Ventoux), gras of donker alpengras.</summary>
    public enum Ground { Bare, Grass, DarkGrass }

    /// <summary>
    /// Alles wat één klim eigen is (stories 4.4 en 4.15): de weg, het landschap, de begroeiing en een eventueel
    /// herkenningspunt op de top. Komt uit een klimpakket (<see cref="ClimbPackage"/>), niet uit een Unity-asset.
    /// </summary>
    public sealed class Climb
    {
        /// <summary>Zonder boomgrens of bos: hoger dan elke berg.</summary>
        public const float None = 100000f;

        /// <summary>Vaste sleutel (bestandsnaam), bijv. "MontVentoux_Bedoin"; ghosts en de wereldkaart gebruiken hem.</summary>
        public string Id;
        /// <summary>Naam zoals de rijder hem ziet, bijv. "Mont Ventoux (Bédoin)"; records en badges staan hieronder.</summary>
        public string Name;
        /// <summary>Gebied voor het filter in het ritmenu, bijv. "Alpen" of "Pyreneeën".</summary>
        public string Region;
        /// <summary>Land van de voet, voor zoeken in het ritmenu (story 4.6).</summary>
        public string Country;
        /// <summary>Versie van het pakket; hoger = nieuwer (story 4.16).</summary>
        public int Version;

        // Kerncijfers, uit de GPX berekend door tools/climb_package.py, zodat het ritmenu niet elke klim hoeft in te lezen.
        public float LengthMeters;
        public float AverageGradePercent;
        public float ElevationGain;

        /// <summary>Boomgrens in meter boven zeeniveau; erboven geen bomen.</summary>
        public float TreeLine = None;
        /// <summary>Vanaf deze hoogte (tot de boomgrens) bosbodem in plaats van gras.</summary>
        public float ForestFrom = None;
        public Ground AboveTreeLine = Ground.Bare;
        /// <summary>Grindberm boven de boomgrens, zoals op de kale top van de Ventoux.</summary>
        public bool GravelVergeAboveTreeLine;

        /// <summary>Stukken met grind, kasseien of sneeuw, in meter vanaf de voet (story 4.13). De rest is asfalt.</summary>
        public SurfaceSection[] Surfaces = new SurfaceSection[0];

        /// <summary>Zendmast op de top, zoals op de Mont Ventoux.</summary>
        public bool SummitTower;
        public double SummitLatitude;
        public double SummitLongitude;

        public string Source;
        public string License;

        /// <summary>De GPX-tekst van de weg.</summary>
        public string Gpx;
        /// <summary>Echt landschap rond de klim (tools/terrain_from_tiles.py), of null: dan verzonnen heuvels.</summary>
        public byte[] Terrain;

        /// <summary>De ondergrond op <paramref name="distance"/> m van de voet.</summary>
        public Surface SurfaceAt(float distance) => World.Surfaces.At(Surfaces, distance);

        public override string ToString() => Id;
    }
}
