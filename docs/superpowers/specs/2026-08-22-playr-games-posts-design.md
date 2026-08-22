# PLAYR — Games + Posts (Create + Feed) Design

## Context

PLAYR has a .NET 8 Clean-Architecture backend (Api / Application / Domain /
Infrastructure) with EF Core + PostgreSQL, JWT auth, and existing Auth +
Profiles vertical slices. The React frontend (sibling repo `../playr-frontend`,
React + Vite + TS + Tailwind v4) has the purple app shell with placeholder Feed
page and an inert "Create Post" sidebar button.

This spec adds the first content feature: users create posts about games ("Idag
klarade jag sista bossen i Hollow Knight!") and see them in a global feed.

## Decisions (locked via brainstorm)

- **Game reference:** full `Game` entity now; posts reference a `GameId`.
- **Populating games:** seed a preset list via EF migration. No user-facing game
  creation, no admin UI.
- **Mood:** optional (`Enjoying | Frustrated | Completed | NeedHelp` or none).
- **Viewing posts:** a real global Feed page lists all posts newest-first.
- **Create UI:** a dedicated `/create-post` page; sidebar "Create Post" button
  navigates there; on success redirect to `/feed`.

## Non-goals (deferred)

- Image upload (post is text + game + optional mood only for now).
- Editing / deleting posts.
- Likes / comments / reposts.
- Pagination / infinite scroll (feed loads latest 50).
- Per-profile post lists.
- Threads, find-players.

## Domain model

### `Game` — `Playr.Domain/Games/Game.cs`
- `Id` : Guid (PK)
- `Name` : string, required, ≤ 128
- `CoverImageUrl` : string?, ≤ 500
- `Genre` : string?, ≤ 64

### `Post` — `Playr.Domain/Posts/Post.cs`
- `Id` : Guid (PK)
- `AuthorId` : Guid — FK → `ApplicationUser`
- `Author` : ApplicationUser (nav)
- `GameId` : Guid — FK → `Game`
- `Game` : Game (nav)
- `TextContent` : string, required, ≤ 1000
- `Mood` : `PostMood?` (nullable enum)
- `CreatedAt` : DateTimeOffset (UTC)

### `PostMood` enum — `Playr.Domain/Posts/PostMood.cs`
`Enjoying, Frustrated, Completed, NeedHelp`. Persisted **as string** in the DB
(readable; matches the frontend `Badge` variants `enjoying/need-help/...`).

### EF configuration (`PlayrDbContext`)
- `DbSet<Game> Games`, `DbSet<Post> Posts`.
- `Game`: key `Id`, `Name` required maxlen 128, `CoverImageUrl` maxlen 500,
  `Genre` maxlen 64.
- `Post`: key `Id`; `TextContent` required maxlen 1000; `Mood` stored via
  `.HasConversion<string>()` maxlen 16; `Author` relationship
  `OnDelete(Cascade)` (matches Profile→User); `Game` relationship
  `OnDelete(Restrict)`; index on `CreatedAt` (feed ordering).
- Migration seeds preset games with fixed Guids (deterministic `HasData`):
  Hollow Knight, Counter-Strike 2, Elden Ring, Valorant, Apex Legends, Genshin
  Impact, Destiny 2, Call of Duty. `CoverImageUrl`/`Genre` may be null for now.

## Backend structure (mirrors the Profiles slice)

### Games slice
- `Application/Games/GameDto.cs` — record `(Guid Id, string Name,
  string? CoverImageUrl, string? Genre)`.
- `Application/Games/IGameService.cs` — `Task<IReadOnlyList<GameDto>>
  GetAllAsync(CancellationToken)`.
- `Infrastructure/Games/GameService.cs` — returns all games ordered by `Name`,
  `AsNoTracking`.
- `Api/Models/Games/GameResponse.cs` — record mirroring `GameDto`.
- `Api/Controllers/GamesController.cs` — `GET /api/games` (public) → list of
  `GameResponse`.

### Posts slice
- `Application/Posts/PostDto.cs` — record with post + denormalized author &
  game display fields:
  `(Guid Id, Guid AuthorId, string AuthorUsername, string AuthorDisplayName,
   string? AuthorAvatarUrl, Guid GameId, string GameName,
   string? GameCoverImageUrl, string TextContent, string? Mood,
   DateTimeOffset CreatedAt)`.
  (`Mood` is the enum's string name or null.)
- `Application/Posts/CreatePostCommand.cs` — record
  `(Guid GameId, string TextContent, string? Mood)`.
- `Application/Posts/IPostService.cs`:
  - `Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command,
    CancellationToken)`
  - `Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken)`
- `Infrastructure/Posts/PostService.cs`:
  - `CreateAsync`: trim + validate text (required, ≤ 1000); parse/validate mood
    (must be null or a defined `PostMood` name, case-insensitive) else
    `InvalidOperationException`; verify `GameId` exists else
    `InvalidOperationException("Game was not found.")`; set `CreatedAt` = UtcNow;
    save; return `PostDto` (join author profile + game for display fields).
  - `GetFeedAsync`: latest 50 posts ordered by `CreatedAt` desc, `AsNoTracking`,
    joined with author profile (username/displayName/avatarUrl) and game
    (name/coverImageUrl) into `PostDto`.
  - Author display fields come from `UserProfile` (username, displayName,
    avatarUrl) matched on `AuthorId == UserProfile.UserId`.
- `Api/Models/Posts/CreatePostRequest.cs` — `(Guid GameId, string TextContent,
  string? Mood)`.
- `Api/Models/Posts/PostResponse.cs` — mirrors `PostDto`.
- `Api/Controllers/PostsController.cs`:
  - `POST /api/posts` `[Authorize]` — body `CreatePostRequest`; resolves author
    from `User.TryGetUserId`; returns `201 Created` with `PostResponse`;
    `InvalidOperationException` → `400 { error }`; missing user id → `401`.
  - `GET /api/posts` (public) — feed list of `PostResponse`.

### DI + Program
- Register `IGameService`→`GameService`, `IPostService`→`PostService` in
  `Infrastructure/DependencyInjection.cs`.
- No CORS change: `Program.cs` already allows `GET`/`POST`; both endpoints use
  those methods.

### Validation & errors
Follow the existing convention: services throw `InvalidOperationException` with
a user-safe message; controllers translate to `400 BadRequest(new { error =
ex.Message })`. All error bodies are `{ "error": "..." }`.

## Frontend structure (`../playr-frontend`)

### API layer
- Refactor: extract `ApiError`, `parseErrorMessage`, and `API_BASE_URL` into a
  shared `src/api/http.ts`; re-export `ApiError`/`API_BASE_URL` from `authApi.ts`
  so existing imports keep working (no behavior change).
- `src/api/gamesApi.ts` — `interface Game { id; name; coverImageUrl; genre }`;
  `getGames(): Promise<Game[]>` → `GET /api/games`.
- `src/api/postsApi.ts`:
  - `type Mood = 'Enjoying' | 'Frustrated' | 'Completed' | 'NeedHelp'`
  - `interface PostFeedItem { id, authorUsername, authorDisplayName,
    authorAvatarUrl, gameId, gameName, gameCoverImageUrl, textContent,
    mood (string|null), createdAt }`
  - `createPost(token, { gameId, textContent, mood? }): Promise<PostFeedItem>`
    → `POST /api/posts` with bearer token
  - `getFeed(): Promise<PostFeedItem[]>` → `GET /api/posts`

### Components / pages
- `components/PostCard.tsx` — presentational: author `Avatar` (uses
  `authorAvatarUrl`/fallback) + display name + `@username`, game name, mood
  `Badge` (map `Enjoying→enjoying`, `NeedHelp→need-help`,
  `Frustrated→frustrated`, `Completed→completed`), text content, relative
  timestamp (simple "Xh ago" helper). Props: `post: PostFeedItem`.
- `pages/CreatePostPage.tsx` (route `/create-post`, protected, inside AppShell):
  - loads games via `getGames` on mount (loading + error states)
  - game `<select>` (required), optional mood picker (segmented buttons styled
    with Badge/Button; includes a "None" option), `<textarea>` for text with a
    live char counter (max 1000, client-side required check)
  - submit calls `createPost` with the auth token from `useAuth`; on success →
    `navigate('/feed')`; on error → shows message (reuse `ApiError`)
- `pages/FeedPage.tsx` (replace placeholder): loads `getFeed` on mount; loading,
  error, and empty ("No posts yet — be the first to share!") states; renders a
  vertical list of `PostCard`. A "Create Post" `Button` linking to
  `/create-post` at the top.
- Routing (`App.tsx`): add `/create-post` route inside the protected `AppShell`
  layout.
- `Sidebar.tsx`: wire the existing "Create Post" button to navigate to
  `/create-post` (currently inert) — use a `NavLink`/`useNavigate`.

### Auth token access
`useAuth()` exposes `token`. `createPost` takes the token explicitly (same style
as `getMe(token)`).

## Testing

### Backend (xUnit, mirrors `ProfileServiceTests`)
- `GameServiceTests`: returns seeded games ordered by name (in-memory or sqlite
  provider as existing tests use).
- `PostServiceTests`: create success; empty text rejected; text > 1000 rejected;
  unknown game rejected; invalid mood rejected; null mood allowed; feed returns
  newest-first with author/game display fields.
- Endpoint-configuration tests mirroring existing `*EndpointConfigurationTests`
  for `POST /api/posts` (requires auth) and `GET /api/posts` / `GET /api/games`
  (public).

### Frontend (Vitest + RTL)
- `gamesApi` / `postsApi`: fetch wrappers succeed + throw `ApiError` on non-OK
  (mock `fetch`).
- `PostCard`: renders author, game, text, and mood badge; handles null mood /
  null avatar.
- `CreatePostPage`: loads games into the select; blocks submit on empty text;
  calls `createPost` and navigates on success; shows error on failure (mock the
  api module + `useAuth`).
- `FeedPage`: renders posts from `getFeed`; shows empty state when none.

## Open questions

None outstanding.
