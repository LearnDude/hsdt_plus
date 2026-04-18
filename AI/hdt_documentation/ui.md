# UI Layer

The UI consists of a main application window and a transparent in-game overlay. Both are WPF windows.

## OverlayWindow

**File:** `Hearthstone Deck Tracker/Windows/OverlayWindow.xaml.cs` (~1,600 lines)

A transparent, always-on-top WPF window that renders over the Hearthstone client. Positioned and sized to match the Hearthstone window via `User32` interop.

### Major sections

| Region | What it renders |
|--------|----------------|
| Player card list | Cards remaining in player deck with draw-count annotations |
| Opponent card list | Cards played/drawn by opponent (predicted deck) |
| Player hand | Hover card details, age and draw information |
| Turn timer | `TurnTimer` countdown display |
| Bob's Buddy panel | `BobsBuddyPanel` — win/loss/tie probabilities |
| Battlegrounds minion pins | Pins on Battlegrounds minions showing tier/stats |
| Mulligan guide | `MulliganGuideDisplay` — card keep/toss recommendations |
| Secrets panel | `SecretsPanel` — possible opponent secrets |
| Counters | Active counter displays (combo count, libram reduction, etc.) |
| Related cards | Cards synergistic with hovered card |
| Arena draft helpers | Card quality indicators during arena drafting |

### Key public members

- `BobsBuddyDisplay` — `BobsBuddyPanel` control, accessed by `BobsBuddyInvoker`
- `ShowOverlay(bool)` — enables/disables rendering
- `Update()` — called on a timer to refresh card lists and positions
- `UpdatePosition()` — repositions overlay to match Hearthstone window bounds
- `ChinaModuleVM` — ViewModel for China-specific module display

### Battlegrounds-specific controls

- `Controls/Overlay/Battlegrounds/` — hero picking overlay, session stats, minion tier display
- `BattlegroundsMinion.xaml.cs` — renders a pinned minion card on the game board
- `BattlegroundsSessionViewModel` — tracks MMR changes and hero performance over a session

## MainWindow

**File:** `Hearthstone Deck Tracker/Windows/MainWindow.xaml.cs` (~2,000 lines)

Standard WPF application window with:
- Deck list panel (import, edit, activate)
- Statistics tabs (win rate charts, match history)
- Settings flyout
- Plugins menu
- Tray icon integration (`TrayIcon.cs`)

## Other Windows

| File | Purpose |
|------|---------|
| `Windows/OpponentWindow.xaml.cs` | Detached opponent card list window |
| `Windows/PlayerWindow.xaml.cs` | Detached player card list window |
| `Windows/DebugWindow.xaml.cs` | Developer tools: entity list, log viewer |
| `Windows/StatsWindow.xaml.cs` | Detailed statistics browser |

## Controls

### Card rendering

- `Controls/Card.xaml.cs` — renders a single card with art, stats, mechanics icons
- `Controls/AnimatedCard.xaml.cs` — animated card draw/play effect
- `Controls/CardMarker.xaml.cs` — overlaid annotations (count drawn, cost reduction)

### Overlay controls

- `Controls/Overlay/Battlegrounds/HeroPicking/` — hero tier display during pick phase
- `Controls/Overlay/Battlegrounds/Minions/` — minion info pins
- `Controls/Overlay/Battlegrounds/Session/` — session tracking panel
- `Controls/Overlay/Constructed/Mulligan/` — keep/toss recommendation cards
- `Controls/Overlay/Arena/` — arena card quality display

## Theming

Uses **MahApps.Metro** (`Utility/Themes/`). Theme (light/dark) and accent color are user-configurable via `Config.Instance`. `ThemeManager` applies changes at runtime without restart.

## WPF Data Binding

`Player` implements `INotifyPropertyChanged`. `OverlayWindow` data-binds to `Player` and `GameV2` properties so zone collections update the UI automatically when the game state changes.

ViewModels follow the pattern in `Utility/MVVM/ViewModel.cs` (base class with `RaisePropertyChanged`).
