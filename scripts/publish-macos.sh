#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/publish-macos.sh [arm64|x64] [output-directory]
ARCH="${1:-arm64}"
OUTPUT_DIRECTORY="${2:-release/macos-${ARCH}}"

case "$OUTPUT_DIRECTORY" in
  ""|/*|*".."*) echo "Output directory must be a relative path inside the project root." >&2; exit 64 ;;
esac

case "$ARCH" in
  arm64) RUNTIME="osx-arm64" ;;
  x64) RUNTIME="osx-x64" ;;
  *) echo "Architecture must be arm64 or x64." >&2; exit 64 ;;
esac

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj"
ICON_FILE="$PROJECT_ROOT/src/DeepSeekBalanceWidget.Mac/Assets/DeepSeekBalanceWidget.icns"
OUTPUT_PATH="$PROJECT_ROOT/$OUTPUT_DIRECTORY"
PUBLISH_PATH="$OUTPUT_PATH/publish"
BUNDLE_PATH="$OUTPUT_PATH/DeepSeekBalanceWidget.app"

rm -rf "$PUBLISH_PATH" "$BUNDLE_PATH"
mkdir -p "$PUBLISH_PATH" "$BUNDLE_PATH/Contents/MacOS" "$BUNDLE_PATH/Contents/Resources"

if [[ ! -f "$ICON_FILE" ]]; then
  echo "Application icon not found: $ICON_FILE" >&2
  exit 1
fi

dotnet publish "$PROJECT_FILE" \
  --configuration Release \
  --runtime "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --output "$PUBLISH_PATH"

ditto "$PUBLISH_PATH" "$BUNDLE_PATH/Contents/MacOS"
chmod +x "$BUNDLE_PATH/Contents/MacOS/DeepSeekBalanceWidget"
cp "$PROJECT_ROOT/src/DeepSeekBalanceWidget.Mac/Info.plist" "$BUNDLE_PATH/Contents/Info.plist"
cp "$ICON_FILE" "$BUNDLE_PATH/Contents/Resources/DeepSeekBalanceWidget.icns"

echo "macOS application bundle created:"
echo "$BUNDLE_PATH"
echo "Open it with: open \"$BUNDLE_PATH\""
