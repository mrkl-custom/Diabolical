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
      "itemPower": 800,
      "affixes": ["+X Fury Generation", "..."],
      "aspect": null
    },
    "weapon1": { }
  }
}
```

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

## Config & Secrets
- Gemini API key lives in `appsettings.local.json`, gitignored.
- Check in `appsettings.example.json` showing the expected shape, no real key.
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
  file at a time.

## Open Decisions (not yet finalized)
- None currently — revisit this section as new design questions come up.

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