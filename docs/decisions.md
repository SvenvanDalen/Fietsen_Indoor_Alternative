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
