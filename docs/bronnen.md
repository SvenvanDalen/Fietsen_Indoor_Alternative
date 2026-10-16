# Bronnen en licenties

Herkomst en licentie van alle route-, hoogte- en terreindata (story 13.5). Voeg bij elke nieuwe klim een regel toe; neem alleen open data op (backlog sectie 4, "Bronnen en rechten").

## Klimmen

| Klim | Bestand | Weg | Hoogte langs de weg | Landschap | Gemaakt met |
|---|---|---|---|---|---|
| Mont Ventoux vanaf Bédoin | `FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 974` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-15 |
| Alpe d'Huez | `FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 211` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-16 |
| Colle delle Finestre vanaf Susa | `FietsApp/Assets/FietsApp/Routes/Finestre_Susa.gpx`, `.terrain.bytes` | OpenStreetMap, wegen "Via Meana", "Via Colle delle Finestre", "Strada Provinciale del Colle delle Finestre" en naamloos verbindingsstuk way 330898189 (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-16 |
| Passo dello Stelvio vanaf Prato | `FietsApp/Assets/FietsApp/Routes/Stelvio_Prato.gpx`, `.terrain.bytes` | OpenStreetMap, weg `SS38` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-16 |
| Mont Ventoux vanaf Sault | `FietsApp/Assets/FietsApp/Routes/MontVentoux_Sault.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 164`, `D 974` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/build_climbs.py` op 2026-10-16 |
| Col de la Colombière vanaf Scionzier | `FietsApp/Assets/FietsApp/Routes/Colombiere.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 4` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/build_climbs.py` op 2026-10-16 |
| Col de Joux Plane vanaf Samoëns | `FietsApp/Assets/FietsApp/Routes/JouxPlane.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 354` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/build_climbs.py` op 2026-10-16 |
| Grand Colombier vanaf Culoz | `FietsApp/Assets/FietsApp/Routes/GrandColombier_Culoz.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 120` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/build_climbs.py` op 2026-10-16 |
| Col du Granon vanaf Saint-Chaffrey | `FietsApp/Assets/FietsApp/Routes/Granon.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 234`, `D 234t` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/build_climbs.py` op 2026-10-16 |

Klimmen uit de catalogus `tools/climbs.json` maak je opnieuw met `python tools/build_climbs.py --force --only <id>`; de klimmen hieronder met losse opdrachten.

Opnieuw maken:

```
python tools/route_from_osm.py --ref "D 974" --bbox 44.10,5.15,44.20,5.32 --start 44.1245,5.1793 --end 44.1740,5.2788 --name "Mont Ventoux (Bédoin)" --out FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx --out FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.terrain.bytes

python tools/route_from_osm.py --ref "D 211" --bbox 45.04,6.00,45.11,6.09 --start 45.0594,6.0379 --end 45.0909,6.0654 --name "Alpe d'Huez" --out FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx --out FietsApp/Assets/FietsApp/Routes/AlpeDHuez.terrain.bytes

python tools/route_from_osm.py --ref "SS38" --bbox 46.52,10.44,46.65,10.62 --start 46.6195,10.5925 --end 46.5287,10.4530 --name "Passo dello Stelvio (Prato)" --out FietsApp/Assets/FietsApp/Routes/Stelvio_Prato.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/Stelvio_Prato.gpx --out FietsApp/Assets/FietsApp/Routes/Stelvio_Prato.terrain.bytes

python tools/route_from_osm.py --filter '["name"~"Colle delle Finestre|^Via Meana$"]' --way 330898189 --bbox 45.06,7.00,45.15,7.10 --start 45.1341,7.0607 --end 45.0726,7.0537 --name "Colle delle Finestre (Susa)" --out FietsApp/Assets/FietsApp/Routes/Finestre_Susa.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/Finestre_Susa.gpx --out FietsApp/Assets/FietsApp/Routes/Finestre_Susa.terrain.bytes
```

Alpe d'Huez loopt hier van het begin van de D 211 tot het dorp (12,9 km, 723 → 1791 m). De bekende 13,8 km eindigt bij de Tour-finish, via straten van het skidorp die geen D 211 heten.

De Colle delle Finestre heeft geen doorlopend wegnummer in OSM (alleen het stuk bij de top heet SP172); daarom selecteert `--filter` op straatnaam en voegt `--way` het naamloze verbindingsstuk toe. Het onverharde stuk (in OSM `surface=compacted`) begint 10,48 km na de start, op 1457 m; dat staat als `unpavedFrom` in `Finestre_Susa.asset`.

## Licenties en naamsvermelding

- **OpenStreetMap**: © OpenStreetMap-bijdragers, Open Database License (ODbL). De GPX-bestanden zijn afgeleid van OSM-data en vallen daarom ook onder de ODbL. In de app vermelden: "Wegen © OpenStreetMap-bijdragers".
- **SRTM** (NASA/USGS): publiek domein. Opgehaald via de gratis OpenTopoData-API (max. 100 punten per verzoek, 1 per seconde, 1000 per dag).
- **Terrain Tiles** (AWS Open Data, oorspronkelijk Mapzen/Tilezen): samengesteld uit o.a. SRTM, GMTED en EU-DEM. Naamsvermelding volgens de Tilezen-richtlijnen; voor Europa onder meer "Produced using Copernicus data and information funded by the European Union – EU-DEM layers".

## Bewust niet gebruikt

- **EU-DEM 25 m voor de hoogte langs de weg**: gaf op de Ventoux uitschieters tot 27% per 100 m, omdat de weg in steile hellingen is uitgehakt; SRTM bleef na dezelfde filtering onder 13%. Op Alpe d'Huez was EU-DEM na filtering ook steiler dan SRTM (17,9% tegen 16,6% per 100 m, bij σ 80 m).
- **Strava-segmenten en commerciële kaartdata**: niet toegestaan zonder toestemming (backlog sectie 4).
