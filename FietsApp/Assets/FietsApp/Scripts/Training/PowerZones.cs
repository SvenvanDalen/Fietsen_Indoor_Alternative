namespace FietsApp.Training
{
    /// <summary>Een vermogenszone: nummer (1-7), naam en kleur als 0xRRGGBB.</summary>
    public readonly struct PowerZone
    {
        public readonly int Number;
        public readonly string Name;
        public readonly uint Rgb;

        public PowerZone(int number, string name, uint rgb)
        {
            Number = number;
            Name = name;
            Rgb = rgb;
        }

        public string Label => $"Z{Number} {Name}";
    }

    /// <summary>
    /// De zeven vermogenszones van Coggan (story 8.5), als deel van de FTP. Bovengrenzen: 55, 75, 90, 105, 120
    /// en 150%; daarboven zone 7. Kleuren zoals in de meeste trainingsapps: grijs, blauw, groen, geel, oranje,
    /// rood, paars.
    /// </summary>
    public static class PowerZones
    {
        static readonly double[] UpperBounds = { 0.55, 0.75, 0.90, 1.05, 1.20, 1.50 };

        public static readonly PowerZone[] All =
        {
            new PowerZone(1, "Herstel", 0x9E9E9E),
            new PowerZone(2, "Duur", 0x3D8BFF),
            new PowerZone(3, "Tempo", 0x3CC35A),
            new PowerZone(4, "Drempel", 0xF5C400),
            new PowerZone(5, "VO2max", 0xFF8A1F),
            new PowerZone(6, "Anaeroob", 0xF03C3C),
            new PowerZone(7, "Sprint", 0xA64DFF),
        };

        /// <summary>Zone bij dit vermogen; een grens zelf hoort bij de lagere zone (55% FTP is nog zone 1).</summary>
        public static PowerZone Of(double watts, double ftpWatts)
        {
            if (ftpWatts <= 0) return All[0];
            double ratio = watts / ftpWatts;
            for (int i = 0; i < UpperBounds.Length; i++)
                if (ratio <= UpperBounds[i]) return All[i];
            return All[All.Length - 1];
        }

        /// <summary>Ondergrens in watt van zone <paramref name="number"/> (1-7) bij deze FTP.</summary>
        public static double LowerWatts(int number, double ftpWatts) =>
            number <= 1 ? 0 : UpperBounds[number - 2] * ftpWatts;
    }
}
