# PLAYR Edit + Delete Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let post authors edit (text + mood, inline) and delete their own posts via a `...` dropdown on each PostCard.

**Architecture:** Backend adds `UpdateAsync`/`DeleteAsync` to `PostService` + two new controller actions (`PUT /api/posts/{id}`, `DELETE /api/posts/{id}`); CORS gets `DELETE` added. Frontend adds `updatePost`/`deletePost` API wrappers and rewrites `PostCard` with a four-state machine (read → menu-open → editing / confirming-delete); `FeedPage` passes owner identity + callbacks down.

**Tech Stack:** .NET 10, EF Core 10, xUnit + FluentAssertions + SQLite (backend tests); React 18 + TS, Vitest + RTL (frontend tests).

## Global Constraints

- Backend repo: `C:\NoBackup\development\Playr`, branch `feature/edit-delete-post` (already created).
- Frontend repo: `C:\NoBackup\development\playr-frontend`, create branch `feature/edit-delete-post` before Task 5.
- Windows / PowerShell 5.1: no `&&`, use `;` or `if ($?) { }`.
- Backend tests: `dotnet test` (all) or `dotnet test tests\Playr.Application.Tests` / `dotnet test tests\Playr.IntegrationTests`. One pre-existing failing test (`RegisterAsync_WhenIdentityRejectsPassword`) — ignore it throughout.
- Frontend tests: `npm test` (all) or `npm test -- <pattern>`. Build: `npm run build`.
- Every frontend test file must import vitest globals explicitly as the first line: `import { describe, it, expect, vi, beforeEach } from 'vitest'`.
- Tailwind tokens: `bg-bg`, `bg-surface`, `bg-surface-raised`, `border-border`, `text-text`, `text-muted`, `text-primary`, `text-frustrated`. All in `src/index.css @theme`.
- Error message strings (exact, used in tests): `"Post was not found."`, `"You are not allowed to edit this post."`, `"You are not allowed to delete this post."`, `"Post text is required."`, `"Post text cannot be longer than 1000 characters."`, `"Invalid mood value."`.
- HTTP error mapping: `"Post was not found."` → 404; `"You are not allowed to..."` → 403; other `InvalidOperationException` → 400; missing user-id claim → 401.
- `PostDto` record (existing): `(Guid Id, Guid AuthorId, string AuthorUsername, string AuthorDisplayName, string? AuthorAvatarUrl, Guid GameId, string GameName, string? GameCoverImageUrl, string TextContent, string? Mood, DateTimeOffset CreatedAt)`.
- `PostFeedItem` interface (existing in `src/api/postsApi.ts`): same fields in camelCase.
- Commit message convention: `feat:`, `fix:`, `chore:`, `test:`.

---

### Task 1: Backend — UpdatePostCommand + IPostService + PostService.UpdateAsync + PostService.DeleteAsync

**Files:**
- Create: `src/Playr.Application/Posts/UpdatePostCommand.cs`
- Modify: `src/Playr.Application/Posts/IPostService.cs`
- Modify: `src/Playr.Infrastructure/Posts/PostService.cs`
- Test: `tests/Playr.Application.Tests/Posts/PostEditDeleteServiceTests.cs`

**Interfaces:**
- Consumes: `PlayrDbContext` (existing), `PostMood` enum (existing), `PostDto` record (existing), `MapToPostDtoAsync` private method already in `PostService`.
- Produces:
  - `UpdatePostCommand` record: `(string TextContent, string? Mood)`
  - `IPostService.UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken) → Task<PostDto>`
  - `IPostService.DeleteAsync(Guid postId, Guid requesterId, CancellationToken) → Task`

- [ ] **Step 1: Write the failing tests**

`tests/Playr.Application.Tests/Posts/PostEditDeleteServiceTests.cs`:
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

