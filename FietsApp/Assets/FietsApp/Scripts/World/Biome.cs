namespace FietsApp.World
{
    /// <summary>
    /// Begroeiingszones op hoogte. Boven de boomgrens geen bos maar kale grond, zoals de witte
    /// kalksteentop van de Mont Ventoux. De grens golft met ruis, zodat hij niet als een liniaal over de berg loopt.
    /// </summary>
    public sealed class Biome
    {
        readonly TerrainSettings _settings;
        readonly Noise _noise;

        public Biome(TerrainSettings settings)
        {
            _settings = settings;
            _noise = new Noise(settings.Seed + 101);
        }

        public float TreeLineAt(float x, float z) =>
            _settings.TreeLine + _noise.Fbm(x / 400f, z / 400f, octaves: 3) * _settings.TreeLineVariation;

        /// <summary>Waar het bos begint, met dezelfde golving als de boomgrens.</summary>
        public float ForestFromAt(float x, float z) =>
            _settings.ForestFrom + _noise.Fbm(x / 400f + 50f, z / 400f, octaves: 3) * _settings.TreeLineVariation;

        public bool IsAboveTreeLine(float x, float z, float elevation) => elevation > TreeLineAt(x, z);

        /// <summary>In de boszone: boven de bosgrens (met dezelfde golving) en onder de boomgrens.</summary>
        public bool IsForest(float x, float z, float elevation) =>
            elevation > ForestFromAt(x, z) && !IsAboveTreeLine(x, z, elevation);
    }
}
