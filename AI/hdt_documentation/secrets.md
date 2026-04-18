# Secrets Detection

Tracks which opponent secrets are currently active and eliminates impossible secrets as game events occur.

## SecretsManager

**File:** `Hearthstone Deck Tracker/Hearthstone/Secrets/SecretsManager.cs`

Extends `SecretsEventHandler`. Owns the live list of possible secrets per secret entity.

Key members:
- `Secrets` — `List<Secret>` currently in opponent secret zone
- `OnSecretsChanged` — `event Action<List<Card>>` fired when the possible-secret list changes (used by overlay)
- `NewSecret(Entity entity)` — called when a new secret entity is detected; adds to `Secrets`, excludes known multi-ID secrets
- `Reset()` — clears all secrets, fires `OnSecretsChanged` with empty list
- `Refresh()` — recalculates and fires `OnSecretsChanged`

Initialized in `GameV2` constructor with a `RemoteArenaSettings` provider (fetches available secrets per class from remote config).

## SecretsEventHandler

**File:** `Hearthstone Deck Tracker/Hearthstone/Secrets/SecretsEventHandler.cs`

Base class implementing the elimination logic. Subscribes to game events and calls `Exclude(cardId, invokeCallback)` when a secret is proved impossible.

### Elimination triggers (examples)

| Game event | Secrets eliminated |
|------------|-------------------|
| Player attacks hero | `Noble Sacrifice`, `Explosive Trap`, `Freezing Trap`, `Misdirection` |
| Player attacks minion | `Vaporize` (if attacking hero), `Snipe` |
| Player plays minion | `Mirror Entity`, `Sacred Trial`, `Effigy` |
| Player plays spell | `Counterspell`, `Spellbender`, `Objection!` |
| End of player turn | `Competitive Spirit`, `Sacred Trial` (if conditions met) |
| Opponent minion dies | `Vendettas`, `Redemption` |
| Player draws card | `Rat Trap` (if 3+ cards played) |

Full elimination logic covers all classes' secrets and is updated each patch.

## Secret

**File:** `Hearthstone Deck Tracker/Hearthstone/Secrets/Secret.cs`

Represents one active secret entity with its list of still-possible `Card` candidates.

- `Entity` — the live entity in the secret zone
- `Possible` — `List<Card>` of cards it could be (narrows as events occur)
- `IsExcluded(cardId)` — whether a candidate has been eliminated

## GetSecretList()

Returns a `List<Card>` aggregated across all active secrets. Each `Card.Count` reflects how many active secrets could be that card. This list drives the `SecretsPanel` overlay display.

## Integration point

`SecretsManager.NewSecret()` is called from `GameEventHandler` when a `ZONE=SECRET` tag change is detected for an opponent entity. `SecretsManager.Exclude()` is called from `SecretsEventHandler` methods that are in turn called by `GameEventHandler` as plays and attacks occur.
