# The release dance

The whole shipping ritual in one place, written down so it is never improvised again
(Anton, 2026.08.10: *"напиши си хубаво записки за този танц, да не го минаваме така всеки път"*).

**Who does what — settled:**

| | |
|---|---|
| **Claude** | version bump · the three change-note tiers · the store-page edits (measured) · `package.ps1` · **runs the Steam Workshop uploader** · git commit |
| **Anton** | pastes the descriptions into Steam and Nexus · uploads the zip to Nexus with its 255-char changelog · playtests |

Everything Claude does is repo-side and reversible. The two store *descriptions* and the Nexus
file are Anton's hands, because they are his pages.

---

## The dance, in order

### 1. Land the work first
- `dotnet test -c Release` green. Game-integration code is verified by Anton playing it —
  **never ship an unplaytested headline feature**; that is what the dev deploy is for.
- Every player-visible change already has its one-line pill under `[Unreleased]` in
  `CHANGELOG.md`. If it does not, it was written wrong the day it shipped.

### 2. Sweep for ghosts
Read `[Unreleased]` top to bottom and ask of every pill: *is this still true?* A feature built
and then retired mid-cycle leaves its pill behind, and it will announce a thing that no longer
exists. (Learned the hard way — see `TASKS_DONE.md`.)

### 3. Bump the version
`module\SubModule.xml` → `<Version value="vX.Y.Z" />`. `package.ps1` stamps the zip name from
it; nothing else needs touching.

**Then LOOK at the file.** A scripted bump that reads and writes the same path in one expression
truncates it, and an empty `SubModule.xml` packages a module the launcher cannot read — caught only
by grepping for the new version afterwards (2026.08.10). `git checkout -- module/SubModule.xml`
puts it back.

### 4. Write all three change-note tiers AT ONCE
They say the same news at three lengths. Writing them together is what keeps them honest.

1. **Nexus — 255 characters, HARD.** The fenced block at the top of the version's section in
   `CHANGELOG.md`. ~5 bullets, bite-sized, no markup. **Measure it** (`len(s)`), and leave a few
   characters spare. Anyone who wants the long version reads the CHANGELOG.
2. **Steam Workshop** — `tools\WorkshopUpdate.xml`'s `<ChangeNotes Value="..."/>`. Room for group
   headlines plus pills. Newlines are `&#10;`. **Any `"` inside the value must be `&quot;`** — a
   raw quote silently breaks the XML and the uploader then parses zero tasks.
3. **`CHANGELOG.md`** — the full grouped record. Retitle `[Unreleased]` to `## vX.Y.Z — DATE`,
   open a fresh empty `## [Unreleased]` above it, and give the section a one-line headline that
   says what the release IS ("When the words will not come, your own hero finds them for you").

### 5. The store pages — every word must be paid for
The live pages are `docs/steam-page-final.bbcode.txt`, `docs/nexus-page.bbcode.txt`, and the
pinned `docs/steam-faq.bbcode.txt`. All three sit **at their length limit**.

- **MEASURE STEAM IN BYTES, NOT CHARACTERS.** The cap is **8000 UTF-8 bytes** and going over
  fails with a bare *"There was a problem trying to save the title and description"*. Em dashes
  are 3 bytes each and the page carries ~40 of them, so `len(s)` understates by ~90.
  Use `len(s.encode('utf-8'))` and **leave 150–200 bytes spare**.
- Nexus is ASCII (bytes == chars). No hard cap, but treat its current length as the ceiling:
  an addition is paid for by a cut in the same file.
- **Pay for every new line by trimming elsewhere** — facts kept, words cut. Deep material goes to
  `docs/` and is LINKED, never pasted.
- **Put the headline feature in the "Looking for…?" list at the very top.** That list is the
  search bait — it is the highest-value real estate on the page, and one line there is worth
  three buried further down.
- **Never touch the closing "Final thoughts" block** — the donations / "if you insist you want to
  thank me" paragraph is Anton's own voice and is off limits (his standing instruction).
