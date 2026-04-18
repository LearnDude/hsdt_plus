# HSDT+ Codebase Index

Hearthstone Deck Tracker (HSDT+) is a .NET 4.7.2 WPF desktop application that monitors Hearthstone gameplay in real-time by parsing game log files. It renders an in-game overlay with card tracking, opponent secret detection, Battlegrounds combat simulation (Bob's Buddy), deck statistics, and HSReplay.net integration. This fork adds a **Battlegrounds positioning simulator** and **opponent battle history reader**.

## Documentation Files

| File | Contents |
|------|----------|
| [objective.md](../objective.md) | Project goals: positioning simulator + opponent battle history |
| [architecture.md](architecture.md) | System layers, data flow, external dependencies |
| [game-state.md](game-state.md) | GameV2, Player, Entity, Card, Deck |
| [log-reader.md](log-reader.md) | Log parsing, HsGameState state machine, handlers |
| [battlegrounds.md](battlegrounds.md) | BobsBuddyInvoker, BattlegroundsBoardState, combat simulator |
| [ui.md](ui.md) | OverlayWindow, MainWindow, controls |
| [api-events.md](api-events.md) | GameEvents, plugin system |
| [secrets.md](secrets.md) | SecretsManager, secret elimination logic |
| [stats.md](stats.md) | GameStats, DeckStats, GameEventHandler recording |
| [hsreplay.md](hsreplay.md) | HSReplay.net integration, OAuth, log upload |

## Key Class Quick-Reference

| Class | File | Responsibility |
|-------|------|----------------|
| `Core` | `Hearthstone Deck Tracker/Core.cs` | Global singleton; initializes everything |
| `GameV2` | `Hearthstone Deck Tracker/Hearthstone/GameV2.cs` | Live game state container |
| `Player` | `Hearthstone Deck Tracker/Hearthstone/Player.cs` | Per-player zone collections and stats |
| `Entity` | `Hearthstone Deck Tracker/Hearthstone/Entities/Entity.cs` | In-game object (minion/card/hero) with tags |
| `Card` | `Hearthstone Deck Tracker/Hearthstone/Card.cs` | Card definition bridging HearthDb |
| `Deck` | `Hearthstone Deck Tracker/Hearthstone/Deck.cs` | Deck composition, versions, metadata |
| `HsGameState` | `Hearthstone Deck Tracker/LogReader/HsGameState.cs` | Log parsing state machine |
| `PowerHandler` | `Hearthstone Deck Tracker/LogReader/Handlers/PowerHandler.cs` | Parses power.log into entity/tag events |
| `TagChangeHandler` | `Hearthstone Deck Tracker/LogReader/Handlers/TagChangeHandler.cs` | Processes GameTag mutations |
| `GameEventHandler` | `Hearthstone Deck Tracker/GameEventHandler.cs` | Fires API events, records stats |
| `BobsBuddyInvoker` | `Hearthstone Deck Tracker/BobsBuddy/BobsBuddyInvoker.cs` | Battlegrounds combat simulation orchestrator |
| `BobsBuddyUtils` | `Hearthstone Deck Tracker/BobsBuddy/BobsBuddyUtils.cs` | Converts entities to BobsBuddy Minion objects |
| `BattlegroundsBoardState` | `Hearthstone Deck Tracker/Hearthstone/BattlegroundsBoardState.cs` | Per-turn opponent board snapshots |
| `SecretsManager` | `Hearthstone Deck Tracker/Hearthstone/Secrets/SecretsManager.cs` | Opponent secret tracking and elimination |
| `OverlayWindow` | `Hearthstone Deck Tracker/Windows/OverlayWindow.xaml.cs` | In-game overlay rendering |
| `GameEvents` | `Hearthstone Deck Tracker/API/GameEvents.cs` | Static event lists for plugin subscriptions |
| `DeckManager` | `Hearthstone Deck Tracker/DeckManager.cs` | Automatic deck detection and selection |
| `GameStats` | `Hearthstone Deck Tracker/Stats/GameStats.cs` | Single-game record (heroes, outcome, cards) |
