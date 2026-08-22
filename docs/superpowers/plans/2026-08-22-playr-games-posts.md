# PLAYR Games + Posts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Game` entity (seeded preset list) and a `Post` entity (author + game + text + optional mood), with REST endpoints to list games, create posts, and fetch a global feed — plus a React Create Post page and a real Feed page.

**Architecture:** Backend follows the existing Clean-Architecture slice pattern (Domain → Application → Infrastructure → Api); each new entity mirrors the Profiles slice exactly. Frontend adds typed API wrappers, a `PostCard` component, a `CreatePostPage`, and replaces the Feed placeholder. Both repos get feature branches and are merged locally when done.

**Tech Stack:** .NET 10 / ASP.NET Core, EF Core 10 + Npgsql, SQLite (tests), xUnit + FluentAssertions; React 18 + TS, Vite, Tailwind v4, Vitest + RTL, lucide-react.

## Global Constraints

- Backend repo: `C:\NoBackup\development\Playr`, branch `feature/games-posts` (already created).
- Frontend repo: `C:\NoBackup\development\playr-frontend`, create branch `feature/games-posts` before Task 9.
- Windows / PowerShell 5.1 in all shell steps: no `&&`, use `;` or `if ($?) { }`.
- Run backend tests from the solution root: `dotnet test` (runs all projects). Run a specific project: `dotnet test tests\Playr.Application.Tests` or `dotnet test tests\Playr.IntegrationTests`.
- Backend test frameworks: xUnit with implicit `using Xunit;`, FluentAssertions 8, SQLite in-memory via `Microsoft.Data.Sqlite`.
- Every backend test fixture uses the pattern: `SqliteConnection("Data Source=:memory:")` → open → `DbContextOptionsBuilder.UseSqlite` → `new PlayrDbContext(options)` → `EnsureCreatedAsync`. **`EnsureCreatedAsync` does NOT run seed data from `HasData`**; seed games directly in the fixture instead.
- Every new test file in the frontend must import vitest globals explicitly: `import { describe, it, expect, vi, beforeEach } from 'vitest'`.
- Frontend run tests: `npm test` (all) or `npm test -- <pattern>`. Run build: `npm run build`.
- Tailwind v4 token names in use: `bg-bg`, `bg-surface`, `bg-surface-raised`, `border-border`, `text-text`, `text-muted`, `text-primary`, `text-frustrated`, `bg-enjoying`, `bg-need-help`, `text-enjoying`, `text-need-help`, `text-frustrated`, `text-completed`. All defined in `src/index.css @theme`.
- `ApiError` and `API_BASE_URL` are currently exported from `src/api/authApi.ts`. The spec calls for a shared `src/api/http.ts` refactor but this must not break any existing imports.
- Commit message convention: `feat:`, `fix:`, `chore:`, `test:` prefixes.
- `dotnet ef` migration command (run from solution root): `dotnet ef migrations add <Name> --project src\Playr.Infrastructure --startup-project src\Playr.Api`
- `dotnet ef database update` command: `dotnet ef database update --project src\Playr.Infrastructure --startup-project src\Playr.Api`

---

### Task 1: Domain entities — Game, PostMood, Post

**Files:**
- Create: `src/Playr.Domain/Games/Game.cs`
- Create: `src/Playr.Domain/Posts/PostMood.cs`
- Create: `src/Playr.Domain/Posts/Post.cs`

**Interfaces:**
- Consumes: `Playr.Domain.Identity.ApplicationUser` (existing).
- Produces:
  - `Game`: `Guid Id`, `string Name`, `string? CoverImageUrl`, `string? Genre`
  - `PostMood` enum: `Enjoying, Frustrated, Completed, NeedHelp`
  - `Post`: `Guid Id`, `Guid AuthorId`, `ApplicationUser Author`, `Guid GameId`, `Game Game`, `string TextContent`, `PostMood? Mood`, `DateTimeOffset CreatedAt`

- [ ] **Step 1: Write a compile-only test to force the types to exist**

In `tests/Playr.Application.Tests/` create `Games/GameEntityTests.cs`:
```csharp
using Playr.Domain.Games;
using Playr.Domain.Posts;

namespace Playr.Application.Tests.Games;

public class GameEntityTests
{
    [Fact]
    public void Game_has_expected_properties()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "Hollow Knight" };
        game.Name.Should().Be("Hollow Knight");
        game.CoverImageUrl.Should().BeNull();
        game.Genre.Should().BeNull();
    }

    [Fact]
    public void Post_has_expected_properties()
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "Cleared the Hollow Knight!",
            Mood = PostMood.Enjoying,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        post.TextContent.Should().NotBeNullOrEmpty();
        post.Mood.Should().Be(PostMood.Enjoying);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (compile error, types not found)**

```
dotnet test tests\Playr.Application.Tests
```
Expected: build error — `Game`, `Post`, `PostMood` not found.

- [ ] **Step 3: Create the domain types**

`src/Playr.Domain/Games/Game.cs`:
```csharp
namespace Playr.Domain.Games;

public sealed class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Genre { get; set; }
}
```

`src/Playr.Domain/Posts/PostMood.cs`:
```csharp
namespace Playr.Domain.Posts;

public enum PostMood
{
    Enjoying,
    Frustrated,
    Completed,
    NeedHelp,
}
```

`src/Playr.Domain/Posts/Post.cs`:
```csharp
using Playr.Domain.Identity;
using Playr.Domain.Games;

namespace Playr.Domain.Posts;

