# Diabolical — Project Spec

## Purpose
A lightweight Windows desktop app that scrapes equipment data from Diablo 4
screenshots and maintains a local JSON "database" of a character's gear.
That JSON is later fed to an AI assistant as context for build/gear planning.

This is a **hobby project** — prioritize simplicity and low maintenance
over robustness, scalability, or high-availability concerns.

## Core Flow
1. **Capture** — User presses a global hotkey, then drag-selects the tooltip
   region on screen. Fixed-region cropping isn't feasible since the item
   tooltip's position moves depending on where the cursor/item is in the
   game window, so the user manually selects the area each time.
2. **Vision LLM parse** — The cropped image is sent directly to
   **Gemini 2.5 Flash** (Google AI Studio free tier) with a fixed prompt
   asking for strict JSON output matching the schema below.
   - No OCR library. No regex parsing. The vision model replaces both steps.
3. **Parse response** — Strip any markdown code fences, deserialize into
   the `EquipmentItem` model via `System.Text.Json`.
4. **Review/Edit UI** — Show the parsed item to the user for confirmation
   or correction before it's saved. LLM output isn't blindly trusted.
5. **Save** — Merge/update the item into the character's local JSON file.
6. **Export** — From the UI, export a character's equipment JSON to the
   clipboard or to a standalone file, for handing off to an AI assistant
   as context. This is the actual point of the app, so it should be a
   one-click/one-command action, not buried in a menu.
7. **Quick Copy (alternate flow)** — A second global hotkey drag-selects
   a tooltip the same way as the main capture flow, sends it to the same
   configured vision provider, and copies the raw extracted item JSON
   straight to the clipboard. No Review/Edit dialog, no character context,
   no save to `data/characters/`. This is a throwaway lookup for pasting a
   single item into an AI assistant mid-session — not part of gear
   tracking, and doesn't touch `ItemDatabaseService`.

No fallback LLM providers needed (hobby scope) — if Gemini's free tier is
rate-limited, just wait and retry.

## Tech Stack
- **C# + WPF** (.NET, Windows-only)
- `System.Net.Http.HttpClient` — direct REST calls to Gemini API (no SDK)
- `System.Text.Json` — serialization
- Local JSON files for storage — no database engine

## JSON Schema (character equipment file)
```json
{
  "character": "MyBarb",
  "class": "Barbarian",
  "lastUpdated": "2026-07-06T00:00:00Z",
  "equipment": {
    "helm": {
      "name": "Rage of Harrogath",
      "rarity": "Unique",
      "quality": "Ancestral",
      "itemPower": 800,
      "affixes": [
        { "text": "+40% Fury Generation", "source": "Base" },
        { "text": "+180 Dexterity +[150 - 180] (Class Only)", "source": "Tempered" }
      ],
      "specialEffects": ["..."],
      "transfigured": false,
      "modifiable": true
    },
    "weapon1": { }
  }
}
```

Field notes:
- `rarity`: `Common | Magic | Rare | Legendary | Unique | Mythic`
- `quality`: `Normal | Ancestral` — separate axis from rarity
- `affixes`: each entry has `text` (verbatim stat line) and `source`:
  `Base | Tempered | Transfigured` — distinguishes a roll's origin, since
  Tempering and Transfiguration add affixes distinct from the item's base roll
