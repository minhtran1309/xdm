# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Xtreme Download Manager (XDM) is a cross-platform download manager that increases download speeds, saves streaming videos, and integrates with major browsers. The project is built with .NET/C# and supports Windows (WPF UI), Linux (GTK UI), macOS (GTK UI), and browser extensions (Chrome/Firefox/Edge).

## Build Commands

### Windows (WPF UI)
```bash
# Navigate to the WPF UI project
cd app/XDM/XDM.Wpf.UI

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test
```

### Linux (GTK UI)
```bash
# Navigate to the GTK UI project
cd app/XDM/XDM.Gtk.UI

# Restore dependencies
dotnet restore

# Build the project
dotnet build
```

### macOS (GTK UI on Apple Silicon)
```bash
# Navigate to the macOS GTK UI project
cd app/XDM/XDM.Gtk.UI.macOS

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Create DMG installer
./create-dmg.sh
```

**Prerequisites:**
- macOS 12.0+ on Apple Silicon (ARM64)
- .NET 9 SDK: `brew install dotnet`
- GTK3: `brew install gtk+3`
- FFmpeg and yt-dlp binaries in Resources/ directory

### Build entire solution
```bash
# From app/XDM directory
dotnet build XDM_CoreFx.sln
```

## Architecture

### Shared Project Structure

The codebase uses a shared project architecture where core functionality is separated from platform-specific UI implementations:

- **XDM.Core** (shared project): Core download engine, HTTP clients, media parsers, scheduling, queue management, and business logic. Contains all download logic including chunked downloads, protocol support (HTTP/HTTPS/FTP/HLS/DASH), and browser integration.

- **XDM.Messaging** (shared project): Native messaging protocol implementation for browser extension communication.

- **XDM.Compatibility** (shared project): Platform compatibility layer and helper utilities.

### Platform-Specific UI Projects

- **XDM.Wpf.UI**: Windows desktop application using WPF (.NET 4.7.2)
- **XDM.Gtk.UI**: Linux desktop application using GTK
- **XDM.WinForms.IntegrationUI**: Windows Forms integration components

### Supporting Projects

- **NativeMessagingHost**: Native messaging host for browser extension communication
- **chrome-extension**: Browser extension source code (Chrome/Edge/Brave/Opera compatible)
- **firefox-amo**: Firefox-specific extension variant
- **MockServer**: Test HTTP server for system tests
- **XDM_Tests** / **XDM.SystemTests**: Unit and system test projects

### Key Components in XDM.Core

- **Downloader**: Multi-threaded download engine with chunking support
- **BrowserMonitoring**: Integration with browser extensions for capturing downloads
- **MediaParser**: Video streaming protocol parsers (HLS, DASH, HDS)
- **HttpServer**: Embedded HTTP server for browser extension communication
- **Clients/Http**: HTTP client implementations (WinHttpClient, NetFxHttpClient)
- **DataAccess**: Database layer using SQLite
- **YDLWrapper**: YouTube-DL integration for video site support

## Translation System

Translations are stored in `app/XDM/Lang/` as JSON files. Each language has its own file (e.g., `en.txt`, `es.txt`).

For translation generation tools, see `translation-generator/translation-gen/` which contains a React app for managing translations:
```bash
cd translation-generator/translation-gen
npm start       # Development mode
npm test        # Run tests
npm run build   # Production build
```

## Browser Extension Development

Browser extension source is in `app/XDM/chrome-extension/`. The extension uses:
- Native messaging to communicate with the desktop application
- Content scripts to detect downloadable media
- Background scripts for download interception

Firefox-specific extension is in `app/XDM/firefox-amo/`.

## Testing

Run all tests:
```bash
cd app/XDM
dotnet test XDM_CoreFx.sln
```

Run specific test project:
```bash
cd app/XDM/XDM_Tests
dotnet test
```

## Active Features / Work in Progress

Check `.claude/context/` for detailed specs on features currently in development:

- **macOS Support** (branch: `feature/macos-support`) - See `.claude/context/macos-refactor.md`
  - Adding native Apple Silicon (ARM64) support via GTK UI
  - Target: .NET 9, core downloads + video streaming
  - Status: Implementation complete, manual testing passed, ready for merge

## Important Notes

- The project uses .NET shared projects (.shproj) to share code between WPF and GTK implementations
- Target framework for Windows is .NET 4.7.2 for maximum compatibility
- The build copies browser extensions and language files to output directory automatically
- SQLite is used for persistent storage (downloads database, settings, queue)
- The application runs a local HTTP server to communicate with browser extensions via native messaging
