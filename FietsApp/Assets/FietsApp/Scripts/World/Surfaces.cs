using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Ondergrond van de weg (story 4.13).</summary>
    public enum Surface { Asphalt, Gravel, Cobbles, Snow }

    /// <summary>Een stuk van een klim met een andere ondergrond dan asfalt, in meters vanaf de voet.</summary>
    [Serializable]
    public sealed class SurfaceSection
    {
        public float from;
        public float to;
        public Surface surface;

        public bool Contains(float distance) => distance >= from && distance <= to;
    }

    /// <summary>Wat een ondergrond met de rit doet: rolweerstand, trilling en hoe hij heet.</summary>
    public static class Surfaces
    {
        /// <summary>
        /// Rolweerstandscoëfficiënt (Crr) met racebanden: glad asfalt 0,004, grind 0,010, kasseien 0,012, aangereden
        /// sneeuw 0,020. Gangbare waarden uit metingen en fietsrekenmodellen; zie docs/decisions.md.
        /// </summary>
        public static float RollingResistance(Surface surface) => surface switch
        {
            Surface.Gravel => 0.010f,
            Surface.Cobbles => 0.012f,
            Surface.Snow => 0.020f,
            _ => 0.004f,
        };

        /// <summary>Hoe hard het beeld trilt (m camerabeweging bij 30 km/u).</summary>
        public static float Shake(Surface surface) => surface switch
        {
            Surface.Gravel => 0.012f,
            Surface.Cobbles => 0.035f,
            Surface.Snow => 0.006f,
            _ => 0f,
        };

        public static string Label(Surface surface) => surface switch
        {
            Surface.Gravel => "grind",
            Surface.Cobbles => "kasseien",
            Surface.Snow => "sneeuw",
            _ => "asfalt",
        };

        /// <summary>
        /// Extra helling (procentpunt) die de trainer laat voelen wat de extra rolweerstand kost ten opzichte van asfalt:
        /// rolweerstand is Crr·m·g en helling ongeveer (helling/100)·m·g, dus ΔCrr·100.
        /// </summary>
        public static float TrainerGradeBonus(Surface surface) =>
            (RollingResistance(surface) - RollingResistance(Surface.Asphalt)) * 100f;

        /// <summary>De ondergrond op <paramref name="distance"/> m van de voet; zonder stuk is het asfalt.</summary>
        public static Surface At(IReadOnlyList<SurfaceSection> sections, float distance)
        {
            if (sections == null) return Surface.Asphalt;
            foreach (var s in sections)
                if (s != null && s.Contains(distance)) return s.surface;
            return Surface.Asphalt;
        }

        /// <summary>Uit de tekst in tools/climbs.json ("gravel", "cobbles", "snow"); onbekend is asfalt.</summary>
        public static Surface Parse(string name) => name switch
        {
            "gravel" => Surface.Gravel,
            "cobbles" => Surface.Cobbles,
            "snow" => Surface.Snow,
            _ => Surface.Asphalt,
        };
    }
}
