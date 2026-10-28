using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class TerrainBlendTests
    {
        // Zonder golving liggen boomgrens en bosgrens overal op dezelfde hoogte.
        static TerrainBlend Blend(bool bareIsGrass = false) => new TerrainBlend(
            new Biome(new TerrainSettings { TreeLine = 2000f, ForestFrom = 1000f, TreeLineVariation = 0f }), bareIsGrass);

        const float Flat = 1f, Steep = 0.5f;

        static float Sum(GroundMix m) => m.Grass + m.Rock + m.Bare + m.Forest;

        [Test]
        public void FlatValley_IsGrass()
        {
            var mix = Blend().At(0f, 0f, 500f, Flat);
            Assert.That(mix.Grass, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Sum(mix), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void SteepSlope_IsRock_AtEveryHeight()
        {
            foreach (float elevation in new[] { 500f, 1500f, 2500f })
                Assert.That(Blend().At(0f, 0f, elevation, Steep).Rock, Is.EqualTo(1f).Within(1e-5f), $"{elevation} m");
        }

        [Test]
        public void ForestZone_IsForestFloor()
        {
            Assert.That(Blend().At(0f, 0f, 1500f, Flat).Forest, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void AboveTreeLine_IsBare_OrGrassForGreenSummits()
        {
            Assert.That(Blend().At(0f, 0f, 2500f, Flat).Bare, Is.EqualTo(1f).Within(1e-5f));
            var green = Blend(bareIsGrass: true).At(0f, 0f, 2500f, Flat);
            Assert.That(green.Grass, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(green.Bare, Is.EqualTo(0f));
        }

        [Test]
        public void TreeLine_IsAGradualTransition()
        {
            // Precies op de grens half om half; 10 m eronder nog duidelijk bos, en geen sprong per meter.
            var blend = Blend();
            Assert.That(blend.At(0f, 0f, 2000f, Flat).Bare, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(blend.At(0f, 0f, 1990f, Flat).Bare, Is.InRange(0.1f, 0.45f));
            for (float e = 1950f; e < 2050f; e += 1f)
                Assert.That(blend.At(0f, 0f, e + 1f, Flat).Bare - blend.At(0f, 0f, e, Flat).Bare, Is.InRange(0f, 0.05f), $"{e} m");
        }

        [Test]
        public void Slope_TurnsToRockGradually()
        {
            // normal.y 0.8 is ongeveer 37°: daar half rots (de oude harde grens).
            var blend = Blend();
            Assert.That(blend.At(0f, 0f, 500f, 0.95f).Rock, Is.EqualTo(0f));
            Assert.That(blend.At(0f, 0f, 500f, 0.8f).Rock, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(blend.At(0f, 0f, 500f, 0.7f).Rock, Is.EqualTo(1f));
        }

        [Test]
        public void Weights_AlwaysSumToOne()
        {
            var blend = new TerrainBlend(new Biome(new TerrainSettings { TreeLine = 1800f, ForestFrom = 900f }), bareIsGrass: false);
            for (int i = 0; i < 2000; i++)
            {
                var mix = blend.At(i * 13.7f, i * 7.1f, 600f + i * 0.8f, 0.6f + (i % 40) / 100f);
                Assert.That(Sum(mix), Is.EqualTo(1f).Within(1e-4f));
                Assert.That(mix.Grass >= 0f && mix.Rock >= 0f && mix.Bare >= 0f && mix.Forest >= 0f);
            }
        }
    }
}
