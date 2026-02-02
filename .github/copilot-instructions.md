# PC Lock Screen Project - C# WPF Application

## Project Overview
Windows screen lock application with time-based access controls and admin protection.

## Key Requirements
1. Full-screen blocking overlay
2. Time-based usage configuration
3. Admin authentication to unlock
4. Protection against Task Manager termination
5. Compile to Windows executable

## Technology Stack
- C# .NET 6.0
- WPF (Windows Presentation Foundation)
- Windows API integration (P/Invoke)
- Registry manipulation for Task Manager blocking

## Architecture
- **MainWindow**: Configuration interface for setting password and time restrictions
- **LockScreenWindow**: Full-screen overlay with unlock interface
- **ConfigManager**: Handles configuration persistence and password encryption
- **ProcessProtection**: Windows API integration for system protection

## Progress Tracking
- [x] Create copilot-instructions.md file
- [x] Get C# WPF project setup information
- [x] Scaffold C# WPF project structure
- [x] Implement core features
- [x] Create documentation
- [ ] Build and test

## Building
Requires .NET 6.0 SDK. Build with:
```bash
dotnet build
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Security Notes
- Application requires administrator privileges (app.manifest)
- Password stored with SHA-256 hashing
- Task Manager disabled via registry modification
- Hotkey blocking for Alt+F4, Alt+Tab, Windows key