public sealed class Post
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string TextContent { get; set; } = string.Empty;
    public PostMood? Mood { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
dotnet test tests\Playr.Application.Tests
```
Expected: 2 new tests pass; all existing tests still pass.

- [ ] **Step 5: Commit**

```
git add src\Playr.Domain\Games\Game.cs src\Playr.Domain\Posts\PostMood.cs src\Playr.Domain\Posts\Post.cs tests\Playr.Application.Tests\Games\GameEntityTests.cs
git commit -m "feat: add Game, Post, PostMood domain entities"
```

---

### Task 2: EF Core configuration + migration with seeded games

**Files:**
- Modify: `src/Playr.Infrastructure/Data/PlayrDbContext.cs`
- New migration (generated by `dotnet ef`): `src/Playr.Infrastructure/Migrations/<timestamp>_AddGamesAndPosts.cs` + `.Designer.cs` + updated `PlayrDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `Game` (Task 1), `Post` (Task 1), `PostMood` (Task 1).
- Produces: `PlayrDbContext` with `DbSet<Game> Games` and `DbSet<Post> Posts`; schema constraints and seed data; migration named `AddGamesAndPosts`.

- [ ] **Step 1: Write a failing test**

In `tests/Playr.IntegrationTests/InfrastructureConfigurationTests.cs`, add at the end of the class (before the closing `}`):
```csharp
[Fact]
public void PlayrDbContext_configures_game_and_post_mapping()
{
    var options = new DbContextOptionsBuilder<PlayrDbContext>()
        .UseNpgsql("Host=localhost;Database=playr;Username=playr;Password=playr_dev_password")
        .Options;

    using var context = new PlayrDbContext(options);

    var gameType = context.Model.FindEntityType(typeof(Game));
    gameType.Should().NotBeNull();
    gameType!.FindPrimaryKey()!.Properties.Should().ContainSingle(p => p.Name == nameof(Game.Id));
    gameType.FindProperty(nameof(Game.Name))!.GetMaxLength().Should().Be(128);
    gameType.FindProperty(nameof(Game.CoverImageUrl))!.GetMaxLength().Should().Be(500);
    gameType.FindProperty(nameof(Game.Genre))!.GetMaxLength().Should().Be(64);

    var postType = context.Model.FindEntityType(typeof(Post));
    postType.Should().NotBeNull();
    postType!.FindPrimaryKey()!.Properties.Should().ContainSingle(p => p.Name == nameof(Post.Id));
    postType.FindProperty(nameof(Post.TextContent))!.GetMaxLength().Should().Be(1000);
    postType.FindProperty(nameof(Post.Mood))!.GetMaxLength().Should().Be(16);

    var authorFk = postType.GetForeignKeys()
        .Should().Contain(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser))
        .Subject;
    authorFk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);

    var gameFk = postType.GetForeignKeys()
        .Should().Contain(fk => fk.PrincipalEntityType.ClrType == typeof(Game))
        .Subject;
    gameFk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
}
```

Also add the required usings at the top of the file (if not already present):
```csharp
using Playr.Domain.Games;
using Playr.Domain.Posts;
```

- [ ] **Step 2: Run the test — expect FAIL**

```
dotnet test tests\Playr.IntegrationTests --filter PlayrDbContext_configures_game_and_post_mapping
```
Expected: FAIL — `Game` / `Post` not in model.

- [ ] **Step 3: Update PlayrDbContext**

Add `DbSet` properties and entity configuration to `src/Playr.Infrastructure/Data/PlayrDbContext.cs`. Replace the entire file:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using System.Text.Json;

namespace Playr.Infrastructure.Data;

public sealed class PlayrDbContext(DbContextOptions<PlayrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(user => user.Profile)
            .WithOne(profile => profile.User)
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserProfile>(profile =>
        {
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.Username).HasMaxLength(32).IsRequired();
            profile.HasIndex(p => p.Username).IsUnique();
            profile.Property(p => p.DisplayName).HasMaxLength(64).IsRequired();
            profile.Property(p => p.Bio).HasMaxLength(500);
            profile.Property(p => p.AvatarUrl).HasMaxLength(500);
            profile.Property(p => p.Region).HasMaxLength(64);
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                profile.Property(p => p.Languages).HasColumnType("jsonb");
                profile.Property(p => p.Platforms).HasColumnType("jsonb");
                profile.Property(p => p.ExternalLinks).HasColumnType("jsonb");
                profile.Property(p => p.CurrentlyPlayingGames).HasColumnType("jsonb");
            }
            else
            {
                profile.Property(p => p.Languages).HasJsonConversion();
                profile.Property(p => p.Platforms).HasJsonConversion();
                profile.Property(p => p.ExternalLinks).HasJsonConversion();
                profile.Property(p => p.CurrentlyPlayingGames).HasJsonConversion();
            }
        });

        builder.Entity<Game>(game =>
        {
            game.HasKey(g => g.Id);
            game.Property(g => g.Name).HasMaxLength(128).IsRequired();
            game.Property(g => g.CoverImageUrl).HasMaxLength(500);
            game.Property(g => g.Genre).HasMaxLength(64);
            game.HasData(
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000001"), Name = "Apex Legends" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000002"), Name = "Call of Duty" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000003"), Name = "Counter-Strike 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000004"), Name = "Destiny 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000005"), Name = "Elden Ring" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000006"), Name = "Genshin Impact" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000007"), Name = "Hollow Knight" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000008"), Name = "Valorant" }
            );
        });

        builder.Entity<Post>(post =>
        {
            post.HasKey(p => p.Id);
            post.Property(p => p.TextContent).HasMaxLength(1000).IsRequired();
            post.Property(p => p.Mood)
                .HasConversion<string>()
                .HasMaxLength(16);
            post.HasOne(p => p.Author)
                .WithMany()
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
            post.HasOne(p => p.Game)
                .WithMany()
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Restrict);
            post.HasIndex(p => p.CreatedAt);
        });
    }
}

file static class JsonPropertyBuilderExtensions
{
    public static void HasJsonConversion<T>(this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> propertyBuilder)
        where T : class, new()
    {
        propertyBuilder.HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<T>(value, (JsonSerializerOptions?)null) ?? new T());
        propertyBuilder.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new T()));
    }
}
```

- [ ] **Step 4: Run the test — expect PASS**

```
dotnet test tests\Playr.IntegrationTests --filter PlayrDbContext_configures_game_and_post_mapping
```
Expected: PASS. Run the full suite to confirm no regressions: `dotnet test`.

- [ ] **Step 5: Generate the migration**

```
dotnet ef migrations add AddGamesAndPosts --project src\Playr.Infrastructure --startup-project src\Playr.Api
```
Expected: three new files created under `src/Playr.Infrastructure/Migrations/`.

- [ ] **Step 6: Apply the migration to the local dev DB**

```
dotnet ef database update --project src\Playr.Infrastructure --startup-project src\Playr.Api
```
Expected: `Done.` — tables `Games` and `Posts` created, 8 seed rows in `Games`.

- [ ] **Step 7: Commit**

```
git add src\Playr.Infrastructure\Data\PlayrDbContext.cs src\Playr.Infrastructure\Migrations\ tests\Playr.IntegrationTests\InfrastructureConfigurationTests.cs
git commit -m "feat: configure Game and Post EF mappings, seed preset games, add migration"
```

---

### Task 3: Application layer — GameDto, IGameService, PostDto, CreatePostCommand, IPostService

**Files:**
- Create: `src/Playr.Application/Games/GameDto.cs`
- Create: `src/Playr.Application/Games/IGameService.cs`
- Create: `src/Playr.Application/Posts/PostDto.cs`
- Create: `src/Playr.Application/Posts/CreatePostCommand.cs`
- Create: `src/Playr.Application/Posts/IPostService.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks at compile time (pure contracts).
- Produces:
  - `GameDto`: record `(Guid Id, string Name, string? CoverImageUrl, string? Genre)`
  - `IGameService`: `Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken)`
  - `PostDto`: record `(Guid Id, Guid AuthorId, string AuthorUsername, string AuthorDisplayName, string? AuthorAvatarUrl, Guid GameId, string GameName, string? GameCoverImageUrl, string TextContent, string? Mood, DateTimeOffset CreatedAt)`
  - `CreatePostCommand`: record `(Guid GameId, string TextContent, string? Mood)`
  - `IPostService`: `Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken)` and `Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken)`

This task has no runtime behaviour — it's pure contracts. No test needed beyond compile. Simply create the five files and verify the project builds.

- [ ] **Step 1: Create the files**

`src/Playr.Application/Games/GameDto.cs`:
```csharp
namespace Playr.Application.Games;

public sealed record GameDto(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);
```

`src/Playr.Application/Games/IGameService.cs`:
```csharp
namespace Playr.Application.Games;

public interface IGameService
{
    Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken);
}
```

`src/Playr.Application/Posts/PostDto.cs`:
```csharp
namespace Playr.Application.Posts;

public sealed record PostDto(
    Guid Id,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string TextContent,
    string? Mood,
    DateTimeOffset CreatedAt);
```

`src/Playr.Application/Posts/CreatePostCommand.cs`:
```csharp
namespace Playr.Application.Posts;

public sealed record CreatePostCommand(
    Guid GameId,
    string TextContent,
    string? Mood);
```

`src/Playr.Application/Posts/IPostService.cs`:
```csharp
namespace Playr.Application.Posts;

public interface IPostService
{
    Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Verify the project builds**

```
dotnet build src\Playr.Application
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src\Playr.Application\Games\GameDto.cs src\Playr.Application\Games\IGameService.cs src\Playr.Application\Posts\PostDto.cs src\Playr.Application\Posts\CreatePostCommand.cs src\Playr.Application\Posts\IPostService.cs
git commit -m "feat: add Game and Post application contracts (DTOs, commands, interfaces)"
```

---

### Task 4: Infrastructure — GameService + PostService

**Files:**
- Create: `src/Playr.Infrastructure/Games/GameService.cs`
- Create: `src/Playr.Infrastructure/Posts/PostService.cs`
- Modify: `src/Playr.Infrastructure/DependencyInjection.cs`
- Test: `tests/Playr.Application.Tests/Games/GameServiceTests.cs`
- Test: `tests/Playr.Application.Tests/Posts/PostServiceTests.cs`

**Interfaces:**
- Consumes: `IGameService`, `GameDto` (Task 3); `IPostService`, `PostDto`, `CreatePostCommand` (Task 3); `PlayrDbContext` (Task 2); `Game`, `Post`, `PostMood` (Task 1).
- Produces:
  - `GameService.GetAllAsync`: returns all `Game` rows ordered by `Name` asc, mapped to `GameDto`.
  - `PostService.CreateAsync(Guid authorId, CreatePostCommand, CancellationToken)`: validates text (required, ≤1000), validates mood string, verifies game exists, saves a new `Post`, returns `PostDto` with author display fields from `UserProfile`.
  - `PostService.GetFeedAsync`: returns latest 50 posts ordered by `CreatedAt` desc, joined with `UserProfile` and `Game`, as `PostDto` list.
  - Both registered in `DependencyInjection.cs`.

**Fixture note:** `EnsureCreatedAsync` does NOT apply seed data. In fixtures, add the 8 game rows directly via `dbContext.Games.Add(...)`.

- [ ] **Step 1: Write failing tests**

`tests/Playr.Application.Tests/Games/GameServiceTests.cs`:
```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Games;

