# Plan: Persist Combat Logs to Disk

## Goal

After every positioning simulation, write a JSON file capturing the full board state and all permutation results. This creates a dataset for future ML analysis — position prediction, tribe synergy detection, hero-power influence, two-phase screening quality, etc. — without any future instrumentation needed.

---

## Schema

Five DTOs, all inner classes of `BobsBuddy/PositioningLogger.cs`.

### `PositioningCombatLog` — top-level record

```csharp
public class PositioningCombatLog
{
    // Identity
    public string GameId    { get; set; }
    public int    Turn      { get; set; }
    public string Timestamp { get; set; }   // ISO 8601 UTC
    public bool   IsDuos    { get; set; }   // capturedInput.PlayerTeammate != null

    // Board state
    public PlayerContext          Player                { get; set; }
    public PlayerContext          Opponent              { get; set; }
    public List<MinionSnapshot>   PlayerMinions         { get; set; }  // canonical ZONE_POSITION order (index = position)
    public List<MinionSnapshot>   OpponentMinions       { get; set; }  // opponent's actual ordering left→right
    public List<MinionSnapshot>?  PlayerTeammateMinions  { get; set; } // null if not duos
    public List<MinionSnapshot>?  OpponentTeammateMinions { get; set; }

    // Simulation results
    public List<PermutationEntry> ScreenPhaseResults  { get; set; }   // all perms at ScreenIterations (noisy)
    public List<PermutationEntry> ConfirmPhaseResults { get; set; }   // top-K at ConfirmIterations (reliable)

    // Summary
    public int   ActualRank     { get; set; }     // 1-based rank of played ordering among all perms
    public int   TotalPerms     { get; set; }
    public float ActualWinRate  { get; set; }     // confirmed win rate for played ordering
    public float OptimalWinRate { get; set; }     // confirmed win rate for best ordering

    // Populated by deferred write after combat resolves (see RecordOutcome)
    public string? ActualOutcome   { get; set; }  // "Win" | "Loss" | "Tie" | null
    public int?    DamageReceived  { get; set; }  // opponent's damage dealt to us, if loss
}
```

### `PlayerContext` — per-player game state

One instance each for `Player` and `Opponent`, populated from `capturedInput.Player` / `capturedInput.Opponent`.

```csharp
public class PlayerContext
{
    public int  Health      { get; set; }   // HP + armor at time of combat snapshot
    public int  TavernTier  { get; set; }

    // Active hero powers (usually 1, sometimes 2 in special formats)
    public List<HeroPowerSnapshot> HeroPowers  { get; set; }

    // Active quests, trinkets, objectives — stored as card IDs for lookup
    public List<string> QuestCardIds     { get; set; }
    public List<string> TrinketCardIds   { get; set; }
    public List<string> ObjectiveCardIds { get; set; }

    // Per-game tribe/archetype counters that affect combat math
    // These are already computed into the BobsBuddyPlayer at capture time
    public int EternalKnightCounter          { get; set; }
    public int UndeadAttackBonus             { get; set; }
    public int BeastAttackBonus              { get; set; }
    public int BeastHealthBonus              { get; set; }
    public int WhelpAttackBonus              { get; set; }
    public int WhelpHealthBonus              { get; set; }
    public int BeetlesAtkBuff               { get; set; }
    public int BeetlesHealthBuff            { get; set; }
    public int AncestralAutomatonCounter     { get; set; }
    public int ElementalPlayCounter          { get; set; }
    public int PiratesSummonCounter          { get; set; }
    public int BeastsSummonCounter           { get; set; }
    public int FriendlyMinionsDeadLastCombat { get; set; }
    public int BattlecryCounter             { get; set; }
    public int ResourcesSpentThisGame       { get; set; }
}
```

> **Note:** Many of these counters directly affect BobsBuddy simulation math (e.g., `EternalKnightCounter` buffs all Undead attack). They are crucial context for ML models that try to learn from win rates.

### `HeroPowerSnapshot`

```csharp
public class HeroPowerSnapshot
{
    public string CardId      { get; set; }
    public bool   IsActivated { get; set; }
}
```

Hero power card IDs already fully identify which power and its effect. `IsActivated` (exhausted this turn) matters for powers like Lich King's that trigger once per combat.

### `MinionSnapshot` — per-minion record

One instance per minion on either side.

