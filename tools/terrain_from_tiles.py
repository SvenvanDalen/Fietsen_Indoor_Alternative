"""Maakt een hoogtebestand van het echte landschap rond een klim (story 4.4).

Haalt open "terrain tiles" (Terrarium-PNG, AWS Open Data, o.a. uit SRTM) op voor het gebied rond een GPX,
leest de hoogtes uit en schrijft een regelmatig rooster in breedte- en lengtegraden naar een .bytes-bestand
dat Unity direct kan inlezen. Alleen standaardbibliotheek:

  python tools/terrain_from_tiles.py --gpx tools/.cache/klimmen/MontVentoux_Bedoin.gpx \\
      --out tools/.cache/klimmen/MontVentoux_Bedoin.terrain.bytes

Bestandsformaat (little-endian), gelezen door FietsApp.World.GeoHeightField:
  "FDEM", int32 versie (1), double zuid, double west, double dlat, double dlon, int32 rijen, int32 kolommen,
  daarna rijen × kolommen int16 hoogtes in meters, van zuid naar noord en per rij van west naar oost.

Bronnen en licenties: zie docs/bronnen.md.
"""
import argparse
import math
import re
import struct
import time
import urllib.error
import urllib.request
import zlib

TILE_URL = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png"
USER_AGENT = "FietsApp-hobbyproject (github.com/SvenvanDalen/Fietsen_Indoor_Alternative)"
METERS_PER_DEGREE = 6371000.0 * math.pi / 180.0


def read_png_rgb(data):
    """Minimale PNG-lezer voor 8-bits RGB (kleurtype 2), zoals de Terrarium-tiles."""
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "geen PNG"
    pos, idat, width = 8, b"", 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, depth, color = struct.unpack(">IIBB", chunk[:10])
            assert depth == 8 and color == 2, f"niet ondersteund: diepte {depth}, kleurtype {color}"
        elif kind == b"IDAT":
            idat += chunk
        pos += 12 + length
    raw = zlib.decompress(idat)
    stride, bpp = width * 3, 3
    rows, prev, i = [], bytearray(stride), 0
    while i < len(raw):
        filter_type, line = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if filter_type == 1:
                line[x] = (line[x] + a) & 255
            elif filter_type == 2:
                line[x] = (line[x] + b) & 255
            elif filter_type == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif filter_type == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return width, rows


def tile_xy(lat, lon, zoom):
    """Web-Mercator-pixelpositie (in tiles, met fractie)."""
    n = 2 ** zoom
    x = (lon + 180.0) / 360.0 * n
    y = (1.0 - math.log(math.tan(math.radians(lat)) + 1.0 / math.cos(math.radians(lat))) / math.pi) / 2.0 * n
    return x, y


class Tiles:
    def __init__(self, zoom):
        self.zoom, self.cache = zoom, {}

    def _tile(self, tx, ty):
        key = (tx, ty)
        if key not in self.cache:
            url = TILE_URL.format(z=self.zoom, x=tx, y=ty)
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            for attempt in range(1, 5):  # netwerk of S3 even weg: opnieuw proberen
                try:
                    with urllib.request.urlopen(request, timeout=60) as response:
                        width, rows = read_png_rgb(response.read())
                    break
                except (urllib.error.URLError, TimeoutError, ConnectionError):
                    if attempt == 4:
                        raise
                    time.sleep(30 * attempt)
            self.cache[key] = [[r[3 * x] * 256 + r[3 * x + 1] + r[3 * x + 2] / 256.0 - 32768.0 for x in range(width)]
                               for r in rows]
            print(f"  tile {self.zoom}/{tx}/{ty}")
        return self.cache[key]

    def pixel(self, px, py):
        tx, ty = px // 256, py // 256
        return self._tile(tx, ty)[py % 256][px % 256]

    def elevation(self, lat, lon):
        """Bilineair tussen de pixels."""
        x, y = tile_xy(lat, lon, self.zoom)
        fx, fy = x * 256 - 0.5, y * 256 - 0.5
        ix, iy = int(math.floor(fx)), int(math.floor(fy))
        tx, ty = fx - ix, fy - iy
        a = self.pixel(ix, iy) * (1 - tx) + self.pixel(ix + 1, iy) * tx
        b = self.pixel(ix, iy + 1) * (1 - tx) + self.pixel(ix + 1, iy + 1) * tx
        return a * (1 - ty) + b * ty


def build_terrain(gpx, out, margin=900.0, spacing=25.0, zoom=13):
    """Schrijft het hoogtebestand voor de klim in gpx naar out; geeft (laagste, hoogste) hoogte terug."""
    text = open(gpx, encoding="utf-8").read()
    points = [(float(a), float(b)) for a, b in re.findall(r'lat="([-\d.]+)" lon="([-\d.]+)"', text)]
    lats, lons = [p[0] for p in points], [p[1] for p in points]
    mid_lat = (min(lats) + max(lats)) / 2
    margin_lat = margin / METERS_PER_DEGREE
    margin_lon = margin / (METERS_PER_DEGREE * math.cos(math.radians(mid_lat)))
    south, north = min(lats) - margin_lat, max(lats) + margin_lat
    west, east = min(lons) - margin_lon, max(lons) + margin_lon
    dlat = spacing / METERS_PER_DEGREE
    dlon = spacing / (METERS_PER_DEGREE * math.cos(math.radians(mid_lat)))
    rows = int(math.ceil((north - south) / dlat)) + 1
    cols = int(math.ceil((east - west) / dlon)) + 1
    print(f"rooster {rows} × {cols} punten om de {spacing:.0f} m; tiles ophalen...")

    tiles = Tiles(zoom)
    heights = []
    lowest, highest = math.inf, -math.inf
    for r in range(rows):
        for c in range(cols):
            h = tiles.elevation(south + r * dlat, west + c * dlon)
            lowest, highest = min(lowest, h), max(highest, h)
            heights.append(int(round(h)))

    with open(out, "wb") as f:
        f.write(b"FDEM")
        f.write(struct.pack("<i4d2i", 1, south, west, dlat, dlon, rows, cols))
        f.write(struct.pack(f"<{len(heights)}h", *heights))
    print(f"hoogte {lowest:.0f} tot {highest:.0f} m; {len(tiles.cache)} tiles; geschreven naar {out}")
    return lowest, highest


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--gpx", required=True, help="klim waarvoor het landschap nodig is")
    parser.add_argument("--out", required=True)
    parser.add_argument("--margin", type=float, default=900.0, help="meter rond de route")
    parser.add_argument("--spacing", type=float, default=25.0, help="meter tussen roosterpunten")
    parser.add_argument("--zoom", type=int, default=13, help="tile-zoom; 13 is ~15 m per pixel hier")
    args = parser.parse_args()
    build_terrain(args.gpx, args.out, args.margin, args.spacing, args.zoom)


if __name__ == "__main__":
    main()
