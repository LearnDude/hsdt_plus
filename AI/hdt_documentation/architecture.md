# Architecture

## System Layers

```
┌─────────────────────────────────────────────────────┐
│              Hearthstone Process                      │
│  Writes log files: power.log, game.log, loading.log  │
└──────────────────────┬──────────────────────────────┘
                       │ file I/O
┌──────────────────────▼──────────────────────────────┐
│  LogReader Layer                                      │
│  LogWatcherManager → HsGameState (state machine)     │
│  Handlers: PowerHandler, TagChangeHandler,            │
│            GameInfoHandler, ChoicesHandler,           │
│            LoadingScreenHandler, ArenaHandler         │
└──────────────────────┬──────────────────────────────┘
                       │ calls IGameHandler
┌──────────────────────▼──────────────────────────────┐
│  Game Model Layer                                     │
│  GameV2 ← Player (×2), Entity (dictionary),          │
│           Deck, SecretsManager, BobsBuddyInvoker,    │
│           BattlegroundsBoardState                    │
└──────────────────────┬──────────────────────────────┘
                       │ fires ActionList events
┌──────────────────────▼──────────────────────────────┐
│  Event Layer  (API/GameEvents.cs)                    │
│  GameEventHandler + plugins subscribe to events      │
└──────────────┬───────────────┬──────────────────────┘
               │               │
┌──────────────▼──┐  ┌─────────▼─────────────────────┐
│  UI Layer        │  │  Side-effect Layer             │
│  OverlayWindow   │  │  Stats recording               │
│  MainWindow      │  │  HSReplay log upload           │
│  Controls/       │  │  Deck import/export            │
└─────────────────┘  └────────────────────────────────┘
```

## Data Flow (step by step)

1. Hearthstone writes structured log lines to `%AppData%\Hearthstone\Logs\`.
2. `LogWatcherManager` tails those files and dispatches lines to the appropriate `Handler`.
3. `PowerHandler` parses `power.log`: creates `Entity` objects, fires tag-change queues.
4. `TagChangeHandler` translates raw `GameTag` mutations into semantic meaning (zone changes, health, position).
5. `HsGameState` maintains the current block stack and triggers `IGameHandler` callbacks.
6. `GameEventHandler` (implements `IGameHandler`) updates `GameV2` and fires `GameEvents.*` static events.
7. `OverlayWindow` subscribes to `GameEvents` and `INotifyPropertyChanged` on `Player`/`GameV2` to repaint the overlay.
8. At game end, `GameEventHandler` writes a `GameStats` record and kicks off an HSReplay log upload.

## External Dependencies

| Library | Purpose | Location |
|---------|---------|----------|
| `HearthDb.dll` | Card database (stats, text, IDs) | `/lib/` |
| `HearthMirror.dll` | Memory reading (collection, hero picks, arena offers) | `/lib/` |
| `BobsBuddy.dll` | Battlegrounds combat simulation engine | `/lib/` |
| `HSReplay.dll` | API types and upload payloads for HSReplay.net | `/lib/` |
| `MahApps.Metro` | WPF theming | NuGet |
| `Newtonsoft.Json` | JSON serialization | NuGet |
| `LiveCharts` | Statistics charts | NuGet |
| `Squirrel.Windows` | Auto-updater | NuGet |
| `SharpRaven/Sentry` | Error reporting | NuGet |

## Project Structure

```
Hearthstone-Deck-Tracker/
├── Hearthstone Deck Tracker/   Main application (~1,291 .cs files)
│   ├── API/                    Public event API for plugins
│   ├── BobsBuddy/              Battlegrounds simulator wrapper
│   ├── Hearthstone/            Core game model
│   │   ├── Arena/
│   │   ├── CounterSystem/
│   │   ├── EffectSystem/
│   │   ├── Entities/
│   │   ├── RelatedCardsSystem/
│   │   └── Secrets/
│   ├── LogReader/
│   │   └── Handlers/
│   ├── HsReplay/
│   ├── Live/
│   ├── Stats/
│   ├── Controls/Overlay/
│   ├── Windows/
│   ├── Importing/
│   ├── Plugins/
│   └── Utility/
├── HDTTests/                   Unit tests
├── HearthWatcher/              Separate project: memory reader service
└── Bootstrap/                  Installer
```

## Plugin Architecture

- `IPlugin` interface: `Load()`, `UnLoad()`, `Update()`, `MenuItem` (WPF menu entry).
- `PluginManager` scans `%AppData%\HearthstoneDeckTracker\Plugins\` for DLLs.
- Plugins subscribe to `GameEvents.*` static `ActionList` events.
- `API.Core` exposes read-only access to `Game`, `Overlay`, `MainWindow`.

## Platform Constraints

- .NET Framework 4.7.2, Windows x86 (32-bit), WPF only.
- C# LangVersion 10, nullable annotations enabled.
- HearthMirror requires 32-bit process to read Hearthstone's 32-bit memory.
