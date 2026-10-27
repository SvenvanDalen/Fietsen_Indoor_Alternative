"""Klimpakketten (story 4.15): elke klim is één zip die de app tijdens het draaien inleest.

Een pakket (FietsApp/Assets/FietsApp/Resources/Klimmen/<id>.klim.bytes; .bytes zodat Unity het meeneemt) bevat:

  klim.json      naam, gebied, land, kerncijfers, begroeiing, ondergrond, herkenningspunt, bron en licentie
  route.gpx      de weg met ruwe hoogte (tools/route_from_osm.py of tools/fantasy_climb.py)
  terrain.bytes  het landschap eromheen (tools/terrain_from_tiles.py); ontbreekt bij een fantasieklim

Naast de pakketten staat catalogus.json: per klim versie, formaat en sha256. De app haalt die van GitHub op en
downloadt nieuwe of gewijzigde pakketten (story 4.16). Een pakket krijgt een hogere versie zodra de inhoud
verandert; de zip is reproduceerbaar (vaste datum en volgorde), dus opnieuw bouwen zonder wijziging laat hem gelijk.

FORMAT gaat omhoog als de app een pakket anders moet lezen (en dan ook ClimbPackage.Format in de app).
"""
import hashlib
import io
import json
import math
import os
import re
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGES = os.path.join(ROOT, "FietsApp", "Assets", "FietsApp", "Resources", "Klimmen")
CATALOG_FILE = os.path.join(PACKAGES, "catalogus.json")
WORK = os.path.join(ROOT, "tools", ".cache", "klimmen")  # GPX en landschap tijdens het bouwen (niet in git)
FORMAT = 1
EXTENSION = ".klim.bytes"
ZIP_DATE = (2026, 1, 1, 0, 0, 0)

REAL_SOURCE = ("Weg: OpenStreetMap-bijdragers (via OSRM of Overpass); hoogte langs de weg: SRTM 30 m via OpenTopoData; "
               "landschap: Terrain Tiles, AWS Open Data (Terrarium, zoom 13)")
REAL_LICENSE = "Weg: ODbL 1.0, © OpenStreetMap-bijdragers; SRTM: publiek domein; Terrain Tiles: open data met bronvermelding"
FANTASY_SOURCE = "Verzonnen met tools/fantasy_climb.py"
FANTASY_LICENSE = "Eigen werk van FietsApp"

# Begroeiing per soort gebied, als een klim in tools/climbs.json geen eigen "vegetation" heeft. Een boomgrens van
# None betekent: overal bomen. above_tree_line: grond boven de boomgrens (bare = kale kalksteen, grass, dark_grass).
BIOMES = {
    "alpen": dict(tree_line=2100, forest_from=800, above_tree_line="dark_grass", gravel_verge=True),
    "dolomieten": dict(tree_line=2100, forest_from=1000, above_tree_line="dark_grass", gravel_verge=True),
    "pyreneeen": dict(tree_line=1900, forest_from=700, above_tree_line="dark_grass", gravel_verge=True),
    "jura": dict(tree_line=1350, forest_from=400, above_tree_line="grass", gravel_verge=False),
    "ventoux": dict(tree_line=1450, forest_from=550, above_tree_line="bare", gravel_verge=True),
    "heuvels_bos": dict(tree_line=None, forest_from=250, above_tree_line="bare", gravel_verge=False),
    "heuvels": dict(tree_line=None, forest_from=None, above_tree_line="bare", gravel_verge=False),
}

# Zelfde instellingen als ClimbImportSettings in de app; de kerncijfers in klim.json moeten daarmee kloppen.
SPACING, MEDIAN_RADIUS, SIGMA = 10.0, 100.0, 150.0
METERS_PER_DEGREE = 6371000.0 * math.pi / 180.0


def package_path(climb_id):
    return os.path.join(PACKAGES, climb_id + EXTENSION)


def work_paths(climb_id):
    """Waar de bouwscripts GPX en landschap van een klim neerzetten voordat ze in het pakket gaan."""
    return os.path.join(WORK, climb_id + ".gpx"), os.path.join(WORK, climb_id + ".terrain.bytes")


