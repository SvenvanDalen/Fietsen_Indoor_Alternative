using System.Collections.Generic;
using FietsApp.Training;
using Unity.Mathematics;
using UnityEngine;

namespace FietsApp
{
    /// <summary>De vloeiende onderdelen van één renner op één soort fiets, klaar om meshes van te maken.</summary>
    public sealed class CyclistShapeData
    {
        public MeshData Body, Helmet, Glasses, Shoe, Frame;
    }

    /// <summary>
    /// Beeldhouwt de renner en zijn frame als afstandsvelden (story 7.3): romp, ledematen en hoofd lopen vloeiend in
    /// elkaar over, de framebuizen krijgen afgeronde lassen. Het lichaam staat in de ruststand (rechterpedaal voor) en
    /// krijgt huidgewichten voor vijf botten: romp, rechter dij en scheen, linker dij en scheen. Kleur hangt niet van
    /// de vorm af, dus de vormen worden per fietssoort één keer gemaakt en daarna hergebruikt.
    /// </summary>
    public static class CyclistShapes
    {
        public const int BoneTorso = 0, BoneThighRight = 1, BoneShinRight = 2, BoneThighLeft = 3, BoneShinLeft = 4, BoneCount = 5;

        static readonly Dictionary<BikeKind, CyclistShapeData> Cache = new Dictionary<BikeKind, CyclistShapeData>();

        /// <summary>De vormen voor deze fiets; de eerste keer worden ze gemaakt (een fractie van een seconde).</summary>
        public static CyclistShapeData For(BikeKind kind)
        {
            if (!Cache.TryGetValue(kind, out var data)) Cache[kind] = data = Make(new CyclistGeometry(kind));
            return data;
        }

        /// <summary>Bouwt alle vormen voor één fiets (in Burst-jobs over alle processorkernen).</summary>
        public static CyclistShapeData Make(CyclistGeometry g)
        {
            int slots = (int)CyclistSlot.Count;
            return new CyclistShapeData
            {
                Body = Body(g, slots), Frame = Frame(g, slots), Helmet = Helmet(g, slots), Glasses = Glasses(g, slots), Shoe = Shoe(slots),
            };
        }

        // ---- Lichaam ----

