using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Kilometerbord (story 3.4): nog <see cref="KmToTop"/> km tot de top, met de helling van de km erna.</summary>
    public readonly struct KilometerMarker
    {
        public readonly float Distance;
        public readonly int KmToTop;
        public readonly float NextKmGradePercent;

        public KilometerMarker(float distance, int kmToTop, float nextKmGradePercent)
        {
            Distance = distance;
            KmToTop = kmToTop;
            NextKmGradePercent = nextKmGradePercent;
        }
    }

    /// <summary>Haarspeldbocht (story 3.4), genummerd aflopend naar de top zoals op Alpe d'Huez (21 onderaan, 1 bovenaan).</summary>
    public readonly struct Hairpin
    {
        public readonly float Distance;
        public readonly int Number;
        public readonly float TurnDegrees; // positief = linksom

        public Hairpin(float distance, int number, float turnDegrees)
        {
            Distance = distance;
            Number = number;
            TurnDegrees = turnDegrees;
        }
    }

    /// <summary>Waar langs een klim kilometerborden en bochtnummers komen (story 3.4).</summary>
    public static class RouteMarkers
    {
        const float Step = 10f;

        /// <summary>
        /// Een bord op elke hele kilometer voor de top, zoals de bornes in de Franse Alpen: "nog 5 km" met de
        /// gemiddelde helling van de kilometer die volgt.
        /// </summary>
        public static List<KilometerMarker> Kilometers(Route route)
        {
            var markers = new List<KilometerMarker>();
            float length = route.Length;
            for (int km = (int)Math.Floor(length / 1000f); km >= 1; km--)
            {
                float d = length - km * 1000f;
                float grade = (route.Sample(d + 1000f).Elevation - route.Sample(d).Elevation) / 1000f * 100f;
                markers.Add(new KilometerMarker(d, km, grade));
            }
            return markers;
        }

        /// <summary>
        /// Haarspeldbochten: de rijrichting draait minstens <paramref name="minTurnDegrees"/> graden binnen
        /// <paramref name="windowMeters"/> m route. Per bocht één punt (het midden van de draai); bochten dichter dan
        /// <paramref name="minSpacing"/> m bij elkaar tellen als één. De standaardwaarden geven op Alpe d'Huez precies
        /// de 21 bochten en op de Stelvio vanaf Prato 46 van de 48 tornanti (de bovenste liggen soms dichter dan 80 m
        /// op elkaar).
        /// </summary>
        public static List<Hairpin> Hairpins(Route route, float minTurnDegrees = 120f, float windowMeters = 80f,
            float minSpacing = 80f)
        {
            int n = (int)(route.Length / Step);
            var heading = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                var s = route.Sample(i * Step);
                heading[i] = Math.Atan2(s.HeadingZ, s.HeadingX);
            }

            // Draai tussen opeenvolgende punten, en het totaal binnen een venster rond elk punt.
            var delta = new double[n + 1];
            for (int i = 1; i <= n; i++) delta[i] = Wrap(heading[i] - heading[i - 1]);
            int half = (int)(windowMeters / 2f / Step);
            var turn = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double sum = 0;
                for (int j = Math.Max(1, i - half + 1); j <= Math.Min(n, i + half); j++) sum += delta[j];
                turn[i] = sum * 180.0 / Math.PI;
            }

            // Pieken boven de drempel; van twee pieken te dicht bij elkaar blijft de grootste.
            var found = new List<(float distance, float turn)>();
            for (int i = 0; i <= n; i++)
            {
                double t = Math.Abs(turn[i]);
                if (t < minTurnDegrees) continue;
                bool peak = (i == 0 || t >= Math.Abs(turn[i - 1])) && (i == n || t > Math.Abs(turn[i + 1]));
                if (!peak) continue;
                float d = i * Step;
                if (found.Count > 0 && d - found[found.Count - 1].distance < minSpacing)
                {
                    if (t > Math.Abs(found[found.Count - 1].turn)) found[found.Count - 1] = (d, (float)turn[i]);
                    continue;
                }
                found.Add((d, (float)turn[i]));
            }

            var hairpins = new List<Hairpin>(found.Count);
            for (int k = 0; k < found.Count; k++)
                hairpins.Add(new Hairpin(found[k].distance, found.Count - k, found[k].turn));
            return hairpins;
        }

        static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
