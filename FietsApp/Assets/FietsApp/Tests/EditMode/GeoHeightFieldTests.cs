using System;
using System.IO;
using System.Text;
using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class GeoProjectionTests
    {
        [Test]
        public void RoundTrip_AndKnownDistances()
        {
            var p = new GeoProjection(44.0, 5.0);

            var (x, z) = p.ToLocal(44.01, 5.0);
            Assert.That(z, Is.EqualTo(1111.95f).Within(0.5f)); // 0,01° breedte ≈ 1112 m
            Assert.That(x, Is.EqualTo(0f).Within(0.01f));

            var (lat, lon) = p.ToGeo(1234f, -567f);
            var (x2, z2) = p.ToLocal(lat, lon);
            Assert.That(x2, Is.EqualTo(1234f).Within(0.01f));
            Assert.That(z2, Is.EqualTo(-567f).Within(0.01f));
        }
    }

    public class GeoHeightFieldTests
    {
        /// <summary>Rooster van 3 × 3 punten op 0,001° afstand; hoogte = 100 × rij + 10 × kolom.</summary>
        static byte[] SmallField()
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("FDEM"));
            w.Write(1);
            w.Write(44.0); w.Write(5.0); w.Write(0.001); w.Write(0.001);
            w.Write(3); w.Write(3);
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                w.Write((short)(100 * r + 10 * c));
            return stream.ToArray();
        }

        [Test]
        public void Parse_AndInterpolate()
        {
            var field = GeoHeightField.Parse(SmallField());

            Assert.That(field.HeightAt(44.0, 5.0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(field.HeightAt(44.002, 5.002), Is.EqualTo(220f).Within(0.001f));
            Assert.That(field.HeightAt(44.0005, 5.0015), Is.EqualTo(50f + 15f).Within(0.001f));
        }

        [Test]
        public void ClampsOutsideTheField()
        {
            var field = GeoHeightField.Parse(SmallField());

            Assert.That(field.HeightAt(43.0, 4.0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(field.HeightAt(45.0, 6.0), Is.EqualTo(220f).Within(0.001f));
        }

        [Test]
        public void RejectsOtherFiles()
        {
            Assert.Throws<FormatException>(() => GeoHeightField.Parse(Encoding.ASCII.GetBytes("geen hoogtebestand")));
        }
    }

    public class RealTerrainTests
    {
        const string GpxPath = "Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx";
        const string TerrainPath = "Assets/FietsApp/Routes/MontVentoux_Bedoin.terrain.bytes";

        [Test]
        public void VentouxTerrain_FollowsTheRealMountain()
        {
            var route = ClimbImport.FromGpx(Gpx.Parse(File.ReadAllText(GpxPath, Encoding.UTF8)));
            var field = GeoHeightField.Parse(File.ReadAllBytes(TerrainPath));
            var settings = new TerrainSettings { Margin = 300f };
            var grid = TerrainHeights.Generate(route, settings, new LocalHeights(field, route.Projection));

            // Langs de weg net onder het asfalt, zoals bij elk terrein.
            for (float d = 0f; d < route.Length; d += 500f)
            {
                var s = route.Sample(d);
                Assert.That(grid.HeightAt(s.X, s.Z), Is.EqualTo(s.Elevation - settings.RoadDepth).Within(0.5f), $"op {d} m");
            }

            // Ver van de weg het echte landschap: 300 m ten zuiden van de top ligt nog ruim boven 1700 m,
            // de rand van het raster bij Bédoin in het dal onder 500 m.
            var top = route.Sample(route.Length);
            Assert.That(grid.HeightAt(top.X, top.Z - 300f), Is.GreaterThan(1700f));
            var start = route.Sample(0f);
            Assert.That(grid.HeightAt(start.X - 250f, start.Z - 250f), Is.LessThan(500f));
        }
    }
}
