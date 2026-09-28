#!/bin/sh
# Load slot 1, go to the cutting table, walk the tutorial, then lay/straighten/fit/cut every piece.
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
$T/replay_load.sh >/dev/null || exit 1
WAIT=5 $T/cmd.sh "key 5" >/dev/null
WAIT=3 $T/cmd.sh "select cool grey" "select black wool" "select patterns" >/dev/null
for round in $(seq 1 16); do
  WAIT=1.5 $T/cmd.sh dump >/dev/null
  piece=$(tail -200 "$LOG" | grep "\[dump\]" | grep "'Pattern piece:" | grep -v "already cut" | head -1 | sed "s/.*'Pattern piece: \([^.,]*\).*/\1/")
  [ -z "$piece" ] && break
  echo "== $piece"
  WAIT=2.5 $T/cmd.sh "select pattern piece: $piece" >/dev/null
  # tutorial tips (modal ones need Enter); harmless otherwise because nothing is focused
  WAIT=1.5 $T/cmd.sh "cutkey g" >/dev/null
  if tail -3 "$LOG" | grep -q "Overlapping"; then WAIT=1.5 $T/cmd.sh "cutkey f" >/dev/null; fi
  WAIT=1 $T/cmd.sh "cutkey c" >/dev/null
  sleep 5
  tail -6 "$LOG" | grep -E "speech" | cut -c38-200
  if tail -4 "$LOG" | grep -q "Press Enter to continue"; then WAIT=1.5 $T/cmd.sh enter >/dev/null; fi
done
WAIT=1 $T/cmd.sh details >/dev/null; sleep 1
tail -2 "$LOG" | grep speech | cut -c38-300
