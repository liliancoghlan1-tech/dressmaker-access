#!/bin/sh
# Load, open the cutting table on Cool Grey Linen, walk the tutorial's first cut by hand,
# cut the rest, put everything back, and return to the front desk (which saves).
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
$T/replay_load.sh >/dev/null || exit 1
WAIT=5 $T/cmd.sh "key 5" >/dev/null
WAIT=3 $T/cmd.sh "select cool grey" "select patterns" >/dev/null
WAIT=1.5 $T/cmd.sh dump >/dev/null
piece=$(tail -200 "$LOG" | grep "\[dump\]" | grep "'Pattern piece:" | grep -v "already cut" | head -1 | sed "s/.*'Pattern piece: \([^.,]*\).*/\1/")
if [ -n "$piece" ]; then
  WAIT=2.5 $T/cmd.sh "select pattern piece: $piece" >/dev/null
  WAIT=2 $T/cmd.sh enter >/dev/null                     # grain tip
  WAIT=2 $T/cmd.sh "cutkey g" "cutkey f" >/dev/null
  WAIT=6 $T/cmd.sh "cutkey c" >/dev/null
  WAIT=2 $T/cmd.sh enter >/dev/null                     # recut tip
  WAIT=3 $T/cmd.sh "select on the fabric" "cutkey back" >/dev/null
  sleep 2
  if tail -4 "$LOG" | grep -q "Press Enter to continue"; then WAIT=1.5 $T/cmd.sh enter >/dev/null; fi
fi
$T/cut_loop.sh
if tail -4 "$LOG" | grep -q "Press Enter to continue"; then WAIT=1.5 $T/cmd.sh enter >/dev/null; $T/cut_loop.sh; fi
WAIT=3 $T/cmd.sh "cutkey allback" >/dev/null
WAIT=4 $T/cmd.sh "key 1" >/dev/null
tail -12 "$LOG" | grep speech | cut -c38-250
