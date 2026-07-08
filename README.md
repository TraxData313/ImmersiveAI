# Immersive AI

An immersive AI-NPC mod for Mount & Blade II: Bannerlord. NPCs converse via LLMs with
**persistent, layered memory**, distinct voices, and the ability to act on what is said.

A clean-from-scratch implementation inspired by the ChatAi workshop mod (studied via
decompilation, no code reused). Original code, freely publishable.

## Why this exists

ChatAi proved the concept but has two core weaknesses we fix by design:

1. **Repetitive NPCs** â€” it stuffs a truncated flat history into a single prompt with one
   generic system message shared by every NPC. Here, each NPC gets a real multi-turn
   conversation, a rolling summary of older exchanges, durable "known facts", and a
   distinct speech style.
2. **No real chat UI** â€” it reuses the vanilla text-input popup. A custom Gauntlet
   conversation window is on the roadmap.

## Architecture

| Project | Target | Purpose |
|---|---|---|
| `src/ImmersiveAI.Core` | netstandard2.0 | Game-independent logic: memory engine, prompt building, LLM abstraction. Fully unit-tested. |
| `src/ImmersiveAI.Module` | net472 | The Bannerlord module: campaign behaviors, dialogs, UI. References game DLLs from the local install. |
| `tests/ImmersiveAI.Core.Tests` | net8.0 | xUnit tests for Core. |

Key Core concepts:

- `NpcMemory` â€” three memory layers per NPC: verbatim `RecentTurns`, rolling `Summary`
  (LLM-compressed when turns exceed a threshold), and `KnownFacts` (distilled one-liners).
- `PromptBuilder` â€” system prompt (persona + scene + memory) plus real user/assistant
  message history.
- `IChatClient` â€” backend abstraction; Anthropic/OpenAI-compatible implementations live
  in the Module layer.

Memory token settings are configured as percentages of the selected model's context window:

- `MaxRecentMemoryPercent`: default `10`; compression starts when verbatim recent memory exceeds this share.
- `MinRecentMemoryPercentAfterCompression`: default `5`; compression shrinks verbatim recent memory toward this share.
- The raw `MaxRecentMemoryTokens` and `MinRecentMemoryTokensAfterCompression` values are derived from those percentages and the active model profile.


## Work flow for the TASKs
- Get the taks you work on from TASKS_TODO.md
- When dove move it to the end of TASKS_DONE.md, rename it if it changed or is badly formatted and add a done ts at the end (YYYY.MM.DD HH.MM.SS)
- When done with changed and tested them, recompile so the mod is rebuild automaticaly in C:\Users\Trax\Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI - dont ask the user to rebuild


## Build & test

Requires .NET 8 SDK and Bannerlord installed (path configured in `Directory.Build.props`).

```powershell
dotnet build            # build everything
dotnet test             # run unit tests
tools\deploy.ps1        # build + install into the game's Modules folder
```

Then enable "Immersive AI" in the Bannerlord launcher.


## To recompile (user):
To test after deploying:
- Close the game first (or be at the main menu) — otherwise the DLL is locked and the copy fails.
- Run the command below from the repo root (C:\Users\Trax\Documents\BannerlordMods\ImmersiveAI)
```powershell
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```
- Launch Bannerlord, make sure "Immersive AI" is enabled in the launcher, load your save, and talk to an NPC.