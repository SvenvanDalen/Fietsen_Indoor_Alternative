# Besluiten

Vastgelegd op 2026-10-15, uit de antwoorden van de eigenaar (bron: [backlog.md](backlog.md), sectie 2).

| Onderwerp | Besluit |
|---|---|
| Doel | Gratis hobbyproject, voorlopig solo |
| Belangrijkste reden | Meer klimmen |
| Trainer | FitBike Smart Drive 50 (direct drive, Bluetooth en ANT+, max. 20% helling, 1200 W) |
| Sensoren | ANT+ en Bluetooth |
| Platform | Windows eerst, daarna telefoon (Android, via Bluetooth) |
| Engine | Unity (C#), gebouwd met Claude en Unity-MCP |
| Klimmen | Mix van echte en fantasieklimmen; vier echte iconen eerst: Stelvio, Alpe d'Huez, Mont Ventoux, Colle delle Finestre |
| Wereld | Eén doorlopende wereld, low-poly, een verzonnen landschap met nagebouwde klimmen |
| Afdalingen | Zelf rijden |
| Ondergronden | Kassei, grind, sneeuw: alleen beeld en extra weerstand, geen gevoel via de trainer |
| GPX-import door gebruikers | Nog niet |
| Hellingsvolging | De trainer volgt de helling; gevoeligheid per klim instelbaar in het menu, niet tijdens de rit |
| Training | Workouts en FTP-test vanaf het begin; adaptief coachplan nog niet |
| Versnellingen | Geen virtuele versnellingen, vaste weerstand zoals MyWhoosh |
| Multiplayer | Voorlopig solo |
| Leaderboards, ghosts, badges | Ja |
| Ranglijst-backend | Gratis via GitHub (Action en Pages), niet vals-spel-bestendig, prima voor een hobbyproject |
| Strava | Ja |
| Garmin Connect, Intervals.icu | Automatisch waar mogelijk |
| Budget | Geen, alles gratis |

## Later vastgelegd

| Datum | Onderwerp | Besluit | Reden |
|---|---|---|---|
| 2026-10-15 | Bluetooth LE op Windows | Eigen .NET-brug (`Bridge/FietsBridge`): de Windows-Bluetooth-API in een apart programma, dat JSON via een lokale WebSocket (`ws://localhost:8765/`) aan Unity geeft | Volledig eigen, testbare code; later ook de plek voor ANT+. Alternatief BleWinrtDll (C++-DLL, WTFPL) afgewezen vanwege een vooraf gebouwde DLL van derden en een beperkte poll-API |
| 2026-10-15 | Draaien op de achtergrond | `Run In Background` aan | Een rit mag niet stilvallen als je een ander venster aanklikt |
| 2026-10-15 | Klimmen namaken | Weg uit OpenStreetMap (wegnummer), hoogte langs de weg uit SRTM 30 m, landschap uit open Terrain Tiles; alles via scripts in `tools/` | Alleen open data; herhaalbaar voor elke klim; bronnen in `docs/bronnen.md` |
| 2026-10-15 | Gladstrijken hoogteprofiel | Mediaan (±100 m) gevolgd door Gauss (σ 80 m), met puntgespiegelde randen | Mediaan haalt uitschieters van het hoogtemodel weg zonder de helling af te vlakken; spiegelen houdt begin en eind van een klim kloppend |
| 2026-10-16 | Gladstrijken hoogteprofiel (herzien) | Gauss σ 150 m in plaats van 80 m | In haarspeldbochten pikken alle hoogtemodellen (SRTM, EU-DEM, Terrain Tiles) hoogtes van de bocht erboven of eronder op; op Alpe d'Huez gaf dat 16-17% per 100 m direct gevolgd door 4-7%. Met σ 150 m: steilste 100 m 13,9%, steilste km 10,7% (echt ±10,5%); Mont Ventoux blijft vrijwel gelijk (steilste km 11,0%) |
| 2026-10-15 | Mont Ventoux | Vanaf Bédoin (zuidkant) als eerste | De klassieke beklimming; Malaucène en Sault kunnen later met dezelfde scripts |
| 2026-10-16 | Klimcategorieën | Score = lengte (m) × gemiddelde helling (%): cat. 4 vanaf 8000, cat. 3 vanaf 16000, cat. 2 vanaf 32000, cat. 1 vanaf 64000, HC vanaf 80000; onder 3% gemiddeld geen categorie | Eenvoudige, bekende indeling uit fietsapps die past bij de backlog (lengte × helling); Mont Ventoux wordt HC, Alpe d'Huez ook |
| 2026-10-16 | Issues sluiten | Een story gaat dicht zodra hij gebouwd, getest en gemerged is volgens zijn criteria; de controle op de echte trainer gebeurt samen in 1.8 | Op verzoek van de eigenaar: de trainer is nog niet te testen, en open issues voor gebouwd werk maken de voortgang onleesbaar |
| 2026-10-16 | Klimroutes uit OSRM | De weg komt standaard uit de OSRM-routeplanner (OSM-data, ODbL) met start, finish en tussenpunten uit `tools/climbs.json`; Overpass op wegnummer blijft mogelijk | Overpass was dagenlang overbelast (502/504); OSRM antwoordt binnen een seconde. Een routeplanner kiest de snelste weg, dus de wegcontrole tegen de bekende lengte en tussenpunten houden de klimweg vast |
| 2026-10-16 | Tunnels en bruggen in het hoogteprofiel | Rechte lijn tussen begin en eind van elke tunnel en brug uit OSM; daarnaast steil op-en-af binnen 800 m wegwerken | Het hoogtemodel meet in een tunnel de berg erboven (Grimsel: bult van 200 m, Croix de Fer: 30% per 100 m); echte steilte die alleen omhoog gaat (Angliru) blijft staan |
| 2026-10-16 | Rijdersprofiel | Lokaal `profiel.json` in de app-map met FTP (standaard 200 W); accounts en cloudprofiel later bij E7 | Zones (8.5), FTP-test (8.1) en workouts (8.2) hebben een FTP nodig, accounts zijn V1 |
| 2026-10-16 | Vermogenszones | Coggan, 7 zones (grenzen 55/75/90/105/120/150% FTP) op het 3-secondenvermogen; kleuren grijs, blauw, groen, geel, oranje, rood, paars | De gangbare indeling in trainingsapps; 3 s middelt losse trappen uit zodat de kleur niet knippert |
| 2026-10-17 | FTP-test | Twee varianten: ramptest (ERG vanaf 100 W, +20 W per minuut, FTP = 75% van de beste minuut) en 20-minutentest (FTP = 95% van het gemiddelde); de rijder kiest na afloop of de uitslag in het profiel komt | De twee gangbare tests; de ramp is kort en vraagt geen tempogevoel, de 20 minuten is nauwkeuriger voor wie hem goed indeelt |
| 2026-10-17 | Weerstandsgevoeligheid | De trainer krijgt helling × 50-150% (stappen van 25), per klim onthouden in het profiel en in te stellen in het ritmenu; de fysica rekent met de echte helling | Zo voelt een klim lichter of zwaarder, maar blijven snelheid en klimtijden eerlijk vergelijkbaar, zoals "trainer difficulty" in andere apps |
| 2026-10-17 | Doorlopende wereld | De wereld is een wegennet: per gebied een dal met een knooppunt, verzonnen dalwegen naar de voet van elke klim, en wegen tussen de dalen. Alleen de corridor rond de weg die je rijdt wordt gebouwd (terrein langs de route, zoals al besloten). Elke klim houdt zijn echte hellingen en landschap, maar wordt in hoogte verschoven zodat de voet op de hoogte van het dal ligt; HUD en boomgrens gebruiken de echte hoogte | 55 echte landschappen passen niet als één aaneengesloten kaart (ze liggen in verschillende landen en op heel verschillende hoogtes); zo voelt het als één wereld zonder laadschermen, en de klimmen blijven echt |
