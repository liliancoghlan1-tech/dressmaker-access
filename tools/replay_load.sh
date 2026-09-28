#!/bin/sh
# Relaunch the test copy and load slot 1 (lands at the front desk).
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
retry() {
  for i in $(seq 1 20); do
    WAIT=1 $T/cmd.sh "$1" >/dev/null
    grep -q "$2" "$LOG" && return 0
  done
  echo "replay stuck at: $1"; exit 1
}
$T/run.sh >/dev/null
retry "select play" "\[click\] Play"
retry "select slot ${SLOT:-1}, saved" "\[speech\] Front desk"
sleep 2
echo "at front desk"
