#!/bin/bash
set -e

APPNAME="AuroraMic"
ICON_NAME="auroramic"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

INSTALL_DIR="$HOME/Applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
DESKTOP_DIR="$HOME/.local/share/applications"

echo "=== AuroraMic Installer ==="

# Find AppImage
APPIMAGE=$(find "$SCRIPT_DIR" -maxdepth 1 -name "AuroraMic-*.AppImage" -type f | head -1)
if [ -z "$APPIMAGE" ]; then
    echo "Error: AuroraMic AppImage not found in $SCRIPT_DIR"
    exit 1
fi

# Find icon
ICON=$(find "$SCRIPT_DIR" -maxdepth 2 -name "auroramic.png" -type f | head -1)
if [ -z "$ICON" ]; then
    ICON="$SCRIPT_DIR/packaging/icons/256x256/auroramic.png"
    if [ ! -f "$ICON" ]; then
        echo "Error: auroramic.png not found"
        exit 1
    fi
fi

echo "AppImage: $APPIMAGE"
echo "Icon:     $ICON"

# Create directories
mkdir -p "$INSTALL_DIR" "$ICON_DIR" "$DESKTOP_DIR"

# Copy files
cp "$APPIMAGE" "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/$(basename "$APPIMAGE")"
cp "$ICON" "$ICON_DIR/$ICON_NAME.png"

# Create .desktop file
cat > "$DESKTOP_DIR/$ICON_NAME.desktop" << EOF
[Desktop Entry]
Type=Application
Name=$APPNAME
Exec=$HOME/Applications/$(basename "$APPIMAGE") %U
Icon=$ICON_NAME
Comment=Wireless desktop microphone via UDP
Categories=Audio;
Terminal=false
StartupWMClass=AuroraMic
EOF

# Update caches
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo ""
echo "=== Installed ==="
echo "AppImage: $INSTALL_DIR/$(basename "$APPIMAGE")"
echo "Desktop:  $DESKTOP_DIR/$ICON_NAME.desktop"
echo "Icon:     $ICON_DIR/$ICON_NAME.png"
echo ""
echo "$APPNAME should now appear in your application menu."
