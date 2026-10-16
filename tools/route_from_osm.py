"""Maakt een GPX-klim uit OpenStreetMap-wegen en een open hoogtemodel (stories 4.1 en 4.4).

Werkwijze:
  1. Haalt alle wegen met een bepaald wegnummer (bijv. "D 974") in een gebied op via de Overpass API,
     of, als een klim geen doorlopend wegnummer heeft, de wegen die aan een Overpass-tagfilter voldoen
     (--filter, bijv. op straatnaam).
  2. Knoopt ze aan elkaar tot een netwerk en zoekt de kortste weg van start naar finish, eventueel via
     tussenpunten (--via) die de juiste weg afdwingen als er kortere zijwegen zijn.
  3. Bemonstert die weg om de --spacing meter.
  4. Haalt per punt de hoogte op bij OpenTopoData (standaard SRTM 30 m) en schrijft een GPX.

Waarom SRTM: op de Ventoux gaf EU-DEM 25 m langs de weg uitschieters tot 27% per 100 m (de weg ligt
uitgehakt in steile hellingen); SRTM 30 m bleef na dezelfde filtering onder 13% en is publiek domein.

De hoogtes worden ruw opgeslagen; gladstrijken gebeurt in de app (story 4.2), zodat elke bron dezelfde
behandeling krijgt. Alleen standaardbibliotheek, dus zonder installatie te draaien:

  python tools/route_from_osm.py --ref "D 974" --bbox 44.10,5.15,44.20,5.32 \\
      --start 44.1245,5.1793 --end 44.1740,5.2788 \\
      --name "Mont Ventoux (Bédoin)" --out FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx

Zie docs/bronnen.md voor de opdrachten per klim, ook met --highway en --via.

Bronnen en licenties: zie docs/bronnen.md. Wegen © OpenStreetMap-bijdragers (ODbL).
"""
import argparse
import heapq
import json
import math
import time
import urllib.error
import urllib.parse
import urllib.request
from xml.sax.saxutils import escape

OVERPASS = "https://overpass-api.de/api/interpreter"
OSRM = {  # dezelfde OSRM-software; de fietsversie neemt ook kasseienstroken waar auto's niet mogen
    "auto": "https://router.project-osrm.org/route/v1/driving",
    "fiets": "https://routing.openstreetmap.de/routed-bike/route/v1/driving",
}
OPENTOPODATA ="https://api.opentopodata.org/v1/{dataset}"
USER_AGENT = "FietsApp-hobbyproject (github.com/SvenvanDalen/Fietsen_Indoor_Alternative)"
EARTH_RADIUS = 6371000.0


def http_json(url, data=None, attempts=4):
    """POST/GET met een paar nieuwe pogingen: Overpass en OpenTopoData zijn gratis en soms druk (502/429)."""
    body = urllib.parse.urlencode(data).encode() if data else None
    for attempt in range(1, attempts + 1):
        request = urllib.request.Request(url, data=body, headers={"User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if error.code not in (429, 502, 503, 504) or attempt == attempts:
                raise
            wait = 5 * attempt
            print(f"  {url} gaf {error.code}; opnieuw over {wait} s")
            time.sleep(wait)
        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:  # netwerk even weg
            if attempt == attempts:
                raise
            wait = 30 * attempt
            print(f"  {url} niet bereikbaar ({error}); opnieuw over {wait} s")
            time.sleep(wait)


def distance_m(a, b):
    """Afstand over de bol (haversine) tussen (lat, lon)-paren."""
    lat1, lon1, lat2, lon2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lon2 - lon1) / 2) ** 2
    return 2 * EARTH_RADIUS * math.asin(math.sqrt(h))


def fetch_ways(bbox, ref=None, tag_filter=None, extra_ids=()):
    """Wegen met wegnummer ref, of anders de wegen die aan een Overpass-tagfilter voldoen, plus losse wegen
    op OSM-id (voor een naamloos verbindingsstuk)."""
    selector = f'["ref"="{ref}"]' if ref else f'["highway"]{tag_filter}'
    extra = f'way(id:{",".join(map(str, extra_ids))});' if extra_ids else ""
    query = f'[out:json][timeout:90];(way{selector}({bbox});{extra});out geom;'
    ways = http_json(OVERPASS, {"data": query})["elements"]
    if not ways:
        raise SystemExit(f"Geen wegen gevonden met {selector} in {bbox}")
    return ways


