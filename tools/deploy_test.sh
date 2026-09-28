#!/bin/sh
# Build and copy the mod into the TEST copy only (stops the test copy first).
set -e
powershell -NoProfile -ExecutionPolicy Bypass -File /c/Users/hp/DressmakerAccess/tools/kill_test.ps1
cd /c/Users/hp/DressmakerAccess/src
dotnet build -c Release -o out > /tmp/dm_build.txt 2>&1 || { grep -E " error " /tmp/dm_build.txt | sort -u; exit 1; }
echo "Build succeeded."
mkdir -p /c/Users/hp/DressmakerTest/BepInEx/plugins/DressmakerAccess
cp out/DressmakerAccess.dll /c/Users/hp/DressmakerTest/BepInEx/plugins/DressmakerAccess/
echo deployed
