#!/bin/bash
# AuroraMic Server launcher for Linux
# Installs to ~/.local/share/auroramic/

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INSTALL_DIR="$HOME/.local/share/auroramic"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"

echo "=== AuroraMic Server Installer ==="
echo ""

# Create directories
mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR"

# Copy binary
cp "$SCRIPT_DIR/publish/server/AuroraMic" "$INSTALL_DIR/AuroraMic"
chmod +x "$INSTALL_DIR/AuroraMic"

# Create launcher script
cat > "$BIN_DIR/auroramic-server" << 'LAUNCHER'
#!/bin/bash
exec "$HOME/.local/share/auroramic/AuroraMic" "$@"
LAUNCHER
chmod +x "$BIN_DIR/auroramic-server"

# Create .desktop file
cat > "$DESKTOP_DIR/auroramic-server.desktop" << DESKTOP
[Desktop Entry]
Type=Application
Name=AuroraMic Server
Comment=Wireless microphone server for Android
Exec=$HOME/.local/share/auroramic/AuroraMic
Icon=audio-input-microphone
Terminal=false
Categories=Audio;Utility;
Keywords=mic;microphone;stream;audio;
DESKTOP

# Update desktop database if available
if command -v update-desktop-database &>/dev/null; then
    update-desktop-database "$DESKTOP_DIR" 2>/dev/null
fi

echo "Installed to: $INSTALL_DIR/AuroraMic"
echo "Launcher:     $BIN_DIR/auroramic-server"
echo "Desktop:      $DESKTOP_DIR/auroramic-server.desktop"
echo ""
echo "Run with:  auroramic-server"
echo "   or:     $BIN_DIR/auroramic-server"
echo ""
echo "NOTE: Make sure $HOME/.local/bin is in your PATH."
echo "      Add this to ~/.bashrc if needed:"
echo "      export PATH=\"\$HOME/.local/bin:\$PATH\""
