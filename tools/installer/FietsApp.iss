; Installer voor FietsApp (story 13.6): FietsApp-Setup.exe in elke release.
; Installeert per gebruiker, zonder adminrechten, in %LOCALAPPDATA%\Programs\FietsApp; daar kan de app zichzelf bijwerken.
; Bouwen: ISCC.exe /DAppVersion=0.1.12 /DSourceDir=<map met FietsApp.exe> /O<uitvoermap> tools\installer\FietsApp.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\dist"
#endif

[Setup]
; Vaste AppId: een nieuwere installer herkent een eerdere installatie en installeert eroverheen.
AppId={{9E8D308E-4DAD-493F-A6BA-F59BEC54CDDD}
AppName=FietsApp
AppVersion={#AppVersion}
AppPublisher=SvenvanDalen
AppPublisherURL=https://github.com/SvenvanDalen/Fietsen_Indoor_Alternative
AppUpdatesURL=https://github.com/SvenvanDalen/Fietsen_Indoor_Alternative/releases
DefaultDirName={localappdata}\Programs\FietsApp
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=FietsApp-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\FietsApp.exe
UninstallDisplayName=FietsApp
; Draait de app of de brug nog, dan vraagt de installer die te sluiten.
CloseApplications=yes

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op het bureaublad"; GroupDescription: "Extra:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\FietsApp"; Filename: "{app}\FietsApp.exe"
Name: "{userdesktop}\FietsApp"; Filename: "{app}\FietsApp.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FietsApp.exe"; Description: "FietsApp starten"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; De brug draait los van de app; stop hem, anders blijft de map half staan.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM FietsBridge.exe"; Flags: runhidden; RunOnceId: "StopBridge"

[UninstallDelete]
; Klaargezette updates en het updatelog; profiel en ritten (persistentDataPath) blijven staan.
Type: filesandordirs; Name: "{localappdata}\FietsApp"
