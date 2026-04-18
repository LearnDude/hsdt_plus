# API and Event System

The `API/` directory provides the public extensibility surface — both for internal use and for third-party plugins.

## GameEvents

**File:** `Hearthstone Deck Tracker/API/GameEvents.cs`

Static class containing `ActionList<T>` event lists. Subscribe with `+=`, unsubscribe with `-=`.

### Player events

| Event | Argument | Fired when |
|-------|----------|-----------|
| `OnPlayerDraw` | `Card` | Player draws a card |
| `OnPlayerGet` | `Card` | Player receives a card (created, not drawn) |
| `OnPlayerPlay` | `Card` | Player plays a card from hand |
| `OnPlayerHandDiscard` | `Card` | Player discards from hand |
| `OnPlayerMulligan` | `Card` | Player mulligans a card |
| `OnPlayerDeckDiscard` | `Card` | Player discards from deck |
| `OnPlayerPlayToDeck` | `Card` | Card added to player deck |
| `OnPlayerPlayToHand` | `Card` | Card returned to player hand |
| `OnPlayerPlayToGraveyard` | `Card` | Card moved to graveyard |
| `OnPlayerCreateInDeck` | `Card` | Card created in deck |
| `OnPlayerCreateInPlay` | `Card` | Card created in play |
| `OnPlayerJoustReveal` | `Card` | Card revealed by joust |
| `OnPlayerDeckToPlay` | `Card` | Card moved from deck directly to play |
| `OnPlayerHeroPower` | — | Player uses hero power |
| `OnPlayerFatigue` | `int` damage | Player takes fatigue |
| `OnPlayerMinionMouseOver` | `Card` | Mouse hovers over player minion |
| `OnPlayerHandMouseOver` | `Card` | Mouse hovers over card in hand |
| `OnPlayerMinionAttack` | `AttackInfo` | Player minion declares attack |

### Opponent events

| Event | Argument | Fired when |
|-------|----------|-----------|
| `OnOpponentDraw` | — | Opponent draws (card unknown) |
| `OnOpponentGet` | — | Opponent receives a card |
| `OnOpponentPlay` | `Card` | Opponent plays a card (if known) |
| `OnOpponentHandDiscard` | `Card` | Opponent discards from hand |
| `OnOpponentMulligan` | — | Opponent mulligans |
| `OnOpponentDeckDiscard` | `Card` | Opponent discards from deck |
| `OnOpponentPlayToDeck` | `Card` | Card added to opponent deck |
| `OnOpponentHandToDeck` | `Card` | Card from opponent hand to deck |
| `OnOpponentPlayToHand` | `Card` | Card returned to opponent hand |
| `OnOpponentPlayToGraveyard` | `Card` | Opponent card to graveyard |
| `OnOpponentSecretTriggered` | `Card` | Opponent secret triggers |
| `OnOpponentCreateInDeck` | `Card` | Card created in opponent deck |
| `OnOpponentCreateInPlay` | `Card` | Card created in opponent play |
| `OnOpponentJoustReveal` | `Card` | Card revealed by joust |
| `OnOpponentDeckToPlay` | `Card` | Opponent deck to play |
| `OnOpponentHeroPower` | — | Opponent uses hero power |
| `OnOpponentFatigue` | `int` damage | Opponent takes fatigue |
| `OnOpponentMinionMouseOver` | `Card` | Mouse hovers over opponent minion |
| `OnOpponentMinionAttack` | `AttackInfo` | Opponent minion declares attack |
| `OnEntityWillTakeDamage` | `PredamageInfo` | Any entity is about to take damage |

### Game events

| Event | Argument | Fired when |
|-------|----------|-----------|
| `OnGameStart` | — | New game begins |
| `OnGameEnd` | — | Game ends (any outcome) |
| `OnGameWon` | — | Player wins |
| `OnGameLost` | — | Player loses |
| `OnGameTied` | — | Game tied |
| `OnInMenu` | — | Returned to main menu |
| `OnTurnStart` | `ActivePlayer` | A new turn begins |
| `OnMouseOverOff` | — | Mouse moved off a card |
| `OnModeChanged` | `Mode` | Game mode/screen changed |

## ActionList

**File:** `Hearthstone Deck Tracker/API/ActionList.cs`

A thin `List<Action<T>>` wrapper supporting `+=` / `-=` operator syntax and null-safe `Fire(T arg)`.

## API.Core

**File:** `Hearthstone Deck Tracker/API/Core.cs`

Static read-only accessors for external consumers:
- `API.Core.Game` → `GameV2`
- `API.Core.Overlay` → `OverlayWindow`
- `API.Core.MainWindow` → `MainWindow`

## Plugin System

**File:** `Hearthstone Deck Tracker/Plugins/PluginManager.cs`

- Scans `%AppData%\HearthstoneDeckTracker\Plugins\` for `.dll` files.
- Loads each DLL via reflection, looks for types implementing `IPlugin`.
- Calls `plugin.Load()` on startup and `plugin.UnLoad()` on shutdown.
- Calls `plugin.Update()` every ~100ms during a game.
- `plugin.MenuItem` — optional WPF `MenuItem` added to the HDT Plugins menu.

**IPlugin interface** (`Hearthstone Deck Tracker/Plugins/IPlugin.cs`):
```csharp
interface IPlugin {
    string Name { get; }
    string Description { get; }
    string ButtonText { get; }
    string Author { get; }
    Version Version { get; }
    MenuItem MenuItem { get; }
    void Load();
    void UnLoad();
    void Update();
    void OnButtonPress();
}
```

## Subscribing to Events (example)

```csharp
public void Load() {
    GameEvents.OnGameStart.Add(OnGameStart);
    GameEvents.OnPlayerPlay.Add(OnPlayerPlay);
}

private void OnGameStart() {
    var game = API.Core.Game;
    // game.CurrentGameMode, game.Player, game.Opponent, etc.
}

private void OnPlayerPlay(Card card) {
    // card.Name, card.Cost, card.CardId, etc.
}
```
