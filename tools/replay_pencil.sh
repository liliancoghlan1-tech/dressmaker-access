#!/bin/sh
# Load slot 1, open the sketchbook and walk its tutorial up to the pencil step.
T=/c/Users/hp/DressmakerAccess/tools
$T/replay_load.sh >/dev/null || exit 1
WAIT=3 $T/cmd.sh "key 3" >/dev/null
WAIT=1.5 $T/cmd.sh enter enter "select next bodice" "select next bodice" "select next skirt" "select next skirt" >/dev/null
sleep 2
WAIT=2 $T/cmd.sh enter >/dev/null
WAIT=4 $T/cmd.sh "select cool grey" >/dev/null
WAIT=2 $T/cmd.sh enter >/dev/null
echo "at pencil step"
