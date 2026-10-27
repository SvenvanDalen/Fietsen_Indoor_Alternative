"""Bouwt de fantasieklimmen uit tools/climbs.json (story 4.4): klimmen met "design" in plaats van een echte weg.

Een ontwerp bestaat uit een weg (stukken rechtdoor, bochten en een trap van haarspelden) en een hellingsprofiel
(helling per stuk, in meter vanaf de voet). Het script legt de weg op een vast punt op de kaart (in zee, zodat de
rit op Strava niet over een echte weg lijkt te gaan) en schrijft een GPX naar FietsApp/Assets/FietsApp/Routes/.
Er is geen hoogtebestand van het landschap; Unity maakt er verzonnen bergen omheen.

  python tools/fantasy_climb.py                 # alle fantasieklimmen
  python tools/fantasy_climb.py --only Bananenberg

Na afloop: in Unity menu FietsApp > Klimmen bijwerken.
"""
import argparse
import json
import math
import os
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROUTES = os.path.join(ROOT, "FietsApp", "Assets", "FietsApp", "Routes")
CATALOG = os.path.join(ROOT, "tools", "climbs.json")
EARTH_RADIUS = 6371000.0
STEP = 5.0  # m tussen de punten van de GPX


def trace(path, step=STEP):
    """De weg als lijst (x, z) in meter (oost, noord), elke `step` m een punt, beginnend in (0, 0) naar het noorden.

    Stukken: ["straight", lengte], ["arc", straal, graden] (positief = linksaf) en
    ["hairpins", aantal, beenlengte, straal, eerste kant] (kant 1 = eerste bocht linksaf, -1 = rechtsaf).
    """
    x, z, heading = 0.0, 0.0, math.pi / 2  # heading in radialen, tegen de klok in vanaf oost
    points = [(x, z)]

    def straight(length):
        nonlocal x, z
        n = max(1, round(length / step))
        for _ in range(n):
            x += math.cos(heading) * length / n
            z += math.sin(heading) * length / n
            points.append((x, z))

    def arc(radius, degrees):
        nonlocal x, z, heading
        turn = math.radians(degrees)
        n = max(1, round(abs(turn) * radius / step))
        side = 1 if turn > 0 else -1
        cx = x - side * radius * math.sin(heading)
        cz = z + side * radius * math.cos(heading)
        for i in range(1, n + 1):
            h = heading + turn * i / n
            points.append((cx + side * radius * math.sin(h), cz - side * radius * math.cos(h)))
        heading += turn
        x, z = points[-1]

    for part in path:
        kind = part[0]
        if kind == "straight":
            straight(part[1])
        elif kind == "arc":
            arc(part[1], part[2])
        elif kind == "hairpins":
            count, leg, radius, side = part[1:]
            for k in range(count):
                straight(leg)
                arc(radius, 180 * side)
                side = -side
            straight(leg)
        else:
            raise ValueError(f"onbekend stuk: {kind}")
    return points


def elevations(points, start_elevation, grades):
    """Hoogte per punt uit het profiel: [[tot_meter, helling_procent], ...]; na het laatste stuk de laatste helling."""
    result = [start_elevation]
    distance = 0.0
    for (ax, az), (bx, bz) in zip(points, points[1:]):
        length = math.hypot(bx - ax, bz - az)
        grade = next((g for until, g in grades if distance < until), grades[-1][1])
        distance += length
        result.append(result[-1] + length * grade / 100.0)
    return result, distance


def to_geo(points, origin):
    lat0, lon0 = origin
    per_degree = EARTH_RADIUS * math.pi / 180.0
    cos_lat = math.cos(math.radians(lat0))
    return [(lat0 + z / per_degree, lon0 + x / (per_degree * cos_lat)) for x, z in points]


def write_gpx(path, name, geo, heights):
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<gpx version="1.1" creator="FietsApp tools/fantasy_climb.py" xmlns="http://www.topografix.com/GPX/1/1">',
        "  <metadata>",
        f"    <name>{escape(name)}</name>",
        "    <desc>Fantasieklim: verzonnen weg en hoogte, geen echte plek.</desc>",
        "  </metadata>",
        "  <trk>",
        f"    <name>{escape(name)}</name>",
        "    <trkseg>",
    ]
    for (lat, lon), ele in zip(geo, heights):
        lines.append(f'      <trkpt lat="{lat:.6f}" lon="{lon:.6f}"><ele>{ele:.1f}</ele></trkpt>')
    lines += ["    </trkseg>", "  </trk>", "</gpx>", ""]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def build(climb):
    design = climb["design"]
    points = trace(design["path"])
    heights, length = elevations(points, design["start_elevation"], design["grades"])
    write_gpx(os.path.join(ROUTES, climb["id"] + ".gpx"), climb["name"], to_geo(points, design["origin"]), heights)
    gain = heights[-1] - heights[0]
    return f"{length / 1000:.1f} km, {heights[0]:.0f} -> {heights[-1]:.0f} m, gemiddeld {100 * gain / length:.1f}%"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", help="komma-gescheiden id's")
    args = parser.parse_args()

    climbs = [c for c in json.load(open(CATALOG, encoding="utf-8"))["climbs"] if "design" in c]
    if args.only:
        wanted = set(args.only.split(","))
        climbs = [c for c in climbs if c["id"] in wanted]
    for climb in climbs:
        print(f"{climb['name']}: {build(climb)}")


if __name__ == "__main__":
    main()
