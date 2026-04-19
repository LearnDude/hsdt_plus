# Session: Positioning Simulator Implementation

## Date
2026-04-18

## What was done

Implemented the full Battlegrounds positioning simulator as planned in `AI/plans/positioning-simulator.md`.

### Files created
- `BobsBuddy/PositioningSimulator.cs` — `CaptureState` + `RunAsync` + `RunAndDisplayAsync`; also defines `PositioningResult` data class
- `Controls/Overlay/Battlegrounds/Positioning/PositioningResultsViewModel.cs` — wraps `PositioningResult`, exposes `BattlegroundsMinionViewModel` lists and display strings
- `Windows/PositioningResultsWindow.xaml` + `.xaml.cs` — MahApps MetroWindow showing OPTIMAL vs. YOURS sections with minion card art

### Files modified
- `BobsBuddy/BobsBuddyInvoker.cs` — `SetupInputPlayer` → `internal`; `GetAttachedEntities` → `internal`; added `CapturedInput => _input` property
- `BobsBuddy/BobsBuddyUtils.cs` — added `Permutations<T>` (Heap's algorithm)
- `Hearthstone/GameV2.cs` — added `PositioningSimulator PositioningSimulator { get; }` property
- `LogReader/Handlers/TagChangeActions.cs` — `OnBattlegroundsSetupChange`: calls `CaptureState` after `StartCombat` (solo only)
- `GameEventHandler.cs` — `HandleTurnStart`: calls `RunAndDisplayAsync` after `StartShoppingAsync` (solo only)

## Key design decisions

**How permutations work:** `CapturedInput.Player.Side` (a `List<Minion>`) is rearranged in place between simulation runs. Each permutation uses `SimulateMultiThreaded` at 1000 iterations / 500ms timeout. The original order is restored after all runs.

**Entity capture timing:** `CaptureState` runs synchronously at combat start (same moment as `SnapshotBoardState` in `StartCombat`), so it captures the true pre-combat player board.

**Duos skipped:** positioning simulation only runs for solo matches (`IsBattlegroundsSoloMatch`).

**Window:** each combat opens a fresh `PositioningResultsWindow` (not persistent/hidden) with two rows — OPTIMAL (green) and YOURS (blue) — each showing `BattlegroundsMinion` controls at 100×100.

## Build status

Build not verified in this session — MSBuild / Visual Studio not available in the shell environment. To build:

```
msbuild "Hearthstone Deck Tracker.sln" /p:Configuration=Debug /p:Platform=x86
```

Run from the `Hearthstone-Deck-Tracker/` directory using a Visual Studio Developer Command Prompt.

## Potential issues to watch for during first build

1. `capturedInput.Player.Side.Clear()` — assumes `Side` is a mutable `List<Minion>`. If it's a read-only collection, `Clear` will throw. Observed `Add` usage in `SetupInputPlayer` makes this very likely to be mutable, but worth confirming.

2. `BobsBuddyInvoker.GetInstance(_gameId, _turn, createInstanceIfNoneFound: false)` — named parameter usage, confirm parameter name matches the third parameter of `GetInstance`.

3. XAML nullable warnings — `PositioningResultsViewModel` is `internal` and `PositioningResultsWindow` is `public partial`; these are in the same assembly so this is fine, but worth checking if the XAML compiler emits any warnings about internal types.

## Commit
`f4770c8` — pushed to `origin/master`.
