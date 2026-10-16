#!/bin/bash
set -e

echo "Building XDM..."
cd /Users/minhtran/Git_Packages/xdm/app/XDM
/opt/homebrew/bin/dotnet build -c Release XDM.Gtk.UI.macOS/XDM.Gtk.UI.macOS.csproj

echo "Stopping XDM..."
killall xdm-app 2>/dev/null || true
sleep 1

echo "Installing to /Applications/XDM.app..."
BUILD_DIR="/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Gtk.UI.macOS/bin/Release/net9.0/osx-arm64"
INSTALL_DIR="/Applications/XDM.app/Contents/MacOS"

# Copy all files
rsync -av --delete \
  --exclude='publish' \
  --exclude='*.deps.json' \
  --exclude='*.runtimeconfig.json' \
  "$BUILD_DIR/" "$INSTALL_DIR/"

echo "Installation complete!"
echo "Starting XDM..."
XDM_DEBUG_MODE=1 /Applications/XDM.app/Contents/MacOS/xdm-app &
sleep 2
pgrep -l xdm-app
echo "XDM started successfully!"