def build_graph(ways):
    """Knooppunten (OSM-node-id) met coördinaten en buren; tweerichtingen, want we fietsen omhoog."""
    coords, edges = {}, {}
    for way in ways:
        ids, geometry = way["nodes"], way["geometry"]
        for node_id, point in zip(ids, geometry):
            coords[node_id] = (point["lat"], point["lon"])
        for a, b in zip(ids, ids[1:]):
            d = distance_m(coords[a], coords[b])
            edges.setdefault(a, []).append((b, d))
            edges.setdefault(b, []).append((a, d))
    return coords, edges


def components(coords, edges):
    label = {}
    for node in coords:
        if node in label:
            continue
        label[node] = node
        stack = [node]
        while stack:
            current = stack.pop()
            for neighbour, _ in edges.get(current, []):
                if neighbour not in label:
                    label[neighbour] = node
                    stack.append(neighbour)
    return label


def join_gaps(coords, edges, tolerance):
    """
    Verbindt losse delen van het netwerk die dicht bij elkaar liggen. Bij rotondes en kruisingen sluiten
    OSM-wegen met hetzelfde wegnummer vaak niet op hetzelfde knooppunt aan. Alleen tussen verschillende
    delen, zodat er geen sluiproutes over haarspeldbochten ontstaan.
    """
    label = components(coords, edges)
    nodes = list(coords)
    joined = 0
    for i, a in enumerate(nodes):
        for b in nodes[i + 1:]:
            if label[a] == label[b]:
                continue
            d = distance_m(coords[a], coords[b])
            if d <= tolerance:
                edges.setdefault(a, []).append((b, d))
                edges.setdefault(b, []).append((a, d))
                joined += 1
    return joined


def nearest_node(coords, point):
    return min(coords, key=lambda n: distance_m(coords[n], point))


def shortest_path(edges, start, end):
    """Dijkstra over het wegennetwerk."""
    best = {start: 0.0}
    previous = {}
    queue = [(0.0, start)]
    while queue:
        dist, node = heapq.heappop(queue)
        if node == end:
            break
        if dist > best.get(node, math.inf):
            continue
        for neighbour, length in edges.get(node, []):
            candidate = dist + length
            if candidate < best.get(neighbour, math.inf):
                best[neighbour] = candidate
                previous[neighbour] = node
                heapq.heappush(queue, (candidate, neighbour))
    if end not in previous and end != start:
        raise SystemExit("Geen verbinding tussen start en finish in deze wegen.")
    path = [end]
    while path[-1] != start:
        path.append(previous[path[-1]])
    return path[::-1]


def resample(points, spacing):
    """Punten op gelijke afstand langs de lijn (lineair tussen OSM-punten)."""
    result = [points[0]]
    carried = 0.0
    for a, b in zip(points, points[1:]):
        segment = distance_m(a, b)
        position = spacing - carried
        while position <= segment:
            t = position / segment
            result.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            position += spacing
        carried = segment - (position - spacing)
    if distance_m(result[-1], points[-1]) > spacing * 0.25:
        result.append(points[-1])
    return result


def fetch_elevations(points, dataset, cache=None):
    """OpenTopoData: maximaal 100 punten per verzoek, 1 verzoek per seconde en 1000 per dag. Met een cache
    (dict van "lat,lon" naar hoogte) worden al bekende punten niet opnieuw gevraagd."""
    cache = {} if cache is None else cache
    key = lambda p: f"{dataset}:{p[0]:.6f},{p[1]:.6f}"
    missing = [p for p in points if key(p) not in cache]
    for i in range(0, len(missing), 100):
        batch = missing[i:i + 100]
        locations = "|".join(f"{lat:.6f},{lon:.6f}" for lat, lon in batch)
        answer = http_json(OPENTOPODATA.format(dataset=dataset), {"locations": locations})
        if answer.get("status") != "OK":
            raise SystemExit(f"OpenTopoData gaf een fout: {answer}")
        for p, r in zip(batch, answer["results"]):
            cache[key(p)] = r["elevation"]
        time.sleep(1.1)
    elevations = [cache[key(p)] for p in points]
    if any(e is None for e in elevations):
        raise SystemExit("Hoogtemodel heeft gaten op deze route; probeer een andere --dataset.")
    return elevations


def fetch_structures(bbox):
    """Tunnels, galerijen en bruggen van wegen in het gebied, als lijsten (lat, lon)."""
    query = (f'[out:json][timeout:90];(way["highway"]["tunnel"]["tunnel"!="no"]({bbox});'
             f'way["highway"]["covered"="yes"]({bbox});way["highway"]["bridge"]["bridge"!="no"]({bbox}););out geom;')
    return [[(g["lat"], g["lon"]) for g in w["geometry"]] for w in http_json(OVERPASS, {"data": query})["elements"]]


