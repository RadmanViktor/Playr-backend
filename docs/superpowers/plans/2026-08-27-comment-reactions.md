# Comment Reactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users react to a comment with one of five emoji reaction types (Like, Haha, Wow, Sad, Angry), one active reaction per user per comment, with per-type counts and the current user's own reaction surfaced on the comment.

**Architecture:** Follow the existing PLAYR layered architecture (`Playr.Domain` → `Playr.Application` → `Playr.Infrastructure` → `Playr.Api`), mirroring the existing `PostLike` toggle pattern but storing a `ReactionType` instead of a plain existence row. A new `CommentReaction` entity with composite key `(CommentId, UserId)` is added, aggregated into `CommentDto`/`CommentResponse` and manipulated via two new endpoints on the existing `CommentsController`.

**Tech Stack:** ASP.NET Core Web API (.NET 10), EF Core with Npgsql (Postgres) provider and SQLite for tests, xUnit + FluentAssertions.

## Global Constraints

- Reaction types are exactly: Like, Haha, Wow, Sad, Angry (per spec).
- One active reaction per user per comment (composite key `(CommentId, UserId)`); setting the same type again removes it (toggle off); setting a different type replaces it.
- Comment payloads (list and single) must include counts per reaction type and the current user's own reaction (if any).
- Follow existing error-message convention: `"...was not found."` → 404, `"You are not allowed to..."` → 403, other `InvalidOperationException` → 400.
- Follow existing EF Core conventions in `PlayrDbContext.cs`: non-Postgres providers use Unix-millisecond `DateTimeOffset` conversions; cascade delete on comment/user FKs.
- New code must not modify existing `PostLike`/post-reaction behavior — this is a comment-only feature.

---

### Task 1: Domain entity `CommentReaction` and `ReactionType` enum

**Files:**
- Create: `src/Playr.Domain/Comments/ReactionType.cs`
- Create: `src/Playr.Domain/Comments/CommentReaction.cs`

**Interfaces:**
- Produces: `Playr.Domain.Comments.ReactionType` enum with values `Like, Haha, Wow, Sad, Angry`.
- Produces: `Playr.Domain.Comments.CommentReaction` class with properties `CommentId (Guid)`, `Comment (PostComment)`, `UserId (Guid)`, `User (ApplicationUser)`, `Type (ReactionType)`, `CreatedAt (DateTimeOffset)`.

- [ ] **Step 1: Create the `ReactionType` enum**

```csharp
namespace Playr.Domain.Comments;

public enum ReactionType
{
    Like,
    Haha,
    Wow,
    Sad,
    Angry
}
```

- [ ] **Step 2: Create the `CommentReaction` entity**