        static MeshData Body(CyclistGeometry g, int slots)
        {
            var sdf = new Sdf();
            var torsoDir = (g.ShoulderCenter - g.Pelvis).normalized;
            var back = Vector3.Cross(torsoDir, Vector3.right).normalized;
            if (Vector3.Dot(back, Vector3.up) < 0f) back = -back;
            var torsoRotation = Quaternion.LookRotation(back, torsoDir); // y langs de romp, z naar de rug
            var spineStart = g.Pelvis + new Vector3(0f, 0.03f, 0.03f);
            float spineLength = Vector3.Distance(spineStart, g.ShoulderCenter) + 0.05f;
            Vector3 Spine(float f) => spineStart + torsoDir * (f * spineLength) + back * (0.028f * Mathf.Sin(Mathf.PI * f));

            sdf.Bone = BoneTorso;
            // Bekken en billen in de koersbroek.
            sdf.Paint((int)CyclistSlot.Shorts);
            sdf.Ellipsoid(g.Pelvis + new Vector3(0f, 0.025f, 0.05f), torsoRotation, new Vector3(0.145f, 0.11f, 0.1f), 0f);
            for (int side = -1; side <= 1; side += 2)
                sdf.Ellipsoid(g.Pelvis + new Vector3(0.06f * side, 0f, 0.005f), torsoRotation, new Vector3(0.067f, 0.078f, 0.07f), 0.04f);

            // Romp: buik, borstkas en schoudergordel, vloeiend.
            // Langs de rug: broek, shirt, band, shirt.
            sdf.PaintAlong(spineStart, spineStart + torsoDir * spineLength, new[] { 0.1f, 0.5f, 0.6f },
                new[] { (int)CyclistSlot.Shorts, (int)CyclistSlot.Jersey, (int)CyclistSlot.Accent, (int)CyclistSlot.Jersey });
            sdf.Ellipsoid(Spine(0.3f), torsoRotation, new Vector3(0.138f, 0.17f, 0.1f), 0.07f);
            sdf.Ellipsoid(Spine(0.66f), torsoRotation, new Vector3(0.162f, 0.2f, 0.118f), 0.07f);
            var shoulderR = g.Arm(true).shoulder;
            var shoulderL = g.Arm(false).shoulder;
            sdf.Cone(shoulderL + back * 0.01f, shoulderR + back * 0.01f, 0.062f, 0.062f, 0.06f);
            sdf.Cone(Spine(0.82f) + back * 0.03f, Spine(1f) + back * 0.01f, 0.07f, 0.055f, 0.05f); // monnikskapspier

            // Nek en hoofd.
            var head = g.HeadCenter;
            var neckTop = head + new Vector3(0f, -0.055f, -0.025f);
            sdf.PaintAlong(Spine(0.92f), neckTop, new[] { 0.32f }, new[] { (int)CyclistSlot.Jersey, (int)CyclistSlot.Skin }); // kraag
            sdf.Cone(Spine(0.92f), neckTop, 0.052f, 0.046f, 0.035f);
            sdf.Paint((int)CyclistSlot.Skin);
            sdf.Ellipsoid(head, Quaternion.identity, new Vector3(0.077f, 0.097f, 0.095f), 0.02f);
            sdf.Ellipsoid(head + new Vector3(0f, -0.052f, 0.04f), Quaternion.Euler(22f, 0f, 0f), new Vector3(0.058f, 0.04f, 0.056f), 0.035f); // kaak
            sdf.Ellipsoid(head + new Vector3(0f, -0.008f, 0.094f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.012f, 0.022f, 0.014f), 0.012f); // neus
            for (int side = -1; side <= 1; side += 2)
                sdf.Ellipsoid(head + new Vector3(0.076f * side, -0.004f, -0.004f), Quaternion.identity, new Vector3(0.011f, 0.027f, 0.018f), 0.008f); // oren

            // Armen: schouder, bovenarm, onderarm en een hand in een handschoen om de remgreep.
            for (int side = -1; side <= 1; side += 2)
            {
                var (shoulder, elbow, hand) = g.Arm(side > 0);
                var upper = elbow - shoulder;
                // Eén kleurlijn voor de hele arm: mouw, boord, huid en vanaf de pols de handschoen.
                var reach = hand - shoulder;
                float elbowAt = Vector3.Dot(elbow - shoulder, reach) / reach.sqrMagnitude;
                sdf.PaintAlong(shoulder, hand, new[] { 0.45f * elbowAt, 0.53f * elbowAt, 1f - 0.065f / reach.magnitude },
                    new[] { (int)CyclistSlot.Jersey, (int)CyclistSlot.Accent, (int)CyclistSlot.Skin, (int)CyclistSlot.Black });
                sdf.Ellipsoid(shoulder + new Vector3(0.012f * side, 0f, 0f), Quaternion.LookRotation(upper), new Vector3(0.06f, 0.064f, 0.075f), 0.045f);
                sdf.Cone(shoulder, elbow, 0.05f, 0.039f, 0.035f);
                var wrist = hand - (hand - elbow).normalized * 0.05f;
                sdf.Cone(elbow, wrist, 0.041f, 0.028f, 0.02f);
                sdf.Ellipsoid(Vector3.Lerp(elbow, wrist, 0.3f), Quaternion.LookRotation(wrist - elbow), new Vector3(0.04f, 0.038f, 0.08f), 0.03f);
                sdf.Ellipsoid(hand, Quaternion.LookRotation(hand - elbow), new Vector3(0.04f, 0.03f, 0.055f), 0.02f);
                sdf.Ellipsoid(hand + new Vector3(0.004f * side, -0.022f, 0.028f), Quaternion.Euler(30f, 0f, 0f), new Vector3(0.038f, 0.03f, 0.028f), 0.018f); // vingers om de greep
            }

            // Benen in de ruststand, elk met een eigen dij- en scheenbot.
            for (int side = 0; side < 2; side++)
            {
                bool right = side == 0;
                var (hip, knee, ankle, _) = g.Leg(CyclistGeometry.RestCrank, right);
                var thighDir = (knee - hip).normalized;
                var top = Vector3.ProjectOnPlane(Vector3.up, thighDir).normalized;
                sdf.Bone = right ? BoneThighRight : BoneThighLeft;
                sdf.PaintAlong(hip, knee, new[] { 0.66f }, new[] { (int)CyclistSlot.Shorts, (int)CyclistSlot.Skin });
                sdf.Cone(hip, knee, 0.082f, 0.052f, 0.06f);
                sdf.Ellipsoid(Vector3.Lerp(hip, knee, 0.5f) + top * 0.018f, Quaternion.LookRotation(thighDir, top), new Vector3(0.068f, 0.066f, 0.17f), 0.05f); // dijspier
                var shinDir = (ankle - knee).normalized;
                var calfSide = Vector3.ProjectOnPlane(Vector3.back, shinDir).normalized;
                sdf.Bone = right ? BoneShinRight : BoneShinLeft;
                sdf.PaintAlong(knee, ankle, new[] { 0.78f }, new[] { (int)CyclistSlot.Skin, (int)CyclistSlot.Sock });
                sdf.Cone(knee, ankle, 0.049f, 0.034f, 0.035f);
                sdf.Ellipsoid(Vector3.Lerp(knee, ankle, 0.3f) + calfSide * 0.022f, Quaternion.LookRotation(shinDir, calfSide), new Vector3(0.046f, 0.05f, 0.12f), 0.04f); // kuit
            }

            return SurfaceNets.Build(sdf, 0.0055f, slots, BoneCount, 0.025f);
        }

