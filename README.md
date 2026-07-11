# Diabolical

A lightweight Windows companion app for **Diablo 4** that turns item-tooltip
screenshots into structured JSON. Hit a hotkey in-game, drag a box over an item
tooltip, and a vision LLM (Google Gemini or a local Ollama model) extracts the
item's name, rarity, item power, affixes, and special effects — no OCR, no
regex. Captured gear is stored per character in local JSON files that you can
export with one click and hand to an AI assistant as context for build and
gear planning.

## Use of AI in the development of this application

This application has been almost entirely developed using LLM tooling. Design, 
documentation, images and user interface has been done using LLM.

## Features

- **Global hotkey capture** — works while the game has focus; the selection
  overlay never steals focus, so the tooltip stays visible while you select it.
- **Vision-LLM extraction** — Gemini 2.5 Flash or any vision-capable local
  Ollama model (e.g. Qwen3-VL), selected by config. Direct REST calls, no SDKs.
- **Review before save** — parsed items are shown in an edit dialog for
  correction before anything is written (skippable via YOLO Mode).
- **Local JSON "database"** — one file per character under `data/characters/`,
  covering gear plus the Talisman system (Seal and Charms). No database engine.
- **One-click export** — copy a character's full equipment JSON to the
  clipboard or save it to a file, ready to paste into an AI chat.
- **Quick Copy** — a second hotkey for throwaway lookups: capture a tooltip and
  get the item's JSON straight onto the clipboard, no review dialog, nothing
  saved.
- **Status & sound cues** — the provider status box doubles as an activity
  indicator (Idle → Capturing → Processing), and a gentle tone plays on each
  successful save or copy.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
  (or just the **.NET 8 Desktop Runtime** to run a published copy)
- A vision provider — one of:
  - A **Google Gemini API key** ([Google AI Studio](https://aistudio.google.com/)), or
  - A local **[Ollama](https://ollama.com/)** install with a vision-capable
    model pulled, e.g. `ollama pull qwen3-vl:8b`
- **Administrator rights** — the app requests elevation on launch. This is
  required for the global hotkey to work while Diablo 4 (which runs elevated)
  has focus; Windows blocks hotkey delivery to lower-integrity processes.

## Installation

### Build from source

```powershell
git clone https://github.com/<you>/Diabolical.git
cd Diabolical
dotnet build
```

### Configure

Copy the example settings to the repo root and edit it:

```powershell
copy appsettings.example.json appsettings.local.json
```

`appsettings.local.json` (gitignored — your API key never leaves your machine):

```json
{
  "VisionProvider": "Gemini",          // "Gemini" or "Ollama"
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY_HERE"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen3-vl:8b"
  },
  "Hotkey":          { "Modifiers": "Control+Alt", "Key": "D" },  // capture & save
  "QuickCopyHotkey": { "Modifiers": "Control+Alt", "Key": "C" },  // capture to clipboard
  "YoloMode": false
}
```

- `VisionProvider` picks which provider is used; the other block can be left
  as-is.
- Hotkeys accept `Control`/`Ctrl`, `Alt`, `Shift`, `Win` combined with `+`,
  plus any key name. Pick bindings Diablo 4 doesn't use.
- `YoloMode: true` skips the review dialog on capture and the confirmation on
  item removal — extractions save immediately.

### Run

```powershell
dotnet run --project src/Diabolical
```

### Standalone copy (optional)

To run a copy independent of the dev checkout:

```powershell
dotnet publish src/Diabolical/Diabolical.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Copy the `publish/` output to a folder outside the repo and place an
`appsettings.local.json` next to the exe — the app finds its config and
creates its `data/characters/` folder alongside itself when no repo root is
present. Requires the .NET 8 Desktop Runtime.

> Don't run the dev build and a standalone copy at the same time — both
> register the same global hotkeys, and the second instance will fail.

## Usage

### Capturing an item

1. Launch Diabolical (approve the elevation prompt) and enter your
   **character name** (and optionally class), then click **Switch**.
2. In Diablo 4, hover an item so its tooltip is visible.
3. Press the capture hotkey (default **Ctrl+Alt+D**). The screen dims slightly.
4. **Left-drag** a rectangle over the tooltip. (**Right-click** to cancel —
   Escape won't work, since the overlay deliberately never takes keyboard
   focus.)
5. The crop is sent to your configured vision provider. When parsing finishes,
   a **Review/Edit** dialog shows the extracted item — fix anything the model
   got wrong (including the inferred slot), then **Save**.
6. The item is merged into `data/characters/<name>.json`, the equipment list
   updates, and a soft tone confirms the save.

Re-scanning an item with the same name (e.g. after tempering) replaces it in
place. Multi-item slots hold up to their in-game capacity (2 rings, 4 weapons,
1 seal, 6 charms); scanning beyond capacity evicts the oldest entry.

### Exporting for an AI assistant

This is the point of the app:

- **Copy JSON** — puts the current character's full equipment JSON on the
  clipboard.
- **Export File** — saves it under `data/exports/` (or wherever you choose).
- **View → Copy JSON** on a single row copies just that item, with its slot
  included.

Paste the result into your AI assistant of choice as context for gear and
build questions.

### Quick Copy

For one-off lookups mid-session, press the Quick Copy hotkey (default
**Ctrl+Alt+C**) and drag-select a tooltip the same way. The extracted item's
JSON goes straight to the clipboard — no review dialog, no character context,
nothing saved.

### Managing equipment

The equipment grid shows everything captured for the current character.
**View** opens a read-only detail popup; **Remove** deletes an item from the
character file (with confirmation, unless YOLO Mode is on). The **Vision
Provider** status box shows connectivity plus live activity
(Capturing / Processing / Error), and **Recheck** re-tests the provider.

## Data & privacy

- Everything is stored locally: character JSON under `data/characters/`,
  exports under `data/exports/`. Both are gitignored, as is
  `appsettings.local.json`.
- The only network traffic is the cropped screenshot sent to your configured
  vision provider — Google's API if you chose Gemini, or your own machine if
  you chose Ollama.

## Troubleshooting

- **"Failed to register hotkey"** — another app owns that binding; change the
  `Hotkey`/`QuickCopyHotkey` values in `appsettings.local.json`.
- **Hotkey does nothing while the game is focused** — make sure Diabolical was
  allowed to elevate; without admin rights the hotkey only works while
  Diabolical itself is focused.
- **Provider status is red** — for Gemini, check the API key; for Ollama,
  check the server is running (`ollama serve`) and the configured model is
  pulled (`ollama list`).
- **Tooltip disappears when the overlay opens** — it shouldn't (the overlay is
  non-activating), but if it does, re-hover the item; the overlay stays up
  until you drag or right-click.
