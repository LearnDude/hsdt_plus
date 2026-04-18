# Plan: Battlegrounds Positioning Simulator

## Context

After each Battlegrounds combat, the player may wonder whether a different minion ordering would have improved their outcome. This feature simulates all n! permutations of the player's pre-combat board, ranks them by win rate, and shows a results window after combat resolves. It is a post-combat learning tool — results do not need to be displayed in real-time.

Background on the existing simulation pipeline: [../hdt_documentation/battlegrounds.md](../hdt_documentation/battlegrounds.md)

---

## How to run

1. Build Debug from `Hearthstone-Deck-Tracker/`:
   ```
   msbuild "Hearthstone Deck Tracker.sln" /p:Configuration=Debug /p:Platform=x86
   ```
2. Launch `Hearthstone Deck Tracker/bin/Debug/HearthstoneDeckTracker.exe`.
3. Start Hearthstone and enter a Battlegrounds game.
4. Play through a combat phase — the simulator captures the board automatically at the start of combat.
5. After combat resolves, the **Positioning Results** window opens automatically.
6. Close the window to continue playing.

---

## Results window

**New file:** `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/Windows/PositioningResultsWindow.xaml` + `.xaml.cs`

A non-modal WPF window (MahApps `MetroWindow`) that opens after combat resolves. Renders actual minion card visuals using the existing `BattlegroundsMinion` control (portrait art + stats + keywords, cached from CDN). Only two orderings are shown — with thousands of permutations a full table adds no value.

```
┌──────────────────────────────────────────────────────────┐
│  Positioning Results — Turn 4                            │
├──────────────────────────────────────────────────────────┤
│  OPTIMAL  Win 61%                                        │
│  [🃏 Deflect-o-Bot] → [🃏 Murozond] → [🃏 Sellemental]   │
├──────────────────────────────────────────────────────────┤
│  YOURS  Win 48%  (ranked #3 of 720, −13%)                │
│  [🃏 Sellemental] → [🃏 Murozond] → [🃏 Deflect-o-Bot]   │
└──────────────────────────────────────────────────────────┘
```

Visual card rendering reuses:
- `BattlegroundsMinion` control — portrait, attack/health boxes, taunt/reborn/divine shield icons
- `BattlegroundsMinionViewModel` / `CardAssetViewModel` — CDN portrait download + local cache
- `CardAssetType.Portrait` — 256×256 from `https://art.hearthstonejson.com/v1/256x/{cardId}.jpg`

Bound to a `PositioningResultsViewModel` with:
- `OptimalOrdering` — `List<BattlegroundsMinionViewModel>`
- `ActualOrdering` — `List<BattlegroundsMinionViewModel>`
- `OptimalWinRate`, `ActualWinRate`, `ActualRank`, `TotalPermutations` — display strings

---

## Implementation steps

### Step 1 — Expose internals in `BobsBuddyInvoker`

**File:** `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/BobsBuddy/BobsBuddyInvoker.cs`

1. `private void SetupInputPlayer(...)` → `internal void SetupInputPlayer(...)`
2. Add `internal Input? CapturedInput => _input;`

### Step 2 — Create `PositioningSimulator.cs`

**New file:** `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/BobsBuddy/PositioningSimulator.cs`

