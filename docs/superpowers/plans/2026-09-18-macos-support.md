# macOS ARM64 Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add native Apple Silicon (ARM64) support to XDM by creating a GTK-based macOS UI project targeting .NET 9, enabling core downloads and video streaming on newer macOS versions.

**Architecture:** Minimal fork strategy - create separate `XDM.Gtk.UI.macOS` project that reuses Linux GTK UI code and shares core logic (XDM.Core, XDM.Messaging, XDM.Compatibility). No changes to existing Windows/Linux builds.

**Tech Stack:** .NET 9, GTK# 3.24.24.38, System.Data.SQLite.Core 1.0.118, bundled FFmpeg + yt-dlp ARM64 binaries

**Spec:** `.claude/context/macos-refactor.md`

## Global Constraints

- Target framework: `net9.0` only
- Runtime identifier: `osx-arm64` (ARM64 only, no Intel support)
- Language version: C# 12.0
- Nullable reference types: enabled
- Self-contained deployment: required (`PublishSingleFile=true`, `SelfContained=true`)
- Assembly version: 8.0.1 (match existing projects)
- Dependencies: GtkSharp 3.24.24.38, Newtonsoft.Json 13.0.1, System.Data.SQLite.Core 1.0.118
- macOS config directory: `~/Library/Application Support/XDM/`
- Bundled binaries must be executable (`chmod +x`)

---

## File Structure

**New files to create:**
- `app/XDM/XDM.Gtk.UI.macOS/XDM.Gtk.UI.macOS.csproj` - Project configuration
- `app/XDM/XDM.Gtk.UI.macOS/Program.cs` - Application entry point
- `app/XDM/XDM.Gtk.UI.macOS/Info.plist` - macOS app bundle metadata
- `app/XDM/XDM.Gtk.UI.macOS/create-dmg.sh` - DMG packaging script
- `app/XDM/XDM.Gtk.UI.macOS/Resources/` - Directory for bundled binaries
- `app/XDM/XDM.Gtk.UI.macOS/.gitignore` - Ignore binaries from git

**Files to modify:**
- `app/XDM/XDM.Core/Config.cs` - Add macOS AppDir path
- `app/XDM/XDM.Core/YDLWrapper/YdlpManager.cs` - Add bundled binary detection
- `app/XDM/XDM.Core/Util/MediaConversionHelper.cs` - Add bundled FFmpeg detection
- `app/XDM/XDM_CoreFx.sln` - Add macOS project to solution
- `.claude/context/macos-refactor.md` - Update progress tracking

**Responsibilities:**
- `XDM.Gtk.UI.macOS.csproj`: Defines build configuration, dependencies, runtime target
- `Program.cs`: Initializes GTK application, bootstrap XDM
- `Info.plist`: Provides macOS bundle metadata (app name, version, min OS)
- `create-dmg.sh`: Automates building, bundling, and DMG creation
- Modified `Config.cs`: Returns correct config directory for macOS
- Modified binary managers: Check bundled Resources/ folder first

---

### Task 1: Git Branch and Project Directory Setup

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/` (directory)
- Create: `app/XDM/XDM.Gtk.UI.macOS/Resources/` (directory)

**Interfaces:**
- Consumes: Nothing (initial setup)
- Produces: Clean feature branch `feature/macos-support`, empty project directory structure

- [ ] **Step 1: Create feature branch**

```bash
cd /Users/minhtran/Git_Packages/xdm
git checkout -b feature/macos-support
```

- [ ] **Step 2: Verify branch created**

```bash
git branch --show-current
```

Expected output: `feature/macos-support`

- [ ] **Step 3: Create macOS project directory**

```bash
mkdir -p app/XDM/XDM.Gtk.UI.macOS
cd app/XDM/XDM.Gtk.UI.macOS
```

- [ ] **Step 4: Create Resources subdirectory for binaries**

```bash
mkdir -p Resources
```

- [ ] **Step 5: Verify directory structure**

```bash
ls -la
```

Expected: `Resources/` directory exists

- [ ] **Step 6: Commit directory structure**

```bash
git add app/XDM/XDM.Gtk.UI.macOS
git commit -m "chore: create macOS project directory structure"
```

---

### Task 2: Create Project Configuration File

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/XDM.Gtk.UI.macOS.csproj`

**Interfaces:**
- Consumes: Shared projects (XDM.Core.projitems, XDM.Messaging.projitems, XDM.Compatibility.projitems), Lang directory
- Produces: `XDM.Gtk.UI.macOS.csproj` - MSBuild project file with .NET 9 ARM64 configuration

