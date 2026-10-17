using System;

namespace FietsApp.World
{
    /// <summary>Instellingen voor <see cref="CornerSpeeds"/>.</summary>
    public sealed class CornerSettings
    {
        /// <summary>Dwarsversnelling die een rijder in een bocht aandurft (m/s²); 5 is zo'n 27° schuin hangen.</summary>
        public float LateralAcceleration = 5f;
        /// <summary>Vertraging waarmee vóór een bocht wordt afgeremd (m/s²); ruim onder het maximum van de rem, zodat het ook op steile afdalingen lukt.</summary>
        public float BrakeDeceleration = 2.5f;
        /// <summary>Halve lengte van het stuk weg waarover de bocht gemeten wordt (m); dempt ruis in de GPX.</summary>
        public float Window = 10f;
        /// <summary>Afstand tussen de rekenpunten (m).</summary>
        public float Step = 5f;
        /// <summary>Snelheid op rechte stukken (m/s): geen grens van betekenis.</summary>
        public float MaxSpeed = 40f;
    }

    /// <summary>
    /// Hoe hard je door de bochten van een route kunt (story 4.12): per plek de bochtstraal, de snelheid die de bocht
    /// toelaat (v = √(a·r)) en, door terug te rekenen, de snelheid waarop je al vóór de bocht moet zitten om hem
    /// met <see cref="CornerSettings.BrakeDeceleration"/> te halen. Zo remt de rijder in een afdaling vanzelf af
    /// voor een haarspeldbocht en trapt hij erna weer vrij door.
    /// </summary>
    public sealed class CornerSpeeds
    {
        readonly float[] _curvature; // 1/m; positief = bocht naar links
        readonly float[] _limit;     // m/s
        readonly float _step;

        public CornerSettings Settings { get; }

        public CornerSpeeds(Route route, CornerSettings settings = null)
        {
            Settings = settings ?? new CornerSettings();
            _step = Settings.Step;
            int n = (int)Math.Ceiling(route.Length / _step) + 1;
            _curvature = new float[n];
            _limit = new float[n];

            for (int i = 0; i < n; i++)
            {
                float d = Math.Min(i * _step, route.Length);
                _curvature[i] = MeasureCurvature(route, d, Settings.Window);
                float k = Math.Abs(_curvature[i]);
                _limit[i] = k > 1e-6f ? Math.Min(Settings.MaxSpeed, (float)Math.Sqrt(Settings.LateralAcceleration / k)) : Settings.MaxSpeed;
            }

            // Terugrekenen: vóór een bocht mag je niet harder dan wat je met de rem nog op tijd kwijtraakt.
            for (int i = n - 2; i >= 0; i--)
            {
                float next = _limit[i + 1];
                float reachable = (float)Math.Sqrt(next * next + 2f * Settings.BrakeDeceleration * _step);
                if (reachable < _limit[i]) _limit[i] = reachable;
            }
        }

        /// <summary>Kromming (1/m) op een afstand; positief = bocht naar links, 0 = rechtdoor.</summary>
        public float CurvatureAt(float distance) => Interpolate(_curvature, distance);

        /// <summary>Bochtstraal (m) op een afstand; oneindig op een recht stuk.</summary>
        public float RadiusAt(float distance)
        {
            float k = Math.Abs(CurvatureAt(distance));
            return k > 1e-6f ? 1f / k : float.PositiveInfinity;
        }

        /// <summary>Hoogste veilige snelheid (m/s) op een afstand, inclusief afremmen voor een bocht verderop.</summary>
        public float SpeedLimitAt(float distance) => Interpolate(_limit, distance);

        float Interpolate(float[] values, float distance)
        {
            float f = Math.Max(0f, distance) / _step;
            int i = (int)f;
            if (i >= values.Length - 1) return values[values.Length - 1];
            float t = f - i;
            return values[i] + (values[i + 1] - values[i]) * t;
        }

        /// <summary>Richtingsverandering over [d − w, d + w] gedeeld door de afgelegde weg.</summary>
        static float MeasureCurvature(Route route, float d, float window)
        {
            float a = Math.Max(0f, d - window), b = Math.Min(route.Length, d + window);
            if (b - a < 1e-3f) return 0f;
            var before = route.Sample(a);
            var after = route.Sample(b);
            // Hoek van 'before' naar 'after'; rijrichting (x, z), links is een draai tegen de klok in van boven gezien.
            float cross = before.HeadingX * after.HeadingZ - before.HeadingZ * after.HeadingX;
            float dot = before.HeadingX * after.HeadingX + before.HeadingZ * after.HeadingZ;
            return (float)Math.Atan2(cross, dot) / (b - a);
        }
    }
}
