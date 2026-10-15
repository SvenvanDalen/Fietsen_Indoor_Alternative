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

## Unity-project (`FietsApp/`)

Code staat in `FietsApp/Assets/FietsApp/`, verdeeld over assembly definitions:

| Assembly | Inhoud | Regel |
|---|---|---|
| `FietsApp.Physics` | Fysicamodel | `noEngineReferences`: puur C#, testbaar zonder Unity |
| `FietsApp.Trainers` | `ITrainer` en implementaties (simulator, later FTMS en ANT+) | `noEngineReferences` |
| `FietsApp.Runtime` | MonoBehaviours: `RideController`, `Hud`, invoer | Mag Unity gebruiken |
| `FietsApp.Tests.EditMode` | NUnit-tests | |

Houd logica in de engine-vrije assemblies, zodat ze met EditMode-tests te dekken zijn; de MonoBehaviours zijn dunne lijmlagen.

- Input: alleen het nieuwe Input System (`Keyboard.current`), de oude `Input`-klasse staat uit.
- UI: uGUI met `LegacyRuntime.ttf`, opgebouwd in code; geen TextMeshPro-assets nodig.
- Na een wijziging in een `.cs`-bestand: laat Unity verversen (`AssetDatabase.Refresh`) en controleer de console op fouten voordat je verder gaat.

### Tests draaien via de Unity-MCP

Er is geen command-line testrunner; draai tests met `Unity_RunCommand` via `TestRunnerApi` met `runSynchronously = true`, filter op assembly `FietsApp.Tests.EditMode`, en tel geslaagd/gefaald in een `ICallbacks`-implementatie. Een PR gaat pas open als alle tests groen zijn.

### Valkuilen

- Unity's AI Assistant-pakket wijzigt soms vanzelf `ProjectSettings/` (bijvoorbeeld `APP_UI_EDITOR_ONLY`, `SceneTemplateSettings.json`). Commit die niet mee met eigen werk.
- Als de Unity-editor op de achtergrond draait, loopt de tijd in de Play-modus niet door. Verifieer gedrag in de Play-modus daarom met de eigenaar, of via EditMode-tests.
- Referentiewaarden in tests reken je los van de code uit (bijvoorbeeld met een Python-script), anders test je de code tegen zichzelf.
