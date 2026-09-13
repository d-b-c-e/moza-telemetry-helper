#ifndef MyAppVersion
  #error MyAppVersion must be supplied by scripts/release.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by scripts/release.ps1
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied by scripts/release.ps1
#endif

#ifdef TestInstall
  #define StartupKey "Software\MozaTelemetryHelper\PackagingTest\Run"
  #define SettingsFile "{app}\packaging-test-settings.json"
#else
  #define StartupKey "Software\Microsoft\Windows\CurrentVersion\Run"
  #define SettingsFile "{localappdata}\MozaTelemetryHelper\settings.json"
#endif

[Setup]
#ifdef TestInstall
AppId={{C9084109-5268-4C2D-AE57-A4E36C79574B}
AppName=MOZA Telemetry Helper Packaging Test
#else
AppId={{C515D9B6-F334-4DB0-90F4-31915471CA8E}
AppName=MOZA Telemetry Helper
#endif
AppVersion={#MyAppVersion}
AppPublisher=d-b-c-e
AppPublisherURL=https://github.com/d-b-c-e/moza-telemetry-helper
AppSupportURL=https://github.com/d-b-c-e/moza-telemetry-helper/issues
AppUpdatesURL=https://github.com/d-b-c-e/moza-telemetry-helper/releases
DefaultDirName={localappdata}\Programs\MozaTelemetryHelper
DefaultGroupName=MOZA Telemetry Helper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#ReleaseDir}
OutputBaseFilename=MozaTelemetryHelper-{#MyAppVersion}-Setup
SetupIconFile=..\assets\app-icon.ico
UninstallDisplayIcon={app}\MozaTelemetryHelper.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#ifdef TestInstall
AppMutex=MozaTelemetryHelper.PackagingTest
#else
AppMutex=MozaTelemetryHelper.Running
#endif
CloseApplications=no
RestartApplications=no
UninstallDisplayName=MOZA Telemetry Helper

[Tasks]
Name: desktopicon; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: startwithwindows; Description: "Start MOZA Telemetry Helper with &Windows (in the system tray)"; Flags: unchecked
Name: checkforupdates; Description: "Automatically &check GitHub for updates"

[Files]
; Put the preference reader first so the wizard can extract it without unpacking the full app.
Source: "{#PublishDir}\updater\MozaTelemetryUpdater.exe"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
#ifndef TestInstall
Name: "{group}\MOZA Telemetry Helper"; Filename: "{app}\MozaTelemetryHelper.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MOZA Telemetry Helper"; Filename: "{app}\MozaTelemetryHelper.exe"; WorkingDir: "{app}"; Tasks: desktopicon
#endif

[Registry]
#ifndef TestInstall
Root: HKCU; Subkey: "Software\MozaTelemetryHelper\Installation"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey
#endif
Root: HKCU; Subkey: "{#StartupKey}"; ValueType: string; ValueName: "MozaTelemetryHelper"; ValueData: """{app}\MozaTelemetryHelper.exe"" --tray"; Tasks: startwithwindows
Root: HKCU; Subkey: "{#StartupKey}"; ValueType: none; ValueName: "MozaTelemetryHelper"; Flags: deletevalue; Tasks: not startwithwindows

[Run]
#ifndef TestInstall
Filename: "{app}\MozaTelemetryHelper.exe"; Description: "Open MOZA Telemetry Helper"; Flags: postinstall nowait skipifsilent unchecked
#endif

[Code]
var PreferenceTasksInitialized: Boolean;

function ReadUpdateChecks: Boolean;
var ResultCode: Integer;
begin
  ExtractTemporaryFile('MozaTelemetryUpdater.exe');
  if not Exec(ExpandConstant('{tmp}\MozaTelemetryUpdater.exe'),
      '--read-update-preference "' + ExpandConstant('{#SettingsFile}') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Could not read the saved update preference.');
  if (ResultCode <> 0) and (ResultCode <> 1) then
    RaiseException('Could not read settings.json. Correct the settings file before continuing.');
  Result := ResultCode = 1;
end;

function StartupAlreadyEnabled: Boolean;
var Command: String;
begin
  Result := RegQueryStringValue(HKCU, '{#StartupKey}', 'MozaTelemetryHelper', Command) and (Command <> '');
end;

procedure InitializePreferenceTasks;
var I: Integer;
begin
  if PreferenceTasksInitialized then Exit;
  PreferenceTasksInitialized := True;
  { Explicit task lists/settings files take precedence over the current app setting. }
  for I := 1 to ParamCount do
    if (Pos('/TASKS=', Uppercase(ParamStr(I))) = 1) or
       (Pos('/LOADINF=', Uppercase(ParamStr(I))) = 1) then Exit;

  { Read the live setting, since the user may have changed it in the app since Setup. }
  if StartupAlreadyEnabled then WizardSelectTasks('startwithwindows')
  else WizardSelectTasks('!startwithwindows');
  if ReadUpdateChecks then WizardSelectTasks('checkforupdates')
  else WizardSelectTasks('!checkforupdates');

  { Preserve normal /MERGETASKS semantics without resetting unrelated tasks. }
  for I := 1 to ParamCount do
    if Pos('/MERGETASKS=', Uppercase(ParamStr(I))) = 1 then
      WizardSelectTasks(Copy(ParamStr(I), 13, MaxInt));
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { Task defaults are restored after InitializeWizard; override them when this page is ready. }
  if CurPageID = wpSelectTasks then InitializePreferenceTasks;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  { Also covers silent installs, which simulate Next clicks. }
  if CurPageID = wpSelectTasks then InitializePreferenceTasks;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer; EnabledText: String;
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('checkforupdates') then EnabledText := 'true'
    else EnabledText := 'false';
    ExtractTemporaryFile('MozaTelemetryUpdater.exe');
    if not Exec(ExpandConstant('{tmp}\MozaTelemetryUpdater.exe'),
        '--write-update-preference "' + ExpandConstant('{#SettingsFile}') + '" ' + EnabledText,
        '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Could not save the update preference.');
    if ResultCode <> 0 then RaiseException('Could not save the update preference to settings.json.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
  if CurUninstallStep = usUninstall then begin
    if RegQueryStringValue(HKCU, '{#StartupKey}', 'MozaTelemetryHelper', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\MozaTelemetryHelper.exe') + '" --tray') = 0 then
        RegDeleteValue(HKCU, '{#StartupKey}', 'MozaTelemetryHelper');
  end;
end;
