# Plan: Positioning Replay & Reliability Tool

## Goal

A standalone C# console app that reads `PositioningCombatLog` JSON files written by the live simulator and re-runs all permutations at a configurable high iteration count (default 10 000). It then compares the high-precision results against the original two-phase results to measure how reliable the fast approximation is.

Runs without Hearthstone or HDT running — only the JSON log files are needed.

---

## Configuration file

All constants and analysis thresholds live in a `replay-config.json` file alongside the exe. The app reads this on startup; missing keys fall back to the defaults shown below.

```json
{
  "ReplayIterations":            10000,
  "MaxTimePerPermutationMs":     5000,
  "ParallelDegree":              0,

  "SignificanceBandPct":         1.0,
  "MeaningfulDeltaThresholdPct": 5.0,
  "NearTieBandPct":              5.0,
  "TopKRecallK":                 15,

  "LogDirectory":                "",

  "OutputFormat":                "json",
  "OutputPath":                  ""
}
```

| Key | Default | Meaning |
|-----|---------|---------|
| `ReplayIterations` | 10000 | Iterations per permutation in the replay run |
| `MaxTimePerPermutationMs` | 5000 | Timeout per permutation (raised from live 100ms to allow full iteration count) |
| `ParallelDegree` | 0 | 0 = `Environment.ProcessorCount` |
| `SignificanceBandPct` | 1.0 | Win-rate differences below this (pp) are treated as tied — prevents noise-vs-noise comparisons |
| `MeaningfulDeltaThresholdPct` | 5.0 | Δ above this is considered "meaningfully suboptimal" for decision-flip analysis |
| `NearTieBandPct` | 5.0 | A combat where all permutations fall within this band is tagged as a near-tie board |
| `TopKRecallK` | 15 | K used in recall@K metric (should match live `TopKCandidates`) |
| `LogDirectory` | "" | Empty = `%AppData%\HearthstoneDeckTracker\PositioningLogs\` |
| `OutputFormat` | "json" | `"json"` or `"csv"` |
| `OutputPath` | "" | Empty = write alongside the input logs |

---

## Project structure

New project: `PositioningReplayTool/` in the solution root (Console, .NET 4.8 to match HDT, x86 platform).

```
PositioningReplayTool/
    Program.cs               — entry point: load config, discover logs, run replays, write report
    ReplayConfig.cs          — deserialises replay-config.json; applies defaults
    CombatLogReader.cs       — reads PositioningCombatLog JSON; pairs main + outcome files
    InputBuilder.cs          — reconstructs BobsBuddy Input from PositioningCombatLog
    MinionBuilder.cs         — reconstructs BobsBuddy Minion from MinionSnapshot
    ReplayRunner.cs          — runs all permutations at ReplayIterations, returns ranked list
    ComparisonReport.cs      — computes all metrics; produces CombatComparison records
    ReportWriter.cs          — serialises the report to JSON or CSV
    replay-config.json       — default config (copied to output dir on build)
```

References: `BobsBuddy.dll`, `HearthDb.dll`, `Newtonsoft.Json.dll` — already in the HDT output directory.

---

## Minion reconstruction

`MinionBuilder.BuildFromSnapshot(MinionSnapshot s)` needs to produce a `BobsBuddy.Minion` with correct deathrattle and attack delegates, not just stat values.

**Strategy:** use the card ID to call BobsBuddy's internal card factory (the same path `BobsBuddyUtils.GetMinionFromEntity` uses), then override the runtime stats from the snapshot.

Steps:
1. Call `BobsBuddyUtils.GetMinionFromCardId(s.CardId)` — confirm this method exists; if not, find the equivalent factory entry point in BobsBuddy and document it here.
2. Override `baseAttack`, `baseHealth`, `vanillaAttack`, `vanillaHealth`, `golden`, `taunt`, `div`, `poisonous`, `venomous`, `reborn`, `windfury`, `megaWindfury`, `cleave`, `stealth`, `HasWingmen`, `ScriptDataNum1–4` from the snapshot.
3. Set `AdditionalDeathrattleCount` / `AdditionalRallyCount` from snapshot fields — the exact mechanism (delegates vs counters) must be confirmed against the BobsBuddy source before implementation.

> **Open question before implementation:** confirm the factory method name and whether step 3 is achievable without an HDT Entity. Flag as a TODO if not resolvable from the DLL surface alone.

---

## Replay run

`ReplayRunner.RunAsync(PositioningCombatLog log, ReplayConfig cfg)` → `List<(int[] perm, float winRate)>` sorted descending.

- Reconstructs `Input` from `InputBuilder` (player side + opponent side + PlayerContext counters).
- Generates all permutations with the same `BobsBuddyUtils.Permutations` helper.
- Runs each permutation with `SimulationRunner.SimulateMultiThreaded(input, cfg.ReplayIterations, 1, cfg.MaxTimePerPermutationMs)` — same parallel pattern as `PositioningSimulator` (semaphore + `TaskCreationOptions.LongRunning`).
- Returns ranked results.

---

## Comparison metrics

`ComparisonReport.Compute(PositioningCombatLog log, List<(int[] perm, float winRate)> replayResults, ReplayConfig cfg)` → `CombatComparison`.

### Per-combat record: `CombatComparison`

```csharp
class CombatComparison
{
    // Identity
    string GameId;
    int    Turn;
    int    N;                      // number of player minions
    bool   IsNearTieBoard;         // all replay win rates within NearTieBandPct