```csharp
public class MinionSnapshot
{
    // Identity
    public string CardId { get; set; }
    public string Name   { get; set; }  // resolved display name; falls back to CardId

    // Current stats (after all buffs applied)
    public int Attack { get; set; }   // baseAttack
    public int Health { get; set; }   // baseHealth (max − damage)

    // Base card stats (before any in-game buffs)
    // Lets ML distinguish a buffed 1/1 from a native 3/2
    public int VanillaAttack { get; set; }
    public int VanillaHealth { get; set; }

    // Classification
    public string Race        { get; set; }  // PrimaryRace.ToString(), e.g. "Beast", "Mech", "Invalid"
    public int    TavernTier  { get; set; }
    public bool   IsGolden    { get; set; }

    // Keywords
    public bool Taunt        { get; set; }
    public bool DivineShield { get; set; }
    public bool Poisonous    { get; set; }
    public bool Venomous     { get; set; }
    public bool Reborn       { get; set; }
    public bool Windfury     { get; set; }
    public bool MegaWindfury { get; set; }
    public bool Cleave       { get; set; }
    public bool Stealth      { get; set; }
    public bool HasWingmen   { get; set; }

    // Special ability magnitudes — encode things like "buff amount", "counter value"
    // Many unique minion effects are fully described by these four ints
    public int ScriptDataNum1 { get; set; }
    public int ScriptDataNum2 { get; set; }
    public int ScriptDataNum3 { get; set; }
    public int ScriptDataNum4 { get; set; }

    // Attached enchantments that granted additional deathrattles / rallies
    // Populated for player minions (from Entity.AttachedEntities); null for opponent
    // Examples: "ReplicatingMenace enchantment", "Leapfrogger enchantment"
    public List<string>? EnchantmentCardIds       { get; set; }
    public int           AdditionalDeathrattleCount { get; set; }  // always available
    public int           AdditionalRallyCount       { get; set; }
}
```

> **Why VanillaAttack/VanillaHealth?** A minion sitting at 5/5 could be a native 5/5 or a 1/1 that was triple-buffed. The vanilla stats let an ML model compute "buff received" as a feature. `vanillaAttack` / `vanillaHealth` are already set by BobsBuddy on the Minion object.

> **Why EnchantmentCardIds for player only?** We have `Entity[]` for the player side and can read `entity.AttachedEntities`. For the opponent, BobsBuddy Minion objects are all we have, and their deathrattle delegates are anonymous; `AdditionalDeathrattleCount` is the only reliable count available.

### `PermutationEntry`

```csharp
public class PermutationEntry
{
    // Indices into PlayerMinions, left→right board position
    // e.g. [2, 0, 4, 1] means "position 0 gets PlayerMinions[2], position 1 gets PlayerMinions[0], ..."
    // Using indices (not card IDs) keeps this unambiguous when the same card appears twice
    public List<int> Ordering { get; set; }
    public float     WinRate  { get; set; }
}
```

---

## File layout

```
%AppData%\HearthstoneDeckTracker\PositioningLogs\
    <gameId>_t<turn>.json          ← written by PositioningLogger.WriteAsync()
    <gameId>_t<turn>_outcome.json  ← written by PositioningLogger.RecordOutcome()
```

`Config.Instance.DataDir` resolves to the AppData path. One file per combat — no append-race conditions, each combat independently readable.

The outcome is a separate file because writing it happens much later (after combat resolves in BobsBuddyInvoker), and overwriting a potentially large permutation file risks corruption on crash.

---

## Implementation

### New file: `BobsBuddy/PositioningLogger.cs`

