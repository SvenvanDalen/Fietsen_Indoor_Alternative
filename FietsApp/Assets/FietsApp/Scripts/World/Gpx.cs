using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace FietsApp.World
{
    public readonly struct GeoPoint
    {
        public readonly double Latitude, Longitude, Elevation;

        public GeoPoint(double latitude, double longitude, double elevation)
        {
            Latitude = latitude;
            Longitude = longitude;
            Elevation = elevation;
        }
    }

    /// <summary>Leest de punten van een GPX-bestand (1.0 of 1.1, track- of routepunten). Hoogte is verplicht.</summary>
    public static class Gpx
    {
        public static List<GeoPoint> Parse(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var points = new List<GeoPoint>();
            // Namespace negeren: GPX 1.0 en 1.1 hebben elk een eigen namespace.
            var nodes = doc.SelectNodes("//*[local-name()='trkpt' or local-name()='rtept']");
            foreach (XmlNode node in nodes)
            {
                var ele = node.SelectSingleNode("*[local-name()='ele']");
                if (ele == null) throw new FormatException("GPX-punt zonder hoogte (<ele>); een klim heeft hoogtedata nodig.");
                points.Add(new GeoPoint(
                    Number(node.Attributes["lat"].Value),
                    Number(node.Attributes["lon"].Value),
                    Number(ele.InnerText)));
            }
            if (points.Count < 2) throw new FormatException("GPX bevat minder dan twee punten.");
            return points;
        }

        static double Number(string text) => double.Parse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