```csharp
using Playr.Domain.Identity;
using Playr.Domain.Posts;

namespace Playr.Domain.Comments;

public sealed class CommentReaction
{
    public Guid CommentId { get; set; }
    public PostComment Comment { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public ReactionType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 3: Build the Domain project**

Run: `dotnet build src\Playr.Domain\Playr.Domain.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/Playr.Domain/Comments/ReactionType.cs src/Playr.Domain/Comments/CommentReaction.cs
git commit -m "feat: add CommentReaction domain entity and ReactionType enum"
```

---

### Task 2: EF Core configuration and migration

**Files:**
- Modify: `src/Playr.Infrastructure/Data/PlayrDbContext.cs`

**Interfaces:**
- Consumes: `Playr.Domain.Comments.CommentReaction`, `Playr.Domain.Comments.ReactionType` (Task 1).
- Produces: `PlayrDbContext.CommentReactions` (`DbSet<CommentReaction>`), plus EF configuration and a new migration named `AddCommentReactions`. Later tasks query `dbContext.CommentReactions`.

- [ ] **Step 1: Add the using and DbSet**

In `src/Playr.Infrastructure/Data/PlayrDbContext.cs`, add the using statement after line 4 (`using Playr.Domain.Chat;`):

```csharp
using Playr.Domain.Comments;
```

Add the DbSet after line 23 (`public DbSet<PostComment> PostComments => Set<PostComment>();`):

```csharp
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
```

- [ ] **Step 2: Add the entity configuration**

In `OnModelCreating`, immediately after the `builder.Entity<PostComment>(...)` block (after line 164, before the `Invitation` block), add:

```csharp
        builder.Entity<CommentReaction>(reaction =>
        {
            reaction.HasKey(r => new { r.CommentId, r.UserId });
            reaction.Property(r => r.Type)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            reaction.HasOne(r => r.Comment)
                .WithMany()
                .HasForeignKey(r => r.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
            reaction.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            reaction.HasIndex(r => r.CommentId);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                reaction.Property(r => r.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });
```

- [ ] **Step 3: Build the Infrastructure project**

Run: `dotnet build src\Playr.Infrastructure\Playr.Infrastructure.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4: Generate the EF Core migration**

Run (from repo root):
```bash
dotnet ef migrations add AddCommentReactions --project src\Playr.Infrastructure --startup-project src\Playr.Api
```
Expected: New files created under `src\Playr.Infrastructure\Migrations\` named `<timestamp>_AddCommentReactions.cs` and `.Designer.cs`, plus an updated `PlayrDbContextModelSnapshot.cs`. Verify the generated migration creates a `CommentReactions` table with composite primary key `(CommentId, UserId)`, a `Type` column (`character varying(16)`), foreign keys to `PostComments` and `AspNetUsers` both with cascade delete, and an index on `CommentId`.

- [ ] **Step 5: Commit**

```bash
git add src/Playr.Infrastructure/Data/PlayrDbContext.cs src/Playr.Infrastructure/Migrations
git commit -m "feat: add CommentReaction EF configuration and migration"
```

---

### Task 3: Application-layer DTOs and service interface

**Files:**
- Create: `src/Playr.Application/Comments/ReactionCounts.cs`
- Create: `src/Playr.Application/Comments/CommentReactionSummary.cs`
- Modify: `src/Playr.Application/Comments/CommentDto.cs`
- Modify: `src/Playr.Application/Comments/ICommentService.cs`

**Interfaces:**
- Consumes: `Playr.Domain.Comments.ReactionType` (Task 1).
- Produces: `ReactionCounts` record `(int Like, int Haha, int Wow, int Sad, int Angry)`; `CommentReactionSummary` record `(ReactionCounts Counts, ReactionType? CurrentUserReaction)`; `CommentDto` gains a required `Reactions (CommentReactionSummary)` field as its last positional parameter; `ICommentService` gains `SetReactionAsync(Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken)` returning `Task<CommentReactionSummary>` and `RemoveReactionAsync(Guid commentId, Guid userId, CancellationToken cancellationToken)` returning `Task<CommentReactionSummary>`.

- [ ] **Step 1: Create `ReactionCounts`**

```csharp
namespace Playr.Application.Comments;

public sealed record ReactionCounts(int Like, int Haha, int Wow, int Sad, int Angry);
```

- [ ] **Step 2: Create `CommentReactionSummary`**

```csharp
using Playr.Domain.Comments;

namespace Playr.Application.Comments;

public sealed record CommentReactionSummary(ReactionCounts Counts, ReactionType? CurrentUserReaction);
```

- [ ] **Step 3: Extend `CommentDto`**

Replace the full contents of `src/Playr.Application/Comments/CommentDto.cs`:

```csharp
namespace Playr.Application.Comments;

public sealed record CommentDto(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string TextContent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    CommentReactionSummary Reactions);
```

- [ ] **Step 4: Extend `ICommentService`**

Replace the full contents of `src/Playr.Application/Comments/ICommentService.cs`:

```csharp
using Playr.Application.Common;
using Playr.Domain.Comments;

namespace Playr.Application.Comments;

public interface ICommentService
{
    Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken);
    Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, int skip, int take, CancellationToken cancellationToken);
    Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, Guid requesterId, UpdateCommentCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid commentId, Guid requesterId, CancellationToken cancellationToken);
    Task<CommentReactionSummary> SetReactionAsync(Guid postId, Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken);
    Task<CommentReactionSummary> RemoveReactionAsync(Guid postId, Guid commentId, Guid userId, CancellationToken cancellationToken);
}
```

Note: `postId` is included as the first parameter (matching `UpdateAsync`/`DeleteAsync` above it) so the service can verify the comment belongs to the given post, consistent with existing methods.

- [ ] **Step 5: Build (expect failure — `CommentService` doesn't implement the new interface members yet)**

Run: `dotnet build src\Playr.Application\Playr.Application.csproj`
Expected: build succeeds (interface has no implementers to check yet in this project). This step only confirms the Application project compiles in isolation.

- [ ] **Step 6: Commit**

```bash
git add src/Playr.Application/Comments/ReactionCounts.cs src/Playr.Application/Comments/CommentReactionSummary.cs src/Playr.Application/Comments/CommentDto.cs src/Playr.Application/Comments/ICommentService.cs
git commit -m "feat: add reaction DTOs and extend ICommentService"
```

---

### Task 4: `CommentService` reaction logic (with tests)

**Files:**
- Modify: `src/Playr.Infrastructure/Comments/CommentService.cs`
- Test: `tests/Playr.Application.Tests/Comments/CommentServiceTests.cs` (new file)

**Interfaces:**
- Consumes: `ICommentService` (Task 3), `CommentReaction`/`ReactionType` (Task 1), `PlayrDbContext.CommentReactions` (Task 2).
- Produces: Working `CommentService.SetReactionAsync`/`RemoveReactionAsync`; `MapToCommentDtoAsync` populates `CommentDto.Reactions` for every comment (batched, no N+1); `GetPagedAsync`/`CreateAsync`/`UpdateAsync` all now require a `currentUserId` to compute `CurrentUserReaction` per comment — see signature change below.

Since `MapToCommentDtoAsync` must know the calling user's own reaction, and `CreateAsync`/`UpdateAsync` already receive an author/requester id but `GetPagedAsync` does not receive any user id today, this task changes `GetPagedAsync`'s signature to accept the current user id (nullable, since anonymous viewers might list comments). Update `ICommentService.GetPagedAsync` accordingly first.

- [ ] **Step 1: Update `ICommentService.GetPagedAsync` signature to accept the current user**

In `src/Playr.Application/Comments/ICommentService.cs`, replace:

```csharp
    Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, int skip, int take, CancellationToken cancellationToken);
