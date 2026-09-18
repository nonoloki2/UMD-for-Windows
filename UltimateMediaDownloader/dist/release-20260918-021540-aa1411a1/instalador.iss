#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
[Setup]
AppId={{7F2C4E91-3A5D-4B8E-9C1A-2D6F8B3E5A70}
AppName=Ultimate Media Downloader
AppVersion={#MyAppVersion}
AppPublisher=Ultimate Media Downloader
DefaultDirName={localappdata}\Programs\UltimateMediaDownloader
DefaultGroupName=Ultimate Media Downloader
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=UltimateMediaDownloader\LICENSE
InfoAfterFile=UltimateMediaDownloader\NOTICE
OutputDir=.
OutputBaseFilename=UltimateMediaDownloader-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "UltimateMediaDownloader\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Ultimate Media Downloader"; Filename: "{app}\UltimateMediaDownloader.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Ultimate Media Downloader"; Filename: "{app}\UltimateMediaDownloader.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\UltimateMediaDownloader.exe"; Description: "{cm:LaunchProgram,Ultimate Media Downloader}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
