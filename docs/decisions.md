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
