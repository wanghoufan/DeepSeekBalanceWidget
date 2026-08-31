#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/install-macos.sh [arm64|x64]
ARCH="${1:-arm64}"
case "$ARCH" in
  arm64|x64) ;;
  *) echo "Architecture must be arm64 or x64." >&2; exit 64 ;;
esac

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_APP="$PROJECT_ROOT/release/macos-${ARCH}/DeepSeekBalanceWidget.app"
APPLICATIONS_DIRECTORY="$HOME/Applications"
TARGET_APP="$APPLICATIONS_DIRECTORY/DeepSeekBalanceWidget.app"

if [[ ! -d "$SOURCE_APP" ]]; then
  echo "macOS app bundle not found: $SOURCE_APP" >&2
  echo "Build it first: bash scripts/publish-macos.sh $ARCH" >&2
  exit 1
fi

mkdir -p "$APPLICATIONS_DIRECTORY"
if [[ -e "$TARGET_APP" ]]; then
  BACKUP_APP="$APPLICATIONS_DIRECTORY/DeepSeekBalanceWidget.backup-$(date +%Y%m%d%H%M%S).app"
  mv "$TARGET_APP" "$BACKUP_APP"
  echo "Existing app backed up to: $BACKUP_APP"
fi

ditto "$SOURCE_APP" "$TARGET_APP"
open "$TARGET_APP"

echo "Installed and opened: $TARGET_APP"
echo "It is now available in Launchpad and Finder > Applications."
