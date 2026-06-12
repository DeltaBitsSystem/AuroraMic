#!/bin/bash
set -e

VERSION="${1:-1.1.0}"
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
PUBLISH_DIR="$PROJECT_DIR/publish/server-aot"
OUTPUT="$PROJECT_DIR/publish/AuroraMic-${VERSION}-x86_64.AppImage"

echo "=== AuroraMic AppImage Builder ==="
echo "Version: $VERSION"
echo ""

# 1. NativeAOT publish
echo "[1/3] Publishing NativeAOT..."
dotnet publish "$PROJECT_DIR/AuroraMic.Server.Desktop/AuroraMic.Server.Desktop.fsproj" \
    -c Release \
    -r linux-x64 \
    -o "$PUBLISH_DIR"

# 2. Generate icon PNG if missing
ICON_SRC="$PROJECT_DIR/packaging/icons/256x256/auroramic.png"
if [ ! -f "$ICON_SRC" ]; then
    echo "Generating icon PNG from SVG..."
    mkdir -p "$PROJECT_DIR/packaging/icons/256x256"
    rsvg-convert -w 256 -h 256 "$PROJECT_DIR/packaging/auroramic.svg" -o "$ICON_SRC"
fi

# 3. Build AppImage
echo "[2/3] Building AppImage..."
cp "$ICON_SRC" "$PUBLISH_DIR/auroramic.png"
dotnetpackager appimage from-directory \
    --directory "$PUBLISH_DIR" \
    --output "$OUTPUT" \
    --application-name "AuroraMic" \
    --executable-name AuroraMic \
    --version "$VERSION" \
    --icon "$PUBLISH_DIR/auroramic.png" \
    --summary "Wireless desktop microphone via UDP" \
    --main-category Audio

echo ""
echo "=== Done ==="
echo "Output: $OUTPUT"
ls -lh "$OUTPUT"
