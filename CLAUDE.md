# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This repo is a fork of Hearthstone Deck Tracker. All source lives under `Hearthstone-Deck-Tracker/`. Project-specific documentation, plans, and work logs live in `AI/` with this layout:

- `AI/hdt_documentation/` — codebase reference docs (see [index](AI/hdt_documentation/index.md))
- `AI/objective.md` — project goals
- `AI/plans/` — implementation plans
- `AI/work/` — logs and next-step notes

## Build

All commands run from `Hearthstone-Deck-Tracker/`.

```powershell
# First-time setup (restores NuGet packages)
./bootstrap.ps1

# Debug build
msbuild "Hearthstone Deck Tracker.sln" /p:Configuration=Debug /p:Platform=x86

# Release build (required before running tests)
msbuild "Hearthstone Deck Tracker.sln" /p:Configuration=Release /p:Platform=x86
```

Platform must always be `x86` — HearthMirror reads Hearthstone's 32-bit process memory.

## Tests

```powershell
# Run all tests (after Release build)
vstest.console "HearthWatcher.Test\bin\x86\Release\HearthWatcher.Test.dll"
vstest.console "HDTTests\bin\x86\Release\HDTTests.dll"

# Run a single test by name
vstest.console "HDTTests\bin\x86\Release\HDTTests.dll" /Tests:LocUtilTests.AgeTest
```

Framework: MSTest v1.4 with RhinoMocks for mocking. Tests live in `HDTTests/` (main app) and `HearthWatcher.Test/` (log parsing library).

## Code style

Enforced by `.editorconfig` at the repo root:
- Indentation: **tabs**
- Line endings: **LF**
- Nullable reference types: **enabled** (warnings as errors in main app, C# 10)
- Braces on new lines; `var` preferred for obvious types

## Architecture

The app monitors Hearthstone by tailing its log files. The pipeline is:

```
Hearthstone log files (power.log, game.log)
  → LogWatcherManager  (tails files on background threads)
  → HsGameState        (state machine: block stack, entity tracking)
  → Handlers           (PowerHandler, TagChangeHandler, ChoicesHandler, …)
  → GameEventHandler   (implements IGameHandler; updates GameV2, fires API events)
  → GameEvents.*       (static ActionList<T> events consumed by UI and plugins)
  → OverlayWindow      (transparent WPF window rendered over Hearthstone)
```

**Key classes to know:**

| Class | File | Role |
|-------|------|------|
| `GameV2` | `Hearthstone/GameV2.cs` | Live game state: all entities, both players, mode |
| `Player` | `Hearthstone/Player.cs` | Zone accessors (`Hand`, `Board`, `Deck`, `Minions`, …) as LINQ over `GameV2.Entities` |
| `Entity` | `Hearthstone/Entities/Entity.cs` | Any in-game object; tags read with `GetTag(GameTag)` |
| `HsGameState` | `LogReader/HsGameState.cs` | Block hierarchy and per-line state; `BlockStart/BlockEnd` |
| `PowerHandler` | `LogReader/Handlers/PowerHandler.cs` | Parses every power.log line; the entry point for all entity/tag events |
| `TagChangeHandler` | `LogReader/Handlers/TagChangeHandler.cs` | Maps `GameTag` mutations to semantic events (zone moves, combat triggers) |
| `GameEventHandler` | `GameEventHandler.cs` | Translates IGameHandler callbacks into `GameEvents.*` fires and stat recording |
| `BobsBuddyInvoker` | `BobsBuddy/BobsBuddyInvoker.cs` | Battlegrounds combat simulator; triggered by `STEP=MAIN_COMBAT` tag change |
| `BobsBuddyUtils` | `BobsBuddy/BobsBuddyUtils.cs` | Converts `Entity` → `BobsBuddy.Simulation.Minion` (reads all combat tags) |
| `BattlegroundsBoardState` | `Hearthstone/BattlegroundsBoardState.cs` | Snapshots opponent board per turn via `Entity.Clone()` |
| `OverlayWindow` | `Windows/OverlayWindow.xaml.cs` | In-game overlay; `BobsBuddyDisplay` is accessed by the simulator |
| `GameEvents` | `API/GameEvents.cs` | All subscribable events; use `+=` / `-=` on static `ActionList<T>` fields |

**Battlegrounds simulation detail:**  
`BobsBuddyInvoker.SnapshotBoardState()` reads `GameV2.Entities` and calls `BobsBuddyUtils.GetMinionFromEntity()` per minion. The `BobsBuddy.dll` engine is invoked via `RunAndDisplaySimulationAsync()` with 10 000 iterations. Minion order on the board is determined by the `ZONE_POSITION` GameTag.

**Plugin system:**  
Third-party plugins implement `IPlugin` and are loaded from `%AppData%\HearthstoneDeckTracker\Plugins\`. They subscribe to `GameEvents.*` and access game state through `API.Core.Game`.

## Project objective

See [AI/objective.md](AI/objective.md). The two goals are:
1. **Positioning simulator** — enumerate all minion placement permutations for the active player in Battlegrounds, simulate each with BobsBuddy, and surface which ordering was optimal vs. what was actually played.
2. **Opponent battle history** — reliably read and expose per-opponent board snapshots from `BattlegroundsBoardState`.

Plans go in `AI/plans/`, work logs and next steps go in `AI/work/`.