"""Bouwt alle klimmen uit tools/climbs.json (story 4.5): weg (route_from_osm), hoogte en landschap (terrain_from_tiles).

Werkt in twee stappen, zodat de daglimiet van OpenTopoData (1000 verzoeken) niet opgaat aan verkeerde routes:

  python tools/build_climbs.py --check          # alleen de weg uit OSM; vergelijkt de lengte met de bekende lengte
  python tools/build_climbs.py                  # daarna: hoogtes en landschap voor de klimmen die nog geen GPX hebben

Opties: --only id1,id2 voor een deel, --force om een bestaande GPX opnieuw te maken. Bestaande bestanden worden
overgeslagen. Hoogtes worden bewaard in tools/.cache/ (niet in git), zodat een nieuwe poging geen quotum kost.
Na afloop: in Unity menu FietsApp > Klimmen bijwerken maakt de Climb-assets en de bibliotheek.
"""
import argparse
import json
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
from route_from_osm import (OVERPASS, distance_m, fetch_elevations, http_json, resample, route_path,  # noqa: E402
                            write_gpx)
from terrain_from_tiles import build_terrain  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROUTES = os.path.join(ROOT, "FietsApp", "Assets", "FietsApp", "Routes")
CATALOG = os.path.join(ROOT, "tools", "climbs.json")
CACHE = os.path.join(ROOT, "tools", ".cache", "elevations.json")
LENGTH_TOLERANCE = 0.15  # 15% verschil met de bekende lengte mag (andere start- of finishplek)
GRADE_TOLERANCE = 1.0    # procentpunt


def ref_filter(refs):
    """Wegnummers als Overpass-filter; spatie optioneel ("D 902" = "D902") en ook in lijsten als "D 902;N 91"."""
    alternatives = "|".join(r.replace(" ", " ?") for r in refs)
    return f'["ref"~"(^|;) *({alternatives}) *(;|$)"]'


def bbox_around(points, margin_m=5000.0):
    lats = [p[0] for p in points]
    lons = [p[1] for p in points]
    dlat = margin_m / 111195.0
    dlon = margin_m / (111195.0 * math.cos(math.radians(sum(lats) / len(lats))))
    return f"{min(lats) - dlat:.4f},{min(lons) - dlon:.4f},{max(lats) + dlat:.4f},{max(lons) + dlon:.4f}"


def find_named_point(name, near, radius_m=4000):
    """Pas, top of plek met deze naam in OSM, het dichtst bij near; None als er niets is."""
    query = (f'[out:json][timeout:60];node["name"="{name}"](around:{radius_m:.0f},{near[0]},{near[1]});out;')
    nodes = http_json(OVERPASS, {"data": query})["elements"]
    if not nodes:
        return None
    best = min(nodes, key=lambda n: distance_m((n["lat"], n["lon"]), near))
    return best["lat"], best["lon"]


def build(climb, check_only, force, cache):
    gpx = os.path.join(ROUTES, climb["id"] + ".gpx")
    terrain = os.path.join(ROUTES, climb["id"] + ".terrain.bytes")
    known = climb["known"]
    if os.path.exists(gpx) and not force:
        if not check_only and not os.path.exists(terrain):
            build_terrain(gpx, terrain)
        return "bestaat"

    start, end = tuple(climb["start"]), tuple(climb["end"])
    if climb.get("end_name"):
        found = find_named_point(climb["end_name"], end)
        if found:
            end = found
        time.sleep(1)
    via = [tuple(v) for v in climb.get("via", [])]
    selector = dict(ref=None, tag_filter=climb.get("filter") or ref_filter(climb["ref"]))
    path, offsets = route_path(bbox_around([start, end] + via), start, end, via, extra_ids=climb.get("ways", []), **selector)
    length = sum(distance_m(a, b) for a, b in zip(path, path[1:])) / 1000.0
    problems = []
    if max(offsets) > 400:
        problems.append("start/finish " + "/".join(f"{o:.0f}" for o in offsets) + " m van de weg")
    if abs(length - known["length_km"]) > LENGTH_TOLERANCE * known["length_km"]:
        problems.append(f"lengte {length:.1f} km, bekend {known['length_km']} km")
    if check_only:
        return f"{length:.1f} km" + ("  !! " + "; ".join(problems) if problems else "  ok")
    if problems:
        return "overgeslagen: " + "; ".join(problems)

    points = resample(path, 20.0)
    elevations = fetch_elevations(points, "srtm30m", cache)
    if elevations[-1] < elevations[0]:  # start en finish omgedraaid opgegeven
        points, elevations = points[::-1], elevations[::-1]
    avg = (elevations[-1] - elevations[0]) / (length * 1000.0) * 100.0
    write_gpx(gpx, climb["name"], points, elevations, "srtm30m")
    build_terrain(gpx, terrain)
    note = "" if abs(avg - known["avg"]) <= GRADE_TOLERANCE else f"  !! gemiddeld {avg:.1f}%, bekend {known['avg']}%"
    return f"{length:.1f} km, {elevations[0]:.0f} -> {elevations[-1]:.0f} m, {avg:.1f}%{note}"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="alleen de weg controleren, geen hoogtes ophalen")
    parser.add_argument("--only", help="komma-gescheiden id's")
    parser.add_argument("--force", action="store_true", help="bestaande GPX opnieuw maken")
    args = parser.parse_args()

    climbs = json.load(open(CATALOG, encoding="utf-8"))["climbs"]
    if args.only:
        wanted = set(args.only.split(","))
        climbs = [c for c in climbs if c["id"] in wanted]
    os.makedirs(os.path.dirname(CACHE), exist_ok=True)
    cache = json.load(open(CACHE, encoding="utf-8")) if os.path.exists(CACHE) else {}

    results = []
    for climb in climbs:
        print(f"== {climb['name']}")
        try:
            outcome = build(climb, args.check, args.force, cache)
        except (SystemExit, Exception) as error:  # één klim mag de rest niet tegenhouden
            outcome = f"FOUT: {error}"
        finally:
            with open(CACHE, "w", encoding="utf-8") as f:
                json.dump(cache, f)
        print("   " + outcome)
        results.append((climb["id"], outcome))
        time.sleep(1)

    print("\nOverzicht:")
    for cid, outcome in results:
        print(f"  {cid:28} {outcome}")


if __name__ == "__main__":
    main()
