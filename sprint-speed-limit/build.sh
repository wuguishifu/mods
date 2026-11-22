#!/usr/bin/env bash
# Builds SprintSpeedLimit.dll and copies it into the r2modman Default profile.
set -euo pipefail
cd "$(dirname "$0")"

CSC="/c/Program Files (x86)/Microsoft Visual Studio/18/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe"
MANAGED="/c/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data/Managed"
BEPINEX="$APPDATA/r2modmanPlus-local/REPO/profiles/Default/BepInEx"

mkdir -p bin
"$CSC" -nologo -target:library -optimize+ -nostdlib+ -langversion:latest \
  -out:bin/SprintSpeedLimit.dll \
  -r:"$MANAGED/mscorlib.dll" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/System.dll" \
  -r:"$MANAGED/System.Core.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  -r:"$MANAGED/Assembly-CSharp.dll" \
  -r:"$BEPINEX/core/BepInEx.dll" \
  -r:"$BEPINEX/core/0Harmony.dll" \
  Plugin.cs

mkdir -p "$BEPINEX/plugins/wuguishifu-SprintSpeedLimit"
cp bin/SprintSpeedLimit.dll "$BEPINEX/plugins/wuguishifu-SprintSpeedLimit/"
echo "Built and installed to $BEPINEX/plugins/wuguishifu-SprintSpeedLimit/"
