#!/usr/bin/env bash
# The 2026-10-01 simulation matrix. Run from the repo root:  bash tools/probe/matrix.sh
# A = the game today; B = A + Codex side doors closed; C = A + fixed loop; D = fixed loop + heart
# tally in every flow + side doors closed (the proposed fix); L = D on gpt-6-luna.
P=tools/probe/bin/Release/net8.0/probe.exe
run() { echo "### $*"; $P run "$@" 2>&1 | grep -v "^\s*$"; }

run --case ira_reply     --label A --samples 2
run --case ira_smile     --label A --samples 1
run --case ira_firstword --label A --samples 2
run --case rh_reply      --label A --samples 2
run --case rh_compose    --label A --samples 2

run --case ira_reply --label B --suppress full --samples 2
run --case rh_reply  --label B --suppress full --samples 2

run --case ira_reply --label C --loop fixed --samples 2
run --case rh_reply  --label C --loop fixed --heart always --samples 2

run --case ira_reply     --label D --loop fixed --heart always --suppress full --samples 2
run --case ira_smile     --label D --loop fixed --heart always --suppress full --samples 1
run --case ira_firstword --label D --loop fixed --heart always --suppress full --samples 2
run --case rh_reply      --label D --loop fixed --heart always --suppress full --samples 2
run --case rh_compose    --label D --loop fixed --heart always --suppress full --samples 2

run --case ira_reply --label L --model gpt-6-luna --loop fixed --heart always --suppress full --samples 2
run --case rh_reply  --label L --model gpt-6-luna --loop fixed --heart always --suppress full --samples 2
run --case ira_reply --label LA --model gpt-6-luna --samples 1
echo "### MATRIX DONE"