        // ---- Helm en bril ----

        static MeshData Helmet(CyclistGeometry g, int slots)
        {
            var sdf = new Sdf();
            var center = g.HeadCenter + new Vector3(0f, 0.025f, -0.012f);
            var tilt = Quaternion.Euler(-14f, 0f, 0f);
            var radii = new Vector3(0.103f, 0.106f, 0.136f);
            sdf.Paint((int)CyclistSlot.Helmet);
            sdf.Ellipsoid(center, tilt, radii, 0f);
            sdf.Ellipsoid(center + tilt * new Vector3(0f, -0.015f, -0.085f), tilt, new Vector3(0.078f, 0.07f, 0.085f), 0.04f); // staart

            sdf.Paint((int)CyclistSlot.Black); // binnenkant en sleuven
            sdf.Ellipsoid(g.HeadCenter + new Vector3(0f, 0.008f, 0.002f), Quaternion.identity, new Vector3(0.082f, 0.102f, 0.1f), 0.006f, subtract: true);
            sdf.Box(center + tilt * new Vector3(0f, -0.13f, 0f), tilt, new Vector3(0.3f, 0.11f, 0.3f), 0.004f, 0.012f, subtract: true);
            // Ventilatiesleuven van voor naar achter, vlak onder het oppervlak zodat ze door de schaal snijden.
            foreach (float x in new[] { -0.064f, -0.022f, 0.022f, 0.064f })
            {
                var path = new List<Vector3>();
                for (int i = 0; i <= 4; i++)
                {
                    float z = Mathf.Lerp(-0.075f, 0.075f, i / 4f);
                    float h = 1f - (x / radii.x) * (x / radii.x) - (z / radii.z) * (z / radii.z);
                    path.Add(center + tilt * new Vector3(x, radii.y * Mathf.Sqrt(Mathf.Max(0.02f, h)) * 0.9f, z));
                }
                for (int i = 0; i < path.Count - 1; i++) sdf.Cone(path[i], path[i + 1], 0.011f, 0.011f, 0.004f, subtract: true);
            }
            return SurfaceNets.Build(sdf, 0.0038f, slots);
        }

        static MeshData Glasses(CyclistGeometry g, int slots)
        {
            var sdf = new Sdf();
            sdf.Paint((int)CyclistSlot.Lens);
            var points = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(-78f, 78f, i / 10f) * Mathf.Deg2Rad;
                points.Add(g.HeadCenter + new Vector3(Mathf.Sin(a) * 0.084f, 0.012f - 0.004f * Mathf.Cos(a), Mathf.Cos(a) * 0.101f));
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                var mid = (points[i] + points[i + 1]) * 0.5f;
                var outward = new Vector3(mid.x - g.HeadCenter.x, 0f, mid.z - g.HeadCenter.z);
                sdf.FlatCone(points[i], points[i + 1], 0.019f, 0.019f, outward, 0.3f, 0.006f);
            }
            return SurfaceNets.Build(sdf, 0.0028f, slots);
        }

        // ---- Schoen (in de ruimte van de voet: z vooruit, de enkel in de oorsprong) ----

