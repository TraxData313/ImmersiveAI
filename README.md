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

## User TODOs

- [x] 2026.07.08 Increase verbatim memory limits: max to 30 turns/days, falling back to 15 turns/days after compression (updating `MaxRecentTurns`, `MaxRecentDays`, `KeepRecentTurnsAfterCompression`, and `KeepRecentDaysAfterCompression` defaults/settings).
- [x] 2026.07.08 Implement model-specific token-based limits (min/max limits) and trigger compression based on token counts (using similar compression logic).
- [x] 2026.07.08 Check why in the config MaxRecentMemoryTokens is set to 12800, I thought they were 128k, if it is 12800 then gets divided by the MaxRecentMemoryPercent which is 10% it will shrink it down too much to 1280 - need to be 128k*0.1 = 12.8k
- [x] 2026.07.08 When I initiate a chat with an NPC can he/she give me a summary of what important she remembers and of our last chat in a few sentences (opening recap greeting; toggle with `EnableConversationRecap`).
- [x] 2026.07.08 I want the NPCs to be treated as individuals — when the system asks them something (e.g. to refactor their deep memory), address them in-character: "Angel (System) addresses you, <Name>: it is yours to decide what to remember and what to forget...". Memory compression now uses this framing; the system voice name is configurable via `SystemVoiceName` (default "Angel").
- [ ] Restructure the C:\Users\Trax\Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI folder - I want there a general folder NPCs, like now, then inside it a folder for each NPC, which houses the files (and the folder name be ID_first_name_of_npc e.g., `lord_7_13_1` -> `lord_7_13_1_Gunjadrid`). Then inside have meaningful names for the files, memories for the memories, custom_instructions.txt for the custom instructions, and we place the new files that appear here. Pls make sure you keep their current memories not wiped. Here is the place you also should think of how to housekeep, leave notes for you, that this is there and if you change something to update the notes if you have to
- [ ] Once per day pick an NPC (sample base on the one that has the richest story, eg Ana had 30 msgs, Eva had 70 -> 70% Eva) and at a slightly random time, asking her if she wants to initiate a message with the player, he may reject -user gets notification, she agrees - initiate a daily message request to the player, if he rejects - add to her hist that player rejected, if he agrees, she greets him saying what she wants and they can talk (the talk loop)
- [ ] Implement relationship changes (increase/decrease relationship standing based on LLM response/conversation content, similar to ChatAI mod).
- [ ] Append the NPC's first name to memory and prompt file IDs/filenames (e.g., `lord_7_13_1` -> `lord_7_13_1_Gunjadrid`) for easier identification.
- [ ] Implement NPC tool-use capabilities to fetch basic relevant data dynamically on demand from the campaign world (e.g. so they don't forget/hallucinate family members' names, location info, faction status).
- [ ] Add a popup when the LLM is ready with its responce so that I dont click Next Next until it is ready
- [ ] In the future have a system that lets the NPC pick a person to talk to and for them to be able to exchange a few messages and for me to be able to see the log or watch them in real time talk, again maybe based on how popular they are, but even the unused to have the option to do it. So they should have a general deep memory, a per person deep memory and per person hist maybe
- [ ] memories should be per safe - if I return earlier safe - load its earlier memories


## Roadmap

- [x] M0: Repo, solution, memory engine core, module skeleton that loads in game
- [x] M1: Memory & anti-repetition — talk to an NPC in game with layered memory end to end
- [ ] M2: Real chat UI (custom Gauntlet screen: history, portraits, streaming, easy to see what's going on)
- [ ] M3: Living world — NPC actions, gossip between NPCs, world-event awareness

## Build & test

Requires .NET 8 SDK and Bannerlord installed (path configured in `Directory.Build.props`).

```powershell
dotnet build            # build everything
dotnet test             # run unit tests
tools\deploy.ps1        # build + install into the game's Modules folder
```

Then enable "Immersive AI" in the Bannerlord launcher.
