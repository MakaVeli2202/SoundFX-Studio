[Setup]
AppName=SoundFX Studio
#define SfxVersion GetEnv('SFX_VERSION')
#if SfxVersion == ""
#define SfxVersion "1.0.0"
#endif
AppVersion={#SfxVersion}
AppPublisher=SoundFX Studio
AppId={{A2B3C4D5-E6F7-4812-9ABC-DEF012345678}
DefaultDirName={autopf}\SoundFX Studio
DefaultGroupName=SoundFX Studio
OutputDir={#SourcePath}installer-output
OutputBaseFilename=SoundFXStudio-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
UninstallDisplayName=SoundFX Studio
UninstallDisplayIcon={app}\SoundFXStudio.exe
SetupIconFile={#SourcePath}SoundFXStudio\icon.ico
DisableWelcomePage=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start SoundFX Studio with &Windows"; GroupDescription: "Additional icons:"

[Files]
; Main app — all files from publish
Source: "{#SourcePath}publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Voicemeeter setup — used only if Voicemeeter is missing
Source: "{#SourcePath}voicemeetersetup\voicemeetersetup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\SoundFX Studio";           Filename: "{app}\SoundFXStudio.exe"
Name: "{group}\Uninstall SoundFX Studio"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SoundFX Studio";   Filename: "{app}\SoundFXStudio.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SoundFXStudio"; ValueData: """{app}\SoundFXStudio.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\SoundFXStudio"

[Run]
Filename: "{tmp}\voicemeetersetup.exe"; Parameters: "-i -h"; WorkingDir: "{tmp}"; StatusMsg: "Installing Voicemeeter (silent)…"; Flags: runhidden waituntilterminated; Check: NotVoicemeeterInstalled
Filename: "{app}\SoundFXStudio.exe"; Description: "Launch SoundFX Studio now"; Flags: nowait postinstall skipifsilent shellexec

[Code]
function GetSystemMetrics(Index: Integer): Integer;
  external 'GetSystemMetrics@user32.dll stdcall';

const
  VmUninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}';
  VmExePath = 'C:\Program Files (x86)\VB\Voicemeeter\voicemeeter.exe';

function VoicemeeterInstalled: Boolean;
var
  S: String;
begin
  Result := RegQueryStringValue(HKLM32, VmUninstallKey, 'DisplayName', S) or
            FileExists(VmExePath);
end;

function NotVoicemeeterInstalled: Boolean;
begin
  Result := not VoicemeeterInstalled;
end;

function InitializeSetup: Boolean;
var
  UninstallKey, InstalledVer, Msg: String;
begin
  Result := True;

  // Check if SoundFX Studio is already installed — if so, ask user about reinstall
  UninstallKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' +
                  '{A2B3C4D5-E6F7-4812-9ABC-DEF012345678}_is1';

  if RegQueryStringValue(HKLM, UninstallKey, 'DisplayVersion', InstalledVer) or
     RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', InstalledVer) then
  begin
    Msg := 'SoundFX Studio ' + InstalledVer + ' is already installed.' + #13#10 + #13#10 +
           'Do you want to reinstall / upgrade it?' + #13#10 +
           '(Your sounds and settings will not be affected.)';
    Result := (MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES);
  end;
end;

procedure InitializeWizard;
var
  X, Y: Integer;
begin
  // Center the setup wizard on screen
  X := (GetSystemMetrics(0) - WizardForm.Width)  div 2;
  Y := (GetSystemMetrics(1) - WizardForm.Height) div 2;
  if X < 0 then X := 0;
  if Y < 0 then Y := 0;
  WizardForm.Left := X;
  WizardForm.Top  := Y;
end;