public sealed class PostEditDeleteServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _authorId;
    private readonly Guid _otherId;
    private readonly Guid _gameId;
    private Guid _postId;

    public PostEditDeleteServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _otherId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _authorId, Email = "author@example.com", UserName = "author",
            NormalizedEmail = "AUTHOR@EXAMPLE.COM", NormalizedUserName = "AUTHOR",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _authorId, Username = "author", DisplayName = "Author",
        });
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _otherId, Email = "other@example.com", UserName = "other",
            NormalizedEmail = "OTHER@EXAMPLE.COM", NormalizedUserName = "OTHER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _otherId, Username = "other", DisplayName = "Other",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        var post = new Post
        {
            Id = Guid.NewGuid(), AuthorId = _authorId, GameId = _gameId,
            TextContent = "Original text", Mood = PostMood.Enjoying,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _dbContext.Posts.Add(post);
        _dbContext.SaveChanges();
        _postId = post.Id;
        _service = new PostService(_dbContext);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTextAndMood()
    {
        var command = new UpdatePostCommand("Updated text", "Completed");
        var result = await _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);

        result.TextContent.Should().Be("Updated text");
        result.Mood.Should().Be("Completed");
        result.AuthorUsername.Should().Be("author");
        result.GameName.Should().Be("Hollow Knight");
    }

    [Fact]
    public async Task UpdateAsync_WithNullMood_ClearsMood()
    {
        var command = new UpdatePostCommand("Some text", null);
        var result = await _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);

        result.Mood.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyText_Throws()
    {
        var command = new UpdatePostCommand("   ", null);
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text is required.");
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongText_Throws()
    {
        var command = new UpdatePostCommand(new string('x', 1001), null);
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text cannot be longer than 1000 characters.");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidMood_Throws()
    {
        var command = new UpdatePostCommand("Hello", "Raging");
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid mood value.");
    }

    [Fact]
    public async Task UpdateAsync_WhenPostNotFound_Throws()
    {
        var command = new UpdatePostCommand("Hello", null);
        var act = () => _service.UpdateAsync(Guid.NewGuid(), _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post was not found.");
    }

    [Fact]
    public async Task UpdateAsync_WhenRequesterIsNotAuthor_Throws()
    {
        var command = new UpdatePostCommand("Hello", null);
        var act = () => _service.UpdateAsync(_postId, _otherId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are not allowed to edit this post.");
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthor_RemovesPost()
    {
        await _service.DeleteAsync(_postId, _authorId, CancellationToken.None);
        var post = await _dbContext.Posts.FindAsync(_postId);
        post.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenPostNotFound_Throws()
    {
        var act = () => _service.DeleteAsync(Guid.NewGuid(), _authorId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post was not found.");
    }

    [Fact]
    public async Task DeleteAsync_WhenRequesterIsNotAuthor_Throws()
    {
        var act = () => _service.DeleteAsync(_postId, _otherId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are not allowed to delete this post.");
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
dotnet test tests\Playr.Application.Tests --filter PostEditDeleteServiceTests
```
Expected: compile errors — `UpdatePostCommand` and `UpdateAsync`/`DeleteAsync` not found.

- [ ] **Step 3: Create UpdatePostCommand**

`src/Playr.Application/Posts/UpdatePostCommand.cs`:
```csharp
namespace Playr.Application.Posts;

public sealed record UpdatePostCommand(
    string TextContent,
    string? Mood);
```

- [ ] **Step 4: Add UpdateAsync and DeleteAsync to IPostService**

Replace the entire `src/Playr.Application/Posts/IPostService.cs`:
```csharp
namespace Playr.Application.Posts;

public interface IPostService
{
    Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken);
    Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement UpdateAsync and DeleteAsync in PostService**

Add these two methods to `src/Playr.Infrastructure/Posts/PostService.cs` (after the existing `GetFeedAsync` method, before the private `MapToPostDtoAsync`):
```csharp
    public async Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken)
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

        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to edit this post.");

        post.TextContent = text;
        post.Mood = mood;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToPostDtoAsync([post], cancellationToken);
        return dtos[0];
    }

    public async Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to delete this post.");

        dbContext.Posts.Remove(post);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
```

- [ ] **Step 6: Run tests — expect PASS**

```
dotnet test tests\Playr.Application.Tests
```
Expected: 10 new tests pass; all pre-existing tests pass (ignore the 1 pre-existing failure).

- [ ] **Step 7: Commit**

```
git add src\Playr.Application\Posts\UpdatePostCommand.cs src\Playr.Application\Posts\IPostService.cs src\Playr.Infrastructure\Posts\PostService.cs tests\Playr.Application.Tests\Posts\PostEditDeleteServiceTests.cs
git commit -m "feat: add UpdateAsync and DeleteAsync to PostService"
```

---

### Task 2: Backend — API models, controller actions, CORS fix

**Files:**
- Create: `src/Playr.Api/Models/Posts/UpdatePostRequest.cs`
- Modify: `src/Playr.Api/Controllers/PostsController.cs`
- Modify: `src/Playr.Api/Program.cs`
- Test: `tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs` (add tests to existing class)

**Interfaces:**
- Consumes: `UpdatePostCommand` (Task 1), `IPostService.UpdateAsync`/`DeleteAsync` (Task 1), `TryGetUserId` (existing).
- Produces:
  - `PUT /api/posts/{id}` `[Authorize]` → 200 `PostResponse` / 400 / 401 / 403 / 404
  - `DELETE /api/posts/{id}` `[Authorize]` → 204 / 401 / 403 / 404
  - CORS allows `DELETE`

- [ ] **Step 1: Write failing tests**

Add these three test methods to the existing class in `tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs` (before the final closing `}`):

```csharp
    [Fact]
    public void Posts_controller_has_put_and_delete_endpoints_requiring_auth()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.PostsController");
        controller.Should().NotBeNull();

        // PUT /api/posts/{id} requires auth
        controller!.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpPutAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);

        // DELETE /api/posts/{id} requires auth
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);
    }

    [Fact]
    public async Task UpdatePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Update(
            Guid.NewGuid(),
            new Playr.Api.Models.Posts.UpdatePostRequest("Hello", null),
            CancellationToken.None);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    [Fact]
    public async Task DeletePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }
```

Also update `ThrowingPostService` at the bottom of the file to implement the new interface methods (it must not compile otherwise). Replace the existing `ThrowingPostService` with:
```csharp
    private sealed class ThrowingPostService : IPostService
    {
        public Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
    }
```

- [ ] **Step 2: Run tests — expect FAIL**

```
dotnet test tests\Playr.IntegrationTests --filter "Posts_controller_has_put_and_delete"
```
Expected: FAIL — `Update` and `Delete` methods not found on controller.

- [ ] **Step 3: Create UpdatePostRequest**

`src/Playr.Api/Models/Posts/UpdatePostRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Posts;

public sealed record UpdatePostRequest(
    [Required][StringLength(1000, MinimumLength = 1)] string TextContent,
    string? Mood);
```

- [ ] **Step 4: Add Update and Delete to PostsController**

Add these two action methods to `src/Playr.Api/Controllers/PostsController.cs` after the existing `GetFeed` method (before the closing `}` of the class):
```csharp
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostResponse>> Update(Guid id, UpdatePostRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var post = await postService.UpdateAsync(id, userId,
                new UpdatePostCommand(request.TextContent, request.Mood),
                cancellationToken);
            return Ok(ToResponse(post));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
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
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            await postService.DeleteAsync(id, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
```

Also add the `UpdatePostRequest` using directive at the top of `PostsController.cs` — it's already in `Playr.Api.Models.Posts` namespace so it's already covered by the existing `using Playr.Api.Models.Posts;` if present. If that using is not there, add it.

- [ ] **Step 5: Add DELETE to CORS policy**

In `src/Playr.Api/Program.cs`, find:
```csharp
            .WithMethods("GET", "POST", "PUT", "OPTIONS")
```
Replace with:
```csharp
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
```

- [ ] **Step 6: Run all tests — expect PASS**

```
dotnet test
```
Expected: all tests pass (ignore the 1 pre-existing failure). The 3 new endpoint tests pass.

- [ ] **Step 7: Commit**

```
git add src\Playr.Api\Models\Posts\UpdatePostRequest.cs src\Playr.Api\Controllers\PostsController.cs src\Playr.Api\Program.cs tests\Playr.IntegrationTests\GamesAndPostsEndpointConfigurationTests.cs
git commit -m "feat: add PUT and DELETE /api/posts/{id} endpoints, add DELETE to CORS"
```

---

### Task 3: Backend — HTTP integration test for edit + delete flow

**Files:**
- Modify: `tests/Playr.IntegrationTests/HttpPostsFlowTests.cs` (add tests to existing class)

**Interfaces:**
- Consumes: `PUT /api/posts/{id}`, `DELETE /api/posts/{id}`, `PlayrWebApplicationFactory` (existing).
- Produces: end-to-end tests verifying the full edit + delete flow through the HTTP pipeline.

- [ ] **Step 1: Add three test methods to `HttpPostsFlowTests`**

Add the following inside the existing `HttpPostsFlowTests` class (before the closing `}`):
```csharp
    [Fact]
    public async Task Can_edit_own_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("editor@example.com", "editor", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("editor", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var createResponse = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(gameId, "Original text", null));
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/posts/{created!.Id}",
            new UpdatePostRequest("Edited text", "Completed"));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PostResponse>();
        updated!.TextContent.Should().Be("Edited text");
        updated.Mood.Should().Be("Completed");
    }

    [Fact]
    public async Task Cannot_edit_another_users_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("owner2@example.com", "owner2", "Password123"));
        var ownerLogin = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("owner2", "Password123"));
        var ownerToken = (await ownerLogin.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createResponse = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(gameId, "Owner's post", null));
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("intruder2@example.com", "intruder2", "Password123"));
        var intruderLogin = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("intruder2", "Password123"));
        var intruderToken = (await intruderLogin.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken);
        var updateResponse = await client.PutAsJsonAsync($"/api/posts/{created!.Id}",
            new UpdatePostRequest("Hacked!", null));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Can_delete_own_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("deleter@example.com", "deleter", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("deleter", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var createResponse = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(gameId, "To be deleted", null));
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/posts/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var feed = await client.GetAsync("/api/posts");
        var posts = await feed.Content.ReadFromJsonAsync<List<PostResponse>>();
        posts!.Should().NotContain(p => p.Id == created.Id);
    }
```

Also add `using Playr.Api.Models.Posts;` at the top of the file if not already present. `UpdatePostRequest` lives there.

- [ ] **Step 2: Run tests — expect PASS**

```
dotnet test tests\Playr.IntegrationTests
```
Expected: 3 new tests pass; all 21 prior integration tests still pass. Total: 24.

- [ ] **Step 3: Commit**

```
git add tests\Playr.IntegrationTests\HttpPostsFlowTests.cs
git commit -m "test: add HTTP integration tests for edit and delete post"
```

---

### Task 4: Merge backend to main

- [ ] **Step 1: Run full test suite**

```
dotnet test
```
Expected: all pass (1 pre-existing failure excluded).

- [ ] **Step 2: Merge**

```
git checkout main
git merge --no-ff feature/edit-delete-post -m "Merge feature/edit-delete-post: edit and delete post backend"
```

- [ ] **Step 3: Verify on main**

```
dotnet test
```
Expected: all pass.

- [ ] **Step 4: Delete branch**

```
git branch -d feature/edit-delete-post
```

---

### Task 5: Frontend — updatePost + deletePost in postsApi.ts

**Files:**
- Modify: `src/api/postsApi.ts`
- Modify: `src/api/postsApi.test.ts`

**Working directory:** `C:\NoBackup\development\playr-frontend`. Create branch first:
```
git checkout -b feature/edit-delete-post
```

**Interfaces:**
- Consumes: `API_BASE_URL`, `ApiError`, `parseErrorMessage` from `./http`, `PostFeedItem` (existing in this file).
- Produces:
  - `updatePost(token: string, postId: string, data: { textContent: string; mood?: string | null }): Promise<PostFeedItem>` — `PUT /api/posts/{postId}` with bearer token.
  - `deletePost(token: string, postId: string): Promise<void>` — `DELETE /api/posts/{postId}` with bearer token.

- [ ] **Step 1: Add failing tests to postsApi.test.ts**

Add these test blocks at the end of `src/api/postsApi.test.ts` (before the last closing line if any, otherwise just append):
```ts
describe('updatePost', () => {
  it('sends PUT with bearer token and returns updated post', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ ...samplePost, textContent: 'Edited!' }) })
    const result = await updatePost('tok', 'p1', { textContent: 'Edited!', mood: 'Completed' })
    expect(result.textContent).toBe('Edited!')
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/posts/p1'),
      expect.objectContaining({
        method: 'PUT',
        headers: expect.objectContaining({ Authorization: 'Bearer tok' }),
      })
    )
  })

  it('throws ApiError on 403', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 403, json: async () => ({ error: 'You are not allowed to edit this post.' }) })
    await expect(updatePost('tok', 'p1', { textContent: 'x' })).rejects.toBeInstanceOf(ApiError)
  })
})

describe('deletePost', () => {
  it('sends DELETE with bearer token', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, status: 204, json: async () => ({}) })
    await deletePost('tok', 'p1')
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/posts/p1'),
      expect.objectContaining({
        method: 'DELETE',
        headers: expect.objectContaining({ Authorization: 'Bearer tok' }),
      })
    )
  })

  it('throws ApiError on 403', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 403, json: async () => ({ error: 'You are not allowed to delete this post.' }) })
    await expect(deletePost('tok', 'p1')).rejects.toBeInstanceOf(ApiError)
  })
})
```

Also add `updatePost` and `deletePost` to the import line at the top of the test file:
```ts
import { createPost, getFeed, updatePost, deletePost } from './postsApi'
```

- [ ] **Step 2: Run tests — expect FAIL**

```
npm test -- postsApi
```
Expected: FAIL — `updatePost` and `deletePost` not exported.

- [ ] **Step 3: Add updatePost and deletePost to postsApi.ts**

Append to `src/api/postsApi.ts` (after the existing `getFeed` function):
```ts
export async function updatePost(
  token: string,
  postId: string,
  data: { textContent: string; mood?: string | null }
): Promise<PostFeedItem> {
  const response = await fetch(`${API_BASE_URL}/api/posts/${postId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(data),
  })
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to update post.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}

export async function deletePost(token: string, postId: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/posts/${postId}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${token}` },
  })
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to delete post.')
    throw new ApiError(response.status, message)
  }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
npm test -- postsApi
```
Expected: all 7 postsApi tests pass (3 existing + 4 new).

- [ ] **Step 5: Run full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/api/postsApi.ts src/api/postsApi.test.ts
git commit -m "feat: add updatePost and deletePost to postsApi"
```

---

### Task 6: Frontend — PostCard with ... menu, inline edit, inline delete confirm

**Files:**
- Modify: `src/components/PostCard.tsx`
- Modify: `src/components/PostCard.test.tsx`

**Interfaces:**
- Consumes: `updatePost`, `deletePost` from `../api/postsApi`; `ApiError` from `../api/http`; `IconButton` from `./ui/IconButton`; `Button` from `./ui/Button`; `Badge`, `Avatar` (existing); `PostFeedItem` (existing).
- Produces: `PostCard` with these additional optional props:
  - `currentUserId?: string`
  - `onDelete?: (postId: string) => void`
  - `onUpdate?: (post: PostFeedItem) => void`
  Internal state: `'read' | 'menu-open' | 'editing' | 'confirming-delete'`
  - `...` button only visible when `currentUserId === post.authorId`
  - Edit: pre-filled textarea + mood picker + Save/Cancel
  - Delete: "Delete this post?" + Delete/Cancel

- [ ] **Step 1: Write failing tests**

Replace the entire `src/components/PostCard.test.tsx` with:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PostCard } from './PostCard'
import type { PostFeedItem } from '../api/postsApi'
import * as postsApi from '../api/postsApi'

vi.mock('../api/postsApi')

const base: PostFeedItem = {
  id: 'p1', authorId: 'a1', authorUsername: 'nexusnova', authorDisplayName: 'NexusNova',
  authorAvatarUrl: null, gameId: 'g1', gameName: 'Elden Ring', gameCoverImageUrl: null,
  textContent: 'Finally beat Radahn!', mood: 'Enjoying',
  createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
}

beforeEach(() => { vi.resetAllMocks() })

describe('PostCard — read mode', () => {
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

  it('renders mood badge', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText('Enjoying')).toBeInTheDocument()
  })

  it('renders no mood badge when mood is null', () => {
    render(<PostCard post={{ ...base, mood: null }} />)
    expect(screen.queryByText('Enjoying')).not.toBeInTheDocument()
  })

  it('maps NeedHelp mood to need-help badge variant', () => {
    render(<PostCard post={{ ...base, mood: 'NeedHelp' }} />)
    expect(screen.getByText('Need Help')).toHaveAttribute('data-variant', 'need-help')
  })

  it('renders a relative timestamp', () => {
    render(<PostCard post={base} />)
    expect(screen.getByText(/ago/i)).toBeInTheDocument()
  })
})

describe('PostCard — ... menu', () => {
  it('does not show ... button when currentUserId differs from authorId', () => {
    render(<PostCard post={base} currentUserId="other-user" />)
    expect(screen.queryByRole('button', { name: /post options/i })).not.toBeInTheDocument()
  })

  it('shows ... button when currentUserId matches authorId', () => {
    render(<PostCard post={base} currentUserId="a1" />)
    expect(screen.getByRole('button', { name: /post options/i })).toBeInTheDocument()
  })

  it('opens dropdown with Edit and Delete on ... click', async () => {
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^delete$/i })).toBeInTheDocument()
  })
})

describe('PostCard — edit mode', () => {
  it('switches to editing on Edit click with pre-filled textarea', async () => {
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^edit$/i }))
    const textarea = screen.getByRole('textbox', { name: /edit post text/i })
    expect(textarea).toHaveValue('Finally beat Radahn!')
  })

  it('Cancel in edit mode returns to read state', async () => {
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^edit$/i }))
    await user.click(screen.getByRole('button', { name: /cancel/i }))
    expect(screen.getByText('Finally beat Radahn!')).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: /edit post text/i })).not.toBeInTheDocument()
  })

  it('Save calls updatePost and calls onUpdate with result', async () => {
    const updatedPost: PostFeedItem = { ...base, textContent: 'Updated!', mood: null }
    vi.mocked(postsApi.updatePost).mockResolvedValueOnce(updatedPost)
    const onUpdate = vi.fn()
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" onUpdate={onUpdate} />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^edit$/i }))
    const textarea = screen.getByRole('textbox', { name: /edit post text/i })
    await user.clear(textarea)
    await user.type(textarea, 'Updated!')
    await user.click(screen.getByRole('button', { name: /save/i }))
    await waitFor(() => expect(onUpdate).toHaveBeenCalledWith(updatedPost))
  })
})

describe('PostCard — delete confirm', () => {
  it('switches to confirming-delete on Delete click', async () => {
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^delete$/i }))
    expect(screen.getByText(/delete this post/i)).toBeInTheDocument()
  })

  it('Cancel in confirm mode returns to read state', async () => {
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^delete$/i }))
    await user.click(screen.getByRole('button', { name: /cancel/i }))
    expect(screen.queryByText(/delete this post/i)).not.toBeInTheDocument()
    expect(screen.getByText('Finally beat Radahn!')).toBeInTheDocument()
  })

  it('Delete calls deletePost and calls onDelete with postId', async () => {
    vi.mocked(postsApi.deletePost).mockResolvedValueOnce(undefined)
    const onDelete = vi.fn()
    const user = userEvent.setup()
    render(<PostCard post={base} currentUserId="a1" onDelete={onDelete} />)
    await user.click(screen.getByRole('button', { name: /post options/i }))
    await user.click(screen.getByRole('button', { name: /^delete$/i }))
    await user.click(screen.getByRole('button', { name: /confirm delete/i }))
    await waitFor(() => expect(onDelete).toHaveBeenCalledWith('p1'))
  })
})
```

- [ ] **Step 2: Run tests — expect FAIL**

```
npm test -- PostCard
```
Expected: several failures — menu buttons not present, no edit/delete logic.

- [ ] **Step 3: Implement new PostCard**

Replace the entire `src/components/PostCard.tsx`:
```tsx
import { useState, useEffect, useRef } from 'react'
import { Avatar } from './ui/Avatar'
import { Badge } from './ui/Badge'
import { Button } from './ui/Button'
import { IconButton } from './ui/IconButton'
import { MoreHorizontal } from 'lucide-react'
import { updatePost, deletePost } from '../api/postsApi'
import { ApiError } from '../api/http'
import type { PostFeedItem } from '../api/postsApi'
import type { ComponentProps } from 'react'

type BadgeVariant = ComponentProps<typeof Badge>['variant']
type CardState = 'read' | 'menu-open' | 'editing' | 'confirming-delete'
type MoodOption = 'None' | 'Enjoying' | 'Frustrated' | 'Completed' | 'Need Help'

const MOOD_OPTIONS: MoodOption[] = ['None', 'Enjoying', 'Frustrated', 'Completed', 'Need Help']

function moodBadge(mood: string | null): { label: string; variant: BadgeVariant } | null {
  switch (mood) {
    case 'Enjoying':   return { label: 'Enjoying',   variant: 'enjoying'   }
    case 'NeedHelp':   return { label: 'Need Help',  variant: 'need-help'  }
    case 'Frustrated': return { label: 'Frustrated', variant: 'frustrated' }
    case 'Completed':  return { label: 'Completed',  variant: 'completed'  }
    default: return null
  }
}

function apiMoodToOption(mood: string | null): MoodOption {
  switch (mood) {
    case 'Enjoying':   return 'Enjoying'
    case 'NeedHelp':   return 'Need Help'
    case 'Frustrated': return 'Frustrated'
    case 'Completed':  return 'Completed'
    default:           return 'None'
  }
}

function moodOptionToApi(mood: MoodOption): string | null {
  if (mood === 'None') return null
  if (mood === 'Need Help') return 'NeedHelp'
  return mood
}

function formatRelativeTime(createdAt: string): string {
  const diffMs = Date.now() - new Date(createdAt).getTime()
  const diffMin = Math.floor(diffMs / 60_000)
  if (diffMin < 60) return `${Math.max(diffMin, 1)}m ago`
  const diffH = Math.floor(diffMin / 60)
  if (diffH < 24) return `${diffH}h ago`
  return `${Math.floor(diffH / 24)}d ago`
}

interface PostCardProps {
  post: PostFeedItem
  currentUserId?: string
  onDelete?: (postId: string) => void
  onUpdate?: (post: PostFeedItem) => void
}

export function PostCard({ post, currentUserId, onDelete, onUpdate }: PostCardProps) {
  const [state, setState] = useState<CardState>('read')
  const [editText, setEditText] = useState(post.textContent)
  const [editMood, setEditMood] = useState<MoodOption>(apiMoodToOption(post.mood))
  const [actionError, setActionError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  const isOwner = currentUserId != null && currentUserId === post.authorId
  const badge = moodBadge(post.mood)

  // Close menu on outside click
  useEffect(() => {
    if (state !== 'menu-open') return
    function handleMouseDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setState('read')
      }
    }
    document.addEventListener('mousedown', handleMouseDown)
    return () => document.removeEventListener('mousedown', handleMouseDown)
  }, [state])

  async function handleSave() {
    setActionError(null)
    setIsSaving(true)
    try {
      const updated = await updatePost(
        // token is read from localStorage by callers that have it; we pass
        // undefined here and rely on the fact that FeedPage users are always
        // authenticated when onUpdate is provided — use localStorage directly
        localStorage.getItem('playr_token') ?? '',
        post.id,
        { textContent: editText.trim(), mood: moodOptionToApi(editMood) }
      )
      onUpdate?.(updated)
      setState('read')
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to update post.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete() {
    setActionError(null)
    setIsDeleting(true)
    try {
      await deletePost(localStorage.getItem('playr_token') ?? '', post.id)
      onDelete?.(post.id)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to delete post.')
      setIsDeleting(false)
    }
  }

  function openEdit() {
    setEditText(post.textContent)
    setEditMood(apiMoodToOption(post.mood))
    setActionError(null)
    setState('editing')
  }

  return (
    <div className="rounded-xl border border-border bg-surface p-4 flex flex-col gap-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Avatar src={post.authorAvatarUrl ?? undefined} alt={post.authorDisplayName} />
          <div>
            <p className="text-sm font-semibold text-text">{post.authorDisplayName}</p>
            <p className="text-xs text-muted">@{post.authorUsername}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {state === 'read' && badge && <Badge variant={badge.variant}>{badge.label}</Badge>}
          {isOwner && state === 'read' && (
            <div className="relative" ref={menuRef}>
              <IconButton
                aria-label="Post options"
                onClick={() => setState(state === 'menu-open' ? 'read' : 'menu-open')}
              >
                <MoreHorizontal className="h-4 w-4" aria-hidden="true" />
              </IconButton>
              {state === 'menu-open' && (
                <div className="absolute right-0 top-10 z-10 min-w-[120px] rounded-lg border border-border bg-surface-raised shadow-lg">
                  <button
                    className="w-full px-4 py-2 text-left text-sm text-text hover:bg-border rounded-t-lg"
                    onClick={openEdit}
                  >
                    Edit
                  </button>
                  <button
                    className="w-full px-4 py-2 text-left text-sm text-frustrated hover:bg-border rounded-b-lg"
                    onClick={() => { setActionError(null); setState('confirming-delete') }}
                  >
                    Delete
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Game */}
      <p className="text-xs font-medium text-primary">{post.gameName}</p>

      {/* Content: read / editing / confirming-delete */}
      {state === 'editing' ? (
        <div className="flex flex-col gap-3">
          {/* Mood picker */}
          <div className="flex flex-wrap gap-2">
            {MOOD_OPTIONS.map((mood) => (
              <button
                key={mood}
                type="button"
                aria-pressed={editMood === mood}
                onClick={() => setEditMood(mood)}
                className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                  editMood === mood ? 'bg-primary text-white' : 'bg-surface-raised text-muted hover:text-text'
                }`}
              >
                {mood}
              </button>
            ))}
          </div>
          {/* Textarea */}
          <textarea
            aria-label="Edit post text"
            className="rounded-lg border border-border bg-surface-raised px-3 py-2 text-sm text-text resize-none h-28 outline-none focus:border-primary"
            value={editText}
            maxLength={1000}
            onChange={(e) => setEditText(e.target.value)}
          />
          <span className="text-xs text-muted self-end">{editText.length} / 1000</span>
          {actionError && <p className="text-frustrated text-xs">{actionError}</p>}
          <div className="flex gap-2">
            <Button size="sm" onClick={handleSave} disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save'}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => { setState('read'); setActionError(null) }}>
              Cancel
            </Button>
          </div>
        </div>
      ) : state === 'confirming-delete' ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-text">Delete this post?</p>
          {actionError && <p className="text-frustrated text-xs">{actionError}</p>}
          <div className="flex gap-2">
            <Button
              size="sm"
              aria-label="Confirm delete"
              className="bg-frustrated hover:bg-frustrated/80 shadow-none"
              onClick={handleDelete}
              disabled={isDeleting}
            >
              {isDeleting ? 'Deleting…' : 'Delete'}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => { setState('read'); setActionError(null) }}>
              Cancel
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-sm text-text leading-relaxed">{post.textContent}</p>
      )}

      {/* Timestamp (always shown) */}
      <p className="text-xs text-muted">{formatRelativeTime(post.createdAt)}</p>
    </div>
  )
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
npm test -- PostCard
```
Expected: all tests pass.

- [ ] **Step 5: Run full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/components/PostCard.tsx src/components/PostCard.test.tsx
git commit -m "feat: add edit and delete to PostCard with inline state machine"
```

