# Game State Model

The game state lives in `Hearthstone Deck Tracker/Hearthstone/` and represents a live snapshot of a running Hearthstone match.

## GameV2

**File:** `Hearthstone Deck Tracker/Hearthstone/GameV2.cs`

The central game state container. Holds references to all other model objects and is the single source of truth for what is happening in the current match.

Key properties:
- `Player`, `Opponent` — two `Player` instances (local player is `IsLocalPlayer=true`)
- `Entities` — `Dictionary<int, Entity>` of every in-game object keyed by entity ID
- `CurrentGameMode` — `GameMode` enum (Ranked, Arena, Battlegrounds, etc.)
- `IsInMenu`, `IsRunning` — coarse lifecycle flags
- `GameEntity` — the special entity that holds global game tags (turn number, etc.)
- `SecretsManager` — opponent secret tracker
- `ActiveEffects`, `CounterManager`, `RelatedCardsManager` — sub-systems initialized in constructor
- `BattlegroundsBoardState` / `BattlegroundsDuosBoardState` — opponent board history
- `BattlegroundsSessionViewModel` — session stats for the overlay

Lifecycle: `Reset()` is called at game start (clears entities, zones, metadata). `DeckList.Instance.ActiveDeckChanged` triggers a re-read of the log when the active deck changes mid-game.

## Player

**File:** `Hearthstone Deck Tracker/Hearthstone/Player.cs`

Represents one player (local or opponent). All zone collections are computed `IEnumerable<Entity>` queries over `GameV2.Entities` filtered by `IsControlledBy(Id)`.

Zone accessors:
- `Hand` — entities with `IsInHand`
- `Board` — entities with `IsInPlay`
- `Deck` — entities with `IsInDeck`
- `Graveyard` — entities with `IsInGraveyard`
- `SecretZone` — entities in secret zone
- `Minions` — board entities that are minions

Tracked statistics:
- `CardsPlayedThisMatch`, `CardsPlayedThisTurn`, `CardsPlayedLastTurn`
- `SpellsPlayedCards`, `SpellsPlayedInFriendlyCharacters`, `SpellsPlayedInOpponentCharacters`
- `DeadMinionsCards`, `SecretsTriggeredCards`
- `Fatigue`, `MaxHealth`, `MaxMana`
- `HasCoin`, `HandCount`, `DeckCount`

## Entity

**File:** `Hearthstone Deck Tracker/Hearthstone/Entities/Entity.cs`

Represents any in-game object: minion, spell, hero, weapon, or the game entity itself.

Key properties:
- `Id` — integer entity ID assigned by the game engine
- `CardId` — Hearthstone card ID string (e.g. `"AT_001"`)
- `Tags` — `Dictionary<GameTag, int>` with all tag values
- `Info` — `EntityInfo` metadata (controller, zone at game start, created/stolen/graveyard flags)
- `Card` — lazy-loaded `HearthDb.Card` reference

Zone predicates: `IsInHand`, `IsInDeck`, `IsInPlay`, `IsInGraveyard`, `IsInSecret`, `IsInSetAside`  
Type predicates: `IsMinion`, `IsSpell`, `IsWeapon`, `IsHero`, `IsHeroPower`  
`Clone()` — creates a shallow copy for board snapshots in `BattlegroundsBoardState`

Reading tags: `GetTag(GameTag tag)` returns int, `HasTag(GameTag tag)` checks non-zero value.

## Card

**File:** `Hearthstone Deck Tracker/Hearthstone/Card.cs`

A card definition instance used in deck lists and UI display. Bridges the live `Entity` system with `HearthDb` static card data.

- Delegates `Cost`, `Attack`, `Health`, `Mechanics`, `Rarity`, `CardType` to `HearthDb.Card`
- `Count` — how many copies exist in context (deck, hand, etc.)
- `IsCreated`, `IsCreatedByOpponent` — provenance flags for deck tracking
- `HighlightInHand` — UI hint for highlighting matching cards
- `ExtraInfo` (ICardExtraInfo) — mode-specific extra data (e.g. dormant status)

## Deck

**File:** `Hearthstone Deck Tracker/Hearthstone/Deck.cs`

Complete deck representation used by `DeckList` and deck management UI.

- `Cards` — `ObservableCollection<Card>` of 30 cards
- `Sideboards`, `MissingCards` — sideboard and missing card tracking
- `Class`, `Name`, `LastEdited`, `Tags`, `Notes`, `Url`
- Multiple `DeckVersion` history entries
- `IsArenaDeck`, `IsDungeonDeck`, `IsDuelsDeck` — mode detection
- `DeckStats` cached via `DeckList` for win/loss statistics

## DeckList

**File:** `Hearthstone Deck Tracker/DeckList.cs`

Singleton. Manages the full list of saved user decks. Persists to XML. Exposes `ActiveDeck` and `ActiveDeckChanged` event.

## Database

**File:** `Hearthstone Deck Tracker/Hearthstone/Database.cs`

Static helper. `GetCardFromId(string cardId)` looks up `HearthDb.Database` and returns a `Card` with stat data. Used throughout to convert entity card IDs into displayable card objects.
