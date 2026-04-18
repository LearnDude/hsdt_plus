# Log Reader

The log reader is responsible for watching Hearthstone's output log files, parsing their content, and translating raw log lines into game state changes.

## Overview

Hearthstone writes structured log files to `%LocalAppData%\Blizzard\Hearthstone\Logs\`. The key files are:
- `Power.log` — every game action, entity creation, tag change
- `GameState.log` / `Game.log` — high-level game events
- `LoadingScreen.log` — scene transitions
- `Arena.log` — arena draft events

## LogWatcherManager

**File:** `Hearthstone Deck Tracker/LogReader/LogWatcherManager.cs`

- Creates one `LogWatcher` per log file, each running on a background thread.
- Tails files incrementally, passing new lines to the appropriate `Handler`.
- Manages start/stop lifecycle tied to `Core` initialization.

## HsGameState

**File:** `Hearthstone Deck Tracker/LogReader/HsGameState.cs`

The central state machine for log parsing. Maintains contextual state that spans multiple log lines.

Key state:
- `CurrentEntityId` — the entity currently being described by the log
- `CurrentBlock` — nested `Block` tree representing game action hierarchy (BLOCK_START/BLOCK_END)
- `KnownCardIds` — `Dictionary<int, IList<(cardId, zone, ...)>>` of cards revealed at known locations
- `JoustReveals`, `DredgeCounter`, `LastCardPlayed`, `LastEntityChosenOnDiscover`
- `DeterminedPlayers` — true once both player IDs are known
- `GameEnded`, `WasInProgress`, `FoundSpectatorStart`

Block management:
- `BlockStart(type, cardId, target, triggerKeyword)` — pushes a child `Block` onto the tree; notifies `SecretsManager.OnNewBlock()`
- `BlockEnd()` — pops the current block; clears `HasOutstandingTagChanges` on the current entity

`Reset()` is called at game start to clear all transient state.

## Handlers

### PowerHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/PowerHandler.cs`

Parses `Power.log` — the main log file containing all game actions.

Recognized patterns:
- `GameEntityRegex` — creates the global game entity
- `PlayerEntityRegex` — creates player entities
- `TagChangeRegex` — entity tag mutations, forwarded to `TagChangeHandler`
- `BlockStartRegex` / `BlockEndRegex` — marks action block boundaries in `HsGameState`
- `CreateEntityRegex`, `FullEntityRegex` — creates new `Entity` objects in `game.Entities`
- `MetaDataRegex` — handles META_DATA lines (damage, healing, targets)

Tag changes are queued if players are not yet determined; they are flushed once player IDs are known.

### TagChangeHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/TagChangeHandler.cs`

Translates raw `GameTag` + `int` value pairs into semantic game events.

Important mappings:
- `ZONE` changes → card moved between hand/deck/board/graveyard → fires player zone events
- `HEALTH`, `DAMAGE`, `ATK` → entity stats updated
- `ZONE_POSITION` → minion position on board changed
- `CONTROLLER` → entity ownership changed (stolen)
- `STEP` (MAIN_COMBAT) → triggers `BobsBuddyInvoker.StartCombat()`
- `GAME_OVER` → triggers game end handling

### GameInfoHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/GameInfoHandler.cs`

Parses `Game.log` for high-level game metadata:
- Game type (ranked, arena, etc.)
- Format (standard, wild, twist)
- Player names and IDs

### ChoicesHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/ChoicesHandler.cs`

Tracks mulligan selections and Discover choices by correlating choice entity IDs with `KnownCardIds`.

### LoadingScreenHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/LoadingScreenHandler.cs`

Detects mode transitions (menu → gameplay → tavern brawl lobby, etc.) by parsing `LoadingScreen.log`.

### ArenaHandler

**File:** `Hearthstone Deck Tracker/LogReader/Handlers/ArenaHandler.cs`

Parses `Arena.log` for draft pick events used by `ArenaPackagesManager`.

## Log Line to Game Event Mapping

```
power.log line
    → PowerHandler.Handle()
        → TagChangeHandler (for TAG_CHANGE lines)
            → gameState.SetCurrentEntity() / game.Entities mutations
            → IGameHandler callbacks (SetZone, EntityWillTakeDamage, etc.)
                → GameEventHandler
                    → GameEvents.OnPlayer* / OnOpponent* fired
                    → UI repaints
```

The `IGameHandler` interface (implemented by `GameEventHandler`) decouples the log reader from game logic, making it testable.