```
class PositioningSimulator
    int Iterations = 1000
    int MaxTimePerPermutationMs = 500

    CaptureState(GameV2 game, int turn)
        — Store turn + gameId
        — Clone player minion entities sorted by ZONE_POSITION

    async Task<PositioningResult?> RunAsync()
        — Get BobsBuddyInvoker.GetInstance(gameId, turn).CapturedInput; bail if null
        — Generate all permutations of player minion entity list (Heap's algorithm)
        — For each permutation:
              simulator = new Simulator()
              input = new Input()
              invoker.SetupInputPlayer(input, opponent entities, simulator, friendly=false)
              invoker.SetupInputPlayer(input, player entities in permuted order, simulator, friendly=true)
              output = await new SimulationRunner()
                           .SimulateMultiThreaded(input, Iterations, ThreadCount, MaxTimePerPermutationMs)
              record (permutation, output.WinRate)
        — Sort by WinRate descending
        — Find rank of actual ordering (permutation [0,1,...,n-1])
        — Return PositioningResult

class PositioningResult
    int Turn
    List<Entity> ActualOrder      // player minions in played order
    float ActualWinRate
    List<Entity> OptimalOrder     // minions in best-found order
    float OptimalWinRate
    int ActualRank                // 1 = best
    int TotalPermutations
```

Heap's algorithm: add static `IEnumerable<T[]> Permutations<T>(T[] arr)` to `BobsBuddyUtils`.

### Step 3 — Hook into `GameEventHandler.cs`

**File:** `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/GameEventHandler.cs`

- Add field: `private readonly PositioningSimulator _positioningSimulator = new();`
- At `STEP=MAIN_COMBAT` call site (same as `BobsBuddyInvoker.StartCombat()`):
  `_positioningSimulator.CaptureState(_game, turn);`
- At combat-end trigger (`STEP=MAIN_END` — confirm in `TagChangeHandler` before coding):
  ```csharp
  var result = await _positioningSimulator.RunAsync();
  if(result != null)
      Core.MainWindow.Dispatcher.Invoke(() => new PositioningResultsWindow(result).Show());
  ```

### Step 4 — Create results window

- `Windows/PositioningResultsWindow.xaml` + `.xaml.cs` — MahApps `MetroWindow`, takes `PositioningResult` in constructor
- `Controls/Overlay/Battlegrounds/Positioning/PositioningResultsViewModel.cs` — wraps result, exposes `BattlegroundsMinionViewModel` lists

---

## Files to create / modify

| File | Change |
|------|--------|
| `BobsBuddy/BobsBuddyInvoker.cs` | `SetupInputPlayer` → internal; add `CapturedInput` property |
| `BobsBuddy/PositioningSimulator.cs` | **New** |
| `BobsBuddy/BobsBuddyUtils.cs` | Add `Permutations<T>` static helper |
| `GameEventHandler.cs` | Add field + `CaptureState` + `RunAsync` + window launch |
| `Windows/PositioningResultsWindow.xaml` | **New** |
| `Windows/PositioningResultsWindow.xaml.cs` | **New** |
| `Controls/Overlay/Battlegrounds/Positioning/PositioningResultsViewModel.cs` | **New** |

All paths relative to `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/`.

## Functions to reuse

| Function | File |
|----------|------|
| `BobsBuddyInvoker.SetupInputPlayer` | `BobsBuddy/BobsBuddyInvoker.cs` |
| `BobsBuddyUtils.GetMinionFromEntity` | `BobsBuddy/BobsBuddyUtils.cs` |
| `BobsBuddyInvoker.GetInstance` | `BobsBuddy/BobsBuddyInvoker.cs` |
| `BattlegroundsMinion` control | `Controls/Overlay/Battlegrounds/Minions/BattlegroundsMinion.xaml` |
| `BattlegroundsMinionViewModel` | `Controls/Overlay/Battlegrounds/Minions/BattlegroundsMinionViewModel.cs` |
| `CardAssetViewModel` | `Controls/Overlay/CardAssetViewModel.cs` |

---

## Verification

1. Build Debug: `msbuild "Hearthstone Deck Tracker.sln" /p:Configuration=Debug /p:Platform=x86`
2. Launch `HearthstoneDeckTracker.exe`, start Hearthstone, enter a Battlegrounds game.
3. Play through one combat phase.
4. Confirm the Positioning Results window opens after combat resolves.
5. Verify: permutation count = n!, actual rank ≥ 1, optimal win rate ≥ actual win rate, minion cards display correct art.