def bridge_structures(points, elevations, structures, near=15.0):
    """
    Trekt een rechte lijn door het hoogteprofiel waar de route door een tunnel of over een brug gaat: het
    hoogtemodel meet daar de berg erboven of het dal eronder. Geeft (hoogtes, aantal overbrugde stukken).
    """
    lat0 = points[0][0]
    kx = 111195.0 * math.cos(math.radians(lat0))
    xy = lambda p: (p[1] * kx, p[0] * 111195.0)
    segments = [(xy(a), xy(b)) for s in structures for a, b in zip(s, s[1:])]

    def on_structure(p):
        px, py = xy(p)
        for (ax, ay), (bx, by) in segments:
            dx, dy = bx - ax, by - ay
            t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy or 1.0)))
            if math.hypot(ax + t * dx - px, ay + t * dy - py) <= near:
                return True
        return False

    marked = [on_structure(p) for p in points]
    d = [0.0]
    for a, b in zip(points, points[1:]):
        d.append(d[-1] + distance_m(a, b))
    result, count, i = list(elevations), 0, 0
    while i < len(points):
        if not marked[i]:
            i += 1
            continue
        j = i
        while j + 1 < len(points) and marked[j + 1]:
            j += 1
        a, b = max(0, i - 1), min(len(points) - 1, j + 1)  # de punten net voor en na het bouwwerk
        if b > a + 1:
            for k in range(a + 1, b):
                result[k] = result[a] + (result[b] - result[a]) * (d[k] - d[a]) / (d[b] - d[a])
            count += 1
        i = j + 1
    return result, count


def repair_bumps(points, elevations, steep=0.15, span=60.0, gap=800.0):
    """
    Haalt tunnels en bruggen uit het hoogteprofiel. Het hoogtemodel meet daar de berg boven de tunnel of het dal
    onder de brug: een steile bult of kuil van tientallen tot honderden meters. Een echte weg gaat niet binnen
    800 m steil op en weer steil af, dus zo'n paar wordt vervangen door een rechte lijn tussen de punten ervoor
    en erna. Steile stukken die alleen omhoog gaan (Angliru) blijven staan. Geeft (hoogtes, aantal reparaties).
    """
    d = [0.0]
    for a, b in zip(points, points[1:]):
        d.append(d[-1] + distance_m(a, b))
    n = len(points)
    slope = [0] * n  # +1 steil omhoog, -1 steil omlaag, over de komende span meter
    j = 0
    for i in range(n):
        while j < n - 1 and d[j] - d[i] < span:
            j += 1
        if d[j] > d[i]:
            grade = (elevations[j] - elevations[i]) / (d[j] - d[i])
            slope[i] = 1 if grade > steep else -1 if grade < -steep else 0
    runs = []  # (richting, eerste, laatste index) van aaneengesloten steile stukken
    for i, s in enumerate(slope):
        if s and runs and runs[-1][0] == s and d[i] - d[runs[-1][2]] <= 2 * span:  # korte onderbreking hoort erbij
            runs[-1][2] = i
        elif s:
            runs.append([s, i, i])
    result, repairs, k = list(elevations), 0, 0
    while k < len(runs) - 1:
        first, second = runs[k], runs[k + 1]
        if first[0] != second[0] and d[second[1]] - d[first[2]] <= gap:
            a = first[1]
            b = second[2]
            while b < n - 1 and d[b] - d[second[2]] < span:  # tot na de helling
                b += 1
            for i in range(a + 1, b):
                result[i] = result[a] + (result[b] - result[a]) * (d[i] - d[a]) / (d[b] - d[a])
            repairs += 1
            k += 2
        else:
            k += 1
    return result, repairs


def trim_to_climb(points, elevations, reach=1500.0):
    """Begin op het laagste punt in de eerste reach meter en eindig op het hoogste in de laatste: een klim
    begint niet met een afdaling uit het dorp en eindigt niet voorbij de top."""
    d = [0.0]
    for a, b in zip(points, points[1:]):
        d.append(d[-1] + distance_m(a, b))
    head = [i for i in range(len(points)) if d[i] <= reach]
    tail = [i for i in range(len(points)) if d[-1] - d[i] <= reach]
    start = min(head, key=lambda i: elevations[i])
    end = max(tail, key=lambda i: elevations[i])
    return points[start:end + 1], elevations[start:end + 1]


