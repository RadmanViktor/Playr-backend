# User Avatar Badges — Design

## Purpose

Introduce a milestone/achievement ("badge") system for users. Unlocking a badge lets a
user display a special ring/border around their avatar. Backend is responsible only for
tracking, unlocking, and exposing badge data; visual rendering of the ring (colors, glow,
animation, SVG assets) is owned entirely by the frontend (`Playr-Frontend`).

## Scope (v1)

Five badge types, each computed from existing data:

| Badge type | Stat counted | Bronze | Silver | Gold |
|---|---|---|---|---|
| `FirstHundredUsers` | signup rank by `CreatedAt` | — | — | rank ≤ 100 (single tier, awarded as `Gold`) |
| `Poster` | number of posts authored | 25 | 100 | 250 |
| `GameCritic` | number of library entries with a rating set | 5 | 15 | 50 |
| `Commentator` | number of comments authored on **other users'** posts | 50 | 200 | 500 |
| `Inviter` | number of invitations sent that reached `Accepted` status | 3 | 10 | 25 |

Out of scope for v1: admin-configurable badge definitions/thresholds, badge revocation,
historical per-tier unlock records (only current tier per type is stored), any visual
design/asset work (frontend concern).

## Domain Model

New folder: `src/Playr.Domain/Badges/`

- **`BadgeType`** (enum): `FirstHundredUsers`, `Poster`, `GameCritic`, `Commentator`, `Inviter`
- **`BadgeLevel`** (enum): `None = 0`, `Bronze = 1`, `Silver = 2`, `Gold = 3`
- **`UserBadge`** (entity): `Id` (Guid), `UserId` (Guid, FK), `Type` (`BadgeType`), `Level` (`BadgeLevel`), `UnlockedAt` (DateTimeOffset, timestamp of most recent tier-up). One row per `(UserId, Type)` — upgrading a tier updates the existing row rather than inserting a new one. Unique index on `(UserId, Type)`.

`UserProfile` (`Domain/Profiles/UserProfile.cs`) gains two new nullable fields:
- `ActiveBadgeType` (`BadgeType?`)
- `ActiveBadgeLevel` (`BadgeLevel?`)

These are denormalized copies of the user's currently-chosen badge, kept in sync by the
badge service, so existing profile/user DTOs can expose the active badge without an extra
join or query.

Thresholds live in a single `BadgeThresholds` static class in `Playr.Application/Badges/`,
not in the database — hardcoded per the "fixed rule set" decision for v1.

## Unlock Flow (real-time)

New service: `IBadgeService` in `Playr.Application/Badges/`, implemented in
`Playr.Infrastructure/Badges/` (or Application if no external deps needed — implementation
plan to decide based on DbContext access patterns used elsewhere in the codebase).

Core method: `CheckAndUnlockBadgesAsync(Guid userId, BadgeType type, CancellationToken ct)`

Algorithm:
1. Compute the current stat for `(userId, type)` via a `COUNT` query against the relevant
   table (Posts, PostComments joined/filtered against Post.AuthorId, UserGameLibraryEntry
   where Rating != null, or accepted Invitations sent).
2. Load or create the `UserBadge` row for `(userId, type)` (default `Level = None`).
3. Compare computed stat against `BadgeThresholds` for `type` to determine the new level.
4. If new level > current level:
   - Update `Level` and `UnlockedAt`, save.
   - Send a notification (`NotificationType.BadgeUnlocked`) via the existing
     `INotificationFeedService` / `INotificationNotifier` pipeline.
   - If the user's `UserProfile.ActiveBadgeType` is currently `null` (no badge chosen yet),
     auto-set `ActiveBadgeType`/`ActiveBadgeLevel` to this newly unlocked badge. Otherwise,
     leave the user's existing choice untouched.

Call sites (added to existing services, invoked after the triggering action is persisted):
- Post creation service → `CheckAndUnlockBadgesAsync(authorId, Poster)`
- Comment creation service → only when `comment.AuthorId != post.AuthorId` →
  `CheckAndUnlockBadgesAsync(commentAuthorId, Commentator)`
- Game library rating-set service → `CheckAndUnlockBadgesAsync(userId, GameCritic)`
- Invitation status-change service → only on transition to `Accepted` →
  `CheckAndUnlockBadgesAsync(senderUserId, Inviter)`
- User registration / profile creation flow → one-time rank check against `CreatedAt` order
  → if rank ≤ 100, unlock `FirstHundredUsers` directly (no threshold table lookup needed)

No background jobs, no message queue — synchronous calls after each relevant `SaveChanges`,
matching the "real-time" decision.

## Notifications

- New `NotificationType.BadgeUnlocked` value added to
  `Domain/Notifications/NotificationType.cs`.
- `Notification` entity needs to carry which badge was unlocked. Add two new nullable
  columns to `Domain/Notifications/Notification.cs`: `BadgeType` (`BadgeType?`) and
  `BadgeLevel` (`BadgeLevel?`) — mirrors how existing notification types carry their own
  optional reference ids (`PostId`, `CommentId`).
- Routed through the existing `INotificationFeedService` (persist) and
  `INotificationNotifier` (SignalR push) exactly like other notification types — no new
  transport mechanism.

## API Surface

New controller: `Playr.Api/Badges/BadgesController.cs`

- `GET /api/badges/me` — returns all `UserBadge` rows (type, level, unlockedAt) for the
  authenticated user, plus which one is currently active.
- `GET /api/badges/user/{userId}` — same shape, public view of another user's unlocked
  badges (for profile pages).
- `PUT /api/badges/active` — body `{ badgeType: BadgeType | null }`. Sets
  `ActiveBadgeType`/`ActiveBadgeLevel` on the caller's own `UserProfile`. Validation: the
  requested `badgeType` must correspond to a `UserBadge` row with `Level > None` owned by
  the caller (403/400 otherwise). Passing `null` clears the active badge (no ring shown).

### Existing DTO changes

Every existing DTO that already represents "a user/profile summary" (used in posts,
comments, search results, friend lists, etc.) gains two new nullable fields:
`activeBadgeType`, `activeBadgeLevel`, sourced directly from `UserProfile`. The
implementation plan must enumerate every such DTO/mapping location in the codebase (e.g.
wherever `UserProfile` is currently projected into a summary DTO) and update them
consistently — likely via a shared mapper/extension method if one exists, otherwise at each
call site.

## Error Handling

- Unlock checks are best-effort side effects: if `CheckAndUnlockBadgesAsync` throws, it must
  not roll back or fail the primary action (post/comment/rating/invite). Wrap calls in
  try/catch with logging in each call site, or make the badge check swallow-and-log
  internally.
- `PUT /api/badges/active` returns `400 Bad Request` if the requested badge is not unlocked
  for the caller, `401` if unauthenticated.

## Testing

- Unit tests (`Playr.Application.Tests`) for `BadgeService`: threshold boundaries (just
  below/at/above each tier), no-downgrade behavior, auto-activation-on-first-unlock,
  no-duplicate-notification-on-same-tier re-check.
- Integration tests (`Playr.IntegrationTests`) covering: creating posts up to a threshold
  unlocks `Poster` at the right tier and creates a notification; `FirstHundredUsers` unlock
  on registration for the 100th vs 101st user; `PUT /api/badges/active` validation (cannot
  set a non-unlocked badge); DTOs on a sample endpoint (e.g. posts feed) include the active
  badge fields for the post's author.

## Migration

One EF Core migration adding: `UserBadges` table, `UserProfile.ActiveBadgeType` /
`ActiveBadgeLevel` columns, `Notification.BadgeType` / `BadgeLevel` columns.
