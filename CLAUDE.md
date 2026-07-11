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
2. **Vision LLM parse** — The cropped image is sent to the configured vision
   provider (Gemini 2.5 Flash or a local Ollama model) with a fixed prompt
   asking for strict JSON output matching the schema below.
   - No OCR library. No regex parsing. The vision model replaces both steps.
3. **Parse response** — Strip any markdown code fences, deserialize into
   the `EquipmentItem` model via `System.Text.Json`.
4. **Review/Edit UI** — Show the parsed item to the user for confirmation
   or correction before it's saved. LLM output isn't blindly trusted
   (unless YOLO Mode is enabled — see Decisions Log).
5. **Save** — Merge/update the item into the character's local JSON file.
   On successful save, a short, gentle audio cue plays and the status
   indicator reflects completion (see UX Feedback below).
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

No fallback LLM providers needed (hobby scope) — if a provider is
rate-limited, just wait and retry.

## UX Feedback
- **Status indicator** — the existing "Vision Provider" status box (dot +
  text) doubles as an activity indicator, not just a connectivity check.
  It cycles through `Idle` → `Capturing` → `Processing` → back to `Idle`
  (or `Error` on failure) during both the main capture flow and Quick
  Copy, so the user has a visible "is it working right now" cue instead
  of only the scrolling status list.
- **Sound cue** — a short, gentle tone plays on a *successful* save
  (main capture flow) and a successful clipboard copy (Quick Copy).
  Does not play on failure/cancel, to avoid noise during misfires. No
  bundled licensed audio asset needed — a small synthesized tone (or
  `SystemSounds.Asterisk` as a trivial fallback) is enough for hobby scope.

## Tech Stack
- **C# + WPF** (.NET, Windows-only)
- `System.Net.Http.HttpClient` — direct REST calls to vision providers (no SDK)
- `System.Text.Json` — serialization
- Local JSON files for storage — no database engine

## JSON Schema (character equipment file)
```json
{
  "character": "MyBarb",
  "class": "Barbarian",
  "lastUpdated": "2026-07-06T00:00:00Z",
  "equipment": {
    "helm": [
      {
        "name": "Rage of Harrogath",
        "itemType": "Helm",
        "rarity": "Unique",
        "quality": "Ancestral",
        "itemPower": 800,
        "masterworkingQuality": 25,
        "affixes": [
          { "text": "+40% Fury Generation", "greaterAffix": false },
          { "text": "+180 Dexterity +[150 - 180] (Class Only)", "greaterAffix": false }
        ],
        "specialEffects": ["..."],
        "sockets": [],
        "transfigured": false,
        "modifiable": true
      }
    ],
    "weapon": [ { }, { }, { }, { } ],
    "seal": [ { } ],
    "charm": [ { }, { }, { } ]
  }
}
```

Field notes:
- `itemType`: the tooltip's type line, verbatim (e.g. "Two-Handed Mace
  (Bludgeoning)", "Chest Armor", "Unique Quarterstaff"). Disambiguates
  same-category items that would otherwise serialize identically — most
  useful for `weapon` (up to 4 entries) and armor slots. Empty string if
  the tooltip had no such line.
- `rarity`: `Common | Magic | Rare | Legendary | Unique | Mythic`
- `quality`: `Normal | Ancestral` — separate axis from rarity
- `masterworkingQuality`: the tooltip's numeric "Quality" stat shown near
  Item Power (e.g. "29 (⊛ +25) Quality" -> `29`) — a completely separate
  axis from `quality` (Normal/Ancestral) above, which shares the word
  "Quality" only by coincidence. Normally `0`-`25` (Masterworking upgrade
  ranks), but Transfiguration can push it higher, so no upper bound is
  enforced or validated.
- `affixes`: each entry has `text` (verbatim stat line, in tooltip order —
  inherent lines, rolled affixes, tempered/transfigured lines, all of it)
  and `greaterAffix` (bool, marks a sunburst Greater Affix glyph). There
  is deliberately no "origin" field distinguishing Base/Tempered/
  Transfigured/Implicit rolls — that distinction isn't reliably readable
  from a screenshot alone (see Decisions Log), so it isn't tracked.
