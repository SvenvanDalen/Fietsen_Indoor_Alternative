using System;

namespace FietsApp.Training
{
    /// <summary>De getallen waarmee klimmers zich meten (story 6.5): W/kg en VAM.</summary>
    public static class RiderNumbers
    {
        /// <summary>Vermogen per kilo lichaamsgewicht (zonder fiets, zoals klimmers het noemen); 0 bij een onbekend gewicht.</summary>
        public static double WattsPerKg(double watts, double weightKg) => weightKg > 0 ? watts / weightKg : 0;

        /// <summary>VAM: gestegen meters per uur over een stuk van <paramref name="seconds"/> seconden; 0 als er geen tijd is.</summary>
        public static double Vam(double ascentM, double seconds) => seconds > 0 ? ascentM / seconds * 3600.0 : 0;
    }
}
