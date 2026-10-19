using System;
using System.Text;

namespace FietsApp.Recording
{
    /// <summary>De regels van de ritsamenvatting (story 5.3), los van de UI zodat ze te testen zijn.</summary>
    public static class RideSummaryText
    {
        /// <summary>Uren:minuten:seconden, zoals de HUD de rittijd toont.</summary>
        public static string Clock(double seconds)
        {
            int s = (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
            return $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
        }

        /// <param name="climbSeconds">Klimtijd van voet tot top, of null als de top niet gehaald is.</param>
        /// <param name="climbWatts">Gemiddeld vermogen op de klim.</param>
        public static string Of(RideLog log, double? climbSeconds, float climbWatts)
        {
            var sb = new StringBuilder();
            Line(sb, "Tijd", Clock(log.TimerSeconds));
            Line(sb, "Afstand", $"{log.DistanceM / 1000f:0.0} km");
            Line(sb, "Gemiddeld vermogen", $"{log.AveragePower:0} W  <color=#bbb>(max {log.MaxPower:0} W)</color>");
            Line(sb, "Klimtijd", climbSeconds.HasValue
                ? $"{Clock(climbSeconds.Value)}  <color=#bbb>(gemiddeld {climbWatts:0} W)</color>"
                : "<color=#bbb>top niet gehaald</color>");
            Line(sb, "Hoogtemeters", $"{log.Ascent:0} m");
            if (log.AverageHeartRate > 0f) Line(sb, "Hartslag", $"{log.AverageHeartRate:0} bpm  <color=#bbb>(max {log.MaxHeartRate:0})</color>");
            return sb.ToString().TrimEnd('\n');
        }

        static void Line(StringBuilder sb, string label, string value) =>
            sb.Append("<color=#bbb>").Append(label).Append(":</color>  <b>").Append(value).Append("</b>\n");
    }
}
