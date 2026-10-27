"""Bouwt alle klimmen uit tools/climbs.json (story 4.5): weg (route_from_osm), hoogte en landschap (terrain_from_tiles),
en pakt elke klim in als klimpakket met catalogusregel (story 4.15, tools/climb_package.py).

Werkt in twee stappen, zodat de daglimiet van OpenTopoData (1000 verzoeken) niet opgaat aan verkeerde routes:

  python tools/build_climbs.py --check          # alleen de weg uit OSM; vergelijkt de lengte met de bekende lengte
  python tools/build_climbs.py                  # daarna: hoogtes en landschap voor de klimmen die nog geen pakket hebben
  python tools/build_climbs.py --packages       # alleen opnieuw inpakken, bijv. na een andere naam of begroeiing

Opties: --only id1,id2 voor een deel, --force om een bestaande klim opnieuw te maken. Klimmen met een pakket worden
alleen opnieuw ingepakt. Hoogtes worden bewaard in tools/.cache/ (niet in git), zodat een nieuwe poging geen quotum
kost. Een pakket dat inhoudelijk verandert krijgt een hogere versie; publiceren is de pakketten en catalogus.json
naar main pushen (story 4.16).
"""
import argparse
import json
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
from route_from_osm import (OVERPASS, bridge_structures, distance_m, fetch_elevations, fetch_structures,  # noqa: E402
                            http_json, osrm_path, repair_bumps, resample, route_path, trim_to_climb, write_gpx)
from terrain_from_tiles import build_terrain  # noqa: E402
from climb_package import WORK, load_catalog, package, package_all, package_path, save_catalog, work_paths  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
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


def route_problems(path, offsets, known):
    """Lengte van de route en wat er niet klopt aan start, finish of lengte."""
    length = sum(distance_m(a, b) for a, b in zip(path, path[1:])) / 1000.0
    problems = []
    if max(offsets) > 400:
        problems.append("start/finish " + "/".join(f"{o:.0f}" for o in offsets) + " m van de weg")
    if abs(length - known["length_km"]) > LENGTH_TOLERANCE * known["length_km"]:
        problems.append(f"lengte {length:.1f} km, bekend {known['length_km']} km")
    return length, problems


def find_route(climb, router):
    """
    De weg van de klim: eerst over wegnummer of filter via Overpass (zeker de juiste weg), en als dat mislukt of
    niet klopt via de OSRM-routeplanner (router "auto"). Een klim kan in de catalogus zelf "router" kiezen.
    Geeft (pad, lengte in km, problemen, bron).
    """
    router = climb.get("router", router)
    start, end = tuple(climb["start"]), tuple(climb["end"])
    via = [tuple(v) for v in climb.get("via", [])]
    attempts = []
    if router in ("auto", "overpass"):
        try:
            if climb.get("end_name"):
                end = find_named_point(climb["end_name"], end) or end
                time.sleep(1)
            selector = dict(ref=None, tag_filter=climb.get("filter") or ref_filter(climb["ref"]))
            path, offsets = route_path(bbox_around([start, end] + via), start, end, via,
                                       extra_ids=climb.get("ways", []), **selector)
            length, problems = route_problems(path, offsets, climb["known"])
            if not problems or router == "overpass":
                return path, length, problems, "overpass"
            attempts.append(("overpass", problems))
        except (SystemExit, Exception) as error:
            if router == "overpass":
                raise
            attempts.append(("overpass", [str(error)]))
    path, offsets = osrm_path(start, end, via, climb.get("osrm", "auto"))
    length, problems = route_problems(path, offsets, climb["known"])
    problems += [f"{source}: {'; '.join(p)}" for source, p in attempts] if problems else []
    return path, length, problems, "osrm"


def build(climb, check_only, force, cache, router="auto"):
    """Weg, hoogte en landschap van een klim in de werkmap; het inpakken doet main daarna."""
    gpx, terrain = work_paths(climb["id"])
    known = climb["known"]
    if os.path.exists(package_path(climb["id"])) and not force:
        return "bestaat"
    if os.path.exists(gpx) and not force:
        if not check_only and not os.path.exists(terrain):
            build_terrain(gpx, terrain)
        return "GPX bestaat"

    path, length, problems, source = find_route(climb, router)
    if check_only:
        return f"{length:.1f} km ({source})" + ("  !! " + "; ".join(problems) if problems else "  ok")
    if problems:
        return "overgeslagen: " + "; ".join(problems)

    points = resample(path, 20.0)
    elevations = fetch_elevations(points, "srtm30m", cache)
    if elevations[-1] < elevations[0]:  # start en finish omgedraaid opgegeven
        points, elevations = points[::-1], elevations[::-1]
    try:
        elevations, structures = bridge_structures(points, elevations, fetch_structures(bbox_around(points, 200)))
    except Exception as error:  # Overpass overbelast: dan alleen de bultenreparatie
        print(f"   tunnels en bruggen niet opgehaald: {error}")
        structures = 0
    elevations, repairs = repair_bumps(points, elevations)
    points, elevations = trim_to_climb(points, elevations)
    length = sum(distance_m(a, b) for a, b in zip(points, points[1:])) / 1000.0
    avg = (elevations[-1] - elevations[0]) / (length * 1000.0) * 100.0
    write_gpx(gpx, climb["name"], points, elevations, "srtm30m")
    build_terrain(gpx, terrain)
    note = "" if abs(avg - known["avg"]) <= GRADE_TOLERANCE else f"  !! gemiddeld {avg:.1f}%, bekend {known['avg']}%"
    fixed = f", {structures} tunnels/bruggen, {repairs} bulten"
    return f"{length:.1f} km, {elevations[0]:.0f} -> {elevations[-1]:.0f} m, {avg:.1f}%{fixed}{note}"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="alleen de weg controleren, geen hoogtes ophalen")
    parser.add_argument("--only", help="komma-gescheiden id's")
    parser.add_argument("--force", action="store_true", help="bestaande klim opnieuw maken")
    parser.add_argument("--packages", action="store_true", help="alleen opnieuw inpakken, zonder netwerk")
    parser.add_argument("--router", choices=["auto", "overpass", "osrm"], default="auto",
                        help="waar de weg vandaan komt (auto: Overpass, bij falen OSRM)")
    args = parser.parse_args()

    climbs = json.load(open(CATALOG, encoding="utf-8"))["climbs"]
    if args.only:
        wanted = set(args.only.split(","))
        climbs = [c for c in climbs if c["id"] in wanted]
    if args.packages:
        for cid, outcome in package_all(climbs):
            print(f"  {cid:28} {outcome}")
        return
    # Fantasieklimmen (met "design") hebben geen echte weg; die bouwt tools/fantasy_climb.py.
    climbs = [c for c in climbs if "design" not in c]
    os.makedirs(os.path.dirname(CACHE), exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    catalog = load_catalog()
    cache = json.load(open(CACHE, encoding="utf-8")) if os.path.exists(CACHE) else {}

    results = []
    for climb in climbs:
        print(f"== {climb['name']}")
        try:
            outcome = build(climb, args.check, args.force, cache, args.router)
            if not args.check and not outcome.startswith("overgeslagen"):
                outcome += "; " + package(climb, catalog)
                save_catalog(catalog)
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