---

### Task 7: Frontend — FeedPage passes currentUserId + callbacks

**Files:**
- Modify: `src/pages/FeedPage.tsx`
- Modify: `src/pages/FeedPage.test.tsx`

**Interfaces:**
- Consumes: `useAuth` from `../context/AuthContext` (returns `{ user: { id, username, ... } | null, ... }`); `PostCard` (Task 6) now accepts `currentUserId`, `onDelete`, `onUpdate`.
- Produces: `FeedPage` that passes `currentUserId={user?.id}`, `onDelete` (filter post out of state), `onUpdate` (replace post in state) to each `PostCard`.

- [ ] **Step 1: Add failing test**

Add to `src/pages/FeedPage.test.tsx` (after the existing tests, inside the file — add the import for `useAuth` mock at the top if not present):

First add at the top of the file (after existing mocks):
```ts
vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({ user: { id: 'a1', username: 'player', displayName: 'Player', email: 'p@p.com' }, token: 'tok', isLoading: false }),
}))
```

Then add this test:
```ts
it('removes a post from the list when onDelete is called', async () => {
  const user = userEvent.setup()
  vi.mocked(postsApi.getFeed).mockResolvedValue([samplePost])
  vi.mocked(postsApi.deletePost).mockResolvedValue(undefined)
  renderFeed()
  await waitFor(() => expect(screen.getByText('Finally cleared it!')).toBeInTheDocument())
  // The ... button appears because currentUserId matches authorId ('a1')
  await user.click(screen.getByRole('button', { name: /post options/i }))
  await user.click(screen.getByRole('button', { name: /^delete$/i }))
  await user.click(screen.getByRole('button', { name: /confirm delete/i }))
  await waitFor(() => expect(screen.queryByText('Finally cleared it!')).not.toBeInTheDocument())
})
```

