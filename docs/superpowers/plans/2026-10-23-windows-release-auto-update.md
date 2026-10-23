# Windows-release met auto-update Implementatieplan (13.6)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Elke push naar `main` levert automatisch een Windows-versie van FietsApp (Unity-speler plus FietsBridge) op als GitHub Release, en een geïnstalleerde app werkt zichzelf bij naar de nieuwste release.

**Architecture:** Een GitHub Actions-workflow test en bouwt het Unity-project met GameCI (Linux-runner, Mono, StandaloneWindows64), publiceert FietsBridge self-contained op een Windows-runner, pakt beide in als `FietsApp-Windows.zip` met sha256 en maakt release `v0.1.<run_number>`. In de app controleert een engine-vrije assembly `FietsApp.Updates` bij het opstarten de GitHub API, downloadt en controleert de zip op de achtergrond en pakt hem uit in een staging-map. Bij afsluiten (of via "Nu herstarten") start de app `apply-update.ps1` uit de nieuwe versie; dat wacht tot app en brug dicht zijn, verwisselt de installatiemap en zet bij een fout de oude versie terug. De app start de meegeleverde brug zelf.

**Tech Stack:** Unity 6000.5.10f1 (URP, Mono, .NET Standard 2.1), `System.Net.Http.HttpClient`, `System.IO.Compression.ZipFile`, `FietsApp.Accounts.Json`, NUnit (EditMode), GitHub Actions met `game-ci/unity-test-runner@v4` en `game-ci/unity-builder@v4`, .NET 10 SDK, Windows PowerShell 5.1, `gh`.

**Spec:** Verzoek van de eigenaar (2026-10-23): "een app-variant vanaf GitHub via releases op main; automatisch updaten zodra je de app hebt; de app moet voor pc werken". Context: `docs/decisions.md` (Windows eerst, budget nul, brug als apart programma) en `CLAUDE.md` (assemblies, tests via de MCP, branch + PR).

**Gekozen en verworpen:** Velopack verworpen: NuGet-afhankelijkheden in Unity/Mono zijn een risico en de installatiehooks zouden de Unity-speler starten. Een eigen updater is volledig te testen en heeft geen afhankelijkheden.

## Global Constraints

