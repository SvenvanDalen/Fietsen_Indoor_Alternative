using System.IO;
using System.Text;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class WorldHeightsTests
    {
        /// <summary>Hoogtebestand (FDEM v1) van rows × cols punten met overal dezelfde hoogte.</summary>
        static GeoHeightField Flat(double south, double west, double step, int rows, int cols, short height)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("FDEM"));
                w.Write(1);
                w.Write(south); w.Write(west); w.Write(step); w.Write(step);
                w.Write(rows); w.Write(cols);
                for (int i = 0; i < rows * cols; i++) w.Write(height);
                w.Flush();
                return GeoHeightField.Parse(ms.ToArray());
            }
        }

        // Rooster van 0,02° × 0,02° rond (45,01; 6,01): ruim 2,2 km van zuid naar noord.
        static readonly GeoHeightField Field = Flat(45.0, 6.0, 0.001, 21, 21, 1500);
        static readonly GeoProjection Projection = new GeoProjection(45.01, 6.01);

        [Test]
        public void EdgeDistanceInMeters()
        {
            // Midden van het rooster: 0,01° tot de noord- en zuidrand = 1112 m; oost-west is korter (cos 45°) = 786 m.
            Assert.That(Field.EdgeDistance(45.01, 6.01), Is.EqualTo(786).Within(2));
            Assert.That(Field.EdgeDistance(45.03, 6.01), Is.LessThan(0));
        }

        [Test]
        public void HeightIsShiftedAndFollowsThePlacement()
        {
            // Klim begint op lokaal (0, 0), komt in de wereld op (5000, -2000), een kwartslag gedraaid, 1000 m lager.
            var placement = new ClimbPlacement((float)(System.Math.PI / 2), 0f, 0f, 5000f, -2000f, -1000f);
            var heights = new WorldHeights(Field, Projection, placement);

            Assert.That(heights.HeightAt(5000f, -2000f), Is.EqualTo(500f).Within(0.01f));
            Assert.That(heights.Coverage(5000f, -2000f), Is.EqualTo(1f));
            Assert.That(heights.Coverage(0f, 0f), Is.EqualTo(0f), "ver buiten het rooster");

            // Lokaal 700 m oost ligt 86 m van de oostrand: binnen de overgangszone van 300 m.
            var (wx, wz) = placement.ToWorld(700f, 0f);
            float c = heights.Coverage(wx, wz);
            Assert.That(c, Is.GreaterThan(0f).And.LessThan(1f));
        }

        sealed class Partial : IHeightSource, IHeightCoverage
        {
            readonly float _coverage;
            public Partial(float coverage) => _coverage = coverage;
            public float HeightAt(float x, float z) => 777f;
            public float Coverage(float x, float z) => _coverage;
        }

        sealed class Full : IHeightSource
        {
            public float HeightAt(float x, float z) => 777f;
        }

        static Route Line()
        {
            var b = new RouteBuilder();
            return b.Straight(800f, 5f).Build();
        }

        [Test]
        public void NoCoverageGivesTheInventedLandscape()
        {
            var s = new TerrainSettings { Margin = 200f };
            var invented = TerrainHeights.Generate(Line(), s);
            var blended = TerrainHeights.Generate(Line(), s, new Partial(0f));
            for (int z = 0; z < invented.CountZ; z += 7)
            for (int x = 0; x < invented.CountX; x += 7)
                Assert.That(blended[x, z], Is.EqualTo(invented[x, z]).Within(1e-3f));
        }

        [Test]
        public void FullCoverageGivesTheRealLandscape()
        {
            var s = new TerrainSettings { Margin = 200f };
            var real = TerrainHeights.Generate(Line(), s, new Full());
            var blended = TerrainHeights.Generate(Line(), s, new Partial(1f));
            for (int z = 0; z < real.CountZ; z += 7)
            for (int x = 0; x < real.CountX; x += 7)
                Assert.That(blended[x, z], Is.EqualTo(real[x, z]).Within(1e-3f));
        }
    }
}