Also add `import userEvent from '@testing-library/user-event'` to the imports if not present, and add `deletePost` to the postsApi mock import.

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- FeedPage
```
Expected: FAIL — `currentUserId` not passed, `...` button not rendered.

- [ ] **Step 3: Update FeedPage**

Replace entire `src/pages/FeedPage.tsx`:
```tsx
import { useEffect, useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { PostCard } from '../components/PostCard'
import { Button } from '../components/ui/Button'
import { getFeed, type PostFeedItem } from '../api/postsApi'
import { useAuth } from '../context/AuthContext'

export default function FeedPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [posts, setPosts] = useState<PostFeedItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getFeed()
      .then(setPosts)
      .catch(() => setError('Failed to load feed.'))
      .finally(() => setIsLoading(false))
  }, [])

  const handleDelete = useCallback((postId: string) => {
    setPosts((prev) => prev.filter((p) => p.id !== postId))
  }, [])

  const handleUpdate = useCallback((updated: PostFeedItem) => {
    setPosts((prev) => prev.map((p) => (p.id === updated.id ? updated : p)))
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
        <PostCard
          key={post.id}
          post={post}
          currentUserId={user?.id}
          onDelete={handleDelete}
          onUpdate={handleUpdate}
        />
      ))}
    </div>
  )
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
npm test -- FeedPage
```
Expected: all FeedPage tests pass.

- [ ] **Step 5: Run full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/pages/FeedPage.tsx src/pages/FeedPage.test.tsx
git commit -m "feat: pass currentUserId and edit/delete callbacks from FeedPage to PostCard"
```

