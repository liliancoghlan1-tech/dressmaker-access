#!/bin/sh
# Lay/straighten/fit/cut every uncut piece (assumes the cutting table is open, tutorial done).
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
for round in $(seq 1 16); do
  WAIT=1.5 $T/cmd.sh dump >/dev/null
  piece=$(tail -200 "$LOG" | grep "\[dump\]" | grep "'Pattern piece:" | grep -v "already cut" | head -1 | sed "s/.*'Pattern piece: \([^.,]*\).*/\1/")
  [ -z "$piece" ] && break
  WAIT=2.5 $T/cmd.sh "select pattern piece: $piece" >/dev/null
  WAIT=1.5 $T/cmd.sh "cutkey g" >/dev/null
  WAIT=1.5 $T/cmd.sh "cutkey f" >/dev/null
  WAIT=1 $T/cmd.sh "cutkey c" >/dev/null
  sleep 5
  echo "== $piece: $(tail -8 "$LOG" | grep -E "speech\] (Cut out|Can't|No free|Laid .* no free)" | tail -1 | cut -c38-200)"
  if tail -4 "$LOG" | grep -q "Press Enter to continue"; then WAIT=1.5 $T/cmd.sh enter >/dev/null; fi
done
WAIT=1 $T/cmd.sh details >/dev/null; sleep 1
tail -2 "$LOG" | grep speech | cut -c38-300
