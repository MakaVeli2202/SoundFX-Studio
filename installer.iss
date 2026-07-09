[Setup]
AppName=SoundFX Studio
AppVersion=1.0.0
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

; Optional bundled default sounds folder (if present, copied to app folder)
Source: "{#SourcePath}installer-assets\DefaultSounds\*"; DestDir: "{app}\DefaultSounds"; Flags: ignoreversion

[Icons]
Name: "{group}\SoundFX Studio";           Filename: "{app}\SoundFXStudio.exe"
Name: "{group}\Uninstall SoundFX Studio"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SoundFX Studio";   Filename: "{app}\SoundFXStudio.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SoundFXStudio"; ValueData: """{app}\SoundFXStudio.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Install Voicemeeter via winget if not already present
Filename: "winget"; Parameters: "install --id VB-Audio.Voicemeeter --exact --silent --accept-package-agreements --accept-source-agreements"; StatusMsg: "Installing VB-Audio Voicemeeter..."; Flags: waituntilterminated; Check: ShouldInstallVoicemeeter

; Launch SoundFX Studio after install — only if no reboot needed (shellexec = runs as normal user)
Filename: "{app}\SoundFXStudio.exe"; Description: "Launch SoundFX Studio now"; Flags: nowait postinstall skipifsilent shellexec; Check: ShouldLaunchApp

[UninstallRun]
; Uninstall Voicemeeter via winget if it's present
Filename: "winget"; Parameters: "uninstall --id VB-Audio.Voicemeeter --exact"; StatusMsg: "Removing VB-Audio Voicemeeter..."; Flags: waituntilterminated; RunOnceId: "UninstallVoicemeeter"; Check: IsVoicemeeterPresent

[Code]
var
  GVoicemeeterWasAbsent: Boolean;

function GetSystemMetrics(Index: Integer): Integer;
  external 'GetSystemMetrics@user32.dll stdcall';

// Checks if Voicemeeter is installed by looking for the registry entry and DLL paths
function IsVoicemeeterPresent: Boolean;
var
  UninstallPath: String;
  DllPath: String;
begin
  Result := False;
  // Check registry first (most reliable)
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}', 'UninstallString', UninstallPath) then
  begin
    Result := True;
    Exit;
  end;
  // Fallback: check known DLL paths
  DllPath := 'C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll';
  if FileExists(DllPath) then begin Result := True; Exit; end;
  DllPath := 'C:\Program Files\VB\Voicemeeter\VoicemeeterRemote64.dll';
  if FileExists(DllPath) then begin Result := True; Exit; end;
end;

function InitializeSetup: Boolean;
var
  UninstallKey, InstalledVer, Msg: String;
begin
  Result := True;
  GVoicemeeterWasAbsent := not IsVoicemeeterPresent;

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

function ShouldInstallVoicemeeter: Boolean;
begin
  Result := GVoicemeeterWasAbsent;
end;

function ShouldLaunchApp: Boolean;
begin
  Result := not GVoicemeeterWasAbsent;
end;

function NeedsRestart: Boolean;
begin
  Result := GVoicemeeterWasAbsent;
end;