---

### Task 8: Merge frontend to main

- [ ] **Step 1: Run full test suite**

```
npm test
```
Expected: all pass.

- [ ] **Step 2: Merge**

```
git checkout main
git merge --no-ff feature/edit-delete-post -m "Merge feature/edit-delete-post: inline edit and delete on PostCard"
```

- [ ] **Step 3: Verify on main**

```
npm test
```
Expected: all pass.

- [ ] **Step 4: Delete branch and push both repos**

```
git branch -d feature/edit-delete-post
git push origin main
```

Also push the backend:
```
cd C:\NoBackup\development\Playr
git push origin main
```

---

## Self-Review

**Spec coverage:**
- `UpdatePostCommand` record → Task 1 ✓
- `IPostService.UpdateAsync` / `DeleteAsync` → Task 1 ✓
- `PostService.UpdateAsync`: validation (text, mood), not-found, not-author → Task 1 ✓
- `PostService.DeleteAsync`: not-found, not-author → Task 1 ✓
- `UpdatePostRequest` model with data annotations → Task 2 ✓
- `PUT /api/posts/{id}` [Authorize], 200/400/401/403/404 → Task 2 ✓
- `DELETE /api/posts/{id}` [Authorize], 204/401/403/404 → Task 2 ✓
- CORS adds DELETE → Task 2 ✓
- HTTP integration tests (edit, 403 on wrong user, delete) → Task 3 ✓
- Backend merged to main → Task 4 ✓
- `updatePost` + `deletePost` in postsApi → Task 5 ✓
- `PostCard` `...` menu owner-only → Task 6 ✓
- Inline edit (textarea + mood picker + Save/Cancel) → Task 6 ✓
- Inline delete confirm → Task 6 ✓
- `onUpdate`/`onDelete` callbacks → Task 6 ✓
- Close menu on outside click → Task 6 ✓
- `FeedPage` passes `currentUserId`, `onDelete`, `onUpdate` → Task 7 ✓
- Frontend merged + both repos pushed → Task 8 ✓

**Placeholder scan:** No TBD/TODO. PostCard reads token from `localStorage.getItem('playr_token')` directly — this is the same key used by `AuthContext` (`TOKEN_STORAGE_KEY = 'playr_token'`). It avoids prop-drilling the token through FeedPage → PostCard while keeping the feature scope tight. Noted explicitly in the code comment.

**Type consistency:**
- `updatePost(token, postId, data)` in postsApi matches the call in PostCard ✓
- `deletePost(token, postId)` matches the call in PostCard ✓
- `onUpdate: (post: PostFeedItem) => void` — PostCard calls `onUpdate?.(updated)` where `updated` is the `PostFeedItem` returned by `updatePost` ✓
- `onDelete: (postId: string) => void` — PostCard calls `onDelete?.(post.id)` where `post.id` is a `string` ✓
- `FeedPage.handleDelete` filters by `p.id !== postId` — matches `string` type ✓
- `FeedPage.handleUpdate` maps by `p.id === updated.id` — matches ✓
