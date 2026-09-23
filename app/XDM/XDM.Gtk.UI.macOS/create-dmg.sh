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
