# Releases en updates (story 13.6)

Elke push naar `main` die iets in `FietsApp/`, `Bridge/` of `tools/updater/` verandert, maakt via `.github/workflows/release.yml` een nieuwe Windows-versie: release `v0.1.<runnummer>` met `FietsApp-Windows.zip` en `FietsApp-Windows.zip.sha256`. De zip bevat de app, de Bluetooth-brug (`Bridge/FietsBridge.exe`) en het updatescript.

## Installeren

1. Download `FietsApp-Windows.zip` van de [nieuwste release](https://github.com/SvenvanDalen/Fietsen_Indoor_Alternative/releases/latest).
2. Pak hem uit naar `%LOCALAPPDATA%\Programs\FietsApp` (plak dat pad in de adresbalk van Verkenner). Een map onder `C:\Program Files` werkt ook, maar daar kan de app zichzelf niet bijwerken; je krijgt dan alleen een melding met een downloadknop.
3. Maak een snelkoppeling naar `FietsApp.exe`.

De build is niet digitaal ondertekend. Bij de eerste start toont Windows SmartScreen een waarschuwing: kies *Meer informatie* → *Toch uitvoeren*.

De app start de brug zelf. Draait er al een brug (bijvoorbeeld via `dotnet run`), dan gebruikt hij die.

## Updates

- Bij het opstarten kijkt de app of er een nieuwere release is. Zo ja, dan downloadt hij die op de achtergrond, controleert de sha256 en zet hem klaar in `%LOCALAPPDATA%\FietsApp\updates\`.
- Rechtsonder verschijnt dan "Update … staat klaar". De update gaat erin bij afsluiten, of meteen met *Nu herstarten*. Midden in een rit gebeurt er niets.
- `apply-update.ps1` wacht tot de app en de brug dicht zijn, verwisselt de map en zet bij een fout de oude versie terug.
- Wat er gebeurde staat in `%LOCALAPPDATA%\FietsApp\update.log`.
- Starten met `FietsApp.exe --no-update` slaat de controle over.
- Het versienummer staat altijd klein rechtsonder in beeld.

## Instellen (eenmalig, eigenaar)

De workflow bouwt met [GameCI](https://game.ci) en heeft een Unity-licentie nodig. Zet onder *Settings → Secrets and variables → Actions* van de repo:

| Secret | Inhoud |
|---|---|
| `UNITY_EMAIL` | E-mailadres van je Unity-account |
| `UNITY_PASSWORD` | Wachtwoord van je Unity-account |
| `UNITY_LICENSE` | Volledige inhoud van `C:\ProgramData\Unity\Unity_lic.ulf` |

Zie <https://game.ci/docs/github/activation> voor een Personal-licentie. Controle: `gh secret list` toont alle drie. Verloopt de licentie, vervang dan `UNITY_LICENSE`.

Handmatig een release maken kan met `gh workflow run release.yml` (of *Run workflow* op GitHub).
