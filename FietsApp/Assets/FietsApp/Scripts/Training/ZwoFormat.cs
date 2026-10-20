using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace FietsApp.Training
{
    /// <summary>
    /// Workouts als <c>.zwo</c> (story 8.3): het XML-formaat van Zwift, dat ook Intervals.icu, TrainerRoad en andere apps
    /// lezen en schrijven. Zo deel je een eigen workout als bestand en haal je er een van een ander binnen.
    /// Vermogens zijn een deel van de FTP; bij Warmup, Cooldown en Ramp is PowerLow het begin en PowerHigh het eind.
    /// De naam van een blok gaat mee als tekstmelding aan het begin van het blok (textevent), zoals Zwift die ook toont.
    /// </summary>
    public static class ZwoFormat
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Write(Workout workout)
        {
            var steps = new XElement("workout");
            for (int i = 0; i < workout.Steps.Count; i++)
            {
                var s = workout.Steps[i];
                XElement e;
                if (!s.IsRamp)
                    e = new XElement("SteadyState", Attr("Duration", s.Seconds), Attr("Power", s.StartFtp));
                else
                {
                    // Zwift kent opwarmen en uitrijden als eigen soort; elders in de workout is het een Ramp.
                    string kind = i == 0 && s.EndFtp > s.StartFtp ? "Warmup"
                        : i == workout.Steps.Count - 1 && s.EndFtp < s.StartFtp ? "Cooldown"
                        : "Ramp";
                    e = new XElement(kind, Attr("Duration", s.Seconds), Attr("PowerLow", s.StartFtp), Attr("PowerHigh", s.EndFtp));
                }
                if (!string.IsNullOrEmpty(s.Name))
                    e.Add(new XElement("textevent", new XAttribute("timeoffset", "0"), new XAttribute("message", s.Name)));
                steps.Add(e);
            }
            var doc = new XElement("workout_file",
                new XElement("author", "FietsApp"),
                new XElement("name", workout.Name),
                new XElement("description", workout.Description ?? ""),
                new XElement("sportType", "bike"),
                new XElement("tags"),
                steps);
            return doc.ToString();
        }

        /// <summary>Leest een .zwo; gooit <see cref="FormatException"/> als het geen bruikbare fietsworkout is.</summary>
        public static Workout Parse(string xml)
        {
            XElement root;
            try { root = XElement.Parse(xml); }
            catch (System.Xml.XmlException e) { throw new FormatException("Geen geldig .zwo-bestand: " + e.Message, e); }
            if (root.Name.LocalName != "workout_file") throw new FormatException("Geen .zwo-bestand (workout_file ontbreekt).");

            var steps = new List<WorkoutStep>();
            foreach (var e in root.Element("workout")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                double seconds = Number(e, "Duration", 0);
                string name = e.Elements("textevent").FirstOrDefault(t => Number(t, "timeoffset", 0) == 0)?.Attribute("message")?.Value;
                switch (e.Name.LocalName)
                {
                    case "SteadyState":
                        double power = e.Attribute("Power") != null ? Number(e, "Power", 0.5)
                            : (Number(e, "PowerLow", 0.5) + Number(e, "PowerHigh", 0.5)) / 2;
                        Add(steps, name ?? StepName(power), seconds, power, power);
                        break;
                    case "Warmup":
                    case "Cooldown":
                    case "Ramp":
                        double low = Number(e, "PowerLow", 0.5), high = Number(e, "PowerHigh", 0.5);
                        string fallback = e.Name.LocalName == "Warmup" ? "Opwarmen" : e.Name.LocalName == "Cooldown" ? "Uitrijden" : "Ramp";
                        Add(steps, name ?? fallback, seconds, low, high);
                        break;
                    case "IntervalsT":
                        int repeat = (int)Number(e, "Repeat", 1);
                        double onPower = Number(e, "OnPower", 1.0), offPower = Number(e, "OffPower", 0.5);
                        for (int r = 0; r < repeat; r++)
                        {
                            Add(steps, name ?? "Interval", Number(e, "OnDuration", 60), onPower, onPower);
                            Add(steps, "Herstel", Number(e, "OffDuration", 60), offPower, offPower);
                        }
                        break;
                    case "FreeRide":
                        // Vrij rijden heeft geen doel; in ERG is dat rustig trappen.
                        Add(steps, name ?? "Vrij rijden", seconds, 0.5, 0.5);
                        break;
                }
            }
            if (steps.Count == 0) throw new FormatException("De workout heeft geen blokken die FietsApp kent.");
            string title = root.Element("name")?.Value?.Trim();
            return new Workout(string.IsNullOrEmpty(title) ? "Workout" : title, root.Element("description")?.Value?.Trim() ?? "", steps);
        }

        /// <summary>Naam voor een blok zonder naam: de zone van het doel, zoals "Drempel".</summary>
        public static string StepName(double ftp) => PowerZones.Of(ftp, 1.0).Name;

        static void Add(List<WorkoutStep> steps, string name, double seconds, double start, double end)
        {
            if (seconds > 0) steps.Add(new WorkoutStep(name, seconds, start, end));
        }

        static XAttribute Attr(string name, double value) => new XAttribute(name, value.ToString("0.###", Invariant));

        static double Number(XElement e, string attribute, double fallback) =>
            double.TryParse(e.Attribute(attribute)?.Value, NumberStyles.Float, Invariant, out double v) ? v : fallback;
    }
}
