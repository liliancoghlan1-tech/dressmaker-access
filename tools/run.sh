#!/bin/sh
# Fresh launch of the test copy (minimised, no focus). Waits for the mod to load.
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
powershell -NoProfile -ExecutionPolicy Bypass -File /c/Users/hp/DressmakerAccess/tools/kill_test.ps1
rm -f "$LOG" /c/Users/hp/DressmakerTest/DressmakerAccess_cmd.txt
python /c/Users/hp/DressmakerAccess/tools/launch_test.py >/dev/null
for i in $(seq 1 60); do sleep 1; grep -q "Dressmaker Access loaded" "$LOG" 2>/dev/null && break; done
echo launched
