[Setup]
AppName=Doc Helper
AppVersion=1.5.0
AppPublisher=DiaTech
AppPublisherURL=https://github.com/ItMeDiaTech/Doc_Helper
AppSupportURL=https://github.com/ItMeDiaTech/Doc_Helper/issues
DefaultDirName={autopf}\DocHelper
; Install to %AppData% instead of Program Files (no admin required)
DefaultDirName={userappdata}\DocHelper
DisableProgramGroupPage=yes
; No admin rights required
PrivilegesRequired=lowest
OutputDir=..\
OutputBaseFilename=DocHelper-Setup
SetupIconFile=..\DocHelper\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Create desktop shortcut
Tasks=desktopicon

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Doc Helper"; Filename: "{app}\DocHelper.exe"
Name: "{autodesktop}\Doc Helper"; Filename: "{app}\DocHelper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DocHelper.exe"; Description: "{cm:LaunchProgram,Doc Helper}"; Flags: nowait postinstall skipifsilent

[Code]
// Pin to taskbar after installation (Windows 10/11)
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PowerShellScript: String;
begin
  if CurStep = ssPostInstall then
  begin
    PowerShellScript := 
      '$WshShell = New-Object -comObject WScript.Shell; ' +
      '$Shortcut = $WshShell.CreateShortcut([Environment]::GetFolderPath("Desktop") + "\Doc Helper.lnk"); ' +
      '$Shortcut.TargetPath = "' + ExpandConstant('{app}') + '\DocHelper.exe"; ' +
      '$Shortcut.Save(); ' +
      'try { ' +
      '  $shell = New-Object -ComObject Shell.Application; ' +
      '  $folder = $shell.Namespace([Environment]::GetFolderPath("Desktop")); ' +
      '  $item = $folder.ParseName("Doc Helper.lnk"); ' +
      '  $item.InvokeVerb("taskbarpin"); ' +
      '  Remove-Item ([Environment]::GetFolderPath("Desktop") + "\Doc Helper.lnk") -Force; ' +
      '} catch { }';
    
    Exec('powershell.exe', '-WindowStyle Hidden -ExecutionPolicy Bypass -Command "' + PowerShellScript + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;