- `specialEffects`: replaces a single `aspect` field. Holds zero entries
  (normal rares/magic items), one entry (a Legendary's imprinted aspect),
  or several (a Unique/Mythic's multiple passive effect paragraphs, or a
  Transfigured amulet's extra Legendary power via Kullean Tuning Prism)
- `transfigured` / `modifiable`: tracks Horadric Cube crafting state —
  whether the item has been Transfigured, and whether it can still be
  modified (tempered/masterworked/enchanted/imprinted) or is locked

## Project Structure
```
Diabolical/
├── .gitignore
├── README.md
├── LICENSE
├── Diabolical.sln
│
├── src/
│   └── Diabolical/
│       ├── Diabolical.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       │
│       ├── Views/
│       │   ├── MainWindow.xaml
│       │   ├── MainWindow.xaml.cs
│       │   ├── ReviewEditDialog.xaml      # confirm/edit parsed item before save
│       │   └── ReviewEditDialog.xaml.cs
│       │
│       ├── Services/
│       │   ├── ScreenCaptureService.cs    # hotkey + screenshot/crop
│       │   ├── GeminiVisionService.cs     # API call + prompt template
│       │   ├── ItemDatabaseService.cs     # read/write character JSON
│       │   └── HotkeyManager.cs           # global hotkey registration
│       │
│       ├── Models/
│       │   ├── EquipmentItem.cs
│       │   ├── CharacterEquipment.cs
│       │   └── AppSettings.cs             # API key, hotkey config, etc.
│       │
│       ├── Prompts/
│       │   └── item_extraction_prompt.txt # Gemini system prompt + schema/examples
│       │
│       └── Resources/
│           └── (icons, sample cropped regions for testing)
│
├── data/
│   └── characters/                        # gitignored — user's actual JSON output
│       └── .gitkeep
│
└── tests/
    └── Diabolical.Tests/                # optional; add if parsing proves fragile
        ├── Diabolical.Tests.csproj
        └── GeminiVisionServiceTests.cs
```

## Deployment
For running a standalone "production" copy independent of the dev
checkout (so active development in VS Code isn't interrupted):

```
dotnet publish src/Diabolical/Diabolical.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Framework-dependent, not self-contained — requires the .NET 8 Desktop
Runtime installed on the machine running it, but produces a much smaller
output (~a few MB vs. ~160MB self-contained). Does not reduce runtime
memory usage (the CLR + WPF stack still loads into the process either
way) — this is a disk-footprint choice, not a memory one.

Copy the resulting `publish/` output to a separate folder outside the
repo, alongside its own `appsettings.local.json` and `data/characters/`.
`RepoPaths.FindRepoRoot()` already supports this with no code changes —
it walks up looking for `Diabolical.sln`, and falls back to the exe's own
directory when none is found, which is exactly where the standalone
copy's config/data live.

Don't run the dev build and a standalone copy simultaneously — both
register the same global hotkey, and the second instance will fail.

## Config & Secrets
- Gemini API key lives in `appsettings.local.json`, gitignored.
- Check in `appsettings.example.json` showing the expected shape, no real key.
- `appsettings.local.json` now has a second hotkey block, `QuickCopyHotkey`
  (same `Modifiers`/`Key` shape as `Hotkey`), for the Quick Copy flow.
  `appsettings.example.json` should default it to something that doesn't
  collide with `Hotkey` (e.g. `Ctrl+Alt+C` alongside `Ctrl+Alt+D`).
- `.gitignore` should include:
  ```
  bin/
  obj/
  *.user
  data/characters/*.json
  appsettings.local.json
  .vs/
  ```

## Decisions Log
- **Item model refactored to support Mythic rarity, Ancestral quality,
  Tempering, and Transfiguration.** Rarity enum now includes `Mythic`.
  `quality` (`Normal`/`Ancestral`) added as a separate axis. `affixes`
  changed from flat strings to `{ text, source }` objects, where `source`
  is `Base`, `Tempered`, or `Transfigured` — needed because Tempering and
  Transfiguration add rolls distinct from an item's base affixes. The
  single `aspect` field was replaced with `specialEffects: string[]` to
  hold a Legendary's one aspect, a Unique/Mythic's several passive effect
  paragraphs, or a Transfigured amulet's extra Legendary power. Added
  `transfigured` and `modifiable` flags to track Horadric Cube crafting
  state (a Transfigured item is usually locked from further crafting).
  This is a breaking change to the prior schema shape — acceptable since
  no real save data exists yet.
- **Vision output includes an inferred `slot` field**, separate from the
  final stored schema, so the merge step knows which equipment slot the
  parsed item belongs to. Review UI allows correcting it if Gemini
  guesses wrong. See `Prompts/item_extraction_prompt.txt` for the
  finalized extraction prompt and its output shape.
- **Capture: drag-select, not fixed-region.** Tooltip position in-game
  moves depending on cursor/item location, so a fixed crop region isn't
  reliable. User hits the hotkey, then drags a selection box over the
  tooltip each time.
- **Storage: one JSON file per character**, stored under
  `data/characters/{characterName}.json`, matching the schema above.
  `ItemDatabaseService` reads/writes/merges against a single character's
  file at a time, and also supports removing a single equipment slot
  (for the equipment list's remove action) without touching the rest
  of the file.
- **HotkeyManager generalized to support multiple registered hotkeys**
  (previously supported exactly one, via a hardcoded id/event pair).
  Needed to add the Quick Copy hotkey without touching the existing
  capture hotkey's registration.
- **Quick Copy is implemented as an independent service, not a
  modification of ScreenCaptureService.** It reuses the same
  SelectionOverlayWindow drag-select and the same IVisionService, but has
  no character context and never touches ItemDatabaseService — keeping the
  tracked-gear flow and the throwaway-lookup flow fully decoupled.
- **Quick Copy's clipboard JSON uses the same shape as
  ItemDatabaseService.SerializeItem** (slot inlined as a leading property
  ahead of the item fields) — consistent formatting for anything handed to
  an AI assistant as a standalone item, whether it came from the equipment
  list or a quick lookup.

## Open Decisions (not yet finalized)
- **Talisman system (Seals + Charms) is out of scope for now.** It's a
  separate itemization layer (not tied to gear slots) added via the
  Lord of Hatred expansion. Deliberately not modeled yet — revisit if
  build planning needs it. If added later, it should live as its own
  top-level section (e.g. `talisman: { seal, charms[] }`), not inside
  `equipment`.

## Notes for Claude Code
- This doc reflects design decisions made in a separate planning chat.
  Implementation happens here in VS Code via Claude Code.
- Favor small, incremental commits per component (capture → vision call →
  parsing → review UI → storage) rather than one large initial commit.
- **Handoffs from the design chat arrive as design-intent, not code.** They
  describe the goal, the component, and any hard constraints (e.g. "must
  use System.Text.Json") — implementation approach, file layout details,
  and function signatures are Claude Code's call to make.
- If a handoff seems ambiguous or underspecified in a way that affects
  architecture (not just implementation detail), it's fine to make a
  reasonable call and note the assumption — but flag anything that
  contradicts or extends this spec so it can be reconciled back into
  CLAUDE.md.
- Keep this file in sync: if implementation surfaces a decision that
  should update CLAUDE.md (e.g. resolving something in "Open Decisions"),
  update it as part of that work rather than letting the two drift apart.