ImmersiveAI probe harness (2026-10-01 review) — simulates real NPC calls outside the game.

It rebuilds the exact message list the mod sends (Core's PromptBuilder + the player's runtime files,
read from COPIES), drives the real ToolLoopRunner, and talks to Codex through an instrumented copy of
CodexAppServerChatClient. Nothing in src\ and nothing in the runtime folder is ever written.

Setup (once, and again whenever a tool's wording changes in src\ImmersiveAI.Module\Tools):
    python tools\probe\extract_tools.py
    dotnet build tools\probe -c Release

Commands (from the repo root):
    dotnet run --project tools\probe -c Release -- dump --case ira_reply
    dotnet run --project tools\probe -c Release -- weights [--case ira_reply,rh_compose] [--tag before]
        per-section token tally (the talk screen's own estimator) of the Codex-road call:
        sheet sections + hands block + transcript + schema → runs\<date>\weights_<tag>.tsv.
        The battle and nights sections are REBUILT from the campaign's ledgers through Core
        (Cases.RebuildScene), so BattleText/NightText changes show; the rest is the frozen file.
    dotnet run --project tools\probe -c Release -- run  --case ira_reply --samples 2
    dotnet run --project tools\probe -c Release -- run  --case rh_reply --loop fixed --heart always --suppress all
    dotnet run --project tools\probe -c Release -- overhead --suppress none|include|hooks|all [--ephemeral false]
    dotnet run --project tools\probe -c Release -- openai --case ira_reply --model gpt-6-sol   (BILLED, API key from config.json)
    dotnet run --project tools\probe -c Release -- anthropic --case ira_reply [--model claude-haiku-4-5]   (BILLED)
    run ... --road claude [--model haiku]     the Claude Code road (headless claude -p, the mod's flags)
    run|openai|anthropic ... --line "TEXT"    another player line on ira_reply's memory and scene
        (2026-10-01 heart-in-the-answer probes: warm / hurtful / neutral / a recall question)

Cases:  ira_reply (Anton: "Hey, wait, you didnt tell me?"), ira_smile ("You made me smille all day…"),
        ira_firstword (19.41 first word), rh_compose (spontaneous letter), rh_reply (answer to
        "your letter got lost").
Options for run:
    --model gpt-6-sol|gpt-6-luna     --effort low (Codex's floor for these models)
    --loop mod      the real ToolLoopRunner (the game today)
    --loop fixed    harness-only fix: words + silent hands = final; words beside a recall are a draft
    --heart game    a HeartTool.Tally only where the game passes one (player turn)
    --heart always  a tally in every flow (the proposed fix)
    --suppress none|include|hooks|all|full|max   Codex config overrides on top of the mod's (see Program.Suppression);
                    full/max also send the AGENTS.md neutralizer as developerInstructions; max also drops
                    Codex's clock.sleep + image_gen tools (the best set found, 2026-10-01)
    --developer TEXT  developerInstructions to send instead
    --codex-home DIR   CODEX_HOME for the spawned codex (default: the real ~/.codex, as the mod)
    --extra '{"key": value}'   any further thread/start config override
    --ephemeral false   keep the rollout (~/.codex/sessions/...) to read what Codex really sent; DELETE it after
Output: tools\probe\runs\<date>\  — results.tsv, overhead.tsv, one JSON per sample, events\*.jsonl
(every app-server line; account e-mail redacted). runs\ is git-ignored.

Approximations (no full_prompt_snapshot.txt existed): the role/traits/crafts/kin lines are hand-written
in Cases.cs; Rhagaea's letter situation is her stale meeting-shaped file re-dated and closed in the
apart shape; recall tools answer from canned lines (Program.Resolve); move_heart mirrors the game's
ResolveHeartShift exactly.

Seeing the exact HTTP request Codex sends (the ground truth for what reaches the model):
    python tools\probe\capture_proxy.py          (background; forwards to chatgpt.com, never writes headers)
    probe.exe run ... --extra '{"openai_base_url":"http://127.0.0.1:8899/backend-api/codex"}'
Bodies land in runs\<date>\capture\HHMMSS_NNN_request.json, one per model sampling, INCLUDING the hidden
code-mode rounds that app-server never reports as items (count thread/tokenUsage/updated events per turn
in events\*.jsonl: more than one means hidden samplings). Stop the proxy when done.
Results of the 2026-10-01 run: .manager\simulation.md.
