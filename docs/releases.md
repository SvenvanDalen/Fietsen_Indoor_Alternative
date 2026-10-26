# Releases en updates (story 13.6)

Elke push naar `main` die iets in `FietsApp/`, `Bridge/`, `tools/updater/` of `tools/installer/` verandert, maakt via `.github/workflows/release.yml` een nieuwe Windows-versie: release `v0.1.<runnummer>` met

- `FietsApp-Setup.exe`: de installer voor nieuwe gebruikers (Inno Setup, `tools/installer/FietsApp.iss`);
- `FietsApp-Windows.zip` en `FietsApp-Windows.zip.sha256`: wat de app zelf gebruikt om bij te werken. De zip bevat de app, de Bluetooth-brug (`Bridge/FietsBridge.exe`) en het updatescript.

## Installeren

1. Download `FietsApp-Setup.exe` van de [nieuwste release](https://github.com/SvenvanDalen/Fietsen_Indoor_Alternative/releases/latest) en start hem.
2. De installer zet de app zonder adminrechten in `%LOCALAPPDATA%\Programs\FietsApp`, maakt een snelkoppeling in het Startmenu (en desgewenst op het bureaublad) en zet FietsApp onder *Instellingen → Apps*, waar je hem ook weer verwijdert.

Liever zonder installer: pak `FietsApp-Windows.zip` uit naar dezelfde map. Een map onder `C:\Program Files` werkt ook, maar daar kan de app zichzelf niet bijwerken; je krijgt dan alleen een melding met een downloadknop.

De installer en de app zijn niet digitaal ondertekend. Windows SmartScreen toont daarom een waarschuwing: kies *Meer informatie* → *Toch uitvoeren*.

De app start de brug zelf. Draait er al een brug (bijvoorbeeld via `dotnet run`), dan gebruikt hij die.

## Updates

- Bij het opstarten kijkt de app of er een nieuwere release is. Zo ja, dan downloadt hij die op de achtergrond, controleert de sha256 en zet hem klaar in `%LOCALAPPDATA%\FietsApp\updates\`.
- Rechtsonder verschijnt dan "Update … staat klaar". De update gaat erin bij afsluiten, of meteen met *Nu herstarten*. Midden in een rit gebeurt er niets.
- `apply-update.ps1` wacht tot de app en de brug dicht zijn, verwisselt de map en zet bij een fout de oude versie terug.
- Lukt installeren niet (bijvoorbeeld omdat een map in gebruik is), dan blijft de oude versie werken en probeert de app het bij de volgende keer afsluiten opnieuw. Na drie mislukte pogingen toont hij een knop om de nieuwe versie handmatig te downloaden.
- Wat er gebeurde staat in `%LOCALAPPDATA%\FietsApp\update.log`.
- Een lokale testbuild heeft versie `0.1.0` en wordt dus ook bijgewerkt (en bij afsluiten vervangen); start hem met `--no-update`.
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

Handmatig een release maken kan met `gh workflow run release.yml` (of *Run workflow* op GitHub), alleen vanaf `main`. Op een andere branch draaien alleen de tests en de build.
