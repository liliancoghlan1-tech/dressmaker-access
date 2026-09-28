#!/bin/sh
# Relaunch the test copy and replay: load slot 1 -> bell -> Rose's first
# conversation -> accept (with tutorial) -> close the measuring tip.
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log

# retry <command> <log pattern>: resend until the pattern shows up in the log
retry() {
  for i in $(seq 1 20); do
    WAIT=1 $T/cmd.sh "$1" >/dev/null
    grep -q "$2" "$LOG" && return 0
  done
  echo "replay stuck at: $1"; exit 1
}

$T/run.sh >/dev/null
retry "select play" "\[click\] Play"
retry "select slot 2, saved" "\[speech\] Front desk"
sleep 2
retry "select bell" "\[speech\] Rose:"
WAIT=1.8 $T/cmd.sh enter enter "key 1" enter enter enter enter enter enter enter >/dev/null
WAIT=4 $T/cmd.sh "key 1" >/dev/null
WAIT=1.5 $T/cmd.sh enter >/dev/null
echo "at measuring"