- `specialEffects`: replaces a single `aspect` field. Holds zero entries
  (normal rares/magic items), one entry (a Legendary's imprinted aspect),
  or several (a Unique/Mythic's multiple passive effect paragraphs, or a
  Transfigured amulet's extra Legendary power via Kullean Tuning Prism)
- `sockets`: one entry per socket, in order, verbatim. Socket capacity is
  fixed by the game and category, not something the schema enforces:
  gloves/boots never have sockets; amulet/ring hold 0-1; helm/chest/pants
  and two-handed weapons/bows hold 0-2; one-handed weapons/Focus/Shield
  hold 0-1. Not every eligible item actually has a socket added (the
  Jeweler adds them one at a time via a Scattered Prism), so fewer than
  the max is normal. A socket entry is one of: the literal `"Empty
  Socket"`; a socketed Gem's resulting stat line with no gem name
  attached (e.g. `"+250 Resistance to All Elements"`, a weapon Gem's
  damage multiplier) — a 2-socket item can show the identical line twice,
  one entry per socket, since Gems are independent per socket; or a
  completed Runeword — only possible when both sockets of a 2-socket item
  hold a matching Ritual+Invocation rune pair — as one combined `"<Name>
  (<ratio>) - <Runeword Name>: <effect text>"` string representing both
  sockets together. Gems and Runes share the same sockets (filling one
  type clears the other), so an item never shows both at once.
- `transfigured` / `modifiable`: tracks Horadric Cube crafting state —
  whether the item has been Transfigured, and whether it can still be
  modified (tempered/masterworked/enchanted/imprinted) or is locked
- Category capacities: single-instance slots (helm, chest, gloves, pants,
  boots, amulet, seal) hold 1 item; `ring` holds 2; `weapon` holds 4
  (Barbarian's weapon-swap mechanic — up to two full one-hand/one-hand
  sets); `charm` holds 6 (the game's hard maximum). Authoritative source is
  `ItemDatabaseService.CategoryCapacities`.

### Talisman items (Seal + Charms)
Seals and Charms (Lord of Hatred's Talisman system) are stored as two more
**categories inside the same `equipment` dictionary**, using the exact same
`EquipmentItem` shape as gear — they carry rarity, item power, and affixes
the same way a piece of gear does, so no separate model or JSON section
was needed:
- `"seal"` — capacity 1, like any single-instance gear slot.
- `"charm"` — capacity 6 (the game's hard maximum). The seal currently
  equipped may unlock fewer than 6 charm slots, but that's an in-game
  current-state detail, not something the schema tracks — the array will
  only ever contain charms the user actually captured, so it self-limits.
- Vision extraction's `slot` field recognizes `"seal"` and `"charm"` as
  valid values alongside the existing gear categories.

This reuses `ItemDatabaseService`'s existing category-capacity/merge/evict
logic as-is (see Decisions Log) — no new storage code path.

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
│       ├── app.manifest                   # requireAdministrator — see Decisions Log
│       │
│       ├── Properties/
│       │   └── AssemblyInfo.cs
│       │
│       ├── Views/
│       │   ├── MainWindow.xaml
│       │   ├── MainWindow.xaml.cs
│       │   ├── ReviewEditDialog.xaml      # confirm/edit parsed item before save
│       │   ├── ReviewEditDialog.xaml.cs
│       │   ├── ItemDetailsDialog.xaml
│       │   ├── ItemDetailsDialog.xaml.cs
│       │   ├── SelectionOverlayWindow.xaml
│       │   └── SelectionOverlayWindow.xaml.cs
│       │
│       ├── Services/
│       │   ├── ScreenCaptureService.cs    # hotkey + screenshot/crop
│       │   ├── QuickCopyService.cs        # independent throwaway lookup flow
│       │   ├── OverlayCaptureSession.cs   # shared overlay-open/reentrancy-guard helper
│       │   ├── ScreenRegionCapture.cs     # shared screen-region → PNG bytes capture
│       │   ├── IVisionService.cs
│       │   ├── GeminiVisionService.cs     # API call + prompt template
│       │   ├── GeminiApiModels.cs
│       │   ├── OllamaVisionService.cs
│       │   ├── OllamaApiModels.cs
│       │   ├── VisionServiceFactory.cs
│       │   ├── ExtractionJsonParser.cs
│       │   ├── ItemDatabaseService.cs     # read/write character JSON
│       │   ├── ProviderStatusPresenter.cs # connectivity/activity status text, UI-free
│       │   ├── ClipboardHelper.cs         # Clipboard.SetText with a flake retry
│       │   ├── HotkeyManager.cs           # global hotkey registration
│       │   ├── AppSettingsLoader.cs
│       │   ├── RepoPaths.cs
│       │   └── DarkTitleBar.cs
│       │
│       ├── Models/
│       │   ├── EquipmentItem.cs
│       │   ├── CharacterEquipment.cs
│       │   ├── ParsedItemExtraction.cs
│       │   ├── ItemExtractionResult.cs
│       │   ├── ItemRarity.cs
│       │   ├── ItemQuality.cs
│       │   ├── ItemAffix.cs
│       │   ├── ActivityState.cs           # Idle/Capturing/Processing/Error
│       │   └── AppSettings.cs             # API key, hotkey config, etc.
│       │
│       ├── Prompts/
│       │   └── item_extraction_prompt.txt # vision system prompt + schema/examples
│       │
│       └── Resources/
│           ├── DarkTheme.xaml             # app-wide dark WPF theme (see App.xaml)
│           └── (icons, background art, sound asset if any)
│
├── data/
│   ├── characters/                        # gitignored — user's actual JSON output
│   │   └── .gitkeep
│   └── exports/
│       └── .gitkeep
│
└── tests/
    └── Diabolical.Tests/
        ├── Diabolical.Tests.csproj
        └── ... (unit tests per component)
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
register the same global hotkeys, and the second instance will fail.

## Config & Secrets
- Vision provider credentials/config live in `appsettings.local.json`, gitignored.
- Check in `appsettings.example.json` showing the expected shape, no real key.
- `appsettings.local.json` has two hotkey blocks, `Hotkey` (main capture)
  and `QuickCopyHotkey`, same `Modifiers`/`Key` shape, defaulting to
  non-colliding bindings (e.g. `Ctrl+Alt+D` and `Ctrl+Alt+C`).
- `appsettings.local.json` also has a top-level `YoloMode` boolean
  (default `false`) — see Decisions Log for what it skips.
- `.gitignore` includes:
  ```
  bin/
  obj/
  *.user
  data/characters/*.json
  data/exports/*.json
  appsettings.local.json
  .vs/
  .vscode/
  ```
- **Open**: first-run population mechanism for `appsettings.local.json`
  (manual copy-and-edit vs. a first-run settings UI) is still unspecified —
  see Open Decisions.

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
- **Vision output includes an inferred `slot` field**, separate from the
  final stored schema, so the merge step knows which equipment slot the
  parsed item belongs to. Review UI allows correcting it if the vision
  model guesses wrong.
- **Capture: drag-select, not fixed-region.** Tooltip position in-game
  moves depending on cursor/item location, so a fixed crop region isn't
  reliable. User hits the hotkey, then drags a selection box over the
  tooltip each time.
- **Storage: one JSON file per character**, stored under
  `data/characters/{characterName}.json`. `ItemDatabaseService`
  reads/writes/merges against a single character's file at a time, and
  supports removing a single item from a category without touching the
  rest of the file.
- **HotkeyManager generalized to support multiple registered hotkeys**
  (previously supported exactly one, via a hardcoded id/event pair).
  Needed to add the Quick Copy hotkey without touching the existing
  capture hotkey's registration.
- **Quick Copy is implemented as an independent service, not a
  modification of ScreenCaptureService.** It reuses the same
  `SelectionOverlayWindow` drag-select and the same `IVisionService`, but
  has no character context and never touches `ItemDatabaseService` —
  keeping the tracked-gear flow and the throwaway-lookup flow fully
  decoupled.
- **Quick Copy's clipboard JSON uses the same shape as
  `ItemDatabaseService.SerializeItem`** (slot inlined as a leading property
  ahead of the item fields) — consistent formatting for anything handed to
  an AI assistant as a standalone item, whether it came from the equipment
  list or a quick lookup.
- **Vision pipeline generalized behind `IVisionService`.** Gemini 2.5
  Flash and a local Ollama model (Qwen3-VL family) are both supported via
  `VisionServiceFactory`, selected by the `VisionProvider` config key. No
  automatic fallback between providers — switching is deliberate and
  config-driven, matching this project's hobby-scope "no fallback LLM
  providers" principle.
- **Talisman system (Seal + Charms) folded into the existing `equipment`
  dictionary, reversing the earlier "separate top-level section" plan.**
  Confirmed via screenshots that a Seal and Charms are structurally
  identical to gear items (rarity, item power, affixes), so they reuse
  `EquipmentItem` directly. Added as two new categories: `"seal"`
  (capacity 1) and `"charm"` (capacity 6, the game's hard maximum — the
  currently-equipped Seal may unlock fewer slots than 6, but the schema
  doesn't model that live-state detail since captured charms are
  inherently self-limiting). `ItemDatabaseService`'s existing
  capacity/merge/eviction logic (already used for `weapon`/`ring`) covers
  this with just one new `CategoryCapacities` entry — no new storage code
  path. The extraction prompt's `slot` enum gains `"seal"` and `"charm"`.
- **Status indicator doubles as an activity indicator.** The existing
  Vision Provider status box (dot + text) now also reflects
  Idle/Capturing/Processing/Error state for both the main capture flow
  and Quick Copy, not just provider connectivity.
- **Sound cue added on successful save/copy only**, not on failure or
  cancel, to avoid noise during misfires. No bundled audio asset required
  for hobby scope — a small synthesized tone or a system sound is enough.
- **YOLO Mode (`AppSettings.YoloMode`, `appsettings.local.json`, off by
  default).** When enabled, skips the Review/Edit dialog on a scanned item
  (the vision model's parse is saved as-is) and skips the "are you sure"
  confirmation on equipment removal, for a faster capture loop once a user
  trusts the vision model's output. Referenced by Core Flow step 4.
- **App runs elevated (`requireAdministrator` in `app.manifest`).** Needed
  because Windows UIPI blocks `WM_HOTKEY` delivery to a non-elevated
  process while an elevated game (Diablo 4, if it's running as admin) has
  foreground focus — without elevation, the capture/Quick Copy hotkeys
  would silently stop firing mid-game. The alternative, `uiAccess`,
  requires code-signing, which isn't a good fit for a hobby project, so
  full elevation is the pragmatic trade-off. This does mean everything
  else in the process — HTTP calls, clipboard, file dialogs/exports —
  also runs elevated; kept in check by sanitizing character-name input
  used as a file name and not crashing on malformed provider responses.
- **Item model extended with `itemType`, `sockets`, and
  `ItemAffix.GreaterAffix`** (2026-07-11 tooltip-screenshot review,
  BACKLOG.md V1–V6). `itemType` captures the tooltip's type line verbatim
  so same-category items (esp. the 4-slot `weapon` category) don't
  serialize identically. `sockets: string[]` holds one entry per socket
  (`"Empty Socket"` or the rune/gem name + runeword effect text verbatim)
  — previously dropped or leaked into `specialEffects`. `ItemAffix.
  GreaterAffix: bool` marks a line carrying the sunburst Greater Affix
  glyph. The prompt also gained an explicit ignore-list (flavor text,
  Sell Value, Durability, Requires Level, Account Bound, expansion tags,
  Crafted/Armory Loadout badges) so these don't leak into
  `specialEffects`. (This entry originally also added an `AffixSource`
  origin field — see below for its removal.)
- **Extraction prompt hardened against two real-world misextractions**
  found via live captures (2026-07-11): (1) `quality` was being flipped to
  `Ancestral` off the unrelated numeric "`<NN> (⊛ +MM) Quality`" roll-score
  stat shown near Item Power this season — the field-name collision with
  our own `quality` (Normal/Ancestral) confused the vision model, so the
  prompt now says explicitly that `quality`/`rarity` must come only from
  the literal words on the item's type line. (2) Socketed runewords (e.g.
  "NeoOhm (200/600) - Graceful Trickery" plus its effect text) were being
  dropped entirely instead of populating `sockets` — the prompt now
  describes the socket block's concrete visual pattern and position
  (after specialEffects, before footer metadata) and includes a worked
  example. A third issue found in the same pass — an explicit red
  "Unmodifiable" tag being ignored in favor of a default `true` — is why
  `modifiable` guidance keys strictly off that tag's presence rather than
  inferring from Transfigured/Tempers state.
- **`AffixSource` (`Base | Tempered | Transfigured | Implicit`) removed
  from `ItemAffix` entirely** (2026-07-11) — after using the app against
  real tooltips, distinguishing an affix's origin (Base vs. Tempered vs.
  Transfigured) turned out not to be reliably readable from a screenshot:
  the marker-icon heuristic the prompt relied on (added in the entry
  above) doesn't hold up in practice, and repeated attempts to refine it
  weren't worth the complexity for a stat that "does not matter that
  much" for build planning. `ItemAffix` now has only `text` and
  `greaterAffix`; every stat line (implicit, rolled, tempered,
  transfigured alike) is captured in tooltip order with no origin
  classification. `Implicit`'s positional detection (above the item's
  divider) was comparatively reliable, but it was removed too for
  consistency — one field for "this is a stat line on the item," not a
  partially-reliable taxonomy.
- **`masterworkingQuality: int` added to `EquipmentItem`/`ParsedItemExtraction`**
  (2026-07-11) to track the tooltip's numeric "Quality" stat (e.g. "29 (⊛
  +25) Quality") as real data instead of ignoring it — it's the item's
  Masterworking quality score, normally `0`-`25` but uncapped since
  Transfiguration can push it higher (confirmed via a live Tuskhelm of
  Joritz capture showing 29). This is a separate axis from the `quality`
  field (Normal/Ancestral) despite the shared word "Quality" in-game; the
  extraction prompt now calls out both fields by name to keep the vision
  model from conflating them, since that exact confusion was the root
  cause of the `Ancestral` misdetections fixed just above.
- **Socket extraction guidance grounded in Diablo 4's actual socket rules**
  (2026-07-11) after repeated icon-guessing attempts from screenshots
  alone kept missing real sockets. Researched current-season mechanics:
  socket capacity is fixed per category (gloves/boots never have sockets;
  amulet/ring 0-1; helm/chest/pants and two-handed weapons/bows 0-2;
  one-handed weapons/Focus/Shield 0-1), and a filled socket holds either
  an independent Gem (bare resulting stat line, no name — e.g. a Skull
  gem's weapon Physical Damage Multiplier) or, only on a 2-socket item,
  a completed Runeword from a matching Ritual+Invocation rune pair (one
  combined name+ratio+effect line covering both sockets). The prompt
  now states these capacities explicitly per category so the model knows
  how many socket-icon lines to look for instead of only whether to look,
  and distinguishes the two content shapes (repeatable bare Gem lines vs.
  one combined Runeword line) with worked examples for each. Schema
  itself (`sockets: string[]`) didn't change — this was a prompt-accuracy
  fix grounded in facts, not a data-shape fix. No code guarantees
  vision-model reliability here; this raises the ceiling, it doesn't
  eliminate misses.

## Open Decisions (not yet finalized)
- **First-run `appsettings.local.json` setup** — manual copy-and-edit of
  `appsettings.example.json` vs. a first-run settings UI. Currently
  manual; revisit if onboarding friction becomes annoying.
- **Qwen3-VL model size** (4B vs 2B vs other) pending confirmation from
  real-world testing before `appsettings.example.json`'s default model
  tag is finalized.

## Notes for Claude Code
- This doc reflects design decisions made in a separate planning chat.
  Implementation happens here in VS Code via Claude Code.
- Favor small, incremental commits per component rather than one large
  initial commit.
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