    // Metric A: Δ-estimation error
    float  DeltaOriginal;          // optimal - actual, from original confirm results
    float  DeltaReplay;            // optimal - actual, from replay results
    float  DeltaError;             // |DeltaOriginal - DeltaReplay| in pp

    // Metric B: Decision flip
    bool   MeaningfulOriginal;     // DeltaOriginal > MeaningfulDeltaThresholdPct
    bool   MeaningfulReplay;       // DeltaReplay   > MeaningfulDeltaThresholdPct
    bool   DecisionFlipped;        // MeaningfulOriginal != MeaningfulReplay

    // Metric C: Top-K screening recall
    bool   TrueWinnerInTopK;       // replay winner's perm was in original top-K confirm set
    int    TrueWinnerScreenRank;   // rank of replay winner in the screen phase results

    // Metric D: Top-2 gap and winner flip
    float  Top2GapReplay;          // rank-1 winRate - rank-2 winRate at 10K (pp)
    bool   WinnerFlipped;          // original confirm winner != replay winner (outside significance band)

    // Metric E: Rank of actual ordering
    int    ActualRankOriginal;     // from log
    int    ActualRankReplay;       // rank of identity perm in replay results
    float  ActualPercentileOriginal;
    float  ActualPercentileReplay;
    float  ActualRankDelta;        // |percentile difference|

    // Metadata
    string ActualOutcome;          // "Win" / "Loss" / "Tie" / null
}
```

### Aggregate report

Computed across all `CombatComparison` records, and also broken out by `N` (board size):

| Metric | Aggregate stat |
|--------|---------------|
| Δ-estimation error | mean, median, p90 of `DeltaError` |
| Decision flip rate | fraction where `DecisionFlipped` is true |
| Winner flip rate | fraction where `WinnerFlipped` is true |
| Winner flip rate on near-tie boards | same, filtered to `IsNearTieBoard` |
| Winner flip rate on peaked boards | same, filtered to `!IsNearTieBoard` |
| Screening recall@K | fraction where `TrueWinnerInTopK` is true |
| Median `TrueWinnerScreenRank` | how far down the screen ranking the true winner sat |
| Rank percentile delta | median, p90 of `ActualRankDelta` |
| Near-tie board fraction | fraction of combats that are near-tie |

All aggregate stats are computed overall **and per N** (broken out by 2, 3, 4, 5, 6, 7 minions) to capture the small-board behaviour separately — small boards may have flatter win-rate distributions and thus higher flip rates even with far fewer permutations.

---

## Near-tie board handling

A combat is tagged `IsNearTieBoard = true` when `max(replayWinRates) - min(replayWinRates) < cfg.NearTieBandPct`. On these boards:

- Rank flips are expected noise, not real error — they are reported but annotated.
- The aggregate flip-rate stats report both the raw rate and the rate excluding near-tie boards.
- This prevents flat boards from inflating the headline flip-rate numbers.

---

## Significance band

When comparing two win rates (e.g., deciding if winner flipped), values within `SignificanceBandPct` of each other are treated as tied. Prevents measuring noise-vs-noise when permutations are nearly identical at both iteration counts.

---

## Output

Two files written per run:

1. **`replay_report.json`** (or `.csv`) — one row per combat with all `CombatComparison` fields.
2. **`replay_summary.json`** — aggregate stats overall and per N.

---

## Files to create

| File | Notes |
|------|-------|
| `PositioningReplayTool/PositioningReplayTool.csproj` | Console, .NET 4.8, x86 |
| `PositioningReplayTool/Program.cs` | |
| `PositioningReplayTool/ReplayConfig.cs` | |
| `PositioningReplayTool/CombatLogReader.cs` | |
| `PositioningReplayTool/InputBuilder.cs` | |
| `PositioningReplayTool/MinionBuilder.cs` | |
| `PositioningReplayTool/ReplayRunner.cs` | |
| `PositioningReplayTool/ComparisonReport.cs` | |
| `PositioningReplayTool/ReportWriter.cs` | |
| `PositioningReplayTool/replay-config.json` | Copied to output dir by build |

Add the new project to `Hearthstone Deck Tracker.sln`.

---

## Open questions (resolve before implementation)

1. **MinionBuilder factory method** — confirm the BobsBuddy API surface for creating a `Minion` from a card ID with delegates. If no direct factory exists, document the workaround.
2. **PlayerContext → BobsBuddyPlayer reconstruction** — confirm all counter fields map directly; check if any fields in `PlayerContext` cannot be written back to `BobsBuddyPlayer` (e.g., if properties are get-only).
3. **`AdditionalDeathrattleCount` / `AdditionalRallyCount`** — confirm whether these can be set on a reconstructed `Minion` without the original enchantment entities.
