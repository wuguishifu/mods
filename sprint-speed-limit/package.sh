#!/usr/bin/env bash
# Builds the mod and creates a Thunderstore-ready zip in dist/.
# Bump "version_number" in manifest.json before running; Plugin.cs is synced automatically.
set -euo pipefail
cd "$(dirname "$0")"

NAME=$(grep -oP '"name"\s*:\s*"\K[^"]+' manifest.json)
VERSION=$(grep -oP '"version_number"\s*:\s*"\K[^"]+' manifest.json)

# Keep the plugin version in Plugin.cs in sync with manifest.json
sed -i -E "s/(PluginVersion = \")[^\"]+(\")/\1${VERSION}\2/" Plugin.cs

bash build.sh

for f in manifest.json README.md icon.png bin/SprintSpeedLimit.dll; do
  [ -f "$f" ] || { echo "Missing $f" >&2; exit 1; }
done

STAGE="dist/stage"
ZIP="dist/${NAME}-${VERSION}.zip"
rm -rf "$STAGE" "$ZIP"
mkdir -p "$STAGE"
cp manifest.json README.md icon.png bin/SprintSpeedLimit.dll "$STAGE/"

# Files must sit at the root of the zip (no wrapping folder)
powershell.exe -NoProfile -Command \
  "Compress-Archive -Path '$(cygpath -w "$STAGE")\\*' -DestinationPath '$(cygpath -w "$ZIP")' -Force"
rm -rf "$STAGE"

echo "Package ready: $ZIP"
