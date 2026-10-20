using System.Collections.Generic;
using System.Text;

namespace FietsApp.Recording
{
    /// <summary>Eén gereden klim van een etappe (story 4.7).</summary>
    public readonly struct StageLeg
    {
        public readonly string Name;
        public readonly RideLog Log;
        public readonly double? ClimbSeconds; // van voet tot top, null als de top niet gehaald is

        public StageLeg(string name, RideLog log, double? climbSeconds)
        {
            Name = name;
            Log = log;
            ClimbSeconds = climbSeconds;
        }
    }

    /// <summary>De samenvatting van een etappe: totalen over alle klimmen en de klimtijd per klim.</summary>
    public static class StageSummaryText
    {
        public static string Of(IReadOnlyList<StageLeg> legs)
        {
            double seconds = 0, work = 0;
            float distance = 0f, ascent = 0f;
            foreach (var leg in legs)
            {
                if (leg.Log == null || leg.Log.Count == 0) continue;
                seconds += leg.Log.TimerSeconds;
                work += leg.Log.AveragePower * leg.Log.TimerSeconds;
                distance += leg.Log.DistanceM;
                ascent += leg.Log.Ascent;
            }

            var sb = new StringBuilder();
            Line(sb, "Tijd", RideSummaryText.Clock(seconds));
            Line(sb, "Afstand", $"{distance / 1000f:0.0} km");
            Line(sb, "Gemiddeld vermogen", $"{(seconds > 0 ? work / seconds : 0):0} W");
            Line(sb, "Hoogtemeters", $"{ascent:0} m");
            for (int i = 0; i < legs.Count; i++)
            {
                var leg = legs[i];
                Line(sb, $"{i + 1}. {leg.Name}", leg.ClimbSeconds.HasValue
                    ? RideSummaryText.Clock(leg.ClimbSeconds.Value)
                    : "<color=#bbb>top niet gehaald</color>");
            }
            return sb.ToString().TrimEnd('\n');
        }

        static void Line(StringBuilder sb, string label, string value) => RideSummaryText.Line(sb, label, value);
    }
}
