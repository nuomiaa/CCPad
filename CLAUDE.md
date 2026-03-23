# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CC Pad (CCPad.dev) — a modern Windows desktop application built with .NET 8.0 and WinUI 3 (Windows App SDK). The app uses MSIX packaging and supports x86, x64, and ARM64 platforms.

## Build Commands

```bash
# Build (debug)
dotnet build CCPad/CCPad.csproj

# Build (release)
dotnet build -c Release CCPad/CCPad.csproj

# Build for specific platform
dotnet build -c Release -r win-x64 CCPad/CCPad.csproj
```

No test or lint commands are configured in this project.

## Architecture

- **Entry point:** `CCPad/App.xaml.cs` — `OnLaunched` method initializes the main window
- **Main UI:** `CCPad/MainWindow.xaml` + `MainWindow.xaml.cs` — primary application window
- **UI pattern:** XAML markup with C# code-behind; MicaBackdrop for Windows 11 visual styling
- **Target:** Windows 10 Build 17763+ minimum; packaged (MSIX) or unpackaged launch modes

### Key Manifest Capabilities

`Package.appxmanifest` declares `runFullTrust` and `systemAIModels` — the app has full trust and access to Windows system AI models.

### Launch Profiles (`Properties/launchSettings.json`)

- **CC Pad (Package)** — runs via MSIX package
- **CC Pad (Unpackaged)** — runs as a plain executable

### Publish Profiles

Located in `Properties/PublishProfiles/`, one per target architecture (x86, x64, ARM64). Release builds enable ReadyToRun compilation and trimming.
