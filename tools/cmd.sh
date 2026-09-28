#!/bin/sh
# Usage: cmd.sh "<command>" ["<command>" ...]
# Sends each command to the running test copy (one at a time, waiting for it
# to be consumed), then prints the log lines that appeared since.
LOG=/c/Users/hp/DressmakerTest/BepInEx/LogOutput.log
CMD=/c/Users/hp/DressmakerTest/DressmakerAccess_cmd.txt
WAIT=${WAIT:-1.2}
before=$(grep -c "" "$LOG")
for c in "$@"; do
  printf '%s\n' "$c" >> "$CMD"
  for i in $(seq 1 40); do
    [ -s "$CMD" ] || break
    sleep 0.2
  done
  sleep "$WAIT"
done
tail -n +$((before + 1)) "$LOG" | grep -v "^\[Debug" | cut -c1-400
