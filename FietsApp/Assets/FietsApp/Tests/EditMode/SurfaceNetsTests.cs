using System.Collections.Generic;
using FietsApp.Training;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    /// <summary>Vloeiende vormen voor renner en frame (story 7.3): afstandsveld naar mesh.</summary>
    public class SurfaceNetsTests
    {
        [Test]
        public void Sphere_IsClosedAndRound()
        {
            var sdf = new Sdf();
            sdf.Sphere(Vector3.zero, 0.1f, 0f);
            var data = SurfaceNets.Build(sdf, 0.01f, 1);

            Assert.Greater(data.Vertices.Length, 300);
            foreach (var v in data.Vertices)
                Assert.AreEqual(0.1f, v.magnitude, 0.003f, "hoekpunt op de bol");
            for (int i = 0; i < data.Vertices.Length; i += 37)
                Assert.Greater(Vector3.Dot(data.Normals[i], data.Vertices[i].normalized), 0.98f, "normaal naar buiten");

            // Dicht: elke ribbe hoort bij precies twee driehoeken, en de driehoeken wijzen naar buiten.
            var edges = new Dictionary<(int, int), int>();
            var tris = data.Triangles[0];
            for (int t = 0; t < tris.Length; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                var p0 = data.Vertices[tris[t]];
                var face = Vector3.Cross(data.Vertices[tris[t + 1]] - p0, data.Vertices[tris[t + 2]] - p0);
                Assert.Greater(Vector3.Dot(face, p0), 0f, "driehoek naar buiten");
            }
            foreach (var count in edges.Values) Assert.AreEqual(2, count);
        }

        [Test]
        public void SmoothBlend_JoinsWithoutAGap()
        {
            // Twee bollen die elkaar net niet raken: met een zachte overgang worden ze één vorm.
            var sdf = new Sdf();
            sdf.Sphere(new Vector3(-0.055f, 0f, 0f), 0.05f, 0f);
            sdf.Sphere(new Vector3(0.055f, 0f, 0f), 0.05f, 0.04f);
            Assert.Less(sdf.Distance(Vector3.zero), 0f, "tussen de bollen is het nu binnen");
        }

        [Test]
        public void ColourBoundary_IsAStraightLine()
        {
            // Een staaf langs x: links slot 1, rechts slot 2, grens in het midden.
            var sdf = new Sdf();
            sdf.PaintAlong(new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), new[] { 0.5f }, new[] { 1, 2 });
            sdf.Cone(new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), 0.03f, 0.03f, 0f);
            var data = SurfaceNets.Build(sdf, 0.008f, 3);

            Assert.AreEqual(0, data.Triangles[0].Length);
            float leftMax = float.MinValue, rightMin = float.MaxValue;
            for (int i = 0; i < data.Triangles[1].Length; i++) leftMax = Mathf.Max(leftMax, data.Vertices[data.Triangles[1][i]].x);
            for (int i = 0; i < data.Triangles[2].Length; i++) rightMin = Mathf.Min(rightMin, data.Vertices[data.Triangles[2][i]].x);
            // Alle hoekpunten op de grens liggen op x = 0: geen trapje over de cellen.
            Assert.AreEqual(0f, leftMax, 0.001f);
            Assert.AreEqual(0f, rightMin, 0.001f);
        }

        [Test]
        public void Body_BendsAtKneeAndHip()
        {
            var body = CyclistShapes.For(BikeKind.Allround).Body;
            Assert.AreEqual(body.Vertices.Length, body.Weights.Length);
            int blended = 0;
            foreach (var w in body.Weights)
            {
                Assert.AreEqual(1f, w.weight0 + w.weight1, 1e-4f);
                bool knee = (w.boneIndex0 == CyclistShapes.BoneThighRight && w.boneIndex1 == CyclistShapes.BoneShinRight) ||
                            (w.boneIndex0 == CyclistShapes.BoneShinRight && w.boneIndex1 == CyclistShapes.BoneThighRight);
                if (knee && w.weight1 > 0.2f) blended++;
            }
            Assert.Greater(blended, 20, "rond de knie buigt de huid met beide botten mee");

            // Het hoofd hoort bij de romp, de enkel bij de scheen.
            var g = new CyclistGeometry(BikeKind.Allround);
            Assert.AreEqual(CyclistShapes.BoneTorso, Nearest(body, g.HeadCenter + Vector3.up * 0.1f).boneIndex0);
            var (_, _, ankle, _) = g.Leg(CyclistGeometry.RestCrank, true);
            Assert.AreEqual(CyclistShapes.BoneShinRight, Nearest(body, ankle).boneIndex0);
        }

        [Test]
        public void EveryBike_HasAFrame()
        {
            foreach (BikeKind kind in System.Enum.GetValues(typeof(BikeKind)))
            {
                var data = CyclistShapes.For(kind);
                Assert.Greater(data.Frame.Triangles[(int)CyclistSlot.Frame].Length + data.Frame.Triangles[(int)CyclistSlot.Metal].Length, 1000, kind.ToString());
                Assert.Greater(data.Frame.Triangles[(int)CyclistSlot.Black].Length, 1000, $"{kind}: stuur en zadel");
            }
        }

        static BoneWeight Nearest(MeshData mesh, Vector3 point)
        {
            int best = 0;
            for (int i = 1; i < mesh.Vertices.Length; i++)
                if ((mesh.Vertices[i] - point).sqrMagnitude < (mesh.Vertices[best] - point).sqrMagnitude) best = i;
            return mesh.Weights[best];
        }
    }
}