namespace Playr.Application.Tests.Games;

public sealed class GameServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly GameService _service;

    public GameServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();
        _dbContext.Games.AddRange(
            new Game { Id = Guid.NewGuid(), Name = "Zelda" },
            new Game { Id = Guid.NewGuid(), Name = "Apex Legends" },
            new Game { Id = Guid.NewGuid(), Name = "Minecraft" });
        _dbContext.SaveChanges();
        _service = new GameService(_dbContext);
    }

    [Fact]
    public async Task GetAllAsync_returns_all_games_ordered_by_name()
    {
        var result = await _service.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(g => g.Name).Should().BeInAscendingOrder();
        result[0].Name.Should().Be("Apex Legends");
        result[1].Name.Should().Be("Minecraft");
        result[2].Name.Should().Be("Zelda");
    }

    [Fact]
    public async Task GetAllAsync_maps_dto_fields_correctly()
    {
        var result = await _service.GetAllAsync(CancellationToken.None);

        result.Should().AllSatisfy(g =>
        {
            g.Id.Should().NotBeEmpty();
            g.Name.Should().NotBeNullOrWhiteSpace();
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

`tests/Playr.Application.Tests/Posts/PostServiceTests.cs`:
```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Posts;

namespace Playr.Application.Tests.Posts;

public sealed class PostServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _authorId;
    private readonly Guid _gameId;

    public PostServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _authorId,
            Email = "player@example.com",
            UserName = "player",
            NormalizedEmail = "PLAYER@EXAMPLE.COM",
            NormalizedUserName = "PLAYER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _authorId,
            Username = "player",
            DisplayName = "Player One",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        _dbContext.SaveChanges();

        _service = new PostService(_dbContext);
    }

    [Fact]
    public async Task CreateAsync_with_valid_data_returns_post_dto()
    {
        var command = new CreatePostCommand(_gameId, "Cleared the boss!", null);
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.AuthorId.Should().Be(_authorId);
        result.AuthorUsername.Should().Be("player");
        result.AuthorDisplayName.Should().Be("Player One");
        result.GameId.Should().Be(_gameId);
        result.GameName.Should().Be("Hollow Knight");
        result.TextContent.Should().Be("Cleared the boss!");
        result.Mood.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_with_mood_sets_mood_string()
    {
        var command = new CreatePostCommand(_gameId, "So fun!", "Enjoying");
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Mood.Should().Be("Enjoying");
    }

    [Fact]
    public async Task CreateAsync_with_null_mood_sets_mood_null()
    {
        var command = new CreatePostCommand(_gameId, "Just playing.", null);
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Mood.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_with_empty_text_throws()
    {
        var command = new CreatePostCommand(_gameId, "   ", null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text is required.");
    }

    [Fact]
    public async Task CreateAsync_with_text_too_long_throws()
    {
        var command = new CreatePostCommand(_gameId, new string('a', 1001), null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text cannot be longer than 1000 characters.");
    }

    [Fact]
    public async Task CreateAsync_with_invalid_mood_throws()
    {
        var command = new CreatePostCommand(_gameId, "Hello!", "Raging");
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid mood value.");
    }

    [Fact]
    public async Task CreateAsync_with_unknown_game_throws()
    {
        var command = new CreatePostCommand(Guid.NewGuid(), "Hello!", null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Game was not found.");
    }

    [Fact]
    public async Task GetFeedAsync_returns_posts_newest_first()
    {
        var older = new CreatePostCommand(_gameId, "First post", null);
        var newer = new CreatePostCommand(_gameId, "Second post", null);
        await _service.CreateAsync(_authorId, older, CancellationToken.None);
        await Task.Delay(10);
        await _service.CreateAsync(_authorId, newer, CancellationToken.None);

        var feed = await _service.GetFeedAsync(CancellationToken.None);

        feed.Should().HaveCount(2);
        feed[0].TextContent.Should().Be("Second post");
        feed[1].TextContent.Should().Be("First post");
    }

    [Fact]
    public async Task GetFeedAsync_returns_at_most_50_posts()
    {
        for (var i = 0; i < 55; i++)
        {
            _dbContext.Posts.Add(new Post
            {
                Id = Guid.NewGuid(),
                AuthorId = _authorId,
                GameId = _gameId,
                TextContent = $"Post {i}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
            });
        }
        await _dbContext.SaveChangesAsync();

        var feed = await _service.GetFeedAsync(CancellationToken.None);

        feed.Should().HaveCount(50);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```
dotnet test tests\Playr.Application.Tests
```
Expected: build errors — `GameService`, `PostService` not found.

- [ ] **Step 3: Implement GameService**

`src/Playr.Infrastructure/Games/GameService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Games;

public sealed class GameService(PlayrDbContext dbContext) : IGameService
{
    public async Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Games
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GameDto(g.Id, g.Name, g.CoverImageUrl, g.Genre))
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Implement PostService**

`src/Playr.Infrastructure/Posts/PostService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Posts;

public sealed class PostService(PlayrDbContext dbContext) : IPostService
{
    private const int MaxTextLength = 1000;
    private const int FeedSize = 50;

    public async Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Post text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Post text cannot be longer than {MaxTextLength} characters.");

        PostMood? mood = null;
        if (command.Mood is not null)
        {
            if (!Enum.TryParse<PostMood>(command.Mood, ignoreCase: true, out var parsed))
                throw new InvalidOperationException("Invalid mood value.");
            mood = parsed;
        }

        var gameExists = await dbContext.Games.AnyAsync(g => g.Id == command.GameId, cancellationToken);
        if (!gameExists)
            throw new InvalidOperationException("Game was not found.");

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            GameId = command.GameId,
            TextContent = text,
            Mood = mood,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.Id == post.Id)
            .Join(dbContext.UserProfiles,
                p => p.AuthorId,
                up => up.UserId,
                (p, up) => new { Post = p, Profile = up })
            .Join(dbContext.Games,
                x => x.Post.GameId,
                g => g.Id,
                (x, g) => new PostDto(
                    x.Post.Id,
                    x.Post.AuthorId,
                    x.Profile.Username,
                    x.Profile.DisplayName,
                    x.Profile.AvatarUrl,
                    g.Id,
                    g.Name,
                    g.CoverImageUrl,
                    x.Post.TextContent,
                    x.Post.Mood == null ? null : x.Post.Mood.ToString(),
                    x.Post.CreatedAt))
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .Join(dbContext.UserProfiles,
                p => p.AuthorId,
                up => up.UserId,
                (p, up) => new { Post = p, Profile = up })
            .Join(dbContext.Games,
                x => x.Post.GameId,
                g => g.Id,
                (x, g) => new PostDto(
                    x.Post.Id,
                    x.Post.AuthorId,
                    x.Profile.Username,
                    x.Profile.DisplayName,
                    x.Profile.AvatarUrl,
                    g.Id,
                    g.Name,
                    g.CoverImageUrl,
                    x.Post.TextContent,
                    x.Post.Mood == null ? null : x.Post.Mood.ToString(),
                    x.Post.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Register services in DependencyInjection.cs**

In `src/Playr.Infrastructure/DependencyInjection.cs`, add these two lines after the existing `IProfileService` registration (line 42):
```csharp
        services.AddScoped<Playr.Application.Games.IGameService, Playr.Infrastructure.Games.GameService>();
        services.AddScoped<Playr.Application.Posts.IPostService, Playr.Infrastructure.Posts.PostService>();
```

- [ ] **Step 6: Run all tests — expect PASS**

```
dotnet test
```
Expected: all tests pass including the 10 new PostService tests and 2 GameService tests.

- [ ] **Step 7: Commit**

```
git add src\Playr.Infrastructure\Games\GameService.cs src\Playr.Infrastructure\Posts\PostService.cs src\Playr.Infrastructure\DependencyInjection.cs tests\Playr.Application.Tests\Games\GameServiceTests.cs tests\Playr.Application.Tests\Posts\PostServiceTests.cs
git commit -m "feat: implement GameService and PostService, register in DI"
```

---

### Task 5: API layer — GamesController, PostsController, request/response models

**Files:**
- Create: `src/Playr.Api/Models/Games/GameResponse.cs`
- Create: `src/Playr.Api/Models/Posts/CreatePostRequest.cs`
- Create: `src/Playr.Api/Models/Posts/PostResponse.cs`
- Create: `src/Playr.Api/Controllers/GamesController.cs`
- Create: `src/Playr.Api/Controllers/PostsController.cs`
- Test: `tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs`

**Interfaces:**
- Consumes: `IGameService` (Task 3), `IPostService` (Task 3), `GameDto` (Task 3), `PostDto` (Task 3), `TryGetUserId` from `ClaimsPrincipalExtensions` (existing).
- Produces:
  - `GET /api/games` (public) → `200 GameResponse[]`
  - `POST /api/posts` `[Authorize]` → `201 PostResponse` or `400 {error}` or `401 {error}`
  - `GET /api/posts` (public) → `200 PostResponse[]`

- [ ] **Step 1: Write the failing endpoint-configuration test**

`tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs`:
```csharp
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Api.Controllers;
using Playr.Application.Games;
using Playr.Application.Posts;
using Playr.Infrastructure;

namespace Playr.IntegrationTests;

public class GamesAndPostsEndpointConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_game_and_post_services()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=playr;Username=playr;Password=playr_dev_password"
            })
            .Build();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        provider.GetService<IGameService>().Should().NotBeNull();
        provider.GetService<IPostService>().Should().NotBeNull();
    }

    [Fact]
    public void Games_controller_has_correct_route_and_get_endpoint()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.GamesController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/games");
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpGetAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() == null);
    }

    [Fact]
    public void Posts_controller_has_correct_route_and_endpoints()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.PostsController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/posts");

        // GET /api/posts is public
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpGetAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() == null);

        // POST /api/posts requires auth
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpPostAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);
    }

    [Fact]
    public async Task CreatePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Create(
            new Playr.Api.Models.Posts.CreatePostRequest(Guid.NewGuid(), "Hello!", null),
            CancellationToken.None);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    private sealed class ThrowingPostService : IPostService
    {
        public Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```
dotnet test tests\Playr.IntegrationTests
```
Expected: build errors — `GamesController`, `PostsController`, `CreatePostRequest` not found.

- [ ] **Step 3: Create response/request models**

`src/Playr.Api/Models/Games/GameResponse.cs`:
```csharp
namespace Playr.Api.Models.Games;

public sealed record GameResponse(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);
```

`src/Playr.Api/Models/Posts/CreatePostRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Posts;

public sealed record CreatePostRequest(
    [Required] Guid GameId,
    [Required][StringLength(1000, MinimumLength = 1)] string TextContent,
    string? Mood);
```

`src/Playr.Api/Models/Posts/PostResponse.cs`:
```csharp
namespace Playr.Api.Models.Posts;

public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string TextContent,
    string? Mood,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 4: Create GamesController**

`src/Playr.Api/Controllers/GamesController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Models.Games;
using Playr.Application.Games;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController(IGameService gameService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var games = await gameService.GetAllAsync(cancellationToken);
        return Ok(games.Select(g => new GameResponse(g.Id, g.Name, g.CoverImageUrl, g.Genre)).ToList());
    }
}
```

- [ ] **Step 5: Create PostsController**

`src/Playr.Api/Controllers/PostsController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Posts;
using Playr.Application.Posts;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(IPostService postService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PostResponse>> Create(CreatePostRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var post = await postService.CreateAsync(userId,
                new CreatePostCommand(request.GameId, request.TextContent, request.Mood),
                cancellationToken);
            return CreatedAtAction(nameof(GetFeed), ToResponse(post));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetFeed(CancellationToken cancellationToken)
    {
        var posts = await postService.GetFeedAsync(cancellationToken);
        return Ok(posts.Select(ToResponse).ToList());
    }

    private static PostResponse ToResponse(PostDto post) => new(
        post.Id,
        post.AuthorId,
        post.AuthorUsername,
        post.AuthorDisplayName,
        post.AuthorAvatarUrl,
        post.GameId,
        post.GameName,
        post.GameCoverImageUrl,
        post.TextContent,
        post.Mood,
        post.CreatedAt);
}
```

- [ ] **Step 6: Run tests — expect PASS**

```
dotnet test
```
Expected: all tests pass including the 4 new endpoint-configuration tests.

- [ ] **Step 7: Smoke-test the running API**

The API should already be running from before (`http://localhost:5258`). If not, start it:
```
Start-Process dotnet -ArgumentList "run --project src\Playr.Api --launch-profile http" -WindowStyle Hidden
Start-Sleep -Seconds 5
```
Then verify:
```
Invoke-WebRequest -Uri "http://localhost:5258/api/games" -UseBasicParsing | Select-Object -ExpandProperty Content
```
Expected: JSON array with 8 game objects (Apex Legends, Call of Duty, Counter-Strike 2, Destiny 2, Elden Ring, Genshin Impact, Hollow Knight, Valorant), ordered alphabetically.

- [ ] **Step 8: Commit**

```
git add src\Playr.Api\Models\Games\GameResponse.cs src\Playr.Api\Models\Posts\CreatePostRequest.cs src\Playr.Api\Models\Posts\PostResponse.cs src\Playr.Api\Controllers\GamesController.cs src\Playr.Api\Controllers\PostsController.cs tests\Playr.IntegrationTests\GamesAndPostsEndpointConfigurationTests.cs
git commit -m "feat: add GamesController and PostsController with request/response models"
```

---

### Task 6: HTTP integration test — create + read feed through the full stack

**Files:**
- Modify: `tests/Playr.IntegrationTests/HttpAuthProfileFlowTests.cs` — look at `PlayrWebApplicationFactory` (used by the existing HTTP flow test); add a **new test class** in a **new file** instead.
- Create: `tests/Playr.IntegrationTests/HttpPostsFlowTests.cs`

**Interfaces:**
- Consumes: `PlayrWebApplicationFactory` (existing), `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/games`, `POST /api/posts`, `GET /api/posts`.
- Produces: end-to-end HTTP integration test verifying the full create-post → feed flow.

- [ ] **Step 1: Find PlayrWebApplicationFactory**

Read `tests/Playr.IntegrationTests/HttpAuthProfileFlowTests.cs` to find where `PlayrWebApplicationFactory` is defined (it may be at the bottom of that file or in a separate file). Note its exact class definition — you'll use it as-is.

- [ ] **Step 2: Write the failing test**

`tests/Playr.IntegrationTests/HttpPostsFlowTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Games;
using Playr.Api.Models.Posts;

namespace Playr.IntegrationTests;

public sealed class HttpPostsFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private readonly PlayrWebApplicationFactory _factory;

    public HttpPostsFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Can_register_login_get_games_create_post_and_read_feed()
    {
        using var client = _factory.CreateClient();

        // Register + login
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("poster@example.com", "poster", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("poster", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        // GET /api/games returns a non-empty list
        var gamesResponse = await client.GetAsync("/api/games");
        gamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        games.Should().NotBeNullOrEmpty();
        var gameId = games![0].Id;

        // POST /api/posts creates a post
        var createResponse = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(gameId, "Cleared the final boss!", "Enjoying"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();
        created.Should().NotBeNull();
        created!.TextContent.Should().Be("Cleared the final boss!");
        created.Mood.Should().Be("Enjoying");
        created.AuthorUsername.Should().Be("poster");

        // GET /api/posts returns the post in the feed
        var feedResponse = await client.GetAsync("/api/posts");
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feed = await feedResponse.Content.ReadFromJsonAsync<List<PostResponse>>();
        feed.Should().NotBeNullOrEmpty();
        feed!.Should().Contain(p => p.TextContent == "Cleared the final boss!");
    }

    [Fact]
    public async Task Create_post_without_auth_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(Guid.NewGuid(), "Hello!", null));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_feed_without_auth_returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_games_without_auth_returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/games");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 3: Run tests — expect PASS**

```
dotnet test tests\Playr.IntegrationTests
```
Expected: all tests pass including the 4 new HTTP flow tests.

- [ ] **Step 4: Commit**

```
git add tests\Playr.IntegrationTests\HttpPostsFlowTests.cs
git commit -m "test: add HTTP integration tests for games and posts endpoints"
```

---

### Task 7: Merge backend branch to main

**Files:** none new — git operations only.

- [ ] **Step 1: Run the full test suite one last time**

```
dotnet test
```
Expected: all tests pass.

- [ ] **Step 2: Merge to main**

```
git checkout main
git merge --no-ff feature/games-posts -m "Merge feature/games-posts: Game entity, Post entity, games + posts API"
```

- [ ] **Step 3: Verify tests still pass on main**

```
dotnet test
```
Expected: all pass.

- [ ] **Step 4: Delete the feature branch**

```
git branch -d feature/games-posts
```

---

### Task 8: Frontend — shared http.ts + gamesApi.ts + postsApi.ts

**Files:**
- Create: `src/api/http.ts`
- Modify: `src/api/authApi.ts` (re-export `ApiError`, `API_BASE_URL`, `parseErrorMessage` from `http.ts` — no breaking changes)
- Create: `src/api/gamesApi.ts`
- Create: `src/api/postsApi.ts`
- Test: `src/api/gamesApi.test.ts`
- Test: `src/api/postsApi.test.ts`

**Working directory:** `C:\NoBackup\development\playr-frontend`, branch `feature/games-posts` (create it first).

**Interfaces:**
- Produces:
  - `http.ts` exports: `API_BASE_URL`, `ApiError`, `parseErrorMessage`
  - `authApi.ts` still exports `ApiError`, `API_BASE_URL`, `getMe`, `login`, `register` (existing callers unbroken)
  - `gamesApi.ts` exports: `interface Game { id: string; name: string; coverImageUrl: string | null; genre: string | null }` and `getGames(): Promise<Game[]>`
  - `postsApi.ts` exports: `type Mood = 'Enjoying' | 'Frustrated' | 'Completed' | 'NeedHelp'`, `interface PostFeedItem { id: string; authorId: string; authorUsername: string; authorDisplayName: string; authorAvatarUrl: string | null; gameId: string; gameName: string; gameCoverImageUrl: string | null; textContent: string; mood: string | null; createdAt: string }`, `createPost(token: string, data: { gameId: string; textContent: string; mood?: string | null }): Promise<PostFeedItem>`, `getFeed(): Promise<PostFeedItem[]>`

- [ ] **Step 1: Create the feature branch**

```
cd C:\NoBackup\development\playr-frontend
git checkout -b feature/games-posts
```

- [ ] **Step 2: Write failing tests**

`src/api/gamesApi.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { getGames } from './gamesApi'
import { ApiError } from './http'

const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

beforeEach(() => { mockFetch.mockReset() })

describe('getGames', () => {
  it('returns games on success', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [
        { id: 'abc', name: 'Hollow Knight', coverImageUrl: null, genre: null }
      ],
    })
    const result = await getGames()
    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('Hollow Knight')
  })

  it('throws ApiError on non-ok response', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => ({ error: 'Server error' }),
    })
    await expect(getGames()).rejects.toBeInstanceOf(ApiError)
  })
})
```

`src/api/postsApi.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPost, getFeed } from './postsApi'
import { ApiError } from './http'

