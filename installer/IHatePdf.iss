; Instalador do I Hate PDF (Inno Setup 6.1+).
;
; Instalacao POR USUARIO, em %LocalAppData%\Programs — de proposito:
; em Program Files o app nao conseguiria se sobrescrever e a atualizacao
; automatica da tela Sobre pararia de funcionar. Assim o app instala sem
; UAC e continua se atualizando sozinho depois.
;
; O LibreOffice (necessario para DOCX/XLSX e PDF -> Word) e baixado e
; instalado em silencio durante a instalacao, se o usuario mantiver a
; tarefa marcada. O msiexec pede elevacao uma vez: isso e do Windows,
; nao ha instalacao per-machine sem UAC.
;
; Compilar:
;   ISCC.exe /DAppVersion=1.2.0 /DSourceExe=..\publish\IHatePdf.exe ^
;            /DLibreOfficeUrl=... /DLibreOfficeVersion=26.8.0 IHatePdf.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\IHatePdf.exe"
#endif
#ifndef LibreOfficeUrl
  #define LibreOfficeUrl ""
#endif
#ifndef LibreOfficeVersion
  #define LibreOfficeVersion "estavel"
#endif

#define AppName "I Hate PDF"
#define AppPublisher "Alexandre Britez Borsuka"
#define AppUrl "https://github.com/XandeBritez/i-hate-pdf"

[Setup]
AppId={{8B3C2F41-7D95-4E6A-9C18-2A5E7F0B4D63}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; Sem UAC: e o que mantem a atualizacao automatica viva.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\IHatePdf
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes

OutputDir=.
OutputBaseFilename=IHatePdf-Setup-{#AppVersion}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\IHatePdf.exe
UninstallDisplayName={#AppName} {#AppVersion}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na area de trabalho"; GroupDescription: "Atalhos:"
Name: "libreoffice"; Description: "Instalar o LibreOffice {#LibreOfficeVersion} (necessario para DOCX, XLSX e PDF para Word)"; GroupDescription: "Componente opcional:"; Check: NeedsLibreOffice

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\IHatePdf.exe"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\IHatePdf.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\IHatePdf.exe"; Description: "Abrir o {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;
  LibreOfficeInstaller: String;

{ Procura o soffice.exe do mesmo jeito que o app: registro e Program Files. }
function LibreOfficeInstalled: Boolean;
var
  InstallPath: String;
begin
  Result := False;

  if RegQueryStringValue(HKLM, 'SOFTWARE\LibreOffice\UNO\InstallPath', '', InstallPath) then
    if FileExists(AddBackslash(InstallPath) + 'soffice.exe') then
    begin
      Result := True;
      Exit;
    end;

  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\LibreOffice\UNO\InstallPath', '', InstallPath) then
    if FileExists(AddBackslash(InstallPath) + 'soffice.exe') then
    begin
      Result := True;
      Exit;
    end;

  if FileExists(ExpandConstant('{commonpf}\LibreOffice\program\soffice.exe')) then
    Result := True
  else if FileExists(ExpandConstant('{commonpf32}\LibreOffice\program\soffice.exe')) then
    Result := True;
end;

{ A tarefa so aparece quando faz sentido: ja instalado, nada a oferecer. }
function NeedsLibreOffice: Boolean;
begin
  Result := (not LibreOfficeInstalled) and ('{#LibreOfficeUrl}' <> '');
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    'Baixando o LibreOffice',
    'O componente necessario para DOCX, XLSX e PDF para Word esta sendo baixado.',
    @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (CurPageID = wpReady) and WizardIsTaskSelected('libreoffice') then
  begin
    LibreOfficeInstaller := ExpandConstant('{tmp}\LibreOffice.msi');
    DownloadPage.Clear;
    DownloadPage.Add('{#LibreOfficeUrl}', 'LibreOffice.msi', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        // Falhar o download nao pode abortar a instalacao do app: o proprio
        // app sabe baixar o LibreOffice depois, pela tela PDF para Word.
        LibreOfficeInstaller := '';
        SuppressibleMsgBox(
          'Nao foi possivel baixar o LibreOffice: ' + GetExceptionMessage + #13#10#13#10 +
          'O ' + '{#AppName}' + ' sera instalado normalmente. Voce pode instalar o LibreOffice ' +
          'depois pelo proprio app, na tela "PDF para Word".',
          mbInformation, MB_OK, IDOK);
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and (LibreOfficeInstaller <> '') and FileExists(LibreOfficeInstaller) then
  begin
    // /qn = totalmente silencioso. O Windows pede elevacao uma vez: nao existe
    // instalacao per-machine do LibreOffice sem isso.
    if not ShellExec('', ExpandConstant('{sys}\msiexec.exe'),
                     '/i "' + LibreOfficeInstaller + '" /qn /norestart',
                     '', SW_SHOW, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    begin
      SuppressibleMsgBox(
        'A instalacao do LibreOffice nao foi concluida (codigo ' + IntToStr(ResultCode) + ').' + #13#10#13#10 +
        'O ' + '{#AppName}' + ' funciona mesmo assim; para DOCX, XLSX e PDF para Word, ' +
        'use o botao "Baixar e instalar LibreOffice" na tela "PDF para Word".',
        mbInformation, MB_OK, IDOK);
    end;
  end;
end;
