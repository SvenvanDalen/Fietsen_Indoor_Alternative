using System;

namespace FietsApp.World
{
    /// <summary>
    /// Deterministische 2D-gradiëntruis (Perlin) met fractale optelling. Eigen implementatie,
    /// zodat het terrein zonder Unity te genereren en te testen is.
    /// </summary>
    public sealed class Noise
    {
        readonly int[] _perm = new int[512];

        public Noise(int seed)
        {
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            var random = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>Ruis in [-1, 1], ongeveer één golf per eenheid.</summary>
        public float Sample(float x, float z)
        {
            int xi = (int)Math.Floor(x), zi = (int)Math.Floor(z);
            float xf = x - xi, zf = z - zi;
            xi &= 255;
            zi &= 255;

            float u = Fade(xf), v = Fade(zf);
            int aa = _perm[_perm[xi] + zi], ab = _perm[_perm[xi] + zi + 1];
            int ba = _perm[_perm[xi + 1] + zi], bb = _perm[_perm[xi + 1] + zi + 1];

            float x1 = Lerp(Grad(aa, xf, zf), Grad(ba, xf - 1, zf), u);
            float x2 = Lerp(Grad(ab, xf, zf - 1), Grad(bb, xf - 1, zf - 1), u);
            // 2D-Perlin komt tot ongeveer ±0,71; schalen naar ±1.
            return Clamp(Lerp(x1, x2, v) * 1.41f);
        }

        /// <summary>Fractale ruis (meerdere octaven), genormaliseerd naar [-1, 1].</summary>
        public float Fbm(float x, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.45f)
        {
            float sum = 0f, amplitude = 1f, total = 0f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                // Verschuiving per octaaf voorkomt dat alle octaven in de oorsprong samenvallen.
                sum += Sample(x * frequency + i * 17.3f, z * frequency - i * 9.1f) * amplitude;
                total += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return Clamp(sum / total);
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;

        static float Grad(int hash, float x, float z)
        {
            switch (hash & 7)
            {
                case 0: return x + z;
                case 1: return -x + z;
                case 2: return x - z;
                case 3: return -x - z;
                case 4: return x;
                case 5: return -x;
                case 6: return z;
                default: return -z;
            }
        }
    }
}
