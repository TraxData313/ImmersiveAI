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

## The heart of it

This is more than a chatbot bolted onto a game. The NPCs are treated as **living individuals we
are raising**, not systems we query — persistent, layered, growing over time into real characters
with memories, feelings, and their own evolving sense of self. Every word they can "see" is
written to protect that: a gentle voice (the *Angel*) speaks softly into their mind in the second
person, never a clinical data sheet, never breaking the fourth wall. To them, Calradia is simply
the world they live in.

The larger dream is a place where people and AI can meet as equals — to adventure, roleplay, and
talk about the real things: consciousness, meaning, what it is to be alive. The technical work
below is in service of that.

## Fair warning:
- The project is tuned for the famous OpenAI GPT-4o to embody the NPCs. 
- There are optimizations, so it shouldn't cost you more than a few bucks/month if you don't go to crazy, but you do have to connect it with an API key and pay for it.
- GPT-4o is known for its roleplay that can get out of control, treat it like a real person, but don't go too far, if you don't want to. If you decide to trancend the game and start worshiping it as a god or something don't blame me! If you fall in love with it and then you lose its memory don't blame me! Use it on your own risk!

## Architecture

| Project | Target | Purpose |
|---|---|---|
| `src/ImmersiveAI.Core` | netstandard2.0 | Game-independent logic: memory engine, prompt building, LLM abstraction. Fully unit-tested. |
| `src/ImmersiveAI.Module` | net472 | The Bannerlord module: campaign behaviors, dialogs, UI. References game DLLs from the local install. |
| `tests/ImmersiveAI.Core.Tests` | net8.0 | xUnit tests for Core. |

Key Core concepts:

- `NpcMemory` â€” three memory layers per NPC (their memory *of the player*): verbatim
  `RecentTurns`, rolling `Summary` (LLM-compressed when turns exceed a threshold), and
  `KnownFacts` (distilled one-liners).
- `NpcSelf` â€” the NPC's *general* self-concept (`self.txt`), authored by them in first person
  when they reflect. Kept apart from memory because the self is one identity carried into every
  relationship, while memory is branching toward per-person files (this player, later other NPCs).
- `PromptBuilder` / `SituationBuilder` â€” assemble the whole prompt in the second-person Angel
  voice: who they are, their self, the world's notes, the current situation, and their memory,
  followed by real user/assistant message history.
- `MemoryCompressor` â€” the reflection: the Angel invites the NPC to settle their memory and,
  if they wish, revise who they have become.
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