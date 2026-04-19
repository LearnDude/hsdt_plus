# Plan: Harmful Minion Removal Analysis

## Goal

After each combat, for every minion in the player's actual board, simulate what the outcome
would have been with that minion removed. If removing a minion improves the win rate by more
than a threshold (3 pp by default), mark it as "harmful" and display a large red X over it
in the YOURS row of `PositioningResultsWindow`.

---

## New data flow

```
RunAsync()
  → [existing two-phase permutation search] → actualWinRate
  → [new removal phase] for i in 0..n-1:
        simulate (originalSide minus minion i) at ConfirmIterations
        if winRate > actualWinRate + RemovalBenefitThreshold → harmfulIndices.Add(i)
  → PositioningResult.HarmfulMinionIndices populated
  → PositioningResultsViewModel sets IsHarmful on each ActualOrdering entry
  → XAML renders red X overlay when IsHarmful is true
```

---

## Files to change (4)

### 1. `BobsBuddy/PositioningSimulator.cs`

**In `PositioningResult` class** — add one new property:

```csharp
public HashSet<int> HarmfulMinionIndices { get; set; } = new();
```

**In `PositioningSimulator`** — add one new constant:

```csharp
private const float RemovalBenefitThreshold = 0.03f; // flag if removing a minion gains ≥3 pp
private const int RemovalIterations = 1000;           // higher count for reliable removal signal
```

**In `RunAsync()`** — after `capturedInput.Player.Side` is restored and `actualResult` is
known, add a removal phase before building the return value:

```csharp
// --- Removal phase: simulate with each minion removed ---
var harmfulIndices = new HashSet<int>();
if(n >= 2) // need at least 1 minion left after removal
{
    Log.Info($"PositioningSimulator: removal phase, {n} simulations at {ConfirmIterations} iters");
    var removalTasks = Enumerable.Range(0, n).Select(removeIdx =>
    {
        var reducedSide = originalSide.Where((_, i) => i != removeIdx).ToList();
        var identityPerm = Enumerable.Range(0, reducedSide.Count).ToArray();
        return (removeIdx, task: RunPermutationAsync(
            capturedInput, reducedSide, identityPerm, RemovalIterations, semaphore, "removal"));
    }).ToList();

    await Task.WhenAll(removalTasks.Select(x => x.task));

    foreach(var (removeIdx, task) in removalTasks)
    {
        var (_, winRate) = task.Result;
        if(winRate > actualResult.winRate + RemovalBenefitThreshold)
            harmfulIndices.Add(removeIdx);
    }
}
```

> **Threading note**: the removal tasks reuse the existing `semaphore` from the outer
> `using` block — move the `await Task.WhenAll` calls into a helper or restructure so the
> semaphore is still in scope. The simplest fix is to hoist the removal phase *inside* the
> `using(semaphore)` block, just before the `finally` that resets the ThreadPool.

**In the `return new PositioningResult { … }` block** — add:

```csharp
HarmfulMinionIndices = harmfulIndices,
```

---

### 2. `Controls/BattlegroundsMinionViewModel.cs`

Add one property using the existing `ViewModel` base pattern:

```csharp
public bool IsHarmful
{
    get => GetProp(false);
    set => SetProp(value);
}
```

---

### 3. `Controls/Overlay/Battlegrounds/Positioning/PositioningResultsViewModel.cs`

Change `ActualOrdering` construction to pass the harmful flag per index:

```csharp
ActualOrdering = result.ActualOrder
    .Select((entity, i) => ToViewModel(entity, result.HarmfulMinionIndices.Contains(i)))
    .ToList();
```

Update `ToViewModel` to accept the flag:

```csharp
private static BattlegroundsMinionViewModel ToViewModel(Entity entity, bool isHarmful = false) =>
    new BattlegroundsMinionViewModel
    {
        // … existing assignments unchanged …
        IsHarmful = isHarmful,
    };
```

---

### 4. `Windows/PositioningResultsWindow.xaml`

In the YOURS row only, change the `ItemsControl.ItemTemplate` DataTemplate to overlay a
red X when `IsHarmful` is true:

```xml
<ItemsControl.ItemTemplate>
    <DataTemplate>
        <Grid>
            <hdt:BattlegroundsMinion Width="100" Height="100" Margin="0,4,-8,4"/>
            <Canvas Width="100" Height="100" Margin="0,4,-8,4"
                    IsHitTestVisible="False"
                    Visibility="{Binding IsHarmful,
                        Converter={StaticResource BoolToVisibility}}">
                <Line X1="12" Y1="12" X2="88" Y2="88"
                      Stroke="#CC FF3333" StrokeThickness="7"
                      StrokeLineCap="Round"/>
                <Line X1="88" Y1="12" X2="12" Y2="88"
                      Stroke="#CC FF3333" StrokeThickness="7"
                      StrokeLineCap="Round"/>
            </Canvas>
        </Grid>
    </DataTemplate>
</ItemsControl.ItemTemplate>
```

`BoolToVisibility` is WPF's built-in `BooleanToVisibilityConverter`. Add it to window
resources if not already present:

```xml
<controls:MetroWindow.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
</controls:MetroWindow.Resources>
```

The OPTIMAL row DataTemplate is unchanged — no X overlays there.

---

## Timing impact

Removal phase: max 7 simulations × ~50 ms each ≈ **~350 ms extra**, running in parallel
with the existing `semaphore`. Negligible relative to the ~26 s main search.

---

## Tuning

| Constant | Default | Effect |
|---|---|---|
| `RemovalBenefitThreshold` | `0.03f` (3 pp) | Lower → more minions flagged; raise if too noisy |
| `RemovalIterations` | `1000` | Raise for tighter removal win-rate estimates |

---

## What NOT to change

- `RunPermutationAsync` — already handles arbitrary `originalSide` lengths; works for n-1 boards unchanged
- `BobsBuddyUtils.Permutations` — not called for removal phase (identity perm only)
- OPTIMAL row DataTemplate — no X overlays needed there
- `PositioningResult.ActualRank` / `TotalPermutations` — unaffected
