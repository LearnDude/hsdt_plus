# Plan: Two-Phase Permutation Search for Positioning Simulator

## Problem

`PositioningSimulator.RunAsync()` currently runs every permutation at `PositioningIterations = 200` iterations. With 7 minions (5040 permutations) at ~0.5s each, the total wall time is far too long. Even the current random-sampling fallback (`SampledPermutations = 360`) still takes ~3 minutes.

## Solution: Two-Phase Screening

Run all permutations twice:

1. **Screen phase** — simulate every permutation at a very low iteration count (e.g. 50). This gives a noisy but directionally correct ranking. Goal: eliminate bad orderings cheaply.
2. **Confirm phase** — re-simulate the top-K candidates at full iteration count (e.g. 500–1000). Goal: get a reliable win-rate for the finalist orderings.

Expected timing for 7 minions:
- Screen: 5040 × ~0.005s ≈ **25s**
- Confirm top 10: 10 × ~0.05s ≈ **0.5s**
- Total: ~26s (vs. current ~42 minutes for exhaustive at full iterations)

This approach is **exhaustive** — it evaluates every permutation — so the final answer is the true optimum, not an approximation. The only risk is that 50 screening iterations is too noisy to reliably surface the true top-K; the tunable `TopKCandidates` mitigates this.

## Implementation

### New constants in `PositioningSimulator.cs`

Replace the existing constants block:

```csharp
private const int ScreenIterations   = 50;    // fast screen of all permutations
private const int ConfirmIterations  = 500;   // full confirm of finalists
private const int TopKCandidates     = 15;    // how many finalists to re-simulate
private const int MaxTimePerPermutationMs = 100;
private const int MaxFullPermsMinions = 7;    // raise: 7! = 5040 is now affordable
```

Remove `SampledPermutations` — we no longer need random sampling as a fallback.

### Replace the main loop in `RunAsync()`

Remove the `if (n <= MaxFullPermsMinions) ... else ...` branch and the `SampledPermutations` random-sampling block entirely.

Replace with:

```csharp
// --- Phase 1: screen all permutations at low iteration count ---
var indices = Enumerable.Range(0, n).ToArray();
var allPerms = BobsBuddyUtils.Permutations(indices).ToList();

Log.Info($"PositioningSimulator: screening {allPerms.Count} permutations at {ScreenIterations} iterations");

var screenResults = new List<(int[] perm, float winRate)>(allPerms.Count);
foreach (var perm in allPerms)
{
    capturedInput.Player.Side.Clear();
    foreach (var i in perm)
        capturedInput.Player.Side.Add(originalSide[i]);

    try
    {
        var output = await new SimulationRunner().SimulateMultiThreaded(
            capturedInput, ScreenIterations, BobsBuddyInvoker.ThreadCount, MaxTimePerPermutationMs);
        screenResults.Add(((int[])perm.Clone(), output?.winRate ?? 0f));
    }
    catch (Exception e)
    {
        Log.Error($"PositioningSimulator screen error: {e.Message}");
    }
}

// --- Phase 2: confirm top-K candidates at full iteration count ---
screenResults.Sort((a, b) => b.winRate.CompareTo(a.winRate));

// Always include the identity permutation (actual ordering) so we can rank it
var identityPerm = Enumerable.Range(0, n).ToArray();
var topK = screenResults.Take(TopKCandidates).ToList();
if (!topK.Any(r => r.perm.SequenceEqual(identityPerm)))
    topK.Add(screenResults.First(r => r.perm.SequenceEqual(identityPerm)));

Log.Info($"PositioningSimulator: confirming top {topK.Count} candidates at {ConfirmIterations} iterations");

var confirmResults = new List<(int[] perm, float winRate)>(topK.Count);
foreach (var candidate in topK)
{
    capturedInput.Player.Side.Clear();
    foreach (var i in candidate.perm)
        capturedInput.Player.Side.Add(originalSide[i]);

    try
    {
        var output = await new SimulationRunner().SimulateMultiThreaded(
            capturedInput, ConfirmIterations, BobsBuddyInvoker.ThreadCount, MaxTimePerPermutationMs);
        confirmResults.Add((candidate.perm, output?.winRate ?? 0f));
    }
    catch (Exception e)
    {
        Log.Error($"PositioningSimulator confirm error: {e.Message}");
        confirmResults.Add(candidate); // fall back to screen result
    }
}

// Merge: for permutations not in confirm set, use screen win rate
var confirmedPerms = new HashSet<string>(confirmResults.Select(r => string.Join(",", r.perm)));
var allResults = confirmResults
    .Concat(screenResults.Where(r => !confirmedPerms.Contains(string.Join(",", r.perm))))
    .ToList();

allResults.Sort((a, b) => b.winRate.CompareTo(a.winRate));
var results = allResults; // rename to match rest of method
```

The remainder of `RunAsync()` (ranking, entity mapping, building `PositioningResult`) is **unchanged**.

### Remove `SampledPermutations` constant and its dead code

Delete:
```csharp
private const int SampledPermutations = 360;
```
and the `else` branch that used it.

### Update `Factorial` visibility (optional)

No change needed — it's still used in the log message in screen phase if you want to log total count.

## Tuning guidance

| Constant | Lower → | Higher → |
|---|---|---|
| `ScreenIterations` | Faster screen, noisier ranking | Slower but more reliable top-K selection |
| `ConfirmIterations` | Faster confirm, less precise final rates | Slower but tighter confidence on winner |
| `TopKCandidates` | Risk missing true optimum if screen is noisy | More confirm work, but safer |

Start with the values above. If the final win-rate difference between actual and optimal seems unreliable (e.g. they flip on repeated runs), raise `ConfirmIterations`. If the wrong winner is selected, raise `TopKCandidates`.

## Files to change

- `Hearthstone-Deck-Tracker/Hearthstone Deck Tracker/BobsBuddy/PositioningSimulator.cs` — only file

## What NOT to change

- `BobsBuddyUtils.Permutations()` — already works correctly
- `PositioningResult` — no changes needed
- `BobsBuddyInvoker` — no changes needed
- The entity-mapping and result-building code at the bottom of `RunAsync()`