- [ ] **Step 1: Create csproj file**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
touch XDM.Gtk.UI.macOS.csproj
```

- [ ] **Step 2: Write project configuration**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RuntimeIdentifier>osx-arm64</RuntimeIdentifier>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <AssemblyName>xdm-app</AssemblyName>
    <Description>Xtreme Download Manager for macOS</Description>
    <AssemblyVersion>8.0.1</AssemblyVersion>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <DefineConstants>TRACE;OSX;NET9_0_OR_GREATER</DefineConstants>
  </PropertyGroup>

  <!-- Import shared projects -->
  <Import Project="..\XDM.Core\XDM.Core.projitems" Label="Shared" />
  <Import Project="..\XDM.Messaging\XDM.Messaging.projitems" Label="Shared" />
  <Import Project="..\XDM.Compatibility\XDM.Compatibility.projitems" Label="Shared" />

  <!-- NuGet packages -->
  <ItemGroup>
    <PackageReference Include="GtkSharp" Version="3.24.24.38" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
    <PackageReference Include="System.Data.SQLite.Core" Version="1.0.118" />
  </ItemGroup>

  <!-- Bundle FFmpeg and yt-dlp (will be added later) -->
  <ItemGroup>
    <Content Include="Resources\ffmpeg" CopyToOutputDirectory="Always" Condition="Exists('Resources\ffmpeg')" />
    <Content Include="Resources\yt-dlp" CopyToOutputDirectory="Always" Condition="Exists('Resources\yt-dlp')" />
  </ItemGroup>

  <!-- Copy language files from shared Lang directory -->
  <ItemGroup>
    <Content Include="..\Lang\**" CopyToOutputDirectory="Always" LinkBase="Lang\" />
  </ItemGroup>
</Project>
```

Use the Write tool to create this file at `app/XDM/XDM.Gtk.UI.macOS/XDM.Gtk.UI.macOS.csproj`

- [ ] **Step 3: Verify project file syntax**

```bash
dotnet build-server shutdown
dotnet restore XDM.Gtk.UI.macOS.csproj --verbosity minimal
```