def read_gpx_points(text):
    """(lat, lon, hoogte) per trackpunt."""
    pattern = r'<trkpt lat="([-\d.]+)" lon="([-\d.]+)">\s*<ele>([-\d.]+)</ele>'
    return [(float(a), float(b), float(c)) for a, b, c in re.findall(pattern, text)]


def _pad(values, distance, reach):
    n = len(values)
    before = after = 0
    while before + 1 < n and distance[before + 1] - distance[0] <= reach:
        before += 1
    while after + 1 < n and distance[n - 1] - distance[n - 2 - after] <= reach:
        after += 1
    v = [2 * values[0] - values[before - i] for i in range(before)] + list(values)
    d = [2 * distance[0] - distance[before - i] for i in range(before)] + list(distance)
    v += [2 * values[n - 1] - values[n - 2 - i] for i in range(after)]
    d += [2 * distance[n - 1] - distance[n - 2 - i] for i in range(after)]
    return v, d, before


def _median(values, distance, radius):
    result, lo, hi = [], 0, 0
    for i in range(len(values)):
        while distance[i] - distance[lo] > radius:
            lo += 1
        while hi < len(values) - 1 and distance[hi + 1] - distance[i] <= radius:
            hi += 1
        window = sorted(values[lo:hi + 1])
        result.append(window[len(window) // 2])
    return result


def _gaussian(values, distance, sigma):
    result, lo, reach, two_sigma2 = [], 0, 3 * sigma, 2 * sigma * sigma
    for i in range(len(values)):
        while distance[i] - distance[lo] > reach:
            lo += 1
        total = weights = 0.0
        j = lo
        while j < len(values) and distance[j] - distance[i] <= reach:
            w = math.exp(-((distance[j] - distance[i]) ** 2) / two_sigma2)
            total += w * values[j]
            weights += w
            j += 1
        result.append(total / weights)
    return result


def figures(points):
    """Lengte (m), gemiddelde helling (%) en hoogtemeters zoals de app ze uitrekent (ClimbImport.FromGpx + ClimbStats):
    projecteren rond het eerste punt, elke 10 m bemonsteren, mediaan ±100 m en Gauss σ 150 m met gespiegelde randen."""
    lat0, lon0 = points[0][0], points[0][1]
    cos_lat = math.cos(math.radians(lat0))
    xz = [((lon - lon0) * METERS_PER_DEGREE * cos_lat, (lat - lat0) * METERS_PER_DEGREE) for lat, lon, _ in points]
    dist = [0.0]
    for (x0, z0), (x1, z1) in zip(xz, xz[1:]):
        dist.append(dist[-1] + math.hypot(x1 - x0, z1 - z0))
    ele = [p[2] for p in points]

    length = dist[-1]
    count = int(math.floor(length / SPACING)) + 1
    total = count + (1 if length - (count - 1) * SPACING > SPACING * 0.25 else 0)
    rd, re_, seg = [], [], 0
    for i in range(total):
        d = i * SPACING if i < count else length
        while seg < len(dist) - 2 and dist[seg + 1] < d:
            seg += 1
        span = dist[seg + 1] - dist[seg]
        t = min(1.0, max(0.0, (d - dist[seg]) / span)) if span > 0 else 0.0
        re_.append(ele[seg] + (ele[seg + 1] - ele[seg]) * t)
        rd.append(d)

    padded_e, padded_d, offset = _pad(re_, rd, max(MEDIAN_RADIUS, 3 * SIGMA))
    smooth = _gaussian(_median(padded_e, padded_d, MEDIAN_RADIUS), padded_d, SIGMA)[offset:offset + total]
    gain = sum(max(0.0, b - a) for a, b in zip(smooth, smooth[1:]))
    return {"length_m": round(length, 1), "average_grade": round((smooth[-1] - smooth[0]) / length * 100.0, 3),
            "elevation_gain": round(gain, 1)}


def climb_json(climb, gpx_text, has_terrain, version):
    """De inhoud van klim.json voor een klim uit tools/climbs.json."""
    vegetation = dict(BIOMES.get(climb.get("biome"), BIOMES["heuvels"]))
    vegetation.update(climb.get("vegetation", {}))
    fantasy = "design" in climb
    return {
        "format": FORMAT,
        "version": version,
        "id": climb["id"],
        "name": climb["name"],
        "region": climb["region"],
        "country": climb["country"],
        "figures": figures(read_gpx_points(gpx_text)),
        "vegetation": vegetation,
        "surfaces": climb.get("surfaces", []),
        "landmark": climb.get("landmark"),
        "source": climb.get("source", FANTASY_SOURCE if fantasy else REAL_SOURCE),
        "license": climb.get("license", FANTASY_LICENSE if fantasy else REAL_LICENSE),
        "route": "route.gpx",
        "terrain": "terrain.bytes" if has_terrain else None,
    }


def _zip(files):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name, data in files:
            info = zipfile.ZipInfo(name, ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, data)
    return buffer.getvalue()


def make_package(climb, gpx_text, terrain, version):
    """De zip als bytes."""
    info = climb_json(climb, gpx_text, terrain is not None, version)
    files = [("klim.json", json.dumps(info, ensure_ascii=False, indent=2) + "\n"), ("route.gpx", gpx_text)]
    if terrain is not None:
        files.append(("terrain.bytes", terrain))
    return _zip(files)


def read_package(path):
    """(klim.json, GPX-tekst, landschap of None) uit een bestaand pakket."""
    with zipfile.ZipFile(path) as z:
        info = json.loads(z.read("klim.json").decode("utf-8"))
        gpx = z.read(info["route"]).decode("utf-8")
        terrain = z.read(info["terrain"]) if info.get("terrain") else None
    return info, gpx, terrain


def load_catalog():
    if not os.path.exists(CATALOG_FILE):
        return {"format": 1, "climbs": []}
    return json.load(open(CATALOG_FILE, encoding="utf-8"))


def save_catalog(catalog):
    catalog["climbs"].sort(key=lambda c: c["id"])
    with open(CATALOG_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n")


def sources(climb):
    """GPX-tekst en landschap van een klim: vers gebouwd uit de werkmap, anders uit het bestaande pakket."""
    gpx_path, terrain_path = work_paths(climb["id"])
    if os.path.exists(gpx_path):
        gpx = open(gpx_path, encoding="utf-8").read()
        terrain = open(terrain_path, "rb").read() if os.path.exists(terrain_path) else None
        return gpx, terrain
    if os.path.exists(package_path(climb["id"])):
        _, gpx, terrain = read_package(package_path(climb["id"]))
        return gpx, terrain
    return None, None


def package(climb, catalog):
    """Maakt of vernieuwt het pakket van een klim en zijn regel in de catalogus. Geeft wat er gebeurde."""
    gpx, terrain = sources(climb)
    if gpx is None:
        return "geen GPX"
    entry = next((e for e in catalog["climbs"] if e["id"] == climb["id"]), None)
    version = entry["version"] if entry else 1
    data = make_package(climb, gpx, terrain, version)
    sha = hashlib.sha256(data).hexdigest()
    if entry and entry["sha256"] == sha and entry["format"] == FORMAT and os.path.exists(package_path(climb["id"])):
        return f"ongewijzigd (versie {version})"
    if entry:  # inhoud gewijzigd: nieuwe versie, zodat de app hem opnieuw downloadt
        version += 1
        data = make_package(climb, gpx, terrain, version)
        sha = hashlib.sha256(data).hexdigest()
    else:
        entry = {"id": climb["id"]}
        catalog["climbs"].append(entry)
    os.makedirs(PACKAGES, exist_ok=True)
    with open(package_path(climb["id"]), "wb") as f:
        f.write(data)
    entry.update(version=version, format=FORMAT, sha256=sha, file=climb["id"] + EXTENSION, size=len(data))
    return f"pakket versie {version}, {len(data) // 1024} kB"


def package_all(climbs):
    """Alle klimmen opnieuw inpakken (zonder netwerk); de catalogus wordt bijgewerkt."""
    catalog = load_catalog()
    results = [(c["id"], package(c, catalog)) for c in climbs]
    save_catalog(catalog)
    return results
