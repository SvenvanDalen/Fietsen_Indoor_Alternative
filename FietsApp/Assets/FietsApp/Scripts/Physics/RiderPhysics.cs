using System;

namespace FietsApp.Physics
{
    /// <summary>Instelbare waarden voor rijder plus fiets.</summary>
    public struct RiderParameters
    {
        public float TotalMassKg;
        public float RollingResistance;   // Crr
        public float DragAreaM2;          // CdA
        public float AirDensity;          // rho, kg/m³

        public static RiderParameters Default => new RiderParameters
        {
            TotalMassKg = 80f,
            RollingResistance = 0.004f,
            DragAreaM2 = 0.32f,
            AirDensity = 1.225f,
        };
    }

    /// <summary>
    /// Fysicamodel uit backlog sectie 3:
    /// P = v · (m g sinθ + Crr m g cosθ + ½ ρ CdA v²).
    /// Hellingen in procent, snelheden in m/s.
    /// </summary>
    public sealed class RiderPhysics
    {
        public const float Gravity = 9.80665f;

        // Onder deze snelheid rekenen we niet met P/v (deelt door nul bij wegrijden).
        const float MinPropulsionSpeed = 1f;

        public RiderParameters Parameters { get; set; }

        public RiderPhysics(RiderParameters parameters)
        {
            // Velden die niet gezet zijn vallen terug op de standaard.
            var d = RiderParameters.Default;
            if (parameters.TotalMassKg <= 0f) parameters.TotalMassKg = d.TotalMassKg;
            if (parameters.RollingResistance <= 0f) parameters.RollingResistance = d.RollingResistance;
            if (parameters.DragAreaM2 <= 0f) parameters.DragAreaM2 = d.DragAreaM2;
            if (parameters.AirDensity <= 0f) parameters.AirDensity = d.AirDensity;
            Parameters = parameters;
        }

        /// <summary>Som van de weerstandskrachten (N) bij snelheid v op een helling.</summary>
        public float ResistanceForce(float speed, float gradePercent)
        {
            var p = Parameters;
            double theta = Math.Atan(gradePercent / 100.0);
            double weight = p.TotalMassKg * Gravity;
            double gravity = weight * Math.Sin(theta);
            double rolling = p.RollingResistance * weight * Math.Cos(theta);
            double aero = 0.5 * p.AirDensity * p.DragAreaM2 * speed * speed;
            return (float)(gravity + rolling + aero);
        }

        /// <summary>Vermogen (W) dat nodig is om snelheid v vast te houden.</summary>
        public float ResistancePower(float speed, float gradePercent) =>
            speed * ResistanceForce(speed, gradePercent);

        /// <summary>Evenwichtssnelheid (m/s) bij constant vermogen, via bisectie.</summary>
        public float SteadySpeed(float powerWatts, float gradePercent)
        {
            float lo = 0f, hi = 40f;
            for (int i = 0; i < 60; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (ResistancePower(mid, gradePercent) > powerWatts) hi = mid;
                else lo = mid;
            }
            return lo;
        }

        /// <summary>Eén tijdstap: m·dv/dt = P/v − F_weerstand. Snelheid wordt nooit negatief.</summary>
        public float Step(float speed, float powerWatts, float gradePercent, float dt)
        {
            float propulsion = powerWatts / Math.Max(speed, MinPropulsionSpeed);
            float accel = (propulsion - ResistanceForce(speed, gradePercent)) / Parameters.TotalMassKg;
            return Math.Max(0f, speed + accel * dt);
        }
    }
}
