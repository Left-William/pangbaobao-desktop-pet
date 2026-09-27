#define AppName "胖宝宝桌宠"
#define AppVersion "0.5.0-preview.1"
#define PackageName "PangBaoBaoPet-0.5.0-preview.1-win-x64-selfcontained"

[Setup]
AppId={{A3F33154-D202-4CB7-8F81-F5EFE821DE5E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Left-William
DefaultDirName={localappdata}\Programs\PangBaoBaoPet
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=PangBaoBaoPet-0.5.0-preview.1-win-x64-setup
SetupIconFile=..\src\PangBaoBaoPet.Desktop\Assets\pet.ico
UninstallDisplayIcon={app}\PangBaoBaoPet.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\dist\{#PackageName}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\PangBaoBaoPet.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\PangBaoBaoPet.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PangBaoBaoPet.exe"; Description: "启动{#AppName}"; Flags: nowait postinstall skipifsilent
