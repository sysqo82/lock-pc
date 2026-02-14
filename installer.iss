[Setup]
AppName=PCLockScreen
AppPublisher=Assaf Itzikson
AppVersion=2.0
DefaultDirName={autopf}\PCLockScreen
DefaultGroupName=PCLockScreen
UninstallDisplayIcon={app}\PCLockScreen.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\installer-output
OutputBaseFilename=PCLockScreenInstaller_v2.0
; Require elevation so installer can write registry/update system settings if needed
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Attempt to close the running application automatically so files can be replaced
CloseApplications=yes
; Only try to close this executable
CloseApplicationsFilter=PCLockScreen.exe

[Files]
; Installed files are produced by `dotnet publish -r win-x64 --self-contained` and placed in installer-output\publish\win-x64
Source: "installer-output\publish\win-x64\PCLockScreen.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "installer-output\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installer-output\publish\win-x64\*"; DestDir: "{commonappdata}\Ghost"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Attribs: hidden system


[Icons]
; Update the filename to PCLockScreen.exe
Name: "{group}\PCLockScreen"; Filename: "{app}\PCLockScreen.exe"
Name: "{commondesktop}\PCLockScreen"; Filename: "{app}\PCLockScreen.exe"

[Run]
; Offer to run the app after install (set to no to avoid auto-run)
Filename: "{app}\PCLockScreen.exe"; Description: "Launch PCLockScreen"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
	UpgradeDir: string;
	FlagPath: string;
begin
	// Create an upgrade flag file in common appdata so the running app
	// can detect an installer is pending and exit gracefully.
	UpgradeDir := ExpandConstant('{commonappdata}\PCLockScreen');
	if not DirExists(UpgradeDir) then
		CreateDir(UpgradeDir);

	FlagPath := ExpandConstant('{commonappdata}\PCLockScreen\upgrading.flag');
	try
		SaveStringToFile(FlagPath, 'upgrading', False);
	except
		// ignore errors creating the flag
	end;

	// Give the application a short grace period to detect the flag and exit
	Sleep(5000);

	Result := True;
end;

procedure DeinitializeSetup();
var
	FlagPath: string;
begin
	FlagPath := ExpandConstant('{commonappdata}\PCLockScreen\upgrading.flag');
	if FileExists(FlagPath) then
	begin
		try
			DeleteFile(FlagPath);
		except
			// ignore
		end;
	end;
end;