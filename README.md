# Lock PC – Windows App

The part of **Lock PC** that runs on the Windows computer itself. It locks the screen at the times you've chosen, so the computer simply can't be used outside its allowed hours.

You set the schedule from a web dashboard or your phone – this app just makes sure it's followed.

## What it does

- **Locks the screen on schedule** – a full-screen lock appears during blocked times and can't be closed, minimised or switched away from.
- **Gives a heads-up first** – a warning pops up shortly before the lock kicks in, so nobody loses their work.
- **Shows reminders** – today's reminders (homework, chores, anything you like) appear on the lock screen and as pop-ups.
- **Admin unlock** – the account password unlocks the computer early if needed.
- **Hard to get around** – while locked, it blocks the usual escape routes like Task Manager and keyboard shortcuts.
- **Starts with Windows** – so it's always on without anyone having to remember.
- **English and Hebrew** supported.

## Getting started

1. Install the app on the Windows PC (an installer can be built from `installer.iss`).
2. Log in with your Lock PC account – the PC links itself to your account.
3. Set up lock times and reminders from the dashboard or the Android admin app.

## ⚠️ Worth knowing

This app deliberately makes the computer hard to use while it's locked. Make sure you know your account password before turning it on.

## The Lock PC family

| Project | What it is |
|---|---|
| **lock-pc** | The Windows app that does the locking (this one) |
| [lock-pc-local](https://github.com/sysqo82/lock-pc-local) | The server and web dashboard, run from home |
| [lock-pc-android](https://github.com/sysqo82/lock-pc-android) | Phone app for managing schedules on the go |
| [lock-pc-android-reminders](https://github.com/sysqo82/lock-pc-android-reminders) | Phone app that receives reminders |
| [lock-pc-server](https://github.com/sysqo82/lock-pc-server) | The earlier cloud-hosted version of the server |

## How it's built

- **Language:** C# on **.NET 8**, with a **WPF** (Windows) interface.
- **Talking to the server:** stays connected to the Lock PC server using **Socket.IO**, so lock, unlock, pause and reminder changes arrive instantly.
- **Locking down the PC:** uses Windows system features to block keyboard shortcuts and Task Manager while locked.
- **Starting with Windows:** set up through **Windows Task Scheduler**.
- **Languages:** text lives in `Strings.resx` (English) and `Strings.he.resx` (Hebrew).
- **Installer:** built with **Inno Setup** (`installer.iss`).

## Building it yourself

You'll need **Windows 10 or later** and the **.NET 8 SDK** (or Visual Studio 2022 with ".NET desktop development").

- **In Visual Studio:** open `lock-pc.sln` and build.
- **From the command line**, to make a single `.exe`:
  ```bash
  dotnet publish -c Release PcLockScreen.csproj -r win-x64 --self-contained -p:PublishSingleFile=true
  ```
- **To make an installer:** open `installer.iss` in Inno Setup and compile it.

Run it as administrator so the protection features work fully.