- Unity `6000.5.10f1`, `StandaloneWindows64`, Mono. Brug: `Bridge/FietsBridge`, `net10.0-windows10.0.19041.0`, gepubliceerd met `-r win-x64 --self-contained true -p:PublishSingleFile=true`.
- Alles gratis: GitHub Actions (publieke repo), Releases, GameCI. Geen code-signing.
- Latest-release-API: `https://api.github.com/repos/SvenvanDalen/Fietsen_Indoor_Alternative/releases/latest`.
- Tag `v0.1.<github.run_number>`; `Application.version` = `0.1.<github.run_number>`.
- Assets: `FietsApp-Windows.zip` en `FietsApp-Windows.zip.sha256` (`<64 lowercase hex>  FietsApp-Windows.zip`).
- Zip-root: `FietsApp.exe`, `UnityPlayer.dll`, `FietsApp_Data/`, `MonoBleedingEdge/`, `apply-update.ps1`, `Bridge/FietsBridge.exe`.
- Geen adminrechten. Aanbevolen installatiemap `%LOCALAPPDATA%\Programs\FietsApp`. Updates en log in `%LOCALAPPDATA%\FietsApp\updates\` en `%LOCALAPPDATA%\FietsApp\update.log`.
- De app herstart nooit zelf tijdens gebruik; toepassen alleen bij afsluiten of na "Nu herstarten".
- Updater en brugstarter draaien alleen als `Application.platform == RuntimePlatform.WindowsPlayer`; updater niet met argument `--no-update`.
- Logica in engine-vrije assemblies (`noEngineReferences`), MonoBehaviours dun. UI met uGUI in code, `LegacyRuntime.ttf`. Nederlands in commentaar, UI, commits; identifiers Engels.
- Tests: `FietsApp.Tests.EditMode`, gedraaid met Unity in batchmode op de worktree (`-runTests -testPlatform EditMode`), of via de MCP als de editor dit project open heeft.

## Review Focus

1. **Geen internet / rate-limit / foutpagina:** app start normaal, niets op schijf. Tests `RunAsync_SourceThrows_ReturnsFailedAndWritesNothing`, `Parse_NotJson_ReturnsNull` (Task 1/2).
2. **Installatie in een map zonder schrijfrechten:** niets downloaden, banner met link. Test `RunAsync_InstallNotWritable_ReturnsManualWithoutDownloading` (Task 2).
3. **Download afgebroken of hash fout:** geen staging, nooit toepassen. Tests `RunAsync_HashMismatch_DeletesZipAndNoStaging`, `RunAsync_Cancelled_LeavesNoStaging` (Task 2).
4. **De brug draait nog uit de installatiemap:** het script stopt hem vóór het verwisselen. Scenario "brug draait" in `tools/updater/test-apply-update.ps1` (Task 3).
5. **Map vergrendeld (Verkenner, virusscanner):** oude versie blijft intact. Scenario "vergrendelde map" (Task 3).

---

## File Structure

| Pad | Verantwoordelijkheid |
|---|---|
| `FietsApp/Assets/FietsApp/Scripts/Updates/FietsApp.Updates.asmdef` | Engine-vrije assembly, referentie `FietsApp.Accounts` |
| `.../Updates/AppVersion.cs` | Versie parsen en vergelijken |
| `.../Updates/GitHubRelease.cs` | Release-model en parser (via `Accounts.Json`) |
| `.../Updates/UpdateDecision.cs` | Is deze release een bruikbare update? |
| `.../Updates/Sha256Verifier.cs`, `UpdatePaths.cs`, `UpdateFlow.cs`, `HttpSources.cs` | Download, controle, staging |
| `.../Updates/UpdateApplier.cs` | Argumenten voor en starten van `apply-update.ps1` |
| `FietsApp/Assets/FietsApp/Scripts/Runtime/Updates/UpdateService.cs` | Lijm met app-levenscyclus |
| `FietsApp/Assets/FietsApp/Scripts/Runtime/Updates/UpdateBanner.cs` | Versielabel en melding (uGUI) |
| `FietsApp/Assets/FietsApp/Scripts/Runtime/Bluetooth/BridgeLauncher.cs` | Start/stopt de meegeleverde brug |
| `FietsApp/Assets/FietsApp/Tests/EditMode/UpdateDecisionTests.cs`, `UpdateFlowTests.cs`, `UpdateApplierTests.cs` | Tests |
| `tools/updater/apply-update.ps1`, `tools/updater/test-apply-update.ps1` | Verwisselscript en scenariotests |
| `.github/workflows/release.yml` | Test, build, release bij push naar `main` |
| `docs/releases.md` | Installeren, updates, secrets |

---

### Task 1: Versie, release-model en updatebeslissing

**Files:** Create `Updates/FietsApp.Updates.asmdef` (`noEngineReferences: true`, references `["FietsApp.Accounts"]`), `AppVersion.cs`, `GitHubRelease.cs`, `UpdateDecision.cs`; Modify `Tests/EditMode/FietsApp.Tests.EditMode.asmdef` (+ `FietsApp.Updates`), `Runtime/FietsApp.Runtime.asmdef` (+ `FietsApp.Updates`); Test `UpdateDecisionTests.cs`.

**Interfaces (namespace `FietsApp.Updates`):**
- `public readonly struct AppVersion : IComparable<AppVersion>` — `Major, Minor, Patch`; `static bool TryParse(string text, out AppVersion v)` (optionele `v`/`V`, precies drie niet-negatieve gehele getallen); `ToString()` → `"0.1.12"`.
- `public sealed class GitHubAsset { public string Name; public string DownloadUrl; }`
- `public sealed class GitHubRelease { public string TagName; public string HtmlUrl; public bool Draft; public bool Prerelease; public List<GitHubAsset> Assets; public static GitHubRelease Parse(string json); }` — `null` bij lege/ongeldige JSON of ontbrekende `tag_name`; leest `tag_name`, `html_url`, `draft`, `prerelease`, `assets[].name`, `assets[].browser_download_url`.
- `public sealed class UpdateCandidate { public AppVersion Version; public string ZipUrl; public string Sha256Url; public string ReleasePageUrl; }`
- `public static class UpdateDecision { public const string ZipAssetName = "FietsApp-Windows.zip"; public const string ShaAssetName = "FietsApp-Windows.zip.sha256"; public static UpdateCandidate Evaluate(AppVersion current, GitHubRelease latest); }`

- [ ] **Step 1: Falende tests** in `UpdateDecisionTests.cs`: `TryParse_WithVPrefix_Parses`; `TryParse_Invalid_ReturnsFalse` (`""`, `null`, `"0.1"`, `"0.1.x"`, `"0.1.2.3"`, `"-1.0.0"`); `CompareTo_IsNumericNotLexical` (0.1.10 > 0.1.9); `Parse_RealApiShape_ReadsTagAndAssets`; `Parse_NotJson_ReturnsNull` (`"<html>rate limited</html>"`, `""`); `Evaluate_NewerWithBothAssets_ReturnsCandidate` (0.1.5 → v0.1.6); `Evaluate_SameVersion_ReturnsNull`; `Evaluate_OlderVersion_ReturnsNull`; `Evaluate_Draft_ReturnsNull`; `Evaluate_Prerelease_ReturnsNull`; `Evaluate_MissingZip_ReturnsNull`; `Evaluate_MissingSha_ReturnsNull`; `Evaluate_UnparsableTag_ReturnsNull`; `Evaluate_NullRelease_ReturnsNull`.
- [ ] **Step 2:** Run EditMode-tests. Expected: compileerfout.
- [ ] **Step 3:** Implementeer volgens Interfaces.
- [ ] **Step 4:** Run EditMode-tests. Expected: alles groen.
- [ ] **Step 5:** Commit `feat: versie en updatebeslissing uit GitHub-release (13.6)`.

### Task 2: Downloaden, verifiëren en klaarzetten

**Files:** Create `Updates/Sha256Verifier.cs`, `UpdatePaths.cs`, `UpdateFlow.cs`, `HttpSources.cs`; Test `UpdateFlowTests.cs`.

**Interfaces:**
- Consumes: Task 1.
- `public static class Sha256Verifier { public static bool Matches(string filePath, string sha256FileContent); }` — eerste token, hoofdletterongevoelig; `false` als het geen 64 hex-tekens is.
- `public sealed class UpdatePaths { public UpdatePaths(string installDir, string updatesRoot); InstallDir; UpdatesRoot; string ZipPath(AppVersion v) /* <root>\FietsApp-<v>.zip */; string StagingDir(AppVersion v) /* <root>\staging-<v> */; LogPath /* <parent van root>\update.log */; static bool IsWritable(string installDir) /* tijdelijk bestand in installDir én de bovenliggende map */; static string DefaultUpdatesRoot() /* %LOCALAPPDATA%\FietsApp\updates */ }`
- `public interface IReleaseSource { Task<GitHubRelease> GetLatestAsync(CancellationToken ct); }`
- `public interface IDownloader { Task DownloadFileAsync(string url, string path, CancellationToken ct); Task<string> GetStringAsync(string url, CancellationToken ct); }`
- `public enum UpdateStatus { UpToDate, ReadyToApply, ManualDownloadRequired, Failed }`
- `public sealed class UpdateResult { public UpdateStatus Status; public AppVersion Version; public string StagingDir; public string ReleasePageUrl; public string Error; }`
- `public sealed class UpdateFlow { public UpdateFlow(AppVersion current, IReleaseSource source, IDownloader downloader, UpdatePaths paths, bool installWritable); public Task<UpdateResult> RunAsync(CancellationToken ct); }`
- `HttpSources.cs`: `GitHubReleaseSource : IReleaseSource` (URL uit Global Constraints) en `HttpDownloader : IDownloader`; één gedeelde `HttpClient`, `User-Agent: FietsApp-Updater`, `Accept: application/vnd.github+json`, API-timeout 30 s; niet-2xx → `HttpRequestException`.

Volgorde in `RunAsync`: (1) bron; exception → `Failed`. (2) `Evaluate` null → `UpToDate`. (3) niet schrijfbaar → `ManualDownloadRequired` zonder download. (4) staging met `FietsApp.exe` bestaat → `ReadyToApply`. (5) zip/`.part`/staging van andere versies opruimen. (6) download naar `.part`, sha ophalen, verifiëren, hernoemen; mismatch → verwijderen, `Failed`. (7) uitpakken naar `StagingDir + ".tmp"`, eisen dat `FietsApp.exe` en `apply-update.ps1` erin staan, hernoemen, zip weg → `ReadyToApply`. (8) `OperationCanceledException` → `.part`/`.tmp` weg, `Failed`.

- [ ] **Step 1: Falende tests** (fakes `FakeReleaseSource`, `FakeDownloader` in het testbestand; eigen tijdmap per test): `Matches_SumFormatUppercase_True`, `Matches_BareHashWithCrlf_True`, `Matches_WrongHash_False`, `Matches_GarbageContent_False`, `Paths_UseVersionInName`, `IsWritable_MissingDir_False`, `IsWritable_TempDir_True`, `RunAsync_NoNewerRelease_ReturnsUpToDate`, `RunAsync_SourceThrows_ReturnsFailedAndWritesNothing`, `RunAsync_InstallNotWritable_ReturnsManualWithoutDownloading`, `RunAsync_ValidUpdate_ExtractsToStaging`, `RunAsync_HashMismatch_DeletesZipAndNoStaging`, `RunAsync_Cancelled_LeavesNoStaging`, `RunAsync_ZipWithoutExe_ReturnsFailed`, `RunAsync_StagingAlreadyPresent_SkipsDownload`, `RunAsync_RemovesStagingOfOtherVersions`. Async tests als `[Test]` die `.GetAwaiter().GetResult()` aanroepen (fakes zijn synchroon voltooid).
- [ ] **Step 2:** Run. Expected: compileerfout.
- [ ] **Step 3:** Implementeer.
- [ ] **Step 4:** Run. Expected: alles groen.
- [ ] **Step 5:** Commit `feat: update downloaden, sha256 controleren en klaarzetten (13.6)`.

### Task 3: Verwisselscript

**Files:** Create `tools/updater/apply-update.ps1`, `tools/updater/test-apply-update.ps1`.

**Interface:** `apply-update.ps1 -ProcessId <int> -InstallDir <string> -StagingDir <string> -ExeName <string> -LogPath <string> [-Restart] [-WaitTimeoutSeconds 60]`; exit 0 succes, 1 fout (oude versie intact).

Gedrag: (1) wacht tot `ProcessId` weg is (timeout → exit 1, niets gewijzigd). (2) stop processen waarvan het pad onder `InstallDir` ligt (de brug), wacht tot ze weg zijn. (3) verwijder oude `<InstallDir>.old`. (4) hernoem `InstallDir` → `.old` (10 pogingen, 1 s). (5) hernoem `StagingDir` → `InstallDir`; op een ander volume `Copy-Item -Recurse`; fout → `.old` terug, exit 1. (6) `.old` verwijderen. (7) `-Restart` → start `InstallDir\ExeName`. Elke stap een regel met tijd in `LogPath`.

- [ ] **Step 1: Testscript** met scenario's: *succes*, *vergrendelde map* (bestand open met `FileShare.None` → exit 1, oud intact), *proces blijft draaien* (`-WaitTimeoutSeconds 3` → exit 1), *oude .old aanwezig* (opgeruimd), *brug draait* (kopie van `PING.EXE` als `install\Bridge\FietsBridge.exe -n 60 127.0.0.1` → gestopt, exit 0). Print `PASS/FAIL <scenario>`, exitcode = aantal fouten.
- [ ] **Step 2:** `powershell -ExecutionPolicy Bypass -File tools/updater/test-apply-update.ps1`. Expected: FAIL.
- [ ] **Step 3:** Schrijf `apply-update.ps1`.
- [ ] **Step 4:** Zelfde commando. Expected: vijf keer PASS, exit 0.
- [ ] **Step 5:** Commit `feat: script dat de installatie veilig verwisselt (13.6)`.

### Task 4: Updater en brug in de app

**Files:** Create `Updates/UpdateApplier.cs`, `Runtime/Updates/UpdateService.cs`, `Runtime/Updates/UpdateBanner.cs`, `Runtime/Bluetooth/BridgeLauncher.cs`; Test `UpdateApplierTests.cs`.

**Interfaces:**
- `public static class UpdateApplier { public static string BuildArguments(string scriptPath, int processId, string installDir, string stagingDir, string exeName, string logPath, bool restart); public static void Launch(UpdateResult ready, UpdatePaths paths, bool restart); }` — `-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<staging>\apply-update.ps1" -ProcessId <pid> -InstallDir "…" -StagingDir "…" -ExeName "FietsApp.exe" -LogPath "…"` (+ ` -Restart`); paden tussen dubbele aanhalingstekens. `Launch` via `Process.Start("powershell.exe", …)` met `UseShellExecute = false`, `CreateNoWindow = true`.
- `UpdateService : MonoBehaviour` — `static UpdateService Instance`, `UpdateResult Result`, `void RestartNow()`. Bootstrap `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, `DontDestroyOnLoad`-object `"UpdateService"` met `UpdateService` + `UpdateBanner`. Installatiemap `Path.GetDirectoryName(Application.dataPath)`. `OnApplicationQuit`: annuleren; bij `ReadyToApply` → `Launch(restart: false)`. `RestartNow`: `Launch(restart: true)` + `Application.Quit()`. Exceptions → `Debug.LogWarning` en het log.
- `UpdateBanner : MonoBehaviour` — eigen Canvas (ScreenSpaceOverlay, `sortingOrder` 1000), rechtsonder: `"FietsApp {Application.version}"`; bij `ReadyToApply` `"Update {v} staat klaar en wordt geïnstalleerd bij afsluiten."` + knop `"Nu herstarten"`; bij `ManualDownloadRequired` `"Nieuwe versie {v} beschikbaar."` + knop `"Downloaden"` → `Application.OpenURL`.
- `BridgeLauncher : MonoBehaviour` — bootstrap `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`; alleen WindowsPlayer; start `<installDir>\Bridge\FietsBridge.exe` (verborgen) als dat bestand bestaat en er nog geen proces `FietsBridge` draait; stopt het bij `OnApplicationQuit` als hij het zelf startte.

- [ ] **Step 1: Falende tests:** `BuildArguments_QuotesPathsWithSpaces`, `BuildArguments_WithRestart_EndsWithRestartSwitch`, `BuildArguments_WithoutRestart_HasNoRestartSwitch`, `BuildArguments_UsesScriptFromStaging`.
- [ ] **Step 2:** Run. Expected: compileerfout.
- [ ] **Step 3:** Implementeer.
- [ ] **Step 4:** Run. Expected: alles groen; geen compileerfouten in de batchmode-log.
- [ ] **Step 5:** Commit `feat: update bij afsluiten toepassen en brug meeleveren (13.6)`.

### Task 5: Release-workflow, documentatie en lokale proef

**Files:** Create `.github/workflows/release.yml`, `docs/releases.md`; Modify `docs/backlog.md` (story 13.6), `docs/decisions.md` (rij distributie), `CLAUDE.md` (kopje Release).

Workflow: `on: push: branches: [main]` met `paths: ['FietsApp/**', 'Bridge/**', 'tools/updater/**', '.github/workflows/release.yml']` + `workflow_dispatch`; `concurrency: release`; `permissions: contents: write`; `env: VERSION: 0.1.${{ github.run_number }}`.
- `test` (ubuntu): checkout, cache `FietsApp/Library`, `game-ci/unity-test-runner@v4` (`projectPath: FietsApp`, `testMode: editmode`), secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
- `build` (ubuntu, needs test): `game-ci/unity-builder@v4` (`targetPlatform: StandaloneWindows64`, `buildName: FietsApp`, `versioning: Custom`, `version: ${{ env.VERSION }}`); artifact `windows-build` uit `build/StandaloneWindows64`.
- `release` (windows-latest, needs build): `actions/setup-dotnet` 10.0.x; `dotnet test Bridge`; download artifact naar `dist/`; `dotnet publish Bridge/FietsBridge -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/Bridge`; `tools/updater/test-apply-update.ps1`; kopieer `apply-update.ps1`; eis `dist/FietsApp.exe` en `dist/Bridge/FietsBridge.exe`; zip; sha256 (ASCII, zonder BOM); `gh release create v$VERSION … --target $GITHUB_SHA --title "FietsApp $VERSION" --generate-notes`.

- [ ] **Step 1:** Schrijf workflow en docs. Valideer YAML met `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml'))"`. Expected: geen fout.
- [ ] **Step 2: Lokale proef van het verwisselpad met echte builds:** bouw de speler in batchmode (`-buildWindows64Player`) naar `A/` en `B/` (B met een andere `bundleVersion`), publiceer de brug in beide, kopieer `apply-update.ps1`; start `A\FietsApp.exe`, controleer dat `FietsBridge` draait, sluit A via het script-pad (`apply-update.ps1 -ProcessId <A> -InstallDir A -StagingDir B`) na het stoppen van A. Expected: exit 0, `A\` bevat B's versie, geen `FietsBridge` meer uit de oude map.
- [ ] **Step 3:** Commit `ci: Windows-release met brug bij elke push naar main (13.6)`.
- [ ] **Step 4 (eigenaar, na merge):** secrets zetten volgens `docs/releases.md`, eerste release afwachten, installeren, tweede push, controleren dat de app bijwerkt.
