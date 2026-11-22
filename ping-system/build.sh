#!/usr/bin/env bash
# Builds PingSystem.dll and copies it into the r2modman Default profile.
set -euo pipefail
cd "$(dirname "$0")"

CSC="/c/Program Files (x86)/Microsoft Visual Studio/18/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe"
MANAGED="/c/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data/Managed"
BEPINEX="$APPDATA/r2modmanPlus-local/REPO/profiles/Default/BepInEx"

REFS=()
for dll in mscorlib netstandard System System.Core \
           UnityEngine UnityEngine.CoreModule UnityEngine.PhysicsModule UnityEngine.IMGUIModule \
           UnityEngine.TextRenderingModule UnityEngine.InputLegacyModule Unity.InputSystem \
           PhotonUnityNetworking PhotonRealtime Photon3Unity3D Assembly-CSharp; do
  REFS+=("-r:$MANAGED/$dll.dll")
done
REFS+=("-r:$BEPINEX/core/BepInEx.dll" "-r:$BEPINEX/core/0Harmony.dll")

mkdir -p bin
"$CSC" -nologo -target:library -optimize+ -nostdlib+ -langversion:latest \
  -out:bin/PingSystem.dll "${REFS[@]}" Plugin.cs

mkdir -p "$BEPINEX/plugins/wuguishifu-PingSystem"
cp bin/PingSystem.dll "$BEPINEX/plugins/wuguishifu-PingSystem/"
echo "Built and installed to $BEPINEX/plugins/wuguishifu-PingSystem/"