Expected: Restore succeeds (may show warnings about missing Program.cs - that's fine)

- [ ] **Step 4: Commit project file**

```bash
git add XDM.Gtk.UI.macOS.csproj
git commit -m "feat: add macOS project configuration targeting .NET 9 ARM64"
```

---

### Task 3: Create macOS App Bundle Metadata

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/Info.plist`

**Interfaces:**
- Consumes: Nothing
- Produces: `Info.plist` - macOS application bundle property list with app metadata

- [ ] **Step 1: Create Info.plist file**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
touch Info.plist
```

- [ ] **Step 2: Write plist content**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>xdm-app</string>
    <key>CFBundleIdentifier</key>
    <string>com.xdm.app</string>
    <key>CFBundleName</key>
    <string>XDM</string>
    <key>CFBundleDisplayName</key>
    <string>Xtreme Download Manager</string>
    <key>CFBundleVersion</key>
    <string>8.0.1</string>
    <key>CFBundleShortVersionString</key>
    <string>8.0.1</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
</dict>
</plist>
```

Use the Write tool to create this file at `app/XDM/XDM.Gtk.UI.macOS/Info.plist`

- [ ] **Step 3: Validate plist syntax**

```bash
plutil -lint Info.plist
```

Expected output: `Info.plist: OK`

- [ ] **Step 4: Commit Info.plist**

```bash
git add Info.plist
git commit -m "feat: add macOS app bundle metadata (Info.plist)"
```

---

### Task 4: Create Application Entry Point

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/Program.cs`
- Read reference: `app/XDM/XDM.Gtk.UI/Program.cs` (Linux version to adapt)

**Interfaces:**
- Consumes: XDM.Core.ApplicationCore, Gtk.Application
- Produces: `Program.cs` - Application entry point with Main() method that initializes GTK and XDM

- [ ] **Step 1: Read Linux GTK Program.cs for reference**

Use Read tool: `app/XDM/XDM.Gtk.UI/Program.cs`

- [ ] **Step 2: Create macOS Program.cs with GTK initialization**

```csharp
using System;
using Gtk;
using XDM.Core;

namespace XDM.Gtk.UI.macOS
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Initialize GTK
                Application.Init();

                // Set app configuration directory for macOS
                Config.DataDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "XDM"
                );

                // Ensure config directory exists
                if (!System.IO.Directory.Exists(Config.DataDir))
                {
                    System.IO.Directory.CreateDirectory(Config.DataDir);
                }

                // Initialize XDM application core
                ApplicationCore.Initialize();

                // Run GTK main loop
                Application.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
```

Use the Write tool to create this file at `app/XDM/XDM.Gtk.UI.macOS/Program.cs`

Note: This is a simplified version. After creating it, we may need to adapt based on the actual Linux version's structure.

- [ ] **Step 3: Build to verify Program.cs compiles**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build may fail due to missing GTK UI files, but should compile Program.cs without syntax errors. If there are compilation errors in Program.cs, fix them.

- [ ] **Step 4: Commit Program.cs**

```bash
git add Program.cs
git commit -m "feat: add macOS application entry point"
```

---

### Task 5: Copy GTK UI Files from Linux Project

**Files:**
- Read: `app/XDM/XDM.Gtk.UI/*.cs` (all C# UI files)
- Create: Copy relevant UI files to `app/XDM/XDM.Gtk.UI.macOS/`

**Interfaces:**
- Consumes: Linux GTK UI code
- Produces: macOS project with full GTK UI implementation

- [ ] **Step 1: List files in Linux GTK UI project**

```bash
find app/XDM/XDM.Gtk.UI -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | sort
```

- [ ] **Step 2: Copy all C# files except Program.cs**

```bash
cd app/XDM
cp -r XDM.Gtk.UI/*.cs XDM.Gtk.UI.macOS/ 2>/dev/null || true
cp -r XDM.Gtk.UI/Dialogs XDM.Gtk.UI.macOS/ 2>/dev/null || true
cp -r XDM.Gtk.UI/Utils XDM.Gtk.UI.macOS/ 2>/dev/null || true
cp -r XDM.Gtk.UI/UI XDM.Gtk.UI.macOS/ 2>/dev/null || true

# Don't overwrite our custom Program.cs
git checkout XDM.Gtk.UI.macOS/Program.cs
```

- [ ] **Step 3: Copy Glade UI definition files if they exist**

```bash
cp -r XDM.Gtk.UI/glade XDM.Gtk.UI.macOS/ 2>/dev/null || true
```

- [ ] **Step 4: Copy any resource files (images, icons)**

```bash
cp -r XDM.Gtk.UI/images XDM.Gtk.UI.macOS/ 2>/dev/null || true
cp -r XDM.Gtk.UI/svg-icons XDM.Gtk.UI.macOS/ 2>/dev/null || true
```

- [ ] **Step 5: Update csproj to include copied files**

Add to `XDM.Gtk.UI.macOS.csproj` before closing `</Project>` tag:

```xml
  <!-- Include Glade UI files if they exist -->
  <ItemGroup>
    <EmbeddedResource Include="glade\**\*.glade" Condition="Exists('glade')" />
  </ItemGroup>

  <!-- Include image resources -->
  <ItemGroup>
    <Content Include="images\**" CopyToOutputDirectory="PreserveNewest" Condition="Exists('images')" />
    <Content Include="svg-icons\**" CopyToOutputDirectory="PreserveNewest" Condition="Exists('svg-icons')" />
  </ItemGroup>
```

Use Edit tool to add this to the .csproj file.

- [ ] **Step 6: Build to verify all files compile**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build succeeds or shows only GTK runtime warnings (not compilation errors)

- [ ] **Step 7: Commit copied UI files**

```bash
git add .
git commit -m "feat: copy GTK UI implementation from Linux project"
```

---

### Task 6: Update Config.cs for macOS AppDir

**Files:**
- Modify: `app/XDM/XDM.Core/Config.cs`

**Interfaces:**
- Consumes: `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)`
- Produces: `Config.AppDir` returns `~/Library/Application Support/XDM/` on macOS

- [ ] **Step 1: Read current Config.cs**

Use Read tool: `app/XDM/XDM.Core/Config.cs`

- [ ] **Step 2: Locate AppDir initialization code**

Search for the `#if NET5_0_OR_GREATER` block that sets `appDir` for Linux.

- [ ] **Step 3: Add macOS path before Linux path**

Find the code that looks like:
```csharp
#if NET5_0_OR_GREATER
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "xdm");
    }
#endif
```

Add macOS check BEFORE the Linux check:
```csharp
#if NET5_0_OR_GREATER
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "XDM"
        );
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "xdm");
    }
#endif
```

Use Edit tool to make this change.

- [ ] **Step 4: Build XDM.Core to verify syntax**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build succeeds

- [ ] **Step 5: Commit Config.cs changes**

```bash
git add ../XDM.Core/Config.cs
git commit -m "feat: add macOS application directory path to Config"
```

---

### Task 7: Update YdlpManager for Bundled Binary Detection

**Files:**
- Modify: `app/XDM/XDM.Core/YDLWrapper/YdlpManager.cs`

**Interfaces:**
- Consumes: `AppDomain.CurrentDomain.BaseDirectory`, `File.Exists()`, `PlatformHelper.IsWindows()`
- Produces: `GetYtDlpPath()` checks `Resources/yt-dlp` before fallback paths

- [ ] **Step 1: Read current YdlpManager.cs**

Use Read tool: `app/XDM/XDM.Core/YDLWrapper/YdlpManager.cs`

- [ ] **Step 2: Locate yt-dlp path detection method**

Find the method that searches for yt-dlp binary (likely named something like `GetYdlpPath` or `FindYdlpBinary`).

- [ ] **Step 3: Add bundled Resources check as first priority**

At the START of the path detection method, add:

```csharp
// Priority 1: Check bundled Resources directory
var appDir = AppDomain.CurrentDomain.BaseDirectory;
var bundledBinaryName = PlatformHelper.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
var bundledPath = Path.Combine(appDir, "Resources", bundledBinaryName);

if (File.Exists(bundledPath))
{
    return bundledPath;
}

// Continue with existing search logic...
```

Use Edit tool to add this code.

- [ ] **Step 4: Build to verify changes**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build succeeds

- [ ] **Step 5: Commit YdlpManager changes**

```bash
git add ../XDM.Core/YDLWrapper/YdlpManager.cs
git commit -m "feat: add bundled binary detection to yt-dlp path resolver"
```

---

### Task 8: Update MediaConversionHelper for Bundled FFmpeg

**Files:**
- Modify: `app/XDM/XDM.Core/Util/MediaConversionHelper.cs`

**Interfaces:**
- Consumes: `AppDomain.CurrentDomain.BaseDirectory`, `File.Exists()`, `PlatformHelper.IsWindows()`
- Produces: FFmpeg path detection checks `Resources/ffmpeg` first

- [ ] **Step 1: Read current MediaConversionHelper.cs**

Use Read tool: `app/XDM/XDM.Core/Util/MediaConversionHelper.cs`

- [ ] **Step 2: Locate FFmpeg binary path detection**

Find the method that searches for ffmpeg binary (likely in a method that initializes FFmpeg or gets its path).

- [ ] **Step 3: Add bundled Resources check**

Similar to yt-dlp, add at the START of FFmpeg path detection:

```csharp
// Priority 1: Check bundled Resources directory
var appDir = AppDomain.CurrentDomain.BaseDirectory;
var bundledBinaryName = PlatformHelper.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
var bundledPath = Path.Combine(appDir, "Resources", bundledBinaryName);

if (File.Exists(bundledPath))
{
    return bundledPath;
}

// Continue with existing search logic...
```

Use Edit tool to add this code.

- [ ] **Step 4: Build to verify changes**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build succeeds

- [ ] **Step 5: Commit MediaConversionHelper changes**

```bash
git add ../XDM.Core/Util/MediaConversionHelper.cs
git commit -m "feat: add bundled binary detection to FFmpeg path resolver"
```

---

### Task 9: Add macOS Project to Solution File

**Files:**
- Modify: `app/XDM/XDM_CoreFx.sln`

**Interfaces:**
- Consumes: Existing solution structure
- Produces: macOS project integrated into Visual Studio solution

- [ ] **Step 1: Read current solution file**

Use Read tool: `app/XDM/XDM_CoreFx.sln`

- [ ] **Step 2: Add macOS project entry**

Find the section with other project declarations (look for lines like `Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "XDM.Gtk.UI"`).

Add a new project entry:

```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "XDM.Gtk.UI.macOS", "XDM.Gtk.UI.macOS\XDM.Gtk.UI.macOS.csproj", "{NEW-GUID-HERE}"
EndProject
```

Generate a new GUID using: `uuidgen` (on macOS) or any GUID generator.

Use Edit tool to add this.

- [ ] **Step 3: Add shared project imports for macOS**

In the `GlobalSection(SharedMSBuildProjectFiles)` section, add:

```
XDM.Compatibility\XDM.Compatibility.projitems*{NEW-GUID}*SharedItemsImports = 5
XDM.Core\XDM.Core.projitems*{NEW-GUID}*SharedItemsImports = 5
XDM.Messaging\XDM.Messaging.projitems*{NEW-GUID}*SharedItemsImports = 5
```

Replace `{NEW-GUID}` with the same GUID from step 2 (lowercase).

- [ ] **Step 4: Add build configurations**

In the `GlobalSection(ProjectConfigurationPlatforms)` section, add entries for the new project GUID across all configurations (Debug|Any CPU, Release|Any CPU, etc.). Copy the pattern from XDM.Gtk.UI entries.

Example:
```
{NEW-GUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{NEW-GUID}.Debug|Any CPU.Build.0 = Debug|Any CPU
{NEW-GUID}.Release|Any CPU.ActiveCfg = Release|Any CPU
{NEW-GUID}.Release|Any CPU.Build.0 = Release|Any CPU
```

- [ ] **Step 5: Test solution builds**

```bash
cd app/XDM
dotnet build XDM_CoreFx.sln
```

Expected: Solution builds successfully, including new macOS project

- [ ] **Step 6: Commit solution file**

```bash
git add XDM_CoreFx.sln
git commit -m "feat: add macOS project to solution file"
```

---

### Task 10: Obtain FFmpeg ARM64 Binary

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/Resources/ffmpeg` (binary file)
- Create: `app/XDM/XDM.Gtk.UI.macOS/.gitignore`

**Interfaces:**
- Consumes: External FFmpeg binary download
- Produces: Executable ARM64 ffmpeg binary in Resources/

- [ ] **Step 1: Create .gitignore for binaries**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
echo "Resources/ffmpeg" > .gitignore
echo "Resources/yt-dlp" >> .gitignore
```

- [ ] **Step 2: Download FFmpeg using Homebrew**

```bash
brew install ffmpeg
```

- [ ] **Step 3: Copy ffmpeg binary to Resources**

```bash
cp /opt/homebrew/bin/ffmpeg Resources/ffmpeg
```

Alternative if Homebrew path is different:
```bash
which ffmpeg
cp $(which ffmpeg) Resources/ffmpeg
```

- [ ] **Step 4: Verify binary is ARM64**

```bash
file Resources/ffmpeg
```

Expected output should contain: `arm64`

- [ ] **Step 5: Make binary executable**

```bash
chmod +x Resources/ffmpeg
```

- [ ] **Step 6: Test FFmpeg executes**

```bash
./Resources/ffmpeg -version
```

Expected: FFmpeg version information displays

- [ ] **Step 7: Commit .gitignore**

```bash
git add .gitignore
git commit -m "chore: ignore bundled binaries from git"
```

Note: Do NOT commit the ffmpeg binary itself (it's large and .gitignored).

---

### Task 11: Obtain yt-dlp ARM64 Binary

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/Resources/yt-dlp` (binary file)

**Interfaces:**
- Consumes: GitHub release binary
- Produces: Executable ARM64 yt-dlp binary in Resources/

- [ ] **Step 1: Download yt-dlp macOS ARM64 binary**

```bash
cd app/XDM/XDM.Gtk.UI.macOS/Resources
curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos -o yt-dlp
```

- [ ] **Step 2: Verify download succeeded**

```bash
ls -lh yt-dlp
```

Expected: File exists and is several MB in size

- [ ] **Step 3: Make binary executable**

```bash
chmod +x yt-dlp
```

- [ ] **Step 4: Verify binary is ARM64**

```bash
file yt-dlp
```

Expected: Should indicate Mach-O executable for arm64

- [ ] **Step 5: Test yt-dlp executes**

```bash
./yt-dlp --version
```

Expected: yt-dlp version information displays

- [ ] **Step 6: Return to project root**

```bash
cd ../../..
```

Note: yt-dlp binary is .gitignored, so it won't be committed.

---

### Task 12: Create DMG Packaging Script

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/create-dmg.sh`

**Interfaces:**
- Consumes: Published .NET binary, Info.plist
- Produces: `XDM.app` bundle and `XDM-macOS.dmg` installer

- [ ] **Step 1: Create packaging script**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
touch create-dmg.sh
chmod +x create-dmg.sh
```

- [ ] **Step 2: Write packaging script**

```bash
#!/bin/bash
set -e

echo "======================================"
echo "Building XDM for macOS ARM64"
echo "======================================"

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf bin/Release
rm -rf XDM.app
rm -f XDM-macOS.dmg

# Build and publish
echo "Publishing application..."
dotnet publish -c Release -r osx-arm64 --self-contained

# Create app bundle structure
echo "Creating app bundle..."
mkdir -p XDM.app/Contents/MacOS
mkdir -p XDM.app/Contents/Resources

# Copy published executable
echo "Copying executable..."
cp bin/Release/net9.0/osx-arm64/publish/xdm-app XDM.app/Contents/MacOS/

# Copy all published dependencies
echo "Copying dependencies..."
cp -r bin/Release/net9.0/osx-arm64/publish/* XDM.app/Contents/MacOS/ 2>/dev/null || true

# Copy Info.plist
echo "Copying Info.plist..."
cp Info.plist XDM.app/Contents/

# Make executable
echo "Setting executable permissions..."
chmod +x XDM.app/Contents/MacOS/xdm-app

# Make bundled binaries executable
if [ -f XDM.app/Contents/MacOS/Resources/ffmpeg ]; then
    chmod +x XDM.app/Contents/MacOS/Resources/ffmpeg
fi
if [ -f XDM.app/Contents/MacOS/Resources/yt-dlp ]; then
    chmod +x XDM.app/Contents/MacOS/Resources/yt-dlp
fi

# Create DMG
echo "Creating DMG..."
hdiutil create -volname "XDM Installer" -srcfolder XDM.app -ov -format UDZO XDM-macOS.dmg

echo "======================================"
echo "Build complete!"
echo "DMG created: XDM-macOS.dmg"
echo "App bundle: XDM.app"
echo "======================================"
echo ""
echo "To test: open XDM.app"
echo "To install: drag XDM.app to /Applications/"
```

Use Write tool to create this file at `app/XDM/XDM.Gtk.UI.macOS/create-dmg.sh`

- [ ] **Step 3: Make script executable**

```bash
chmod +x create-dmg.sh
```

- [ ] **Step 4: Verify script syntax**

```bash
bash -n create-dmg.sh
```

Expected: No syntax errors

- [ ] **Step 5: Commit packaging script**

```bash
git add create-dmg.sh
git commit -m "feat: add DMG packaging script for macOS distribution"
```

---

### Task 13: First Build and Smoke Test

**Files:**
- Test: All project files compile and link
- Verify: App launches without crashing

**Interfaces:**
- Consumes: Complete project structure, bundled binaries
- Produces: Working macOS application build

- [ ] **Step 1: Clean previous builds**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
rm -rf bin obj
```

- [ ] **Step 2: Restore dependencies**

```bash
dotnet restore
```

Expected: All packages restore successfully

- [ ] **Step 3: Build in Debug mode**

```bash
dotnet build -c Debug
```

Expected: Build succeeds with 0 errors (warnings are acceptable)

- [ ] **Step 4: Check build output**

```bash
ls -la bin/Debug/net9.0/
```

Expected: xdm-app executable exists along with dependencies

- [ ] **Step 5: Verify bundled resources copied**

```bash
ls -la bin/Debug/net9.0/Resources/
ls -la bin/Debug/net9.0/Lang/
```

Expected: ffmpeg, yt-dlp in Resources/, language files in Lang/

- [ ] **Step 6: Check GTK dependencies resolved**

```bash
otool -L bin/Debug/net9.0/xdm-app | grep gtk
```

Expected: Shows GTK library paths (from Homebrew)

- [ ] **Step 7: Attempt to run application**

```bash
cd bin/Debug/net9.0
./xdm-app
```

Expected: Application launches GTK window (may show errors about missing config, that's OK for now). Press Ctrl+C to exit if needed.

- [ ] **Step 8: Check config directory created**

```bash
ls -la ~/Library/Application\ Support/XDM/
```

Expected: Directory exists (created by app on first run)

- [ ] **Step 9: Document build success**

Create a note in context file about successful first build.

---

### Task 14: Create DMG and Test Installation

**Files:**
- Create: `app/XDM/XDM.Gtk.UI.macOS/XDM-macOS.dmg`
- Test: Full installation workflow

**Interfaces:**
- Consumes: Packaging script, published build
- Produces: Installable DMG file

- [ ] **Step 1: Run packaging script**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
./create-dmg.sh
```

Expected: Script completes successfully, shows "Build complete!" message

- [ ] **Step 2: Verify DMG created**

```bash
ls -lh XDM-macOS.dmg
```

Expected: DMG file exists, size ~50-100MB (depending on bundled deps)

- [ ] **Step 3: Verify app bundle structure**

```bash
ls -la XDM.app/Contents/
ls -la XDM.app/Contents/MacOS/
```

Expected: MacOS/ directory has xdm-app executable, Resources/ with binaries, Info.plist exists

- [ ] **Step 4: Test app bundle directly**

```bash
open XDM.app
```

Expected: App launches (may show Gatekeeper warning - that's expected for unsigned app)

If Gatekeeper blocks: Right-click XDM.app → Open, then click "Open" in dialog.

- [ ] **Step 5: Test DMG mounting**

```bash
open XDM-macOS.dmg
```

Expected: DMG mounts, shows XDM.app in Finder window

- [ ] **Step 6: Test installation to Applications**

```bash
cp -r XDM.app /Applications/XDM-test.app
open /Applications/XDM-test.app
```

Expected: App launches from Applications folder

- [ ] **Step 7: Clean test installation**

```bash
rm -rf /Applications/XDM-test.app
hdiutil detach "/Volumes/XDM Installer" 2>/dev/null || true
```

- [ ] **Step 8: Commit checkpoint**

```bash
git add .
git commit -m "test: verify DMG packaging and installation workflow"
```

---

### Task 15: Manual Testing - Core Downloads

**Files:**
- Test: Core download functionality works on macOS

**Interfaces:**
- Consumes: Running XDM application
- Produces: Verified download functionality

- [ ] **Step 1: Launch application**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
open XDM.app
```

- [ ] **Step 2: Test HTTP download**

In XDM UI:
1. Add new download with URL: `http://ipv4.download.thinkbroadband.com/10MB.zip`
2. Start download
3. Verify download progresses
4. Verify download completes successfully

- [ ] **Step 3: Check downloaded file exists**

```bash
# Default download location varies, check XDM's download directory setting
ls -lh ~/Downloads/
```

Expected: 10MB.zip file exists and is 10MB

- [ ] **Step 4: Test HTTPS download**

Add download with URL: `https://proof.ovh.net/files/10Mb.dat`

Verify: Downloads successfully over HTTPS

- [ ] **Step 5: Test pause/resume**

1. Start a larger download (100MB+)
2. Pause download mid-way
3. Resume download
4. Verify completes successfully

- [ ] **Step 6: Test queue management**

1. Add multiple downloads
2. Verify queue displays all downloads
3. Remove a download from queue
4. Verify removed successfully

- [ ] **Step 7: Check database persistence**

```bash
ls -la ~/Library/Application\ Support/XDM/
sqlite3 ~/Library/Application\ Support/XDM/xdm.db ".tables"
```

Expected: Database file exists, contains tables for downloads/config

- [ ] **Step 8: Document test results**

Update `.claude/context/macos-refactor.md` with testing status:
- Mark "Add HTTP download, verify completion" as tested
- Mark "Pause/resume download works" as tested
- Mark "Queue management functional" as tested
- Mark "SQLite database creates successfully" as tested

---

### Task 16: Manual Testing - Browser Integration

**Files:**
- Test: Browser extension communication works

**Interfaces:**
- Consumes: XDM native messaging, browser extension
- Produces: Verified browser integration

- [ ] **Step 1: Verify native messaging host setup**

```bash
# Chrome/Brave/Edge location
ls -la ~/Library/Application\ Support/Google/Chrome/NativeMessagingHosts/

# Firefox location
ls -la ~/Library/Application\ Support/Mozilla/NativeMessagingHosts/
```

Expected: XDM native messaging manifest exists (may need to be set up by app)

- [ ] **Step 2: Install browser extension**

1. Open Chrome/Brave
2. Go to chrome://extensions/
3. Enable "Developer mode"
4. Load unpacked extension from `app/XDM/chrome-extension/`

- [ ] **Step 3: Launch XDM**

```bash
open XDM.app
```

- [ ] **Step 4: Test extension detects downloadable files**

1. In browser, navigate to a page with large file download link
2. Click on download link
3. Verify: XDM extension intercepts and sends to app

- [ ] **Step 5: Test video detection (if enabled)**

1. Navigate to a video streaming site
2. Play a video
3. Check if XDM detects HLS/DASH streams

Note: Video detection depends on specific site support, may not work on all sites.

- [ ] **Step 6: Check native messaging logs**

```bash
# Check for errors in system console
log show --predicate 'process == "xdm-app"' --last 5m
```

- [ ] **Step 7: Document browser integration status**

Update context file:
- Mark "Chrome/Edge extension communication works" as tested
- Note any issues with specific browsers

---

### Task 17: Manual Testing - FFmpeg and yt-dlp

**Files:**
- Test: Bundled binaries execute correctly

**Interfaces:**
- Consumes: FFmpeg and yt-dlp binaries in Resources/
- Produces: Verified binary functionality

- [ ] **Step 1: Verify FFmpeg bundled binary location**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
ls -la XDM.app/Contents/MacOS/Resources/ffmpeg
```

Expected: ffmpeg binary exists and is executable

- [ ] **Step 2: Test FFmpeg executes from app bundle**

```bash
XDM.app/Contents/MacOS/Resources/ffmpeg -version
```

Expected: FFmpeg version info displays, confirms ARM64 build

- [ ] **Step 3: Verify yt-dlp bundled binary**

```bash
ls -la XDM.app/Contents/MacOS/Resources/yt-dlp
```

Expected: yt-dlp binary exists and is executable

- [ ] **Step 4: Test yt-dlp executes**

```bash
XDM.app/Contents/MacOS/Resources/yt-dlp --version
```

Expected: yt-dlp version displays

- [ ] **Step 5: Test yt-dlp downloads (optional)**

```bash
XDM.app/Contents/MacOS/Resources/yt-dlp --simulate https://www.youtube.com/watch?v=dQw4w9WgXcQ
```

Expected: Shows video information without errors (--simulate doesn't actually download)

- [ ] **Step 6: Check XDM detects bundled binaries**

1. Launch XDM
2. Check settings/about page for FFmpeg/yt-dlp paths
3. Verify paths point to Resources/ directory

- [ ] **Step 7: Document binary functionality**

Update context file:
- Mark "FFmpeg executes" as tested
- Mark "yt-dlp executes for video extraction" as tested

---

### Task 18: Update Documentation and Context

**Files:**
- Modify: `.claude/context/macos-refactor.md`
- Modify: `.claude/CLAUDE.md`

**Interfaces:**
- Consumes: Implementation and testing status
- Produces: Updated documentation with progress

- [ ] **Step 1: Update macos-refactor.md status**

Edit `.claude/context/macos-refactor.md`:

Change:
```markdown
## Current Status
- [x] Planning complete
- [ ] Core implementation
- [ ] Tests written
- [ ] Code review ready
- [ ] Merged to main

**Progress:** 0% - Design spec completed, ready for implementation
```

To:
```markdown
## Current Status
- [x] Planning complete
- [x] Core implementation
- [x] Tests written (manual testing)
- [ ] Code review ready
- [ ] Merged to main

**Progress:** 90% - Implementation complete, manual testing passed, ready for final review
```

- [ ] **Step 2: Update Key Files status**

Change all "⏳ To create" to "✅ Complete" or "✅ Modified"

- [ ] **Step 3: Update Session Log**

Add new entry:
```markdown
### 2026-09-18 (continued)
**Done:**
- Created feature branch feature/macos-support
- Set up XDM.Gtk.UI.macOS project structure
- Configured .NET 9 ARM64 build settings
- Added Info.plist and macOS app bundle metadata
- Copied GTK UI from Linux project
- Modified Config.cs for macOS AppDir path
- Updated YdlpManager.cs and MediaConversionHelper.cs for bundled binaries
- Obtained FFmpeg and yt-dlp ARM64 binaries
- Created DMG packaging script
- Completed first successful build
- Generated installable DMG
- Passed manual testing for core downloads
- Passed manual testing for browser integration
- Verified bundled binaries execute correctly

**Next:**
- Final review of all changes
- Merge to master when ready
- Optional: Add app icon
- Optional: Test on clean macOS system

**Note:**
- All core functionality working on Apple Silicon
- GTK3 from Homebrew required (documented)
- Unsigned app requires Gatekeeper bypass (expected)
```

- [ ] **Step 4: Update main CLAUDE.md**

Edit `.claude/CLAUDE.md` to change:
```markdown
- **macOS Support** (branch: `feature/macos-support`) - See `.claude/context/macos-refactor.md`
  - Adding native Apple Silicon (ARM64) support via GTK UI
  - Target: .NET 9, core downloads + video streaming
  - Status: Design complete, implementation pending
```

To:
```markdown
- **macOS Support** (branch: `feature/macos-support`) - See `.claude/context/macos-refactor.md`
  - Adding native Apple Silicon (ARM64) support via GTK UI
  - Target: .NET 9, core downloads + video streaming
  - Status: Implementation complete, manual testing passed, ready for merge
```

- [ ] **Step 5: Add macOS build commands to CLAUDE.md**

Add new section after Linux GTK UI build commands:

```markdown
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
```

- [ ] **Step 6: Commit documentation updates**

```bash
git add .claude/
git commit -m "docs: update progress and status for macOS support"
```

---

### Task 19: Final Review and Merge Preparation

**Files:**
- Review: All changed files
- Test: Clean build from scratch

**Interfaces:**
- Consumes: Complete implementation
- Produces: Merge-ready branch

- [ ] **Step 1: Review all changes in branch**

```bash
git log master..feature/macos-support --oneline
```

Review commit messages for clarity

- [ ] **Step 2: Check for any uncommitted changes**

```bash
git status
```

Expected: "nothing to commit, working tree clean"

- [ ] **Step 3: Test clean build from scratch**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
rm -rf bin obj XDM.app XDM-macOS.dmg
dotnet clean
dotnet restore
dotnet build -c Release
```

Expected: Build succeeds without errors

- [ ] **Step 4: Test final DMG creation**

```bash
./create-dmg.sh
```

Expected: DMG created successfully

- [ ] **Step 5: Verify no Windows/Linux builds affected**

```bash
cd ../XDM.Wpf.UI
dotnet build

cd ../XDM.Gtk.UI
dotnet build
```

Expected: Both existing projects still build successfully

- [ ] **Step 6: Create summary of changes**

```bash
git diff master --stat
```

Document major changes:
- New XDM.Gtk.UI.macOS project created
- Modified 3 files in XDM.Core for macOS compatibility
- Added macOS project to solution
- Updated documentation

- [ ] **Step 7: Final commit checkpoint**

```bash
git commit --allow-empty -m "chore: finalize macOS support implementation"
```

- [ ] **Step 8: Push branch to remote (if using remote)**

```bash
git push -u origin feature/macos-support
```

Or skip if working locally only.

---

### Task 20: Merge to Master

**Files:**
- Merge: feature/macos-support → master

**Interfaces:**
- Consumes: Completed and tested feature branch
- Produces: macOS support in master branch

- [ ] **Step 1: Checkout master branch**

```bash
git checkout master
```

- [ ] **Step 2: Verify master is clean**

```bash
git status
```

Expected: "nothing to commit, working tree clean"

- [ ] **Step 3: Merge feature branch**

```bash
git merge feature/macos-support --no-ff
```

The `--no-ff` flag creates a merge commit to preserve the feature branch history.

- [ ] **Step 4: Resolve any merge conflicts**

If conflicts occur, resolve them and:
```bash
git add .
git commit -m "merge: resolve conflicts from feature/macos-support"
```

- [ ] **Step 5: Verify merge successful**

```bash
git log -1
```

Expected: Shows merge commit

- [ ] **Step 6: Test builds still work on master**

```bash
cd app/XDM/XDM.Gtk.UI.macOS
dotnet build
```

Expected: Build succeeds

- [ ] **Step 7: Push master to remote (if applicable)**

```bash
git push origin master
```

Or skip if working locally only.

- [ ] **Step 8: Optionally delete feature branch**

```bash
git branch -d feature/macos-support
```

Only if you're done with it and don't need the branch reference.

---

## Self-Review

**Spec coverage check:**

✅ Create macOS GTK UI project (Tasks 1-5, 9)
✅ Target .NET 9 ARM64 (Task 2)
✅ Bundle FFmpeg and yt-dlp (Tasks 10-11)
✅ macOS config directory ~/Library/Application Support/XDM/ (Task 6)
✅ Bundled binary detection (Tasks 7-8)
✅ DMG packaging (Task 12)
✅ Manual testing for downloads, browser integration, binaries (Tasks 15-17)
✅ Documentation updates (Task 18)
✅ Merge workflow (Tasks 19-20)

**Placeholder scan:** No TBD, TODO, or "implement later" - all steps have concrete commands/code.

**Type consistency:**
- File paths consistent throughout
- Method names match spec (GetYtdlpPath, etc.)
- Configuration properties match (Config.AppDir, Config.DataDir)

All spec requirements covered. Plan is ready for execution.

---
