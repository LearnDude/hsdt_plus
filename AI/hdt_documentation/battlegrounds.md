# Battlegrounds Systems

This file covers the two core Battlegrounds subsystems: the existing Bob's Buddy combat simulator and board state snapshotting.

## BobsBuddyInvoker

**File:** `Hearthstone Deck Tracker/BobsBuddy/BobsBuddyInvoker.cs`

Orchestrates Battlegrounds combat simulation. Instances are cached per `(gameId, turn)` pair so each turn only simulates once.

### Instance management

```csharp
BobsBuddyInvoker.GetInstance(Guid gameId, int turn)
```
Returns the cached instance for this game+turn, creating one if needed. All instances are cleared when a new `gameId` is seen.

### Simulation parameters

| Constant | Value | Meaning |
|----------|-------|---------|
| `Iterations` | 10,000 | Simulation runs per call |
| `StateChangeDelay` | 500 ms | Wait after combat starts before snapshotting |
| `MaxTime` | 1,500 ms | Timeout for normal boards |
| `MaxTimeForComplexBoards` | 3,000 ms | Timeout for boards with many deathrattles |
| `MaxTimeForLeapfrogger` | 5,000 ms | Timeout for Leapfrogger boards |
| `ThreadCount` | `CPU count / 2` | Parallel simulation threads |

### Simulation flow

1. `StartCombat()` is called by `TagChangeHandler` when `STEP=MAIN_COMBAT` is detected.
2. `SnapshotBoardState(turn)` reads the current `GameV2.Entities` to build a `BobsBuddy.Simulation.Input`.
3. `RunAndDisplaySimulationAsync()` invokes the `BobsBuddy.dll` simulation engine.
4. Results (`Output`) contain win/tie/loss probabilities and lethal probabilities.
5. `BobsBuddyDisplay` (the overlay panel) is updated with the result.

### Board extraction

`SnapshotBoardState` reads entities from `GameV2` and calls `BobsBuddyUtils.GetMinionFromEntity()` to convert each `Entity` into a `BobsBuddy.Simulation.Minion`. Key tags read: `ATK`, `HEALTH`, `DAMAGE`, `TAUNT`, `DIVINE_SHIELD`, `POISONOUS`, `VENOMOUS`, `WINDFURY`, `MEGA_WINDFURY`, `REBORN`, `STEALTH`, `PREMIUM` (golden), `TECH_LEVEL`, `CARDRACE`, `TAG_SCRIPT_DATA_NUM_1-4`.

Attached enchantment entities are also read for buff values.

Supports Duos mode: `DuosInputPlayer`, `DuosInputOpponent`, `DuosInputPlayerTeammate`, `DuosInputOpponentTeammate` hold the four boards.

### Output

`Output` property (type `BobsBuddy.Output`):
- `WinRate`, `TieRate`, `LossRate` — probabilities (0–1)
- `PlayerLethal`, `OpponentLethal` — lethal probabilities
- `SimulationCount` — actual number of simulations completed

## BobsBuddyUtils

**File:** `Hearthstone Deck Tracker/BobsBuddy/BobsBuddyUtils.cs`

Static helper. `GetMinionFromEntity(Simulator sim, bool player, Entity entity, IEnumerable<Entity> attachedEntities)` — the key function that converts an HSDT `Entity` into a `BobsBuddy.Simulation.Minion`.

Also handles:
- Enchantment parsing (Replicating Menace, Whirring Protector, Sneed's, etc.)
- Modular entity merging (Magnetic mechs)
- Golden detection, tier reading

## BattlegroundsBoardState

**File:** `Hearthstone Deck Tracker/Hearthstone/BattlegroundsBoardState.cs`

Stores a snapshot of the opponent's board at the time of combat, keyed by `PLAYER_ID`.

```csharp
internal class BattlegroundsBoardState
{
    Dictionary<int, BoardSnapshot> LastKnownBoardState { get; }

    void SnapshotCurrentBoard();  // captures opponent minions + turn number
    BoardSnapshot? GetSnapshot(int entityId);  // retrieve by entity id
    void Reset();  // called at game start
}
```

`SnapshotCurrentBoard()`:
1. Finds opponent hero entity in `Zone.PLAY`.
2. Reads `PLAYER_ID` tag from the hero.
3. Clones all opponent minion entities currently in `Zone.PLAY`.
4. Stores as `BoardSnapshot(entities[], turnNumber)`.

`GetSnapshot(entityId)` resolves an arbitrary entity's `PLAYER_ID` and returns the stored snapshot.

`BoardSnapshot` contains the cloned `Entity[]` array and the turn number when the snapshot was taken.

## Duos Support

`GameV2.IsBattlegroundsDuosMatch` gates Duos-specific code paths. `BattlegroundsDuosBoardState` (separate from `BattlegroundsBoardState`) handles Duos. The positioning simulator should check this flag to handle both modes.