```csharp
internal static class PositioningLogger
{
    private static readonly string LogDir =
        Path.Combine(Config.Instance.DataDir, "PositioningLogs");

    // Called from PositioningSimulator.RunAsync() — fire and forget
    public static async Task WriteAsync(
        Guid    gameId,
        int     turn,
        bool    isDuos,
        Input   capturedInput,               // BobsBuddy Input — source of PlayerContext
        Entity[] playerEntities,             // parallel to originalSide (ZONE_POSITION order)
        List<Minion> originalSide,           // player minions in canonical order
        List<(int[] perm, float winRate)> screenResults,    // Phase 1 output
        List<(int[] perm, float winRate)> confirmResults,   // Phase 2 output
        PositioningResult result)
    {
        try
        {
            Directory.CreateDirectory(LogDir);

            var log = new PositioningCombatLog
            {
                GameId    = gameId.ToString(),
                Turn      = turn,
                Timestamp = DateTime.UtcNow.ToString("o"),
                IsDuos    = isDuos,

                Player   = BuildPlayerContext(capturedInput.Player),
                Opponent = BuildPlayerContext(capturedInput.Opponent),

                PlayerMinions   = originalSide
                    .Select((m, i) => SnapshotPlayer(m, playerEntities[i]))
                    .ToList(),
                OpponentMinions = capturedInput.Opponent.Side
                    .Select(SnapshotOpponent)
                    .ToList(),
                PlayerTeammateMinions  = capturedInput.PlayerTeammate?.Side.Select(SnapshotOpponent).ToList(),
                OpponentTeammateMinions = capturedInput.OpponentTeammate?.Side.Select(SnapshotOpponent).ToList(),

                ScreenPhaseResults  = screenResults.Select(ToEntry).ToList(),
                ConfirmPhaseResults = confirmResults.Select(ToEntry).ToList(),

                ActualRank     = result.ActualRank,
                TotalPerms     = screenResults.Count,
                ActualWinRate  = result.ActualWinRate,
                OptimalWinRate = result.OptimalWinRate,
            };

            var path = Path.Combine(LogDir, $"{gameId}_t{turn}.json");
            var json = JsonConvert.SerializeObject(log, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);
            Log.Info($"PositioningLogger: wrote {path}");
        }
        catch(Exception e)
        {
            Log.Error($"PositioningLogger.WriteAsync: {e.Message}");
        }
    }

    // Called from BobsBuddyInvoker after combat resolves — fire and forget
    public static async Task RecordOutcomeAsync(Guid gameId, int turn, string outcome, int? damageReceived)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var record = new { Outcome = outcome, DamageReceived = damageReceived };
            var path = Path.Combine(LogDir, $"{gameId}_t{turn}_outcome.json");
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(record, Formatting.Indented));
            Log.Info($"PositioningLogger: outcome={outcome} damage={damageReceived} → {path}");
        }
        catch(Exception e)
        {
            Log.Error($"PositioningLogger.RecordOutcomeAsync: {e.Message}");
        }
    }

    private static PermutationEntry ToEntry((int[] perm, float winRate) r)
        => new() { Ordering = r.perm.ToList(), WinRate = r.winRate };

    private static PlayerContext BuildPlayerContext(BobsBuddyPlayer p) => new()
    {
        Health     = p.Health,
        TavernTier = p.Tier,
        HeroPowers = p.HeroPowers.Select(hp => new HeroPowerSnapshot
        {
            CardId      = hp.CardId,
            IsActivated = hp.IsActivated,
        }).ToList(),
        QuestCardIds     = p.Quests.Select(q => q.CardId).ToList(),
        TrinketCardIds   = p.Trinkets.Select(t => t.CardId).ToList(),
        ObjectiveCardIds = p.Objectives.Select(o => o.CardId).ToList(),
        EternalKnightCounter          = p.EternalKnightCounter,
        UndeadAttackBonus             = p.UndeadAttackBonus,
        BeastAttackBonus              = p.BeastAttackBonus,
        BeastHealthBonus              = p.BeastHealthBonus,
        WhelpAttackBonus              = p.WhelpAttackBonus,
        WhelpHealthBonus              = p.WhelpHealthBonus,
        BeetlesAtkBuff               = p.BeetlesAtkBuff,
        BeetlesHealthBuff            = p.BeetlesHealthBuff,
        AncestralAutomatonCounter     = p.AncestralAutomatonCounter,
        ElementalPlayCounter          = p.ElementalPlayCounter,
        PiratesSummonCounter          = p.PiratesSummonCounter,
        BeastsSummonCounter           = p.BeastsSummonCounter,
        FriendlyMinionsDeadLastCombat = p.FriendlyMinionsDeadLastCombatCounter,
        BattlecryCounter             = p.BattlecryCounter,
        ResourcesSpentThisGame       = p.ResourcesSpentThisGame,
    };

    private static MinionSnapshot SnapshotPlayer(Minion m, Entity e) => new()
    {
        CardId        = m.CardID,
        Name          = e.Card?.Name ?? m.CardID,
        Attack        = m.baseAttack,
        Health        = m.baseHealth,
        VanillaAttack = m.vanillaAttack,
        VanillaHealth = m.vanillaHealth,
        Race          = m.PrimaryRace.ToString(),
        TavernTier    = m.tier,
        IsGolden      = m.golden,
        Taunt         = m.taunt,
        DivineShield  = m.div > 0,
        Poisonous     = m.poisonous,
        Venomous      = m.venomous,
        Reborn        = m.reborn,
        Windfury      = m.windfury,
        MegaWindfury  = m.megaWindfury,
        Cleave        = m.cleave,
        Stealth       = m.stealth,
        HasWingmen    = m.HasWingmen,
        ScriptDataNum1 = m.ScriptDataNum1,
        ScriptDataNum2 = m.ScriptDataNum2,
        ScriptDataNum3 = m.ScriptDataNum3,
        ScriptDataNum4 = m.ScriptDataNum4,
        EnchantmentCardIds        = e.AttachedEntities
            .Where(ae => ae.CardId != null)
            .Select(ae => ae.CardId!)
            .ToList(),
        AdditionalDeathrattleCount = m.AdditionalDeathrattles.Count,
        AdditionalRallyCount       = m.AdditionalRallies.Count,
    };

    private static MinionSnapshot SnapshotOpponent(Minion m) => new()
    {
        CardId        = m.CardID,
        Name          = HearthDb.Cards.All.GetValueOrDefault(m.CardID)?.Name ?? m.CardID,
        Attack        = m.baseAttack,
        Health        = m.baseHealth,
        VanillaAttack = m.vanillaAttack,
        VanillaHealth = m.vanillaHealth,
        Race          = m.PrimaryRace.ToString(),
        TavernTier    = m.tier,
        IsGolden      = m.golden,
        Taunt         = m.taunt,
        DivineShield  = m.div > 0,
        Poisonous     = m.poisonous,
        Venomous      = m.venomous,
        Reborn        = m.reborn,
        Windfury      = m.windfury,
        MegaWindfury  = m.megaWindfury,
        Cleave        = m.cleave,
        Stealth       = m.stealth,
        HasWingmen    = m.HasWingmen,
        ScriptDataNum1 = m.ScriptDataNum1,
        ScriptDataNum2 = m.ScriptDataNum2,
        ScriptDataNum3 = m.ScriptDataNum3,
        ScriptDataNum4 = m.ScriptDataNum4,
        EnchantmentCardIds         = null,   // no Entity objects available for opponents
        AdditionalDeathrattleCount = m.AdditionalDeathrattles.Count,
        AdditionalRallyCount       = m.AdditionalRallies.Count,
    };
}
```