const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

beforeEach(() => { mockFetch.mockReset() })

const samplePost = {
  id: '1', authorId: 'a1', authorUsername: 'player', authorDisplayName: 'Player',
  authorAvatarUrl: null, gameId: 'g1', gameName: 'Hollow Knight', gameCoverImageUrl: null,
  textContent: 'Cleared it!', mood: 'Enjoying', createdAt: new Date().toISOString(),
}

describe('createPost', () => {
  it('sends bearer token and returns post on 201', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, status: 201, json: async () => samplePost })
    const result = await createPost('my-token', { gameId: 'g1', textContent: 'Cleared it!', mood: 'Enjoying' })
    expect(result.textContent).toBe('Cleared it!')
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/posts'),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer my-token' }) })
    )
  })

  it('throws ApiError on 400', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 400, json: async () => ({ error: 'Game was not found.' }) })
    await expect(createPost('tok', { gameId: 'bad', textContent: 'Hi', mood: null })).rejects.toBeInstanceOf(ApiError)
  })
})

describe('getFeed', () => {
  it('returns list of posts', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => [samplePost] })
    const feed = await getFeed()
    expect(feed).toHaveLength(1)
    expect(feed[0].authorUsername).toBe('player')
  })
})
```

- [ ] **Step 3: Run tests — expect FAIL**

```
npm test -- gamesApi postsApi
```
Expected: FAIL — `gamesApi`, `postsApi`, `http` modules not found.

- [ ] **Step 4: Create http.ts**

`src/api/http.ts`:
```ts
export const API_BASE_URL = 'http://localhost:5258'

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function parseErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body = await response.json()
    if (body && typeof body.error === 'string') return body.error
    if (body && body.errors && typeof body.errors === 'object') {
      const messages = Object.values(body.errors as Record<string, unknown>)
        .flat()
        .filter((m): m is string => typeof m === 'string')
      if (messages.length > 0) return messages.join(' ')
    }
  } catch { /* ignore */ }
  return fallback
}
```

- [ ] **Step 5: Update authApi.ts to re-export from http.ts**

Replace the top of `src/api/authApi.ts` — remove the inline `API_BASE_URL`, `ApiError`, and `parseErrorMessage` definitions and replace them with imports from `./http`. The three functions (`register`, `login`, `getMe`) stay unchanged. The file should start with:
```ts
export { API_BASE_URL, ApiError } from './http'
import { API_BASE_URL, ApiError, parseErrorMessage } from './http'
```
Then the three `UserResponse`/`LoginResponse` interfaces and the three functions follow exactly as before. No callers change.

- [ ] **Step 6: Create gamesApi.ts**

`src/api/gamesApi.ts`:
```ts
import { API_BASE_URL, ApiError, parseErrorMessage } from './http'