```

with:

```csharp
    Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, Guid? currentUserId, int skip, int take, CancellationToken cancellationToken);
```

- [ ] **Step 2: Write failing tests for `CommentService` reaction behavior**

Create `tests/Playr.Application.Tests/Comments/CommentServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Comments;
using Playr.Domain.Comments;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Comments;
using Playr.Infrastructure.Data;

namespace Playr.Application.Tests.Comments;

public sealed class CommentServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly CommentService _service;
    private readonly Guid _authorId;
    private readonly Guid _reactorId;
    private readonly Guid _postId;
    private readonly Guid _commentId;

    public CommentServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _reactorId = Guid.NewGuid();
        _postId = Guid.NewGuid();
        _commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _dbContext.Users.AddRange(
            new ApplicationUser { Id = _authorId, Email = "author@example.com", UserName = "author", NormalizedEmail = "AUTHOR@EXAMPLE.COM", NormalizedUserName = "AUTHOR" },
            new ApplicationUser { Id = _reactorId, Email = "reactor@example.com", UserName = "reactor", NormalizedEmail = "REACTOR@EXAMPLE.COM", NormalizedUserName = "REACTOR" });
        _dbContext.UserProfiles.AddRange(
            new UserProfile { UserId = _authorId, Username = "author", DisplayName = "Author" },
            new UserProfile { UserId = _reactorId, Username = "reactor", DisplayName = "Reactor" });
        _dbContext.Games.Add(new Game { Id = gameId, Name = "Hollow Knight" });
        _dbContext.Posts.Add(new Post { Id = _postId, AuthorId = _authorId, GameId = gameId, TextContent = "Post", CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.PostComments.Add(new PostComment { Id = _commentId, PostId = _postId, AuthorId = _authorId, TextContent = "Nice!", CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.SaveChanges();

        _service = new CommentService(_dbContext);
    }

    [Fact]
    public async Task SetReactionAsync_with_no_existing_reaction_creates_it()
    {
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);

        summary.Counts.Like.Should().Be(1);
        summary.Counts.Haha.Should().Be(0);
        summary.CurrentUserReaction.Should().Be(ReactionType.Like);
    }

    [Fact]
    public async Task SetReactionAsync_with_same_type_again_toggles_it_off()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task SetReactionAsync_with_different_type_replaces_existing_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Wow, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.Counts.Wow.Should().Be(1);
        summary.CurrentUserReaction.Should().Be(ReactionType.Wow);
    }

    [Fact]
    public async Task SetReactionAsync_with_unknown_comment_throws()
    {
        var act = () => _service.SetReactionAsync(_postId, Guid.NewGuid(), _reactorId, ReactionType.Like, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Comment was not found.");
    }

    [Fact]
    public async Task RemoveReactionAsync_removes_existing_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Angry, CancellationToken.None);
        var summary = await _service.RemoveReactionAsync(_postId, _commentId, _reactorId, CancellationToken.None);

        summary.Counts.Angry.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task RemoveReactionAsync_with_no_existing_reaction_is_a_noop()
    {
        var summary = await _service.RemoveReactionAsync(_postId, _commentId, _reactorId, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_reports_reaction_counts_and_current_user_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Haha, CancellationToken.None);
        await _service.SetReactionAsync(_postId, _commentId, _authorId, ReactionType.Haha, CancellationToken.None);

        var pageForReactor = await _service.GetPagedAsync(_postId, _reactorId, 0, 20, CancellationToken.None);
        var pageForAnonymous = await _service.GetPagedAsync(_postId, null, 0, 20, CancellationToken.None);

        pageForReactor.Items[0].Reactions.Counts.Haha.Should().Be(2);
        pageForReactor.Items[0].Reactions.CurrentUserReaction.Should().Be(ReactionType.Haha);
        pageForAnonymous.Items[0].Reactions.CurrentUserReaction.Should().BeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

- [ ] **Step 3: Run the new tests to verify they fail (compile error expected — methods don't exist yet)**

Run: `dotnet test tests\Playr.Application.Tests\Playr.Application.Tests.csproj --filter "FullyQualifiedName~CommentServiceTests"`
Expected: build error, e.g. `'CommentService' does not implement interface member 'ICommentService.SetReactionAsync'` or similar — confirms the test project references code that doesn't exist yet.

- [ ] **Step 4: Implement `SetReactionAsync`, `RemoveReactionAsync`, and update `GetPagedAsync`/`MapToCommentDtoAsync`**

Replace the full contents of `src/Playr.Infrastructure/Comments/CommentService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Playr.Application.Comments;
using Playr.Application.Common;
using Playr.Domain.Comments;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Comments;

public sealed class CommentService(PlayrDbContext dbContext) : ICommentService
{
    private const int MaxTextLength = 500;

    public async Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Comment text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Comment text cannot be longer than {MaxTextLength} characters.");

        var postExists = await dbContext.Posts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            throw new InvalidOperationException("Post was not found.");

        var comment = new PostComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            TextContent = text,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.PostComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync([comment], authorId, cancellationToken);
        return dtos[0];
    }

    public async Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, Guid? currentUserId, int skip, int take, CancellationToken cancellationToken)
    {
        var postExists = await dbContext.Posts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            throw new InvalidOperationException("Post was not found.");

        var totalCount = await dbContext.PostComments.CountAsync(c => c.PostId == postId, cancellationToken);

        var comments = await dbContext.PostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync(comments, currentUserId, cancellationToken);
        var hasMore = skip + comments.Count < totalCount;
        return new PagedResult<CommentDto>(dtos, totalCount, hasMore);
    }

    public async Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, Guid requesterId, UpdateCommentCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Comment text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Comment text cannot be longer than {MaxTextLength} characters.");

        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        if (comment.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to edit this comment.");

        comment.TextContent = text;
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync([comment], requesterId, cancellationToken);
        return dtos[0];
    }

    public async Task DeleteAsync(Guid postId, Guid commentId, Guid requesterId, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        if (comment.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to delete this comment.");

        dbContext.PostComments.Remove(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommentReactionSummary> SetReactionAsync(Guid postId, Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        var existing = await dbContext.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == comment.Id && r.UserId == userId, cancellationToken);

        if (existing is null)
        {
            dbContext.CommentReactions.Add(new CommentReaction
            {
                CommentId = comment.Id,
                UserId = userId,
                Type = type,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else if (existing.Type == type)
        {
            dbContext.CommentReactions.Remove(existing);
        }
        else
        {
            existing.Type = type;
            existing.CreatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildReactionSummaryAsync(comment.Id, userId, cancellationToken);
    }

    public async Task<CommentReactionSummary> RemoveReactionAsync(Guid postId, Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        var existing = await dbContext.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == comment.Id && r.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            dbContext.CommentReactions.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await BuildReactionSummaryAsync(comment.Id, userId, cancellationToken);
    }

    private async Task<CommentReactionSummary> BuildReactionSummaryAsync(Guid commentId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var reactions = await dbContext.CommentReactions
            .AsNoTracking()
            .Where(r => r.CommentId == commentId)
            .ToListAsync(cancellationToken);

        var counts = BuildCounts(reactions);
        ReactionType? currentUserReaction = currentUserId.HasValue
            ? reactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.Type
            : null;

        return new CommentReactionSummary(counts, currentUserReaction);
    }

    private static ReactionCounts BuildCounts(IReadOnlyCollection<CommentReaction> reactions) => new(
        reactions.Count(r => r.Type == ReactionType.Like),
        reactions.Count(r => r.Type == ReactionType.Haha),
        reactions.Count(r => r.Type == ReactionType.Wow),
        reactions.Count(r => r.Type == ReactionType.Sad),
        reactions.Count(r => r.Type == ReactionType.Angry));

    private async Task<IReadOnlyList<CommentDto>> MapToCommentDtoAsync(IList<PostComment> comments, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(up => authorIds.Contains(up.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(up => up.UserId);

        var commentIds = comments.Select(c => c.Id).ToList();
        var allReactions = await dbContext.CommentReactions
            .AsNoTracking()
            .Where(r => commentIds.Contains(r.CommentId))
            .ToListAsync(cancellationToken);
        var reactionsByComment = allReactions
            .GroupBy(r => r.CommentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return comments.Select(comment =>
        {
            var profile = profileMap[comment.AuthorId];
            var commentReactions = reactionsByComment.TryGetValue(comment.Id, out var list) ? list : [];
            var counts = BuildCounts(commentReactions);
            ReactionType? currentUserReaction = currentUserId.HasValue
                ? commentReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.Type
                : null;

            return new CommentDto(
                comment.Id,
                comment.PostId,
                comment.AuthorId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                comment.TextContent,
                comment.CreatedAt,
                comment.UpdatedAt,
                new CommentReactionSummary(counts, currentUserReaction));
        }).ToList();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests\Playr.Application.Tests\Playr.Application.Tests.csproj --filter "FullyQualifiedName~CommentServiceTests"`
Expected: `Passed!` for all 7 tests.

- [ ] **Step 6: Run the full Application test suite to check for regressions**

Run: `dotnet test tests\Playr.Application.Tests\Playr.Application.Tests.csproj`
Expected: All tests pass (existing `PostServiceTests`, `PostEditDeleteServiceTests`, `PostsByUsernameTests` unaffected).

- [ ] **Step 7: Commit**

```bash
git add src/Playr.Infrastructure/Comments/CommentService.cs src/Playr.Application/Comments/ICommentService.cs tests/Playr.Application.Tests/Comments/CommentServiceTests.cs
git commit -m "feat: implement comment reaction toggling in CommentService"
```

---

### Task 5: API models and `CommentsController` endpoints (with integration tests)

**Files:**
- Create: `src/Playr.Api/Models/Comments/SetReactionRequest.cs`
- Create: `src/Playr.Api/Models/Comments/ReactionCountsResponse.cs`
- Create: `src/Playr.Api/Models/Comments/CommentReactionResponse.cs`
- Modify: `src/Playr.Api/Models/Comments/CommentResponse.cs`
- Modify: `src/Playr.Api/Controllers/CommentsController.cs`
- Test: `tests/Playr.IntegrationTests/HttpCommentReactionsFlowTests.cs` (new file)

**Interfaces:**
- Consumes: `ICommentService.SetReactionAsync`/`RemoveReactionAsync`/`GetPagedAsync` (Task 3/4), `Playr.Domain.Comments.ReactionType` (Task 1), `CommentReactionSummary`/`ReactionCounts` (Task 3).
- Produces: `PUT /api/posts/{postId}/comments/{commentId}/reactions` and `DELETE /api/posts/{postId}/comments/{commentId}/reactions` endpoints returning `CommentReactionResponse`.

- [ ] **Step 1: Create `SetReactionRequest`**

```csharp
namespace Playr.Api.Models.Comments;

public sealed record SetReactionRequest(string Type);
```

- [ ] **Step 3: Create `ReactionCountsResponse`**

```csharp
namespace Playr.Api.Models.Comments;

public sealed record ReactionCountsResponse(int Like, int Haha, int Wow, int Sad, int Angry);
```

- [ ] **Step 4: Create `CommentReactionResponse`**

```csharp
namespace Playr.Api.Models.Comments;

public sealed record CommentReactionResponse(ReactionCountsResponse Counts, string? CurrentUserReaction);
```

- [ ] **Step 5: Extend `CommentResponse`**

Replace the full contents of `src/Playr.Api/Models/Comments/CommentResponse.cs`:

```csharp
namespace Playr.Api.Models.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string TextContent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    CommentReactionResponse Reactions);
```

- [ ] **Step 6: Update `CommentsController`**

Replace the full contents of `src/Playr.Api/Controllers/CommentsController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Comments;
using Playr.Application.Comments;
using Playr.Domain.Comments;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/posts/{postId:guid}/comments")]
public sealed class CommentsController(ICommentService commentService) : ControllerBase
{
    private const int DefaultTake = 20;
    private const int MaxTake = 50;

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(Guid postId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var comment = await commentService.CreateAsync(postId, userId, new CreateCommentCommand(request.TextContent), cancellationToken);
            return CreatedAtAction(nameof(GetPaged), new { postId }, ToResponse(comment));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedCommentResponse>> GetPaged(Guid postId, [FromQuery] int skip, [FromQuery] int take, CancellationToken cancellationToken)
    {
        var effectiveTake = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);
        var effectiveSkip = Math.Max(skip, 0);
        Guid? currentUserId = User.TryGetUserId(out var userId) ? userId : null;

        try
        {
            var result = await commentService.GetPagedAsync(postId, currentUserId, effectiveSkip, effectiveTake, cancellationToken);
            return Ok(new PagedCommentResponse(result.Items.Select(ToResponse).ToList(), result.TotalCount, result.HasMore));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{commentId:guid}")]
    public async Task<ActionResult<CommentResponse>> Update(Guid postId, Guid commentId, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var comment = await commentService.UpdateAsync(postId, commentId, userId, new UpdateCommentCommand(request.TextContent), cancellationToken);
            return Ok(ToResponse(comment));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            await commentService.DeleteAsync(postId, commentId, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{commentId:guid}/reactions")]
    public async Task<ActionResult<CommentReactionResponse>> SetReaction(Guid postId, Guid commentId, SetReactionRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        if (!Enum.TryParse<ReactionType>(request.Type, ignoreCase: true, out var type))
        {
            var validValues = string.Join(", ", Enum.GetNames<ReactionType>());
            return BadRequest(new { error = $"Invalid reaction type. Valid values are: {validValues}." });
        }

        try
        {
            var summary = await commentService.SetReactionAsync(postId, commentId, userId, type, cancellationToken);
            return Ok(ToResponse(summary));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{commentId:guid}/reactions")]
    public async Task<ActionResult<CommentReactionResponse>> RemoveReaction(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var summary = await commentService.RemoveReactionAsync(postId, commentId, userId, cancellationToken);
            return Ok(ToResponse(summary));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static CommentResponse ToResponse(CommentDto comment) => new(
        comment.Id,
        comment.PostId,
        comment.AuthorId,
        comment.AuthorUsername,
        comment.AuthorDisplayName,
        comment.AuthorAvatarUrl,
        comment.TextContent,
        comment.CreatedAt,
        comment.UpdatedAt,
        ToResponse(comment.Reactions));

    private static CommentReactionResponse ToResponse(CommentReactionSummary summary) => new(
        new ReactionCountsResponse(summary.Counts.Like, summary.Counts.Haha, summary.Counts.Wow, summary.Counts.Sad, summary.Counts.Angry),
        summary.CurrentUserReaction?.ToString());
}
```

- [ ] **Step 7: Write integration tests**

Create `tests/Playr.IntegrationTests/HttpCommentReactionsFlowTests.cs`. This mirrors the bootstrap pattern of `tests/Playr.IntegrationTests/HttpPostsFlowTests.cs` (same `PlayrWebApplicationFactory` fixture, inline register/login, `MultipartFormDataContent` for post creation):

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Comments;
using Playr.Api.Models.Games;
using Playr.Api.Models.Posts;

namespace Playr.IntegrationTests;

public sealed class HttpCommentReactionsFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private readonly PlayrWebApplicationFactory _factory;

    public HttpCommentReactionsFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string username)
    {
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, username, "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(username, "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.AccessToken;
    }

    private static async Task<(Guid PostId, Guid CommentId)> CreatePostAndCommentAsync(HttpClient client)
    {
        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var form = new MultipartFormDataContent
        {
            { new StringContent(gameId.ToString()), "GameId" },
            { new StringContent("A post to comment on"), "TextContent" },
        };
        var createPostResponse = await client.PostAsync("/api/posts", form);
        var post = await createPostResponse.Content.ReadFromJsonAsync<PostResponse>();

        var createCommentResponse = await client.PostAsJsonAsync(
            $"/api/posts/{post!.Id}/comments", new CreateCommentRequest("Nice post!"));
        var comment = await createCommentResponse.Content.ReadFromJsonAsync<CommentResponse>();

        return (post.Id, comment!.Id);
    }

    [Fact]
    public async Task SetReaction_then_GetPaged_reflects_count_and_current_user_reaction()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor1@example.com", "reactor1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        var reactResponse = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));
        reactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await reactResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Like.Should().Be(1);
        reaction.CurrentUserReaction.Should().Be("Like");

        var pagedResponse = await client.GetAsync($"/api/posts/{postId}/comments");
        pagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await pagedResponse.Content.ReadFromJsonAsync<PagedCommentResponse>();
        var listedComment = paged!.Items.Single(c => c.Id == commentId);
        listedComment.Reactions.Counts.Like.Should().Be(1);
        listedComment.Reactions.CurrentUserReaction.Should().Be("Like");
    }

    [Fact]
    public async Task SetReaction_twice_with_same_type_toggles_it_off()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor2@example.com", "reactor2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        await client.PutAsJsonAsync($"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));
        var secondResponse = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await secondResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Like.Should().Be(0);
        reaction.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task SetReaction_without_authentication_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/api/posts/{Guid.NewGuid()}/comments/{Guid.NewGuid()}/reactions", new SetReactionRequest("Like"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetReaction_on_unknown_comment_returns_404()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor3@example.com", "reactor3");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, _) = await CreatePostAndCommentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{Guid.NewGuid()}/reactions", new SetReactionRequest("Like"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetReaction_with_invalid_type_returns_400()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor4@example.com", "reactor4");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Excited"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteReaction_removes_existing_reaction()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor5@example.com", "reactor5");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        await client.PutAsJsonAsync($"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Angry"));
        var deleteResponse = await client.DeleteAsync($"/api/posts/{postId}/comments/{commentId}/reactions");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await deleteResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Angry.Should().Be(0);
        reaction.CurrentUserReaction.Should().BeNull();
    }
}
```

- [ ] **Step 8: Run the new integration tests**

Run: `dotnet test tests\Playr.IntegrationTests\Playr.IntegrationTests.csproj --filter "FullyQualifiedName~HttpCommentReactionsFlowTests"`
Expected: `Passed!` for all 6 tests.

- [ ] **Step 9: Run the full solution test suite to check for regressions**

Run: `dotnet test`
Expected: All tests pass, including previously-existing `Playr.Application.Tests` and `Playr.IntegrationTests` suites.

- [ ] **Step 10: Commit**

```bash
git add src/Playr.Api/Models/Comments src/Playr.Api/Controllers/CommentsController.cs tests/Playr.IntegrationTests/HttpCommentReactionsFlowTests.cs
git commit -m "feat: add comment reaction endpoints with integration tests"
```

---

### Task 6: Full solution build and final verification

**Files:**
- None (verification only).

**Interfaces:**
- Consumes: everything from Tasks 1–5.
- Produces: nothing new — confirms the whole solution builds and all tests pass end-to-end.

- [ ] **Step 1: Build the full solution**

Run: `dotnet build`
Expected: `Build succeeded.` with no errors or new warnings related to `Comments` or `CommentReaction`.

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test`
Expected: All tests pass (0 failed).

- [ ] **Step 3: Manually confirm migration applies cleanly against a throwaway SQLite/Postgres check (optional but recommended)**

Run: `dotnet ef migrations script --project src\Playr.Infrastructure --startup-project src\Playr.Api --idempotent`
Expected: A SQL script is generated without errors, and it includes `CREATE TABLE "CommentReactions"` with the composite primary key and foreign keys described in Task 2.

- [ ] **Step 4: Commit any final fixups (only if changes were needed)**

```bash
git status
```
If there are no pending changes, skip committing — Task 5's commit is the final one for this feature.
