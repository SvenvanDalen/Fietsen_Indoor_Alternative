# Bronnen en licenties

Herkomst en licentie van alle route-, hoogte- en terreindata (story 13.5). Voeg bij elke nieuwe klim een regel toe; neem alleen open data op (backlog sectie 4, "Bronnen en rechten").

## Klimmen

| Klim | Bestand | Weg | Hoogte langs de weg | Landschap | Gemaakt met |
|---|---|---|---|---|---|
| Mont Ventoux vanaf Bédoin | `FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 974` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-15 |
| Alpe d'Huez | `FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx`, `.terrain.bytes` | OpenStreetMap, weg `D 211` (ODbL) | SRTM 30 m via OpenTopoData (publiek domein) | Terrain Tiles, AWS Open Data (Terrarium, zoom 13) | `tools/route_from_osm.py`, `tools/terrain_from_tiles.py` op 2026-10-16 |

Opnieuw maken:

```
python tools/route_from_osm.py --ref "D 974" --bbox 44.10,5.15,44.20,5.32 --start 44.1245,5.1793 --end 44.1740,5.2788 --name "Mont Ventoux (Bédoin)" --out FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.gpx --out FietsApp/Assets/FietsApp/Routes/MontVentoux_Bedoin.terrain.bytes

python tools/route_from_osm.py --ref "D 211" --bbox 45.04,6.00,45.11,6.09 --start 45.0594,6.0379 --end 45.0909,6.0654 --name "Alpe d'Huez" --out FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx
python tools/terrain_from_tiles.py --gpx FietsApp/Assets/FietsApp/Routes/AlpeDHuez.gpx --out FietsApp/Assets/FietsApp/Routes/AlpeDHuez.terrain.bytes
```

Alpe d'Huez loopt hier van het begin van de D 211 tot het dorp (12,9 km, 723 → 1791 m). De bekende 13,8 km eindigt bij de Tour-finish, via straten van het skidorp die geen D 211 heten.

## Licenties en naamsvermelding

- **OpenStreetMap**: © OpenStreetMap-bijdragers, Open Database License (ODbL). De GPX-bestanden zijn afgeleid van OSM-data en vallen daarom ook onder de ODbL. In de app vermelden: "Wegen © OpenStreetMap-bijdragers".
- **SRTM** (NASA/USGS): publiek domein. Opgehaald via de gratis OpenTopoData-API (max. 100 punten per verzoek, 1 per seconde, 1000 per dag).
- **Terrain Tiles** (AWS Open Data, oorspronkelijk Mapzen/Tilezen): samengesteld uit o.a. SRTM, GMTED en EU-DEM. Naamsvermelding volgens de Tilezen-richtlijnen; voor Europa onder meer "Produced using Copernicus data and information funded by the European Union – EU-DEM layers".

## Bewust niet gebruikt

- **EU-DEM 25 m voor de hoogte langs de weg**: gaf op de Ventoux uitschieters tot 27% per 100 m, omdat de weg in steile hellingen is uitgehakt; SRTM bleef na dezelfde filtering onder 13%. Op Alpe d'Huez was EU-DEM na filtering ook steiler dan SRTM (17,9% tegen 16,6% per 100 m, bij σ 80 m).
- **Strava-segmenten en commerciële kaartdata**: niet toegestaan zonder toestemming (backlog sectie 4).
