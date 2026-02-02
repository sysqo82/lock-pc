# PC Lock Screen

A Windows desktop application that locks the screen and prevents user interaction with time-based controls and admin authentication.

## Features

✅ **Full-Screen Lock**: Creates an overlay that blocks all interaction with Windows desktop  
✅ **Time-Based Controls**: Configure allowed usage hours (e.g., 8 AM to 10 PM)  
✅ **Admin Authentication**: Password-protected unlock mechanism  
✅ **Process Protection**: Prevents termination via Task Manager (requires admin privileges)  
✅ **Keyboard Block**: Disables common shortcuts like Alt+F4, Alt+Tab, Ctrl+Alt+Delete  

## Requirements

- Windows 10 or later
- .NET 6.0 SDK or later
- Administrator privileges (for full protection features)

## Building the Application

### Option 1: Using .NET CLI (Recommended)

1. Install [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) if not already installed

2. Open terminal in project directory:
   ```bash
   cd "d:\D backup\My Documents\projects\lock-pc"
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Publish as a single executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   ```

The executable will be in: `bin\Release\net6.0-windows\win-x64\publish\PCLockScreen.exe`

### Option 2: Using Visual Studio

1. Install [Visual Studio 2022](https://visualstudio.microsoft.com/) with ".NET Desktop Development" workload
2. Open `PCLockScreen.csproj` in Visual Studio
3. Right-click project → Publish → Create a publish profile
4. Select "Folder" as target
5. Configure settings and publish

## How to Use

### Initial Setup

1. **Run as Administrator**: Right-click `PCLockScreen.exe` → "Run as administrator"
   - This is required for Task Manager blocking features

2. **Set Admin Password**:
   - Enter a password in the "Admin Settings" section
   - Confirm the password
   - Click "Set Password"
   - **⚠️ Important**: Remember this password! You'll need it to unlock the screen

### Configure Time Restrictions (Optional)

1. Check "Enable Time Restrictions"
2. Set allowed hours in HH:mm format (24-hour):
   - Example: Start: `08:00`, End: `22:00`
3. Click "Save Configuration"

### Activate Lock Screen

1. Click "Activate Lock" button
2. The screen will be locked immediately
3. Configuration window will hide

### Unlocking

**Method 1: Admin Password**
- Enter the admin password on the lock screen
- Click "Unlock" or press Enter

**Method 2: Automatic (Time-based)**
- If time restrictions are enabled, the screen automatically unlocks during allowed hours

## Configuration Storage

Settings are stored in:
```
%APPDATA%\PCLockScreen\
├── config.json       (time settings)
└── password.dat      (encrypted password)
```

## Security Features

### 1. Task Manager Protection
- Modifies Windows registry to disable Task Manager
- Automatically re-enabled after unlocking

### 2. Keyboard Blocking
- Blocks Alt+F4 (close window)
- Blocks Alt+Tab (switch windows)
- Blocks Windows key
- Attempts to block Ctrl+Alt+Delete (limited by Windows security)

### 3. Window Protection
- Always-on-top window
- Cannot be minimized or closed
- Hides from taskbar
- Prevents close via OnClosing event

### 4. Password Security
- SHA-256 hashing
- Salt-based encryption
- Stored outside application directory

## Important Warnings

⚠️ **Administrator Access Required**: Full protection features require running as administrator.

⚠️ **Password Recovery**: There is NO password recovery mechanism. If you forget the admin password, you may need to:
- Delete `%APPDATA%\PCLockScreen\password.dat` from another account
- Restart the computer in Safe Mode

⚠️ **System Impact**: Task Manager blocking affects the entire user account, not just the application.

⚠️ **Development/Testing**: During testing, keep the configuration window open or remember your password!

## Troubleshooting

### Lock screen doesn't block Task Manager
- Ensure you're running as administrator
- Check if antivirus is blocking registry modifications

### Can't unlock with correct password
- Verify Caps Lock is off
- Try closing and reopening the application
- Check `%APPDATA%\PCLockScreen\password.dat` exists

### Application won't start
- Install [.NET 6.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)
- Right-click → Properties → Unblock the executable

## Uninstalling

1. Run the application and unlock the screen properly
2. Delete the executable
3. Optionally delete configuration:
   ```
   %APPDATA%\PCLockScreen\
   ```

## Development

### Project Structure
```
PCLockScreen/
├── App.xaml/.cs              - Application entry point
├── MainWindow.xaml/.cs       - Configuration interface
├── LockScreenWindow.xaml/.cs - Lock screen overlay
├── ConfigManager.cs          - Configuration management
├── ProcessProtection.cs      - Security features
├── app.manifest              - Admin privilege request
└── PCLockScreen.csproj       - Project file
```

### Technologies Used
- C# .NET 6.0
- WPF (Windows Presentation Foundation)
- Windows API (P/Invoke)
- Registry manipulation
- SHA-256 encryption

## License

This software is provided as-is for educational and personal use. Use responsibly and at your own risk.

## Disclaimer

This application modifies Windows system settings and can prevent normal system interaction. Always ensure you have:
- A working admin password set
- Understanding of how to unlock the system
- Ability to restart in Safe Mode if needed

The developers are not responsible for any system lockouts or data loss.