        static MeshData Shoe(int slots)
        {
            var sdf = new Sdf();
            sdf.Paint((int)CyclistSlot.Shoe);
            sdf.Ellipsoid(new Vector3(0f, -0.012f, 0.055f), Quaternion.identity, new Vector3(0.045f, 0.042f, 0.12f), 0f);
            sdf.Ellipsoid(new Vector3(0f, -0.026f, 0.135f), Quaternion.Euler(-6f, 0f, 0f), new Vector3(0.041f, 0.03f, 0.075f), 0.03f); // neus
            sdf.Ellipsoid(new Vector3(0f, -0.008f, -0.03f), Quaternion.identity, new Vector3(0.038f, 0.046f, 0.045f), 0.03f); // hiel
            sdf.Paint((int)CyclistSlot.Black);
            sdf.Box(new Vector3(0f, -0.05f, 0.06f), Quaternion.identity, new Vector3(0.046f, 0.007f, 0.135f), 0.006f, 0.008f);
            sdf.Box(new Vector3(0f, -0.06f, 0.115f), Quaternion.identity, new Vector3(0.03f, 0.005f, 0.035f), 0.004f, 0.003f); // schoenplaatje
            sdf.Sphere(new Vector3(0.032f, 0.014f, 0.035f), 0.014f, 0.006f); // draaisluiting
            return SurfaceNets.Build(sdf, 0.0035f, slots);
        }

        // ---- Frame, vork, stuur en zadel ----

        static MeshData Frame(CyclistGeometry g, int slots)
        {
            var kind = g.Kind;
            bool aero = kind == BikeKind.Aero, classic = kind == BikeKind.Classic;
            float s = kind == BikeKind.Climbing ? 0.88f : classic ? 0.75f : kind == BikeKind.Gravel ? 1.08f : 1f;
            float weld = classic ? 0.006f : 0.016f; // klassieker: kleine moffen, carbon: brede overgangen
            var seatDir = (g.SeatTop - g.BottomBracket).normalized;
            var headUp = (g.HeadTop - g.HeadBottom).normalized;
            var sdf = new Sdf();
            sdf.Paint((int)CyclistSlot.Frame);

            void Main(Vector3 a, Vector3 b, float r, float depth = 2.3f)
            {
                if (aero) sdf.FlatCone(a, b, r * depth, r * depth, Vector3.right, 0.6f / depth, weld);
                else sdf.Cone(a, b, r * s, r * s, weld);
            }

            Main(g.HeadBottom - headUp * 0.015f, g.HeadTop + headUp * 0.01f, 0.024f, 1.5f);
            Main(g.SeatTop + seatDir * 0.005f, g.HeadTop - headUp * 0.035f, 0.016f, 1.6f);
            Main(g.BottomBracket, g.HeadBottom + headUp * 0.025f, 0.022f);
            Main(g.BottomBracket, g.SeatTop + seatDir * 0.02f, 0.017f);
            sdf.Cone(g.BottomBracket + Vector3.left * 0.042f, g.BottomBracket + Vector3.right * 0.042f, 0.024f * s, 0.024f * s, weld);
            if (classic)
                foreach (var lug in new[] { g.SeatTop, g.HeadTop - headUp * 0.035f, g.HeadBottom + headUp * 0.025f })
                    sdf.Sphere(lug, 0.0175f, 0.008f);

            var stayTop = aero ? g.SeatTop - seatDir * 0.13f : g.SeatTop - seatDir * 0.015f;
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = g.RearAxle + Vector3.right * (0.064f * side);
                var bb = g.BottomBracket + Vector3.right * (0.03f * side);
                var mid = Vector3.Lerp(bb, axle, 0.45f) + Vector3.right * (0.03f * side);
                sdf.Chain(new[] { bb, mid, axle }, new[] { 0.0125f * s, 0.011f * s, 0.008f * s }, weld);
                var top = stayTop + Vector3.right * (0.02f * side);
                var bend = Vector3.Lerp(top, axle, 0.6f) + Vector3.right * (0.03f * side);
                sdf.Chain(new[] { top, bend, axle }, new[] { 0.0095f * s, 0.0085f * s, 0.007f * s }, weld);
                sdf.Ellipsoid(axle, Quaternion.identity, new Vector3(0.006f, 0.017f, 0.02f), 0.01f); // uitval
            }

