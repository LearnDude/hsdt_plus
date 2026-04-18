# Statistics and Game Recording

## GameStats

**File:** `Hearthstone Deck Tracker/Stats/GameStats.cs`

A serializable record of a single completed game. Persisted to XML via `DeckStatsList`.

Key fields:
- `PlayerHero`, `OpponentHero` — hero class names
- `GameMode` — `GameMode` enum value
- `Format` — standard/wild/twist/classic
- `Result` — `GameResult` (Win/Loss/Draw)
- `StartTime`, `EndTime`, `Duration`
- `PlayerName`, `OpponentName`
- `Rank`, `LegendRank`, `Stars` — ranked ladder position
- `PlayerDeckName` — active deck at game start
- `OpponentCards` — reconstructed opponent deck
- `Note` — optional user annotation
- `HsReplayId` — GUID for the replay on HSReplay.net

Turn-level data:
- Cards drawn, mulliganed, played per turn (stored as serialized lists)
- `WasConceded` — whether opponent conceded

## DeckStats / DeckStatsList

**File:** `Hearthstone Deck Tracker/Stats/DeckStats.cs`, `DeckStatsList.cs`

`DeckStats` aggregates all `GameStats` for one `Deck`:
- `Games` — `List<GameStats>`
- `WinRate`, `WinRatePercent`, `Wins`, `Losses`, `Draws`
- `GetRecentGames(int count)` — last N games
- Filter methods by game mode, format, time range

`DeckStatsList` is a singleton XML-serialized container holding all decks' stats. Loaded at startup, saved on game end and on close.

## LastGames

**File:** `Hearthstone Deck Tracker/Stats/LastGames.cs`

Lightweight cache of the most recently played games across all decks. Used by the tray icon and quick-stats overlay.

## GameEventHandler — recording flow

**File:** `Hearthstone Deck Tracker/GameEventHandler.cs`

Recording is gated by `RecordCurrentGameMode` (checks `Config.Instance.Record*` flags per mode).

Flow:
1. `HandleGameStart()` — creates a new `GameStats`, sets `StartTime`, assigns active deck.
2. During the game — `HandlePlayerDraw`, `HandlePlayerPlay`, etc. append to in-progress stats.
3. `HandleGameEnd()` — sets `Result`, `EndTime`, computes duration; saves to `DeckStats`; kicks off HSReplay upload if enabled.
4. `HandleLoss()` / `HandleWon()` — set result and fire `GameEvents.OnGameLost/OnGameWon`.

`RecordCurrentGameMode` checks:
```csharp
_game.CurrentGameMode == Ranked && Config.Instance.RecordRanked
|| _game.CurrentGameMode == Arena && Config.Instance.RecordArena
|| ... (all modes)
```

## CompiledStats

**File:** `Hearthstone Deck Tracker/Stats/CompiledStats/`

Pre-aggregated statistics used by the detailed stats window: win rate by opponent class, win rate by turn, card mulligan performance, etc. Computed lazily from `DeckStats.Games`.

## Battlegrounds Stats

`BattlegroundsSessionViewModel` (`Controls/Overlay/Battlegrounds/Session/`) tracks current-session MMR changes and hero placements separately from the standard `GameStats` recording path, since Battlegrounds has placement (1–8) rather than win/loss.
