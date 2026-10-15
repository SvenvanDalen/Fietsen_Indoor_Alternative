using System;
using System.IO;
using System.Text;

namespace FietsApp.World
{
    /// <summary>
    /// Omrekening tussen lengte-/breedtegraad en lokale meters rond een vast punt (oost = +X, noord = +Z).
    /// Equirectangulair: op een klim van tientallen kilometers nauwkeurig genoeg.
    /// </summary>
    public sealed class GeoProjection
    {
        const double MetersPerDegree = 6371000.0 * Math.PI / 180.0;
        readonly double _lat0, _lon0, _cosLat;

        public GeoProjection(double latitude, double longitude)
        {
            _lat0 = latitude;
            _lon0 = longitude;
            _cosLat = Math.Cos(latitude * Math.PI / 180.0);
        }

        public (float x, float z) ToLocal(double latitude, double longitude) =>
            ((float)((longitude - _lon0) * MetersPerDegree * _cosLat), (float)((latitude - _lat0) * MetersPerDegree));

        public (double latitude, double longitude) ToGeo(float x, float z) =>
            (_lat0 + z / MetersPerDegree, _lon0 + x / (MetersPerDegree * _cosLat));
    }

    /// <summary>Hoogte van het natuurlijke terrein op een lokale plek, voor <see cref="TerrainHeights"/>.</summary>
    public interface IHeightSource
    {
        float HeightAt(float x, float z);
    }

    /// <summary>
    /// Echt landschap: een rooster van hoogtes in breedte- en lengtegraden, gemaakt met
    /// tools/terrain_from_tiles.py (formaat "FDEM", zie daar).
    /// </summary>
    public sealed class GeoHeightField
    {
        readonly double _south, _west, _dLat, _dLon;
        readonly int _rows, _cols;
        readonly short[] _heights;

        GeoHeightField(double south, double west, double dLat, double dLon, int rows, int cols, short[] heights)
        {
            _south = south;
            _west = west;
            _dLat = dLat;
            _dLon = dLon;
            _rows = rows;
            _cols = cols;
            _heights = heights;
        }

        public static GeoHeightField Parse(byte[] bytes)
        {
            if (bytes.Length < 48 || Encoding.ASCII.GetString(bytes, 0, 4) != "FDEM")
                throw new FormatException("Geen hoogtebestand (FDEM) van tools/terrain_from_tiles.py.");
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.ReadBytes(4);
                int version = reader.ReadInt32();
                if (version != 1) throw new FormatException($"Onbekende versie {version} van het hoogtebestand.");
                double south = reader.ReadDouble(), west = reader.ReadDouble();
                double dLat = reader.ReadDouble(), dLon = reader.ReadDouble();
                int rows = reader.ReadInt32(), cols = reader.ReadInt32();
                var heights = new short[rows * cols];
                for (int i = 0; i < heights.Length; i++) heights[i] = reader.ReadInt16();
                return new GeoHeightField(south, west, dLat, dLon, rows, cols, heights);
            }
        }

        /// <summary>Hoogte in meters, bilineair tussen de roosterpunten; buiten het rooster de dichtstbijzijnde rand.</summary>
        public float HeightAt(double latitude, double longitude)
        {
            double fr = Clamp((latitude - _south) / _dLat, 0, _rows - 1.000001);
            double fc = Clamp((longitude - _west) / _dLon, 0, _cols - 1.000001);
            int r = (int)fr, c = (int)fc;
            double tr = fr - r, tc = fc - c;
            int i = r * _cols + c;
            double a = _heights[i] + (_heights[i + 1] - _heights[i]) * tc;
            double b = _heights[i + _cols] + (_heights[i + _cols + 1] - _heights[i + _cols]) * tc;
            return (float)(a + (b - a) * tr);
        }

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>Het hoogteveld bekeken in de lokale meters van een route.</summary>
    public sealed class LocalHeights : IHeightSource
    {
        readonly GeoHeightField _field;
        readonly GeoProjection _projection;

        public LocalHeights(GeoHeightField field, GeoProjection projection)
        {
            _field = field;
            _projection = projection ?? throw new ArgumentNullException(nameof(projection),
                "Echt terrein heeft een route met geografische ligging nodig (uit een GPX).");
        }

        public float HeightAt(float x, float z)
        {
            var (lat, lon) = _projection.ToGeo(x, z);
            return _field.HeightAt(lat, lon);
        }
    }
}
