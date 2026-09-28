#!/bin/sh
# From a fresh launch: sketchbook tutorial -> princess bodice + princess skirt -> Draft Pattern.
T=/c/Users/hp/DressmakerAccess/tools
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
$T/replay_pencil.sh >/dev/null || exit 1
WAIT=2 $T/cmd.sh "select pencil" "select colour: dark red" >/dev/null
sleep 2
WAIT=1 $T/cmd.sh "select put the pencil" >/dev/null
# Cycle bodice/skirt until both are the princess ones.
for i in $(seq 1 12); do
  grep -q "Now Princess Bodice" "$LOG" 2>/dev/null
  WAIT=0.8 $T/cmd.sh dump >/dev/null
  if tail -200 "$LOG" | grep -q "Previous bodice. Now Princess Bodice'"; then break; fi
  WAIT=0.8 $T/cmd.sh "select next bodice. now" >/dev/null
done
for i in $(seq 1 12); do
  WAIT=0.8 $T/cmd.sh dump >/dev/null
  if tail -200 "$LOG" | grep -q "Previous skirt. Now Princess Skirt'"; then break; fi
  WAIT=0.8 $T/cmd.sh "select next skirt. now" >/dev/null
done
for i in $(seq 1 20); do
  WAIT=1 $T/cmd.sh dump >/dev/null
  if tail -200 "$LOG" | grep -q "'Draft Pattern'"; then break; fi
done
WAIT=4 $T/cmd.sh "select draft pattern" >/dev/null
WAIT=2 $T/cmd.sh enter >/dev/null
echo "at sizing"