export interface Game {
  id: string
  name: string
  coverImageUrl: string | null
  genre: string | null
}

export async function getGames(): Promise<Game[]> {
  const response = await fetch(`${API_BASE_URL}/api/games`)
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to load games.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}
```

- [ ] **Step 7: Create postsApi.ts**

`src/api/postsApi.ts`:
```ts
import { API_BASE_URL, ApiError, parseErrorMessage } from './http'

export type Mood = 'Enjoying' | 'Frustrated' | 'Completed' | 'NeedHelp'

export interface PostFeedItem {
  id: string
  authorId: string
  authorUsername: string
  authorDisplayName: string
  authorAvatarUrl: string | null
  gameId: string
  gameName: string
  gameCoverImageUrl: string | null
  textContent: string
  mood: string | null
  createdAt: string
}

export async function createPost(
  token: string,
  data: { gameId: string; textContent: string; mood?: string | null }
): Promise<PostFeedItem> {
  const response = await fetch(`${API_BASE_URL}/api/posts`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(data),
  })
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to create post.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}

export async function getFeed(): Promise<PostFeedItem[]> {
  const response = await fetch(`${API_BASE_URL}/api/posts`)
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to load feed.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}
```

- [ ] **Step 8: Run tests — expect PASS**

```
npm test
```
Expected: all tests pass including `gamesApi` (2) and `postsApi` (3). Existing `authApi` tests must still pass.

- [ ] **Step 9: Run build — expect SUCCESS**

```
npm run build
```
Expected: zero TS errors.

- [ ] **Step 10: Commit**

```
git add src/api/http.ts src/api/authApi.ts src/api/gamesApi.ts src/api/gamesApi.test.ts src/api/postsApi.ts src/api/postsApi.test.ts
git commit -m "feat: add shared http.ts, gamesApi, postsApi; refactor authApi to re-export"
```

---

### Task 9: PostCard component

**Files:**
- Create: `src/components/PostCard.tsx`
- Test: `src/components/PostCard.test.tsx`

**Interfaces:**
- Consumes: `Avatar` (`src/components/ui/Avatar`), `Badge` (`src/components/ui/Badge`), `PostFeedItem` from `../api/postsApi`.
- Produces: `PostCard({ post: PostFeedItem })` — renders author avatar + display name + `@username`, game name, optional mood `Badge`, text content, relative timestamp. Mood mapping: `'Enjoying' → 'enjoying'`, `'NeedHelp' → 'need-help'`, `'Frustrated' → 'frustrated'`, `'Completed' → 'completed'`, any other/null → no badge. Timestamp helper: `formatRelativeTime(createdAt: string): string` — shows "Xm ago", "Xh ago", "Xd ago" (no external lib).

- [ ] **Step 1: Write failing test**

`src/components/PostCard.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PostCard } from './PostCard'
import type { PostFeedItem } from '../api/postsApi'

