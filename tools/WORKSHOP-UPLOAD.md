# Updating the mod on the Steam Workshop

The upload path is Bannerlord's **own official uploader** —
`TaleWorlds.MountAndBlade.SteamWorkshop.exe` in the game's `bin\Win64_Shipping_Client`.
It rides the **already-logged-in Steam client**: no SteamCMD, no password, no Steam Guard.
Just have Steam running and logged in.

Item: **3764210301** (already created via `WorkshopCreate.xml` — never run that one again;
`WorkshopUpdate.xml` is the one for every update from now on).

## The whole update loop (3 steps)

1. **Package a clean build** (from the repo root):
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1
   ```
   This rebuilds `dist\ImmersiveAI` from scratch — that folder is what gets uploaded.

2. **Edit `tools\WorkshopUpdate.xml`**: set `<ChangeNotes Value="..."/>` to what changed
   in this release (players see it on the item's Change Notes tab). Bump the game-version
   `<Tag>` if the supported game version moved.

3. **Run the uploader** (Steam open and logged in):
   ```powershell
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Trax\Documents\BannerlordMods\ImmersiveAI\tools\WorkshopUpdate.xml"
   ```

## Uploader quirks (decompiled 2026.07.13 — trust these)

- The root `<Tasks>` element must be the **first node** of the task file. An `<?xml?>`
  declaration or a comment above it makes the tool parse ZERO tasks and exit as if
  successful. Comments are only safe INSIDE `<GetItem>`/`<UpdateItem>`.
- The item **title** comes from the module's `SubModule.xml <Name>`, not the task file.
- The tool ends by writing `steam_workshop_uploader.txt` to the working directory and then
  **crashing on a harmless press-any-key read** when run non-interactively. Judge success
  by **"Uploading done!"** in the output, never by the exit code.
- Title/description/visibility on the Workshop page are NOT touched by `WorkshopUpdate.xml`
  (it has no `<ItemDescription>`/`<Visibility>`) — edit those on the item page itself
  (Owner Controls), including flipping Private → Public.

## Sanity check after upload

Steam re-downloads the item; enable the plain "Immersive AI" (not the .Dev copy) in the
launcher and smoke-test the exact build subscribers get. Never enable both at once.
