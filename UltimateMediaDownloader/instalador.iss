; instalador.iss
; Script do Inno Setup para o Ultimate Media Downloader.
;
; Requer o Inno Setup 6: https://jrsoftware.org/isdl.php
; O publicar.ps1 chama este arquivo automaticamente quando o Inno esta instalado.
;
; Para compilar a mao:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" instalador.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Ultimate Media Downloader"
#define MyAppExeName "UltimateMediaDownloader.exe"
#define MyAppPublisher "Ultimate Media Downloader"

[Setup]
AppId={{7F2C4E91-3A5D-4B8E-9C1A-2D6F8B3E5A70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Baseado no UMD de NK2552003 - Apache 2.0

DefaultDirName={autopf}\UltimateMediaDownloader
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE
InfoAfterFile=NOTICE

OutputDir=dist
OutputBaseFilename=UltimateMediaDownloader-{#MyAppVersion}-setup
SetupIconFile=src\UMD.App\app.ico

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; O aplicativo e 64 bits, entao instala em Program Files de verdade.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Sem privilegios de administrador o usuario pode instalar so para si.
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "dist\UltimateMediaDownloader\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; yt-dlp, ffmpeg e deno. Sao programas separados que o aplicativo executa;
; o NOTICE descreve as licencas de cada um.
Source: "dist\UltimateMediaDownloader\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs

Source: "dist\UltimateMediaDownloader\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\UltimateMediaDownloader\NOTICE";  DestDir: "{app}"; Flags: ignoreversion

; Qualquer outro arquivo que a publicacao gere (caso nao seja executavel unico).
Source: "dist\UltimateMediaDownloader\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "dist\UltimateMediaDownloader\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; O yt-dlp se atualiza sozinho e grava arquivos proprios em tools\.
; Sem isto a pasta fica para tras depois de desinstalar.
Type: filesandordirs; Name: "{app}\tools"

[Code]
// As configuracoes ficam em %APPDATA% e sao preservadas por padrao.
// Aqui o desinstalador pergunta se o usuario tambem quer apaga-las.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ConfigDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ConfigDir := ExpandConstant('{userappdata}\UltimateMediaDownloader');
    if DirExists(ConfigDir) then
    begin
      if MsgBox('Deseja remover tambem suas configuracoes?' + #13#10 +
                'Os arquivos ja baixados nao serao afetados.',
                mbConfirmation, MB_YESNO) = IDYES then
        DelTree(ConfigDir, True, True, True);
    end;
  end;
end;