            // Voorvork, bij de klassieker verchroomd.
            sdf.Paint(classic ? (int)CyclistSlot.Metal : (int)CyclistSlot.Frame);
            var crown = g.HeadBottom - headUp * 0.022f;
            sdf.Ellipsoid(crown, Quaternion.identity, new Vector3(0.055f, 0.02f, 0.03f), weld);
            for (int side = -1; side <= 1; side += 2)
            {
                var top = crown + Vector3.right * (0.042f * side);
                var tip = g.FrontAxle + Vector3.right * (0.052f * side);
                var mid = Vector3.Lerp(top, tip, 0.55f) + new Vector3(0.006f * side, 0f, -0.012f);
                if (aero)
                {
                    sdf.FlatCone(top, mid, 0.024f, 0.02f, Vector3.right, 0.45f, 0.012f);
                    sdf.FlatCone(mid, tip, 0.02f, 0.012f, Vector3.right, 0.5f, 0.004f);
                }
                else sdf.Chain(new[] { top, mid, tip }, new[] { 0.016f * s, 0.012f * s, 0.008f * s }, 0.012f);
                sdf.Ellipsoid(tip, Quaternion.identity, new Vector3(0.005f, 0.014f, 0.016f), 0.008f);
            }

            // Stuurpen, stuur, remgrepen.
            sdf.Paint(classic ? (int)CyclistSlot.Metal : (int)CyclistSlot.Black);
            sdf.Cone(g.HeadTop, g.HeadTop + headUp * (classic ? 0.07f : 0.03f), classic ? 0.012f : 0.018f, classic ? 0.012f : 0.017f, 0.006f);
            if (classic) sdf.Cone(g.HeadTop + headUp * 0.065f, g.StemEnd, 0.011f, 0.011f, 0.008f);
            else sdf.FlatCone(g.HeadTop + headUp * 0.02f, g.StemEnd + Vector3.forward * 0.01f, 0.02f, 0.019f, Vector3.right, 0.85f, 0.008f);
            sdf.Paint((int)CyclistSlot.Black);
            sdf.Ellipsoid(g.StemEnd, Quaternion.identity, new Vector3(0.024f, 0.022f, 0.022f), 0.006f);
            float w = g.BarHalfWidth, flare = kind == BikeKind.Gravel ? 0.035f : 0.008f;
            var c = g.StemEnd;
            var right = new[]
            {
                c + new Vector3(0.0f, 0f, 0.002f), c + new Vector3(w * 0.6f, 0f, 0.004f), c + new Vector3(w * 0.9f, 0.001f, 0.012f), c + new Vector3(w, 0.004f, 0.045f),
                c + new Vector3(w, 0.004f, 0.085f), c + new Vector3(w + flare * 0.3f, -0.04f, 0.128f), c + new Vector3(w + flare * 0.7f, -0.105f, 0.12f),
                c + new Vector3(w + flare, -0.135f, 0.075f), c + new Vector3(w + flare, -0.135f, 0.005f),
            };
            for (int side = -1; side <= 1; side += 2)
            {
                var bar = new List<Vector3>();
                foreach (var p in right) bar.Add(side > 0 ? p : CyclistGeometry.Mirror(p));
                var radii = new float[bar.Count];
                for (int i = 0; i < radii.Length; i++) radii[i] = i < 2 ? 0.0155f - i * 0.002f : 0.0125f;
                sdf.Chain(Smooth(bar, 4), Smooth(radii, 4), 0.004f);

                var hood = side > 0 ? g.HoodRight : CyclistGeometry.Mirror(g.HoodRight);
                sdf.Ellipsoid(hood + new Vector3(0f, 0.002f, 0.002f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(0.016f, 0.021f, 0.042f), 0.012f);
                sdf.Ellipsoid(hood + new Vector3(0f, 0.016f, 0.036f), Quaternion.Euler(-40f, 0f, 0f), new Vector3(0.012f, 0.012f, 0.016f), 0.01f); // knop
                sdf.Paint((int)CyclistSlot.Metal);
                sdf.Chain(Smooth(new List<Vector3> { hood + new Vector3(0f, 0.004f, 0.045f), hood + new Vector3(0f, -0.04f, 0.07f), hood + new Vector3(0.004f * side, -0.1f, 0.065f), hood + new Vector3(0.008f * side, -0.13f, 0.045f) }, 3),
                    Smooth(new[] { 0.007f, 0.0065f, 0.006f, 0.005f }, 3), 0.004f);
                sdf.Paint((int)CyclistSlot.Black);
            }

            // Zadelpen en zadel: breed achter, smalle neus, licht hol in het midden.
            var postTop = g.SaddleTop - Vector3.up * 0.022f;
            if (aero) sdf.FlatCone(g.SeatTop, postTop, 0.022f, 0.022f, Vector3.right, 0.5f, 0.006f);
            else
            {
                sdf.Paint(classic ? (int)CyclistSlot.Metal : (int)CyclistSlot.Black);
                sdf.Cone(g.SeatTop, postTop, 0.0135f, 0.0135f, 0.006f);
                sdf.Paint((int)CyclistSlot.Black);
            }
            var saddle = g.SaddleTop + new Vector3(0f, -0.012f, 0f);
            sdf.FlatCone(saddle + new Vector3(0f, 0.004f, -0.115f), saddle + new Vector3(0f, -0.002f, -0.03f), 0.066f, 0.058f, Vector3.up, 0.24f, 0f);
            sdf.FlatCone(saddle + new Vector3(0f, -0.002f, -0.03f), saddle + new Vector3(0f, -0.003f, 0.06f), 0.058f, 0.026f, Vector3.up, 0.38f, 0.025f);
            sdf.FlatCone(saddle + new Vector3(0f, -0.003f, 0.06f), saddle + new Vector3(0f, -0.001f, 0.13f), 0.026f, 0.017f, Vector3.up, 0.6f, 0.02f);

            // Bidon (los van het frame) met dop.
            var down = (g.HeadBottom - g.BottomBracket).normalized;
            var above = Vector3.Cross(down, Vector3.right).normalized;
            if (Vector3.Dot(above, Vector3.up) < 0f) above = -above;
            var bottleBase = g.BottomBracket + down * 0.17f + above * (0.045f * s + 0.014f);
            sdf.Paint((int)CyclistSlot.Bottle);
            sdf.Cone(bottleBase + down * 0.03f, bottleBase + down * 0.16f, 0.034f, 0.034f, 0f);
            sdf.Cone(bottleBase + down * 0.16f, bottleBase + down * 0.19f, 0.034f, 0.022f, 0.012f);
            sdf.Paint((int)CyclistSlot.Black);
            sdf.Cone(bottleBase + down * 0.19f, bottleBase + down * 0.215f, 0.016f, 0.01f, 0.004f);

            // Derailleur en remmen.
            sdf.Ellipsoid(g.RearAxle + new Vector3(0.058f, -0.05f, 0.012f), Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.011f, 0.05f, 0.022f), 0f);
            if (classic || kind == BikeKind.Climbing)
            {
                sdf.Paint((int)CyclistSlot.Metal);
                sdf.Ellipsoid(crown + new Vector3(0f, -0.012f, 0.035f), Quaternion.identity, new Vector3(0.05f, 0.02f, 0.014f), 0.006f);
                sdf.Ellipsoid(Vector3.Lerp(stayTop, g.RearAxle, 0.35f) + Vector3.forward * 0.006f, Quaternion.identity, new Vector3(0.05f, 0.02f, 0.014f), 0.006f);
            }
            else
            {
                sdf.Ellipsoid(g.FrontAxle + new Vector3(-0.06f, 0.06f, -0.06f), Quaternion.identity, new Vector3(0.012f, 0.03f, 0.04f), 0.008f);
                sdf.Ellipsoid(g.RearAxle + new Vector3(-0.07f, 0.06f, 0.05f), Quaternion.identity, new Vector3(0.012f, 0.03f, 0.04f), 0.008f);
            }

            return SurfaceNets.Build(sdf, 0.0035f, slots);
        }

        /// <summary>Catmull-Rom tussenpunten, zodat een keten van kegels een vloeiende bocht wordt.</summary>
        static List<Vector3> Smooth(IList<Vector3> controls, int steps)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < controls.Count - 1; i++)
            {
                Vector3 p0 = controls[Mathf.Max(i - 1, 0)], p1 = controls[i], p2 = controls[i + 1], p3 = controls[Mathf.Min(i + 2, controls.Count - 1)];
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    points.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t)));
                }
            }
            points.Add(controls[controls.Count - 1]);
            return points;
        }

        static List<float> Smooth(IList<float> radii, int steps)
        {
            var result = new List<float>();
            for (int i = 0; i < radii.Count - 1; i++)
                for (int k = 0; k < steps; k++) result.Add(Mathf.Lerp(radii[i], radii[i + 1], k / (float)steps));
            result.Add(radii[radii.Count - 1]);
            return result;
        }
    }
}
