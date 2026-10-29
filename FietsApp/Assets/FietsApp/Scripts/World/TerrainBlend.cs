using System;

namespace FietsApp.World
{
    /// <summary>Hoeveel van elke bodemlaag op één plek ligt; samen 1.</summary>
    public readonly struct GroundMix
    {
        public readonly float Grass, Rock, Bare, Forest;

        public GroundMix(float grass, float rock, float bare, float forest)
        {
            Grass = grass;
            Rock = rock;
            Bare = bare;
            Forest = forest;
        }
    }

    /// <summary>
    /// Mengt de bodem van het terrein: gras, rots op steile hellingen, kale grond boven de boomgrens en bosbodem
    /// in de boszone. De grenzen lopen geleidelijk in elkaar over, zoals in het echt, in plaats van per driehoek
    /// om te springen.
    /// </summary>
    public sealed class TerrainBlend
    {
        /// <summary>Rots vanaf deze helling (normaal.y, ~26°), helemaal rots vanaf <see cref="RockFull"/> (~46°).</summary>
        public const float RockFrom = 0.9f, RockFull = 0.7f;

        /// <summary>Halve breedte (m hoogte) van de overgang bij boomgrens en bosgrens.</summary>
        public const float ZoneTransition = 30f;

        readonly Biome _biome;
        readonly bool _bareIsGrass;

        /// <param name="bareIsGrass">Groene top: boven de boomgrens gras in plaats van kale grond.</param>
        public TerrainBlend(Biome biome, bool bareIsGrass)
        {
            _biome = biome;
            _bareIsGrass = bareIsGrass;
        }

        /// <param name="normalY">Verticale component van de (gladde) terreinnormaal: 1 vlak, 0 loodrecht.</param>
        public GroundMix At(float x, float z, float elevation, float normalY)
        {
            float rock = SmoothStep(RockFrom, RockFull, normalY);
            float above = SmoothStep(-ZoneTransition, ZoneTransition, elevation - _biome.TreeLineAt(x, z));
            float forest = SmoothStep(-ZoneTransition, ZoneTransition, elevation - _biome.ForestFromAt(x, z)) * (1f - above);
            float grass = Math.Max(0f, 1f - above - forest);

            float soil = 1f - rock;
            float bare = above * soil;
            grass *= soil;
            if (_bareIsGrass)
            {
                grass += bare;
                bare = 0f;
            }
            return new GroundMix(grass, rock, bare, forest * soil);
        }

        /// <summary>0 bij <paramref name="from"/>, 1 bij <paramref name="to"/>, vloeiend daartussen (ook als to &lt; from).</summary>
        static float SmoothStep(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