- `README.md` is the third description (the git one). No cap, but keep it in step with the other
  two: the same hook in its "Looking for…?" list and a bullet where the feature lives.

### 6. Package
```powershell
powershell -ExecutionPolicy Bypass -File tools\package.ps1
```
Rebuilds `dist\ImmersiveAI` from scratch and writes `dist\ImmersiveAI_vX.Y.Z.zip`.

**It will refuse to package a voice we may not ship.** `module\Voices\` travels with the mod, and a
voice folder carries the embedding *itself* — packaging one distributes that person's voice. The
`$neverShip` list at the top of the voices block in `package.ps1` stops the release dead, matching
the folder name AND the name/id inside `voice.json` so a rename cannot slip past. Development
copies are fine (`deploy.ps1` deliberately does not check); they simply may not leave the machine.
The fix is to move the folder out of the repo, never to soften the guard — unless it genuinely is a
different voice that merely shares a name.

**Check the package is actually current:** compare the timestamp of
`dist\ImmersiveAI\bin\Win64_Shipping_Client\ImmersiveAI.dll` against the newest source file. Any
code change after packaging — even a one-line fix — means packaging again.

### 7. Upload to Steam (Claude runs this)
Steam client open and logged in. Details and uploader quirks: `tools/WORKSHOP-UPLOAD.md`.

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Trax\Documents\BannerlordMods\ImmersiveAI\tools\WorkshopUpdate.xml"
```

- **Success is the words `Uploading done!` in the output — never the exit code.** The tool ends by
  crashing on a press-any-key read when run non-interactively (`System.InvalidOperationException:
  Cannot read keys…`, exit 82). That is normal and means nothing.
- Run it from a scratch directory: it drops a `steam_workshop_uploader.txt` in the working folder.
- It does NOT touch the title, description or visibility. Those are Owner Controls on the item
  page — Anton's hands.
- **The locally subscribed copy under `steamapps\workshop\content\261550\3764210301` lags** — it
  still shows the old version right after an upload. That is Steam's download schedule, not a
  failed upload. Verify on the item page, or launch the game once.

### 8. Anton's half
1. Item page → Owner Controls → paste `docs/steam-page-final.bbcode.txt` into the description.
2. Nexus → upload `dist\ImmersiveAI_vX.Y.Z.zip`, paste `docs/nexus-page.bbcode.txt` as the
   description and the CHANGELOG's fenced block into the per-version changelog field.
3. Both pinned FAQ threads, if `docs/steam-faq.bbcode.txt` changed.

### 9. Close the loop
- `TASKS_DONE.md` gets the release entry; `TASKS_TODO.md`'s shipping item is replaced with the
  next one (with what is left for Anton spelled out).
- Commit and tag: `git tag vX.Y.Z`.
- Enable the plain "Immersive AI" (not the `.Dev` copy) in the launcher and smoke-test the exact
  build subscribers get. **Never both at once.**

---

## Traps, each learned the hard way

- **A version prepared is not a version shipped.** v2.0.0 and v2.1.0 were both fully prepared —
  bumped, packaged, notes written — and never uploaded; the Workshop item sat at v1.4.1 while the
  repo said v2.1.0. **Before writing change notes, check what the store actually has** (the local
  subscribed `SubModule.xml` is a fair proxy). If the store is several versions behind, this
  update delivers all of them at once and the notes should say so.
- Change notes are written per upload and cannot be edited afterward without another upload.
- `<Tasks>` must be the FIRST node of the task XML — a declaration or comment above it makes the
  uploader parse zero tasks and exit looking successful.
- The Workshop item's title comes from `SubModule.xml <Name>`, not the task file.
- A PATCH needs no store-page work: the descriptions describe the mod, not the build. Only the
  Nexus file and its 255-char changelog change hands.
- `WorkshopCreate.xml` is spent — never run it again. `WorkshopUpdate.xml` is the one, forever.
