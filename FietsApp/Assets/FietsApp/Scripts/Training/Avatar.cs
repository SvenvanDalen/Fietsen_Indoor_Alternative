using System;

namespace FietsApp.Training
{
    public enum BikeKind { Allround, Climbing, Aero, Gravel, Classic }

    /// <summary>
    /// Een fiets uit de keuzelijst (story 7.3). Het gewicht telt mee in de fysica, net als het verschil in luchtweerstand
    /// (CdA) ten opzichte van een gewone racefiets: een aerofiets met diepe velgen scheelt zo'n 0,01 m², brede
    /// gravelbanden en een rechtop zittende rijder op een stalen fiets kosten er wat bij.
    /// </summary>
    public sealed class Bike
    {
        public readonly BikeKind Kind;
        public readonly string Name;
        public readonly string Description;
        public readonly float MassKg;
        public readonly float DragAreaDeltaM2;

        public Bike(BikeKind kind, string name, string description, float massKg, float dragAreaDeltaM2)
        {
            Kind = kind;
            Name = name;
            Description = description;
            MassKg = massKg;
            DragAreaDeltaM2 = dragAreaDeltaM2;
        }

        public static readonly Bike[] All =
        {
            new Bike(BikeKind.Allround, "Racefiets", "carbon allrounder, 8,0 kg", 8.0f, 0f),
            new Bike(BikeKind.Climbing, "Klimfiets", "dunne buizen, ondiepe velgen, 6,8 kg", 6.8f, 0.004f),
            new Bike(BikeKind.Aero, "Aerofiets", "platte buizen, diepe velgen, 7,6 kg", 7.6f, -0.012f),
            new Bike(BikeKind.Gravel, "Gravelfiets", "brede banden, uitlopend stuur, 9,2 kg", 9.2f, 0.010f),
            new Bike(BikeKind.Classic, "Stalen klassieker", "dunne stalen buizen, 10,5 kg", 10.5f, 0.015f),
        };

        public static Bike Of(BikeKind kind)
        {
            foreach (var bike in All)
                if (bike.Kind == kind) return bike;
            return All[0];
        }
    }

    /// <summary>
    /// Hoe de rijder eruitziet en op welke fiets hij zit (story 7.3). Kleuren zijn een index in een vaste palet, zodat
    /// het profiel klein blijft en elke combinatie er goed uitziet. Onderdeel van het profiel, dus gaat mee naar de Gist.
    /// </summary>
    [Serializable]
    public sealed class AvatarSettings
    {
        /// <summary>Shirt-, helm- en framekleuren (hex, sRGB).</summary>
        public static readonly string[] Colors =
        {
            "1F6FD1", // blauw
            "D7262E", // rood
            "F2C318", // geel
            "1E9A4B", // groen
            "F07A1A", // oranje
            "E8E8E8", // wit
            "1B1B1F", // zwart
            "7B3FB8", // paars
            "E0408A", // roze
            "1FB5B0", // turquoise
        };

        public static readonly string[] ColorNames =
            { "blauw", "rood", "geel", "groen", "oranje", "wit", "zwart", "paars", "roze", "turquoise" };

        /// <summary>Huidtinten, van licht naar donker.</summary>
        public static readonly string[] SkinTones = { "F3D2BC", "E2B391", "C68E62", "8D5A3B", "5A3825" };

        public int bike = (int)BikeKind.Allround;
        public int jersey;      // blauw
        public int accent = 5;  // wit: band over het shirt
        public int helmet = 5;  // wit
        public int frame = 6;   // zwart
        public int skin = 1;

        public BikeKind Bike => Enum.IsDefined(typeof(BikeKind), bike) ? (BikeKind)bike : BikeKind.Allround;

        public static int Wrap(int value, int count) => ((value % count) + count) % count;

        public string JerseyColor => Colors[Wrap(jersey, Colors.Length)];
        public string AccentColor => Colors[Wrap(accent, Colors.Length)];
        public string HelmetColor => Colors[Wrap(helmet, Colors.Length)];
        public string FrameColor => Colors[Wrap(frame, Colors.Length)];
        public string SkinColor => SkinTones[Wrap(skin, SkinTones.Length)];

        public AvatarSettings Copy() => (AvatarSettings)MemberwiseClone();
    }
}
