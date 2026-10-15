using System;
using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>
    /// Bouwt een route uit stukken met een vaste helling, recht of met een bocht.
    /// Bedoeld voor test- en fantasieroutes; echte klimmen komen uit GPX.
    /// De route start in de oorsprong en rijdt langs +Z; een positieve bocht gaat naar rechts.
    /// </summary>
    public sealed class RouteBuilder
    {
        readonly float _spacing;
        readonly List<RoutePoint> _points = new List<RoutePoint>();
        double _x, _z, _elevation, _distance, _heading; // heading in radialen, 0 = +Z, positief = naar rechts (+X)

        public RouteBuilder(float spacing = 10f)
        {
            _spacing = spacing;
            _points.Add(new RoutePoint(0f, 0f, 0f, 0f));
        }

        public RouteBuilder Straight(float length, float gradePercent) => Curve(length, gradePercent, 0f);

        public RouteBuilder Curve(float length, float gradePercent, float turnDegrees)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(length / _spacing));
            double step = length / (double)steps;
            double turnPerStep = turnDegrees * Math.PI / 180.0 / steps;

            for (int i = 0; i < steps; i++)
            {
                // Halverwege de stap draaien geeft een nauwkeurige boog (middelpuntsregel).
                double mid = _heading + turnPerStep / 2.0;
                _x += Math.Sin(mid) * step;
                _z += Math.Cos(mid) * step;
                _heading += turnPerStep;
                _elevation += step * gradePercent / 100.0;
                _distance += step;
                _points.Add(new RoutePoint((float)_distance, (float)_x, (float)_z, (float)_elevation));
            }
            return this;
        }

        public Route Build() => new Route(_points);
    }

    public static class TestRoutes
    {
        /// <summary>
        /// Oefenklim van ruim 5 km: aanloop, oplopend naar 10% met haarspeldbochten, en een afdaling.
        /// Dekt het testprofiel uit sprint 1 (0 → 10% → 0).
        /// </summary>
        public static Route Testklim() => new RouteBuilder()
            .Straight(400f, 0f)
            .Curve(300f, 2f, 30f)
            .Straight(600f, 5f)
            .Curve(250f, 6f, -60f)
            .Straight(500f, 7f)
            .Curve(120f, 8f, 170f)   // haarspeld
            .Straight(400f, 8f)
            .Curve(120f, 9f, -170f)  // haarspeld
            .Straight(300f, 10f)
            .Curve(200f, 4f, 40f)
            .Straight(200f, 0f)      // top
            .Curve(600f, -6f, -50f)
            .Straight(800f, -4f)
            .Straight(400f, 0f)
            .Build();
    }
}
