# Church Time Tracker

A local-first desktop timer for churches and volunteer teams. Data stays on the device in SQLite; no account or internet connection is required.

## What the app supports

- A universal workday timer plus a timer for the selected category
- One Pause/Resume control for both timers and an End Day workflow
- Recovery of an active workday after an accidental or ordinary app close
- Notes exported with the category entry they belong to
- Ten configurable quick-switch slots with global `Ctrl+Alt+1` through `Ctrl+Alt+0` shortcuts
- Date-range reports with 7-day, 30-day, and current-month presets
- Native Save dialog for formatted Excel exports
- Light and dark mode on Windows and macOS

## Development

Requirements:

- .NET 9 SDK with the MAUI workload
- Windows 10 1809+ with Visual Studio's MAUI tools, or macOS 12+ with a compatible Xcode release

```powershell
dotnet workload restore
dotnet restore ChurchTimeTracker.csproj
dotnet build ChurchTimeTracker.csproj -f net9.0-windows10.0.19041.0
```

On a Mac, use `-f net9.0-maccatalyst` for the final command.

## Installers and updates

Free GitHub Releases deployment is configured with a Velopack installer/updater on Windows and an unsigned DMG with Sparkle updates on macOS. See [RELEASING.md](RELEASING.md) for the one-time setup and the tag-based update workflow.

Signed MSIX and signed/notarized PKG scripts remain available as an optional future distribution path. See [DISTRIBUTION.md](DISTRIBUTION.md).

Application data is saved under the operating system's per-user application-data directory as `ChurchTimeTracker.db`. Installing an update with the same application identity preserves that database.