const base: PostFeedItem = {
  id: '1', authorId: 'a', authorUsername: 'nexusnova', authorDisplayName: 'NexusNova',
  authorAvatarUrl: null, gameId: 'g', gameName: 'Elden Ring', gameCoverImageUrl: null,
  textContent: 'Finally beat Radahn!', mood: 'Enjoying',
  createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
}

describe('PostCard', () => {
  it('renders author display name and username', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText('NexusNova')).toBeInTheDocument()
    expect(screen.getByText('@nexusnova')).toBeInTheDocument()
  })

  it('renders game name', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText('Elden Ring')).toBeInTheDocument()
  })

  it('renders text content', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText('Finally beat Radahn!')).toBeInTheDocument()
  })

  it('renders mood badge when mood is set', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText('Enjoying')).toBeInTheDocument()
  })

  it('renders no mood badge when mood is null', () => {
    render(<PostCard post={{ ...base, mood: null }} />)
    expect(screen.queryByText('Enjoying')).not.toBeInTheDocument()
  })

  it('maps NeedHelp mood to need-help badge variant', () => {
    render(<PostCard post={{ ...base, mood: 'NeedHelp' }} />)
    const badge = screen.getByText('Need Help')
    expect(badge).toHaveAttribute('data-variant', 'need-help')
  })

  it('renders a relative timestamp', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText(/ago/i)).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- PostCard
```
Expected: FAIL — `./PostCard` not found.

- [ ] **Step 3: Implement PostCard**

`src/components/PostCard.tsx`:
```tsx
import { Avatar } from './ui/Avatar'
import { Badge } from './ui/Badge'
import type { PostFeedItem } from '../api/postsApi'
import type { ComponentProps } from 'react'

type BadgeVariant = ComponentProps<typeof Badge>['variant']

function moodBadge(mood: string | null): { label: string; variant: BadgeVariant } | null {
  switch (mood) {
    case 'Enjoying': return { label: 'Enjoying', variant: 'enjoying' }
    case 'NeedHelp': return { label: 'Need Help', variant: 'need-help' }
    case 'Frustrated': return { label: 'Frustrated', variant: 'frustrated' }
    case 'Completed': return { label: 'Completed', variant: 'completed' }
    default: return null
  }
}

function formatRelativeTime(createdAt: string): string {
  const diffMs = Date.now() - new Date(createdAt).getTime()
  const diffMin = Math.floor(diffMs / 60_000)
  if (diffMin < 60) return `${Math.max(diffMin, 1)}m ago`
  const diffH = Math.floor(diffMin / 60)
  if (diffH < 24) return `${diffH}h ago`
  return `${Math.floor(diffH / 24)}d ago`
}

export function PostCard({ post }: { post: PostFeedItem }) {
  const badge = moodBadge(post.mood)

  return (
    <div className="rounded-xl border border-border bg-surface p-4 flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Avatar
            src={post.authorAvatarUrl ?? undefined}
            alt={post.authorDisplayName}
          />
          <div>
            <p className="text-sm font-semibold text-text">{post.authorDisplayName}</p>
            <p className="text-xs text-muted">@{post.authorUsername}</p>
          </div>
        </div>
        {badge && <Badge variant={badge.variant}>{badge.label}</Badge>}
      </div>

      <p className="text-xs font-medium text-primary">{post.gameName}</p>
      <p className="text-sm text-text leading-relaxed">{post.textContent}</p>
      <p className="text-xs text-muted">{formatRelativeTime(post.createdAt)}</p>
    </div>
  )
}
```

- [ ] **Step 4: Run test — expect PASS**

```
npm test -- PostCard
```
Expected: 7 tests pass.

- [ ] **Step 5: Run full suite + build**

```
npm test
npm run build
```
Expected: all pass, build succeeds.

- [ ] **Step 6: Commit**

```
git add src/components/PostCard.tsx src/components/PostCard.test.tsx
git commit -m "feat: add PostCard component"
```

---

### Task 10: CreatePostPage

**Files:**
- Create: `src/pages/CreatePostPage.tsx`
- Test: `src/pages/CreatePostPage.test.tsx`

**Interfaces:**
- Consumes: `getGames` (Task 8), `createPost` (Task 8), `Game` (Task 8), `useAuth` (`../context/AuthContext`), `useNavigate`/`react-router-dom`, `Button` (`../components/ui/Button`), `ApiError` (`../api/http`).
- Produces: default-exported `CreatePostPage`. On mount loads `getGames`; renders a `<select>` for game (required, `aria-label="Select a game"`), 5 mood buttons ("None" / "Enjoying" / "Frustrated" / "Completed" / "Need Help" — `aria-pressed` true on selected), a `<textarea>` (`aria-label="Post text"`) with live char counter showing `X / 1000`, a submit `Button`. On submit: validates text non-empty (shows `"Post text is required."`) and ≤1000 chars; calls `createPost(token, {gameId, textContent, mood})` where mood maps "None" → null and "Need Help" → "NeedHelp"; on success navigates to `/feed`; on `ApiError` shows its message.

- [ ] **Step 1: Write failing test**

`src/pages/CreatePostPage.test.tsx`:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import CreatePostPage from './CreatePostPage'
import * as gamesApi from '../api/gamesApi'
import * as postsApi from '../api/postsApi'

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({ user: { username: 'player' }, token: 'test-token' }),
}))
vi.mock('../api/gamesApi')
vi.mock('../api/postsApi')

const mockGames: gamesApi.Game[] = [
  { id: 'g1', name: 'Hollow Knight', coverImageUrl: null, genre: null },
  { id: 'g2', name: 'Elden Ring', coverImageUrl: null, genre: null },
]

beforeEach(() => {
  vi.mocked(gamesApi.getGames).mockResolvedValue(mockGames)
  vi.mocked(postsApi.createPost).mockResolvedValue({
    id: 'p1', authorId: 'a', authorUsername: 'player', authorDisplayName: 'Player',
    authorAvatarUrl: null, gameId: 'g1', gameName: 'Hollow Knight', gameCoverImageUrl: null,
    textContent: 'Hello', mood: null, createdAt: new Date().toISOString(),
  })
})

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/create-post']}>
      <Routes>
        <Route path="/create-post" element={<CreatePostPage />} />
        <Route path="/feed" element={<div>Feed page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('CreatePostPage', () => {
  it('loads and displays games in the select', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByRole('option', { name: 'Hollow Knight' })).toBeInTheDocument())
    expect(screen.getByRole('option', { name: 'Elden Ring' })).toBeInTheDocument()
  })

  it('shows validation error when submitting empty text', async () => {
    const user = userEvent.setup()
    renderPage()
    await waitFor(() => screen.getByRole('option', { name: 'Hollow Knight' }))
    await user.click(screen.getByRole('button', { name: /post/i }))
    expect(await screen.findByText('Post text is required.')).toBeInTheDocument()
    expect(postsApi.createPost).not.toHaveBeenCalled()
  })

  it('calls createPost and navigates to feed on success', async () => {
    const user = userEvent.setup()
    renderPage()
    await waitFor(() => screen.getByRole('option', { name: 'Hollow Knight' }))
    await user.type(screen.getByRole('textbox', { name: /post text/i }), 'Cleared the boss!')
    await user.click(screen.getByRole('button', { name: /post/i }))
    await waitFor(() => expect(screen.getByText('Feed page')).toBeInTheDocument())
    expect(postsApi.createPost).toHaveBeenCalledWith('test-token', expect.objectContaining({ textContent: 'Cleared the boss!' }))
  })

  it('shows error message on createPost failure', async () => {
    vi.mocked(postsApi.createPost).mockRejectedValueOnce(new postsApi.Mood === undefined ? Error('fail') : (() => { const e = new Error('Game was not found.') as any; e.name = 'ApiError'; return e })())
    vi.mocked(postsApi.createPost).mockRejectedValueOnce(Object.assign(new Error('Game was not found.'), { name: 'ApiError', status: 400 }))
    const user = userEvent.setup()
    renderPage()
    await waitFor(() => screen.getByRole('option', { name: 'Hollow Knight' }))
    await user.type(screen.getByRole('textbox', { name: /post text/i }), 'Hello')
    await user.click(screen.getByRole('button', { name: /post/i }))
    await waitFor(() => expect(screen.getByText('Game was not found.')).toBeInTheDocument())
  })
})
```

