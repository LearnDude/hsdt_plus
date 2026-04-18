# HSReplay.net Integration

HSReplay.net is the cloud service for replays, deck statistics, and premium features (mulligan guides, Tier 7 Battlegrounds analysis).

## Log Upload

**File:** `Hearthstone Deck Tracker/HsReplay/LogUploader.cs`

At game end, `GameEventHandler` calls `LogUploader.Upload()`. This:
1. Reads `PowerLog` from `GameV2.PowerLog` (the raw log lines accumulated during the game).
2. Builds an `UploadMetaData` payload via `UploadMetaDataGenerator`.
3. POSTs to the HSReplay API using `ApiWrapper`.
4. On success, stores the returned replay GUID in `GameStats.HsReplayId`.

Upload is skipped if the user is not authenticated or if recording is disabled for the current mode.

## UploadMetaDataGenerator

**File:** `Hearthstone Deck Tracker/HsReplay/UploadMetaDataGenerator.cs`

Constructs the `UploadMetaData` object:
- Player/opponent BattleTag, hero, deck list, rank
- Game mode, format, game type
- `TwitchVodData` (channel + VOD URL if streaming)
- Match info from `HearthMirror` (BattleTag, ranked tier)

## ApiWrapper

**File:** `Hearthstone Deck Tracker/HsReplay/ApiWrapper.cs`

REST client for HSReplay.net API:
- `GetUploadToken()` — gets a one-time upload token
- `UploadLog(token, metadata, log)` — POSTs the game log
- `GetDecks()` / `GetDeckWinrates()` — fetches opponent deck archetypes
- `GetMulliganGuideData(params)` — fetches keep/toss recommendations
- `GetTier7HeroPickStats()` / `GetTier7QuestStats()` — Battlegrounds hero pick tier data

All requests use `HttpClient` with OAuth bearer token from `HSReplayNetOAuth`.

## HSReplayNetOAuth

**File:** `Hearthstone Deck Tracker/HsReplay/HSReplayNetOAuth.cs`

OAuth 2.0 flow (Authorization Code with PKCE):
1. `StartOAuthFlow()` — opens browser to HSReplay authorization URL with PKCE challenge.
2. Local HTTP listener catches the callback redirect.
3. `ExchangeCode(code, verifier)` — exchanges code for access + refresh tokens.
4. Tokens are stored encrypted in config.
5. `GetCurrentVideo(twitchUserId)` — queries Twitch API for current live VOD (used for VOD linking).

`TwitchUsers` — list of linked Twitch accounts.

## Feature Trials

Trial gating controls which premium features are accessible without a subscription:

| Class | File | Purpose |
|-------|------|---------|
| `ArenaTrial` | `HsReplay/ArenaTrial.cs` | Arena card quality overlay trial |
| `MulliganGuideTrial` | `HsReplay/MulliganGuideTrial.cs` | Mulligan keep/toss guide trial |
| `Tier7Trial` | `HsReplay/Tier7Trial.cs` | Battlegrounds Tier 7 hero/quest stats trial |

Each trial tracks free uses and shows an upsell after exhaustion.

## Account

**File:** `Hearthstone Deck Tracker/HsReplay/Account.cs`

Stores the authenticated user's HSReplay account data: username, is-premium flag, BattleTag list. Retrieved from `ApiWrapper.GetAccount()` after OAuth.

## Analytics

**File:** `Hearthstone Deck Tracker/Utility/Analytics/`

- `Influx.cs` — posts metrics (game count, feature usage) to an InfluxDB endpoint.
- `Sentry.cs` — error reporting via SharpRaven/Sentry. `CaptureBobsBuddyException()` reports simulation failures with the simulation input as context.
- `HSReplayNetClientAnalytics.cs` — tracks client-side events (feature seen, feature used) for HSReplay product analytics.
