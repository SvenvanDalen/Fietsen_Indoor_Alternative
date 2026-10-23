using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>Materiaal-slots van de fietser; elk slot is een submesh met een eigen kleur.</summary>
    public enum CyclistSlot { Frame, Metal, Black, Tyre, Rim, Jersey, Accent, Shorts, Skin, Helmet, Shoe, Sock, Lens, Bottle, Hair, Count }

    /// <summary>
    /// Maten van fiets en rijder in meters, in het assenstelsel van de fiets: x naar rechts, y omhoog, z vooruit, de
    /// grond op y = 0 midden tussen de wielen (story 7.3). Gebaseerd op een racefiets maat 56 (wielbasis 99 cm,
    /// zitbuishoek 73°, crank 172,5 mm) en een rijder van ongeveer 1,80 m.
    /// </summary>
    public sealed class CyclistGeometry
    {
        public readonly BikeKind Kind;

        // Fiets
        public readonly float WheelRadius = 0.336f;
        public readonly float TyreRadius;
        public readonly float RimDepth;
        public readonly int Spokes;
        public readonly Vector3 RearAxle, FrontAxle, BottomBracket;
        public readonly float CrankLength = 0.1725f;
        public readonly float PedalHalfWidth = 0.11f; // Q-factor: pedalen zoveel opzij
        public readonly Vector3 SeatTop, HeadTop, HeadBottom, SaddleTop, StemEnd;
        public readonly float BarHalfWidth;
        public readonly Vector3 HoodRight; // de rechterhand rust op de remgreep; links gespiegeld

        // Rijder
        public readonly float Thigh = 0.445f, Shin = 0.435f, UpperArm = 0.31f, Forearm = 0.30f;
        public readonly Vector3 HipRight, ShoulderRight, Pelvis, ShoulderCenter, HeadCenter;

        public CyclistGeometry(BikeKind kind)
        {
            Kind = kind;
            TyreRadius = kind == BikeKind.Gravel ? 0.021f : kind == BikeKind.Classic ? 0.012f : 0.0135f;
            RimDepth = kind == BikeKind.Aero ? 0.062f : kind == BikeKind.Allround ? 0.040f : kind == BikeKind.Climbing ? 0.028f : 0.020f;
            Spokes = kind == BikeKind.Classic ? 32 : kind == BikeKind.Aero ? 18 : 24;

            float axle = WheelRadius;
            RearAxle = new Vector3(0f, axle, -0.495f);
            FrontAxle = new Vector3(0f, axle, 0.495f);
            BottomBracket = new Vector3(0f, axle - 0.07f, RearAxle.z + 0.408f);

            // Zitbuis onder 73° naar achteren; de bovenbuis loopt bij een klassieker vlak, anders licht af naar achteren.
            var seatDir = new Vector3(0f, Mathf.Sin(73f * Mathf.Deg2Rad), -Mathf.Cos(73f * Mathf.Deg2Rad));
            SeatTop = BottomBracket + seatDir * (kind == BikeKind.Classic ? 0.56f : 0.50f);
            SaddleTop = BottomBracket + seatDir * 0.715f + Vector3.up * 0.03f;

            // Balhoofd onder 73°; de stuuras snijdt de grond iets voor het wiel (naloop), de vork heeft 45 mm voorover.
            float slope = 1f / Mathf.Tan(73f * Mathf.Deg2Rad); // z terug per meter omhoog
            Vector3 OnSteeringAxis(float y) => new Vector3(0f, y, FrontAxle.z - 0.045f - (y - axle) * slope);
            HeadBottom = OnSteeringAxis(axle + 0.33f);
            HeadTop = OnSteeringAxis(kind == BikeKind.Classic ? SeatTop.y + 0.015f : axle + (kind == BikeKind.Gravel ? 0.495f : 0.478f));
            StemEnd = HeadTop + new Vector3(0f, kind == BikeKind.Classic ? 0.06f : 0.035f, kind == BikeKind.Classic ? 0.085f : 0.105f);
            BarHalfWidth = kind == BikeKind.Gravel ? 0.23f : 0.205f;
            HoodRight = StemEnd + new Vector3(BarHalfWidth, 0.03f, 0.075f);

            Pelvis = SaddleTop + new Vector3(0f, 0.085f, 0f);
            HipRight = Pelvis + new Vector3(0.095f, -0.01f, 0.05f);
            ShoulderCenter = Pelvis + new Vector3(0f, 0.34f, 0.50f);
            ShoulderRight = ShoulderCenter + new Vector3(0.185f, -0.02f, 0f);
            HeadCenter = ShoulderCenter + new Vector3(0f, 0.19f, 0.11f);
        }

        /// <summary>Plek van het rechterpedaal bij een crankhoek (0 = crank recht omhoog, vooruit draaiend); links is +180°.</summary>
        public Vector3 Pedal(float angleRadians, bool right)
        {
            float a = right ? angleRadians : angleRadians + Mathf.PI;
            return BottomBracket + new Vector3(right ? PedalHalfWidth : -PedalHalfWidth, Mathf.Cos(a) * CrankLength, Mathf.Sin(a) * CrankLength);
        }

        /// <summary>
        /// Enkelstand bij een crankhoek: de bal van de voet staat op het pedaal, de hiel zakt iets in de neergaande slag.
        /// Geeft de enkel en de kanteling van de voet (graden, positief = tenen omlaag).
        /// </summary>
        public (Vector3 ankle, float pitch) Ankle(float angleRadians, bool right)
        {
            float a = right ? angleRadians : angleRadians + Mathf.PI;
            var pedal = Pedal(angleRadians, right);
            float pitch = 12f - 14f * Mathf.Sin(a + 0.6f);
            var back = Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0.075f, -0.115f);
            return (pedal + back, pitch);
        }

        /// <summary>Crankhoek waarin het lichaam gemodelleerd wordt: rechterpedaal voor, linker achter (knie half gebogen).</summary>
        public const float RestCrank = Mathf.PI / 2f;

        /// <summary>Heup, knie en enkel van één been bij een crankhoek, en de kanteling van de voet.</summary>
        public (Vector3 hip, Vector3 knee, Vector3 ankle, float pitch) Leg(float angleRadians, bool right)
        {
            var hip = right ? HipRight : Mirror(HipRight);
            var (ankle, pitch) = Ankle(angleRadians, right);
            // Knie naar voren en een fractie naar buiten.
            var knee = Joint(hip, ankle, Thigh, Shin, new Vector3(right ? 0.12f : -0.12f, 0.2f, 1f));
            return (hip, knee, ankle, pitch);
        }

        /// <summary>Schouder, elleboog en hand van één arm; de hand ligt op de remgreep.</summary>
        public (Vector3 shoulder, Vector3 elbow, Vector3 hand) Arm(bool right)
        {
            var shoulder = right ? ShoulderRight : Mirror(ShoulderRight);
            var hand = (right ? HoodRight : Mirror(HoodRight)) + new Vector3(0f, 0.03f, -0.005f);
            var elbow = Joint(shoulder, hand, UpperArm, Forearm, new Vector3(right ? 0.7f : -0.7f, -0.35f, -0.6f));
            return (shoulder, elbow, hand);
        }

        public static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>
        /// Twee-delige ketting (dij en scheen, of boven- en onderarm): waar het middelste gewricht komt als het begin op
        /// <paramref name="root"/> zit en het eind op <paramref name="end"/>, gebogen naar <paramref name="pole"/>.
        /// </summary>
        public static Vector3 Joint(Vector3 root, Vector3 end, float upper, float lower, Vector3 pole)
        {
            var span = end - root;
            float d = Mathf.Clamp(span.magnitude, Mathf.Abs(upper - lower) + 1e-3f, upper + lower - 1e-3f);
            var dir = span.normalized;
            float a = (upper * upper - lower * lower + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, upper * upper - a * a));
            var bend = Vector3.ProjectOnPlane(pole, dir).normalized;
            return root + dir * a + bend * h;
        }
    }
}