**NOTE for implementer:** The "shows error" test fixture is intentionally simplified — as long as `ApiError` is thrown, the error message renders. Use the `ApiError` import from `../api/http` in the actual page (not in the test). The test just needs the last `mockRejectedValueOnce` call that sets `message = 'Game was not found.'`.

Simplify the test's error mock to:
```tsx
  it('shows error message on createPost failure', async () => {
    const { ApiError } = await import('../api/http')
    vi.mocked(postsApi.createPost).mockRejectedValueOnce(new ApiError(400, 'Game was not found.'))
    const user = userEvent.setup()
    renderPage()
    await waitFor(() => screen.getByRole('option', { name: 'Hollow Knight' }))
    await user.type(screen.getByRole('textbox', { name: /post text/i }), 'Hello')
    await user.click(screen.getByRole('button', { name: /post/i }))
    await waitFor(() => expect(screen.getByText('Game was not found.')).toBeInTheDocument())
  })
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- CreatePostPage
```
Expected: FAIL — `./CreatePostPage` not found.

- [ ] **Step 3: Implement CreatePostPage**

`src/pages/CreatePostPage.tsx`:
```tsx
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '../components/ui/Button'
import { useAuth } from '../context/AuthContext'
import { getGames, type Game } from '../api/gamesApi'
import { createPost } from '../api/postsApi'
import { ApiError } from '../api/http'

type MoodOption = 'None' | 'Enjoying' | 'Frustrated' | 'Completed' | 'Need Help'
const MOOD_OPTIONS: MoodOption[] = ['None', 'Enjoying', 'Frustrated', 'Completed', 'Need Help']

function moodToApi(mood: MoodOption): string | null {
  if (mood === 'None') return null
  if (mood === 'Need Help') return 'NeedHelp'
  return mood
}

export default function CreatePostPage() {
  const { token } = useAuth()
  const navigate = useNavigate()

  const [games, setGames] = useState<Game[]>([])
  const [gamesError, setGamesError] = useState<string | null>(null)
  const [selectedGameId, setSelectedGameId] = useState('')
  const [selectedMood, setSelectedMood] = useState<MoodOption>('None')
  const [text, setText] = useState('')
  const [textError, setTextError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    getGames()
      .then((g) => {
        setGames(g)
        if (g.length > 0) setSelectedGameId(g[0].id)
      })
      .catch(() => setGamesError('Failed to load games.'))
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setTextError(null)
    setSubmitError(null)

    const trimmed = text.trim()
    if (!trimmed) { setTextError('Post text is required.'); return }
    if (trimmed.length > 1000) { setTextError('Post text cannot be longer than 1000 characters.'); return }

    setIsSubmitting(true)
    try {
      await createPost(token!, { gameId: selectedGameId, textContent: trimmed, mood: moodToApi(selectedMood) })
      navigate('/feed')
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Something went wrong.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-xl flex flex-col gap-6">
      <h1 className="text-2xl font-bold text-text">Create Post</h1>

      {gamesError && <p className="text-frustrated">{gamesError}</p>}

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
        <label className="flex flex-col gap-1 text-sm text-muted">
          Game
          <select
            aria-label="Select a game"
            className="rounded-lg border border-border bg-surface-raised px-3 py-2 text-text"
            value={selectedGameId}
            onChange={(e) => setSelectedGameId(e.target.value)}
          >
            {games.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
        </label>

        <div className="flex flex-col gap-2">
          <span className="text-sm text-muted">Mood (optional)</span>
          <div className="flex flex-wrap gap-2">
            {MOOD_OPTIONS.map((mood) => (
              <button
                key={mood}
                type="button"
                aria-pressed={selectedMood === mood}
                onClick={() => setSelectedMood(mood)}
                className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                  selectedMood === mood
                    ? 'bg-primary text-white'
                    : 'bg-surface-raised text-muted hover:text-text'
                }`}
              >
                {mood}
              </button>
            ))}
          </div>
        </div>

        <label className="flex flex-col gap-1 text-sm text-muted">
          What happened?
          <textarea
            aria-label="Post text"
            className="rounded-lg border border-border bg-surface-raised px-3 py-2 text-text resize-none h-32 outline-none focus:border-primary"
            value={text}
            maxLength={1000}
            onChange={(e) => setText(e.target.value)}
          />
          <span className="text-xs text-muted self-end">{text.length} / 1000</span>
        </label>

        {textError && <p className="text-frustrated text-sm">{textError}</p>}
        {submitError && <p className="text-frustrated text-sm">{submitError}</p>}

        <Button type="submit" disabled={isSubmitting} className="w-full">
          {isSubmitting ? 'Posting…' : 'Post'}
        </Button>
      </form>
    </div>
  )
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
npm test -- CreatePostPage
```
Expected: 4 tests pass.

- [ ] **Step 5: Run full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/pages/CreatePostPage.tsx src/pages/CreatePostPage.test.tsx
git commit -m "feat: add CreatePostPage"
```

---

### Task 11: FeedPage + wire routing + wire Sidebar button

**Files:**
- Modify: `src/pages/FeedPage.tsx` (replace placeholder)
- Test: `src/pages/FeedPage.test.tsx` (replace placeholder test — if one exists, replace entirely)
- Modify: `src/App.tsx` (add `/create-post` route)
- Modify: `src/components/layout/Sidebar.tsx` (wire "Create Post" button to navigate)

**Interfaces:**
- Consumes: `getFeed` (Task 8), `PostFeedItem` (Task 8), `PostCard` (Task 9), `Button` (`../components/ui/Button`), `NavLink`/`useNavigate`/`react-router-dom`.
- Produces: `FeedPage` showing a list of `PostCard` items or an empty state; a working `/create-post` route; a working "Create Post" sidebar button.