### Changes to `PositioningSimulator.cs`

The current `RunAsync()` discards the screen results after merging. We need to preserve them for the logger. Change:

1. Save `screenResults` to a local variable before sorting (it's already a list, just save the reference before `Sort()`).
2. Save `confirmResults` similarly.
3. Before `return result`, fire-and-forget the logger:

```csharp
// preserve before sort mutates order
var screenForLog  = screenResults.Select(r => ((int[])r.perm.Clone(), r.winRate)).ToList();
var confirmForLog = confirmResults.Select(r => ((int[])r.perm.Clone(), r.winRate)).ToList();

// ... existing merge + sort logic ...

_ = PositioningLogger.WriteAsync(
    _gameId, _turn,
    isDuos: capturedInput.PlayerTeammate != null,
    capturedInput,
    entities,
    originalSide,
    screenForLog,
    confirmForLog,
    result);

return result;
```

### Changes to `BobsBuddyInvoker.cs`

After `GetLastCombatResult()` resolves (around line 1172), add:

```csharp
var damageReceived = result == CombatResult.Loss
    ? /* hero damage taken this combat */ (int?)someValue
    : null;

_ = PositioningLogger.RecordOutcomeAsync(
    _gameId, _turn,
    result.ToString(),   // "Win", "Loss", or "Tie"
    damageReceived);
```

> **Note:** The exact expression for `damageReceived` needs to be confirmed during implementation — it may come from the hero's `DamageTaken` tag delta or a separate field on the combat output. This is flagged as a TODO for the implementation step.

---

## What the two permutation lists enable for ML

| Analysis | Uses |
|----------|------|
| Optimal ordering prediction | `ConfirmPhaseResults[0].Ordering` as training label |
| Full win-rate distribution | All `ScreenPhaseResults` — gives the full preference landscape |
| Screening quality study | Compare `ScreenPhaseResults` vs `ConfirmPhaseResults` rankings — quantifies how noisy 50-iter screening is |
| Actual-vs-optimal delta | `ActualRank / TotalPerms` as a "positioning efficiency" scalar |

---

## File size estimate

| Component | Size |
|-----------|------|
| 5040 screen entries × 28 bytes each | ~140 KB |
| 15 confirm entries | ~400 bytes |
| 14 minion snapshots (7+7) × ~300 bytes | ~4 KB |
| Player context (×2) | ~1 KB |
| **Total per combat** | **~145 KB** |
| 12 combats per game | ~1.7 MB/game |

Acceptable. If disk use becomes a concern, the screen phase results can be compressed or dropped post-collection.

---

## Dependencies

- `Newtonsoft.Json` — already a project dependency
- `HearthDb` — already referenced; used for opponent name resolution
- No new NuGet packages needed
