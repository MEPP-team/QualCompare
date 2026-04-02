; installer/qualcompare.iss
#define AppName "QualCompare"
#define AppVersion "1.0.0"
#define AppPublisher "QualCompare Research Project"
#define AppExeName "QualCompare.exe"
#define BuildOutputDir "..\QualCompare\bin\Release"

[Setup]
AppId={{B2E0D9B1-8E31-4D39-9D4E-4A0D9D77B361}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription=QualCompare Installer
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=qualcompare_setup_{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
SetupIconFile=..\PatchifyWrapper\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
ChangesEnvironment=no
UsePreviousAppDir=yes
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{app}\scripts"
Name: "{app}\resources"

[Files]
Source: "{#BuildOutputDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'QualCompare installs the desktop application, rendering scripts, and bundled resources.' + #13#10#13#10 +
    'Blender is not bundled. On first launch, the application will create its configuration automatically and guide the user if blender.exe still needs to be selected.';
end;