- [ ] **Step 1: Write failing FeedPage test**

`src/pages/FeedPage.test.tsx` (replace any existing content):
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import FeedPage from './FeedPage'
import * as postsApi from '../api/postsApi'

vi.mock('../api/postsApi')

const samplePost: postsApi.PostFeedItem = {
  id: '1', authorId: 'a', authorUsername: 'player', authorDisplayName: 'Player One',
  authorAvatarUrl: null, gameId: 'g', gameName: 'Hollow Knight', gameCoverImageUrl: null,
  textContent: 'Finally cleared it!', mood: 'Enjoying',
  createdAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
}

beforeEach(() => { vi.mocked(postsApi.getFeed).mockResolvedValue([samplePost]) })

function renderFeed() {
  return render(
    <MemoryRouter><Routes><Route path="/" element={<FeedPage />} /></Routes></MemoryRouter>
  )
}

describe('FeedPage', () => {
  it('renders posts from the feed', async () => {
    renderFeed()
    await waitFor(() => expect(screen.getByText('Finally cleared it!')).toBeInTheDocument())
    expect(screen.getByText('Player One')).toBeInTheDocument()
    expect(screen.getByText('Hollow Knight')).toBeInTheDocument()
  })

  it('renders empty state when feed is empty', async () => {
    vi.mocked(postsApi.getFeed).mockResolvedValueOnce([])
    renderFeed()
    await waitFor(() =>
      expect(screen.getByText(/no posts yet/i)).toBeInTheDocument()
    )
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- FeedPage
```
Expected: FAIL — old placeholder has no post rendering.

- [ ] **Step 3: Replace FeedPage**

`src/pages/FeedPage.tsx`:
```tsx
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { PostCard } from '../components/PostCard'
import { Button } from '../components/ui/Button'
import { getFeed, type PostFeedItem } from '../api/postsApi'

export default function FeedPage() {
  const navigate = useNavigate()
  const [posts, setPosts] = useState<PostFeedItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getFeed()
      .then(setPosts)
      .catch(() => setError('Failed to load feed.'))
      .finally(() => setIsLoading(false))
  }, [])

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-text">Feed</h1>
        <Button onClick={() => navigate('/create-post')}>Create Post</Button>
      </div>

      {isLoading && <p className="text-muted">Loading…</p>}
      {error && <p className="text-frustrated">{error}</p>}
      {!isLoading && !error && posts.length === 0 && (
        <p className="text-muted">No posts yet — be the first to share!</p>
      )}
      {posts.map((post) => (
        <PostCard key={post.id} post={post} />
      ))}
    </div>
  )
}
```

- [ ] **Step 4: Add `/create-post` route to App.tsx**

In `src/App.tsx`, add the import:
```tsx
import CreatePostPage from './pages/CreatePostPage'
```
And inside the protected `AppShell` layout route, add:
```tsx
<Route path="/create-post" element={<CreatePostPage />} />
```
The complete App.tsx should look like:
```tsx
import { Routes, Route } from 'react-router-dom'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import HomePage from './pages/HomePage'
import FeedPage from './pages/FeedPage'
import FindPlayersPage from './pages/FindPlayersPage'
import ThreadsPage from './pages/ThreadsPage'
import ProfilePage from './pages/ProfilePage'
import CreatePostPage from './pages/CreatePostPage'
import { ProtectedRoute } from './components/ProtectedRoute'
import { AppShell } from './components/layout/AppShell'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        element={
          <ProtectedRoute>
            <AppShell />
          </ProtectedRoute>
        }
      >
        <Route index element={<HomePage />} />
        <Route path="/feed" element={<FeedPage />} />
        <Route path="/find-players" element={<FindPlayersPage />} />
        <Route path="/threads" element={<ThreadsPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/create-post" element={<CreatePostPage />} />
      </Route>
    </Routes>
  )
}

export default App
```

- [ ] **Step 5: Wire "Create Post" button in Sidebar**

In `src/components/layout/Sidebar.tsx`, the "Create Post" `Button` is currently:
```tsx
<Button className="w-full">
  <Plus className="h-4 w-4" aria-hidden="true" />
  Create Post
</Button>
```
Replace it with a `NavLink` styled as a button (or add `onClick` with `useNavigate`). Use `useNavigate`:
Add `import { NavLink, useNavigate } from 'react-router-dom'` (already has NavLink; add useNavigate).
Add `const navigate = useNavigate()` at the top of the component.
Change the "Create Post" Button to:
```tsx
<Button className="w-full" onClick={() => navigate('/create-post')}>
  <Plus className="h-4 w-4" aria-hidden="true" />
  Create Post
</Button>
```

- [ ] **Step 6: Run all tests — expect PASS**

```
npm test
```
Expected: all tests pass.

- [ ] **Step 7: Run build**

```
npm run build
```
Expected: zero TS errors.

- [ ] **Step 8: Commit**

```
git add src/pages/FeedPage.tsx src/pages/FeedPage.test.tsx src/App.tsx src/components/layout/Sidebar.tsx
git commit -m "feat: real FeedPage, CreatePost route, wire Sidebar Create Post button"
```

---

### Task 12: Merge frontend branch to main

- [ ] **Step 1: Run full test suite**

```
npm test
```
Expected: all pass.

- [ ] **Step 2: Merge to main**

```
git checkout main
git merge --no-ff feature/games-posts -m "Merge feature/games-posts: games API, posts API, PostCard, CreatePostPage, FeedPage"
```

- [ ] **Step 3: Verify tests on main**

```
npm test
```
Expected: all pass.

- [ ] **Step 4: Delete feature branch**

```
git branch -d feature/games-posts
```

---

## Self-Review

**Spec coverage:**
- Game entity (Id/Name/CoverImageUrl/Genre) → Task 1 ✓
- PostMood enum stored as string → Task 1, Task 2 ✓
- Post entity (Id/AuthorId/GameId/TextContent/Mood/CreatedAt) → Task 1 ✓
- EF config + cascades + seeded games → Task 2 ✓
- Application contracts (DTOs, commands, interfaces) → Task 3 ✓
- GameService.GetAllAsync ordered by Name → Task 4 ✓
- PostService.CreateAsync validation (text required/≤1000, mood valid, game exists) → Task 4 ✓
- PostService.GetFeedAsync latest 50 newest-first with author display fields → Task 4 ✓
- DI registration → Task 4 ✓
- GET /api/games public → Task 5 ✓
- POST /api/posts authorized, 201, InvalidOperationException→400 → Task 5 ✓
- GET /api/posts public → Task 5 ✓
- HTTP integration test (full flow) → Task 6 ✓
- Shared http.ts, refactored authApi.ts → Task 8 ✓
- gamesApi.ts, postsApi.ts → Task 8 ✓
- PostCard component with mood badge mapping → Task 9 ✓
- CreatePostPage (game select, mood picker, textarea, validation, navigate) → Task 10 ✓
- FeedPage (loads feed, empty state, PostCard list) → Task 11 ✓
- /create-post route added → Task 11 ✓
- Sidebar "Create Post" button wired → Task 11 ✓

**Placeholder scan:** No TBD/TODO. Task 10's test step contains a now-superseded complex mock in Step 1 with a corrected simpler version at the end of the same step — implementer should use only the corrected version (the final `it('shows error...')` block with `import('../api/http')`).

**Type consistency:**
- `PostDto` constructor order used in `PostService` Join matches the record definition in Task 3 ✓
- `PostFeedItem` field names in `postsApi.ts` match `PostResponse` record property names (camelCase from System.Text.Json defaults) ✓
- `moodToApi` in `CreatePostPage` maps "Need Help" → "NeedHelp" matching the backend `PostMood` enum ✓
- `ApiError` imported from `../api/http` (not `../api/authApi`) in pages — consistent with the refactor in Task 8 ✓
- `formatRelativeTime` in `PostCard` matches no interface — internal only ✓
