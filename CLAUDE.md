# FietsApp

Virtuele fietsapp voor een slimme trainer, gericht op klimmen. Unity (C#), Windows eerst. Solo hobbyproject van de eigenaar, gebouwd met Claude en de Unity-MCP.

- **Backlog:** `docs/backlog.md` is de bron van waarheid voor stories, sprints en de volgorde van werk (sectie 6 en 8). Elke story is ook een GitHub-issue met de titel `x.y Titel`.
- **Besluiten:** `docs/decisions.md`. Lees die voordat je een ontwerpkeuze maakt; leg nieuwe besluiten daar vast.
- **Taal:** Nederlands voor code-commentaar, commits, PR's, docs en UI-teksten. Identifiers in het Engels.

## Git-workflow

Werk altijd op een eigen branch en lever op via een pull request naar `main`:

1. Branch vanaf een actuele `main`, genoemd naar het werk: `feat/1.1-bluetooth-zoeken`, `docs/...`, `fix/...`.
2. Commit in kleine stappen; noem het story-nummer in het bericht, zoals `feat: ... (1.6)`.
3. Push de branch en open een PR met `gh pr create`. Noem in de body de stories (`Story #<issue>`) en hoe het getest is.
4. Laat het mergen aan de eigenaar. Sluit geen issues: een story is pas klaar als hij werkt op de echte trainer (definitie van klaar, backlog sectie 6).

## Bluetooth-brug (`Bridge/`)

Unity heeft op Windows geen Bluetooth LE; `Bridge/FietsBridge` (.NET 10, Windows-API) doet het Bluetooth-werk en praat JSON met Unity via `ws://localhost:8765/` (besluit in `docs/decisions.md`).

- `FietsBridge.Core`: platformloze logica (service-herkenning, apparatenregister, protocol), gedekt door `dotnet test Bridge`.
- `FietsBridge`: de Windows-kant (`BleScanner`, `BridgeServer`). Starten: `dotnet run --project Bridge/FietsBridge`.
- Het protocol staat aan twee kanten: `Bridge/FietsBridge.Core/Protocol.cs` en `FietsApp/Assets/FietsApp/Scripts/Runtime/Bluetooth/BridgeMessages.cs` (veldnamen gelijk voor `JsonUtility`). Wijzig ze samen en houd de voorbeeldberichten in beide testsets gelijk.
- Zonder fietsapparaat test je de Unity-kant met een nepbrug: een klein script dat op `localhost:8765` luistert en `connection`- en `data`-berichten stuurt. Het echte verbindingspad test je door met een willekeurig Bluetooth-apparaat te verbinden; dat moet eindigen in een nette `failed`-melding.
- Bluetooth-UUID's en berichtindelingen controleer je tegen de Bluetooth SIG assigned numbers (`bitbucket.org/bluetooth-SIG/public`: `assigned_numbers/uuids` voor UUID's, `gss/` voor de veldindeling per characteristic). Let op: de huidige GSS wijkt soms af van FTMS 1.0, dat trainers in de praktijk volgen (zie Resistance Level in `IndoorBikeData`).

## Klimmen namaken (`tools/`)

Een klim bestaat uit een GPX (`tools/route_from_osm.py`: weg uit OpenStreetMap, hoogte uit SRTM) en een hoogtebestand van het landschap (`tools/terrain_from_tiles.py`), beide in `FietsApp/Assets/FietsApp/Routes/`. Koppel ze aan `RouteView`. Leg bij elke klim de bron vast in `docs/bronnen.md`, en controleer het profiel tegen de bekende cijfers van de klim met een test (zie `MontVentouxTests`). Alleen open data; geen Strava-segmenten.

## Unity-project (`FietsApp/`)

Code staat in `FietsApp/Assets/FietsApp/`, verdeeld over assembly definitions:

| Assembly | Inhoud | Regel |
|---|---|---|
| `FietsApp.Physics` | Fysicamodel | `noEngineReferences`: puur C#, testbaar zonder Unity |
| `FietsApp.Trainers` | `ITrainer` en implementaties (simulator, later FTMS en ANT+) | `noEngineReferences` |
| `FietsApp.Runtime` | MonoBehaviours: `RideController`, `Hud`, invoer, brugclient (`Bluetooth/`) | Mag Unity gebruiken |
| `FietsApp.Tests.EditMode` | NUnit-tests | |

Houd logica in de engine-vrije assemblies, zodat ze met EditMode-tests te dekken zijn; de MonoBehaviours zijn dunne lijmlagen.

- Input: alleen het nieuwe Input System (`Keyboard.current`), de oude `Input`-klasse staat uit.
- UI: uGUI met `LegacyRuntime.ttf`, opgebouwd in code; geen TextMeshPro-assets nodig.
- Na een wijziging in een `.cs`-bestand: laat Unity verversen (`AssetDatabase.Refresh`) en controleer de console op fouten voordat je verder gaat.

### Tests draaien via de Unity-MCP

Er is geen command-line testrunner; draai tests met `Unity_RunCommand` via `TestRunnerApi` met `runSynchronously = true`, filter op assembly `FietsApp.Tests.EditMode`, en tel geslaagd/gefaald in een `ICallbacks`-implementatie. Een PR gaat pas open als alle tests groen zijn, ook `dotnet test Bridge` als de brug geraakt is.

### Valkuilen

- Unity's AI Assistant-pakket wijzigt soms vanzelf `ProjectSettings/` (bijvoorbeeld `APP_UI_EDITOR_ONLY`, `SceneTemplateSettings.json`). Commit die niet mee met eigen werk.
- Als de Unity-editor op de achtergrond draait, loopt de Play-modus niet door (ook niet met Run In Background). Zet de editor op pauze (`EditorApplication.isPaused = true`) en roep per `Unity_RunCommand` één keer `EditorApplication.Step()` aan: elke opdracht draait dan ongeveer één frame van 20 ms, inclusief async-voortzettingen en WebSocket-berichten. Herhaal de opdracht tot de toestand er is; een lus van `Step()` binnen één opdracht levert niet meer frames op.
- Beeld controleren: render `Camera.main` naar een `RenderTexture`, sla die op als PNG in de scratchpad en bekijk hem met Read. Overlay-UI komt alleen in beeld als je de canvassen tijdelijk op `ScreenSpaceCamera` zet (en `Canvas.ForceUpdateCanvases()` aanroept). Met `RideController.StartAt(afstand)` spring je naar een plek op de route.
- UI in code: geef een eigen `Graphic`-subklasse expliciet een `CanvasRenderer` mee bij `new GameObject(...)`, anders tekent hij niets (bij `Image` gaat het vanzelf).
- Compileer nooit terwijl de Play-modus loopt: Unity herlaadt dan de scripts tijdens het spelen, niet-geserialiseerde velden worden leeg en componenten werpen NullReferenceExceptions. Stop Play eerst; dat duurt een frame, dus controleer `EditorApplication.isPlaying` in een volgende opdracht.
- `Unity_RunCommand` staat `System.Reflection` niet toe, weigert `AssetDatabase.DeleteAsset` (vraagt om bevestiging; verwijder via git) en ondersteunt geen opmaak zoals `{0:0.0}` in `result.Log` (gebruik `$"..."`). Druk knoppen in via `Button.onClick.Invoke()`.
- `PlayerSettings` via de MCP wijzigen komt niet altijd op schijf; controleer `ProjectSettings.asset` met git diff.
- Referentiewaarden in tests reken je los van de code uit (bijvoorbeeld met een Python-script), anders test je de code tegen zichzelf.