def write_gpx(path, name, points, elevations, dataset):
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<gpx version="1.1" creator="FietsApp tools/route_from_osm.py" xmlns="http://www.topografix.com/GPX/1/1">',
        "  <metadata>",
        f"    <name>{escape(name)}</name>",
        f"    <desc>Weg: © OpenStreetMap-bijdragers (ODbL). Hoogte: {dataset} via OpenTopoData, ruw.</desc>",
        "  </metadata>",
        "  <trk>",
        f"    <name>{escape(name)}</name>",
        "    <trkseg>",
    ]
    for (lat, lon), ele in zip(points, elevations):
        lines.append(f'      <trkpt lat="{lat:.6f}" lon="{lon:.6f}"><ele>{ele:.1f}</ele></trkpt>')
    lines += ["    </trkseg>", "  </trk>", "</gpx>", ""]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def route_path(bbox, start, end, via=(), ref=None, tag_filter=None, extra_ids=(), join=20.0):
    """De weg van start via de tussenpunten naar de finish als lijst (lat, lon), plus per stop hoe ver
    (m) het opgegeven punt van de weg lag; groot betekent meestal een verkeerd punt of de verkeerde weg."""
    ways = fetch_ways(bbox, ref=ref, tag_filter=tag_filter, extra_ids=extra_ids)
    coords, edges = build_graph(ways)
    join_gaps(coords, edges, join)
    stops = [start] + list(via) + [end]
    nodes = [nearest_node(coords, p) for p in stops]
    route = [nodes[0]]
    for a, b in zip(nodes, nodes[1:]):
        route += shortest_path(edges, a, b)[1:]
    return [coords[n] for n in route], [distance_m(coords[n], p) for n, p in zip(nodes, stops)]


def osrm_path(start, end, via=(), profile="auto"):
    """
    Dezelfde vorm als route_path, maar via de OSRM-routeplanner (ook OpenStreetMap-wegen, ODbL). Snel en
    betrouwbaarder dan Overpass als die overbelast is; kiest wel de snelste weg voor een auto, dus
    tussenpunten (via) moeten de klimweg afdwingen. Offsets: hoe ver elk punt van de weg lag.
    """
    stops = [start] + list(via) + [end]
    coordinates = ";".join(f"{lon:.6f},{lat:.6f}" for lat, lon in stops)
    answer = http_json(f"{OSRM[profile]}/{coordinates}?overview=full&geometries=geojson")
    if answer.get("code") != "Ok":
        raise SystemExit(f"OSRM gaf geen route: {answer.get('code')}")
    path = [(lat, lon) for lon, lat in answer["routes"][0]["geometry"]["coordinates"]]
    return path, [w["distance"] for w in answer["waypoints"]]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    which = parser.add_mutually_exclusive_group(required=True)
    which.add_argument("--ref", help='wegnummer in OSM, bijv. "D 974"')
    which.add_argument("--filter", help="Overpass-tagfilter als er geen doorlopend wegnummer is, bijv. '[\"name\"~\"Finestre\"]'")
    parser.add_argument("--bbox", required=True, help="zuid,west,noord,oost")
    parser.add_argument("--start", required=True, help="lat,lon van de start")
    parser.add_argument("--end", required=True, help="lat,lon van de finish")
    parser.add_argument("--via", action="append", default=[], help="lat,lon van een tussenpunt; mag vaker")
    parser.add_argument("--way", action="append", type=int, default=[], help="OSM-id van een extra weg; mag vaker")
    parser.add_argument("--name", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--spacing", type=float, default=20.0, help="meter tussen punten")
    parser.add_argument("--dataset", default="srtm30m", help="OpenTopoData-dataset, bijv. srtm30m of eudem25m")
    parser.add_argument("--join", type=float, default=20.0, help="losse wegdelen binnen zoveel meter verbinden")
    args = parser.parse_args()

    start = tuple(map(float, args.start.split(",")))
    end = tuple(map(float, args.end.split(",")))
    via = [tuple(map(float, v.split(","))) for v in args.via]

    path, offsets = route_path(args.bbox, start, end, via, ref=args.ref, tag_filter=args.filter,
                               extra_ids=args.way, join=args.join)
    print("afstand van start, tussenpunten en finish tot de weg: " + ", ".join(f"{o:.0f}" for o in offsets) + " m")
    length = sum(distance_m(a, b) for a, b in zip(path, path[1:]))
    points = resample(path, args.spacing)
    print(f"route {length / 1000:.2f} km, {len(points)} punten; hoogtes ophalen ({args.dataset})...")

    elevations = fetch_elevations(points, args.dataset)
    write_gpx(args.out, args.name, points, elevations, args.dataset)
    print(f"van {elevations[0]:.0f} m naar {elevations[-1]:.0f} m; geschreven naar {args.out}")


if __name__ == "__main__":
    main()
