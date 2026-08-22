# PLAYR Profile Page + Edit Profile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a public `/profile/:username` page showing a user's profile and posts, with an inline edit form for profile owners, plus a new backend endpoint for fetching a user's posts.

**Architecture:** Backend adds `GetByUsernameAsync` to `PostService` and a new `GET /api/profiles/{username}/posts` action to `ProfilesController`. Frontend adds a `profilesApi.ts` wrapper, `ProfileHeader` and `EditProfileForm` components, a real `ProfilePage`, and wires routing + sidebar. The profile page is public (inside AppShell but outside ProtectedRoute).

**Tech Stack:** .NET 10, EF Core 10, xUnit + FluentAssertions + SQLite; React 18 + TS, Vite, Tailwind v4, Vitest + RTL, lucide-react.

## Global Constraints

- Backend repo: `C:\NoBackup\development\Playr`, branch `feature/profile-page` (already created).
- Frontend repo: `C:\NoBackup\development\playr-frontend`, create branch `feature/profile-page` before Task 4.
- Windows / PowerShell 5.1: no `&&`, use `;`.
- Backend tests: `dotnet test`. Pre-existing failing test `RegisterAsync_WhenIdentityRejectsPassword` — ignore throughout.
- Frontend tests: `npm test`. Build: `npm run build`. Every test file must start with `import { describe, it, expect, vi, beforeEach } from 'vitest'`.
- Tailwind tokens: `bg-surface`, `bg-surface-raised`, `border-border`, `text-text`, `text-muted`, `text-primary`, `text-frustrated`.
- `ProfileResponse` record field order (exact): `UserId, Username, DisplayName, Bio, AvatarUrl, Region, Languages, Platforms, ExternalLinks, CurrentlyPlayingGames, LookingForPlayers, CreatedAt, UpdatedAt`.
- `PostFeedItem` interface (existing in `src/api/postsApi.ts`): `id, authorId, authorUsername, authorDisplayName, authorAvatarUrl, gameId, gameName, gameCoverImageUrl, textContent, mood, createdAt`.
- Token storage key in localStorage: `'playr_token'`.
- Commit convention: `feat:`, `fix:`, `test:`.

---

### Task 1: Backend — PostService.GetByUsernameAsync + ProfilesController endpoint

**Files:**
- Modify: `src/Playr.Application/Posts/IPostService.cs`
- Modify: `src/Playr.Infrastructure/Posts/PostService.cs`
- Modify: `src/Playr.Api/Controllers/ProfilesController.cs`
- Test: `tests/Playr.Application.Tests/Posts/PostsByUsernameTests.cs`
- Test: `tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs` (append one test)

**Interfaces:**
- Consumes: `PlayrDbContext` (existing), `MapToPostDtoAsync` private method in `PostService`, `IPostService` interface, `PostResponse` (existing), `ProfilesController` (existing).
- Produces:
  - `IPostService.GetByUsernameAsync(string username, CancellationToken) → Task<IReadOnlyList<PostDto>>`
  - `GET /api/profiles/{username}/posts` (public) → `200 IReadOnlyList<PostResponse>`

- [ ] **Step 1: Write failing service test**

`tests/Playr.Application.Tests/Posts/PostsByUsernameTests.cs`:
```csharp
using FluentAssertions;
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

public sealed class PostsByUsernameTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _userId;
    private readonly Guid _gameId;

    public PostsByUsernameTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _userId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _userId, Email = "gamer@example.com", UserName = "gamer",
            NormalizedEmail = "GAMER@EXAMPLE.COM", NormalizedUserName = "GAMER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _userId, Username = "gamer", DisplayName = "Gamer",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        _dbContext.Posts.AddRange(
            new Post { Id = Guid.NewGuid(), AuthorId = _userId, GameId = _gameId, TextContent = "Post A", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) },
            new Post { Id = Guid.NewGuid(), AuthorId = _userId, GameId = _gameId, TextContent = "Post B", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
        );
        _dbContext.SaveChanges();
        _service = new PostService(_dbContext);
    }

    [Fact]
    public async Task GetByUsernameAsync_ReturnsPostsNewestFirst()
    {
        var result = await _service.GetByUsernameAsync("gamer", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].TextContent.Should().Be("Post B");
        result[1].TextContent.Should().Be("Post A");
    }

    [Fact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        var result = await _service.GetByUsernameAsync("GAMER", CancellationToken.None);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUsernameAsync_ReturnsEmptyListForUnknownUsername()
    {
        var result = await _service.GetByUsernameAsync("nobody", CancellationToken.None);
        result.Should().BeEmpty();
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
dotnet test tests\Playr.Application.Tests --filter PostsByUsernameTests
```
Expected: compile error — `GetByUsernameAsync` not on `PostService`.

- [ ] **Step 3: Add GetByUsernameAsync to IPostService**

In `src/Playr.Application/Posts/IPostService.cs`, add this method to the interface:
```csharp
    Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, CancellationToken cancellationToken);
```

- [ ] **Step 4: Implement GetByUsernameAsync in PostService**

Add this method to `src/Playr.Infrastructure/Posts/PostService.cs` after `DeleteAsync` and before the private `MapToPostDtoAsync`:
```csharp
    public async Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Username.ToUpper() == normalized, cancellationToken);

        if (profile is null)
            return [];

        var posts = await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.AuthorId == profile.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .ToListAsync(cancellationToken);

        return await MapToPostDtoAsync(posts, cancellationToken);
    }
```

- [ ] **Step 5: Add endpoint to ProfilesController**

In `src/Playr.Api/Controllers/ProfilesController.cs`, inject `IPostService` via primary constructor and add the new action. Replace the constructor line and add the new action:

Replace:
```csharp
public sealed class ProfilesController(IProfileService profileService) : ControllerBase
```
With:
```csharp
public sealed class ProfilesController(IProfileService profileService, IPostService postService) : ControllerBase
```

Add `using Playr.Api.Models.Posts;` and `using Playr.Application.Posts;` at the top if not present.

Add this action after the existing `UpdateMe` method:
```csharp
    [HttpGet("{username}/posts")]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetPostsByUsername(
        string username, CancellationToken cancellationToken)
    {
        var posts = await postService.GetByUsernameAsync(username, cancellationToken);
        return Ok(posts.Select(p => new PostResponse(
            p.Id, p.AuthorId, p.AuthorUsername, p.AuthorDisplayName, p.AuthorAvatarUrl,
            p.GameId, p.GameName, p.GameCoverImageUrl, p.TextContent, p.Mood, p.CreatedAt
        )).ToList());
    }
```

- [ ] **Step 6: Add endpoint config test**

In `tests/Playr.IntegrationTests/GamesAndPostsEndpointConfigurationTests.cs`, add this method to the existing class:
```csharp
    [Fact]
    public void Profile_posts_endpoint_is_public()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.ProfilesController");
        controller.Should().NotBeNull();
        controller!.GetMethods()
            .Should().Contain(m =>
                m.GetCustomAttribute<HttpGetAttribute>()?.Template == "{username}/posts" &&
                m.GetCustomAttribute<AuthorizeAttribute>() == null);
    }
```

- [ ] **Step 7: Run all tests — expect PASS**

```
dotnet test
```
Expected: 3 new PostsByUsername tests + 1 new endpoint config test pass. All others still pass (ignore pre-existing failure).

- [ ] **Step 8: Commit**

```
git add src\Playr.Application\Posts\IPostService.cs src\Playr.Infrastructure\Posts\PostService.cs src\Playr.Api\Controllers\ProfilesController.cs tests\Playr.Application.Tests\Posts\PostsByUsernameTests.cs tests\Playr.IntegrationTests\GamesAndPostsEndpointConfigurationTests.cs
git commit -m "feat: add GetByUsernameAsync to PostService and GET /api/profiles/{username}/posts"
```

---

### Task 2: Backend — merge to main

- [ ] **Step 1: Run full suite**

```
dotnet test
```
Expected: all pass (1 pre-existing failure excluded).

- [ ] **Step 2: Merge**

```
git checkout main
git merge --no-ff feature/profile-page -m "Merge feature/profile-page: user posts endpoint"
git branch -d feature/profile-page
```

- [ ] **Step 3: Push**

```
git push origin main
```

---

### Task 3: Frontend — profilesApi.ts

**Files:**
- Create: `src/api/profilesApi.ts`
- Create: `src/api/profilesApi.test.ts`

**Working directory:** `C:\NoBackup\development\playr-frontend`. Create branch first:
```
git checkout -b feature/profile-page
```

**Interfaces:**
- Consumes: `API_BASE_URL`, `ApiError`, `parseErrorMessage` from `./http`; `PostFeedItem` from `./postsApi`.
- Produces:
  - `interface ProfileData { userId: string; username: string; displayName: string; bio: string | null; avatarUrl: string | null; region: string | null; languages: string[]; platforms: string[]; externalLinks: Record<string, string>; currentlyPlayingGames: string[]; lookingForPlayers: boolean; createdAt: string; updatedAt: string }`
  - `interface UpdateProfileData { displayName: string; bio?: string | null; avatarUrl?: string | null; region?: string | null; languages: string[]; platforms: string[]; externalLinks: Record<string, string>; currentlyPlayingGames: string[]; lookingForPlayers: boolean }`
  - `getProfile(username: string): Promise<ProfileData>` — `GET /api/profiles/{username}`, throws `ApiError` on non-2xx (including 404).
  - `getProfilePosts(username: string): Promise<PostFeedItem[]>` — `GET /api/profiles/{username}/posts`.
  - `updateProfile(token: string, data: UpdateProfileData): Promise<ProfileData>` — `PUT /api/profiles/me` with bearer token.

- [ ] **Step 1: Create branch**

```
cd C:\NoBackup\development\playr-frontend
git checkout -b feature/profile-page
```

- [ ] **Step 2: Write failing tests**

`src/api/profilesApi.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { getProfile, getProfilePosts, updateProfile } from './profilesApi'
import { ApiError } from './http'

const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

beforeEach(() => { mockFetch.mockReset() })

const sampleProfile = {
  userId: 'u1', username: 'player', displayName: 'Player One', bio: 'Hi',
  avatarUrl: null, region: 'EU', languages: ['English'], platforms: ['PC'],
  externalLinks: { Steam: 'https://steam.com/player' }, currentlyPlayingGames: [],
  lookingForPlayers: false, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
}

describe('getProfile', () => {
  it('returns profile on success', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => sampleProfile })
    const result = await getProfile('player')
    expect(result.username).toBe('player')
    expect(mockFetch).toHaveBeenCalledWith(expect.stringContaining('/api/profiles/player'))
  })

  it('throws ApiError on 404', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 404, json: async () => ({ error: 'Profile was not found.' }) })
    await expect(getProfile('nobody')).rejects.toBeInstanceOf(ApiError)
  })
})

describe('getProfilePosts', () => {
  it('returns posts array on success', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => [] })
    const result = await getProfilePosts('player')
    expect(result).toEqual([])
    expect(mockFetch).toHaveBeenCalledWith(expect.stringContaining('/api/profiles/player/posts'))
  })
})

describe('updateProfile', () => {
  it('sends PUT with bearer token and returns updated profile', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => sampleProfile })
    const data = { displayName: 'New Name', languages: [], platforms: ['PC'], externalLinks: {}, currentlyPlayingGames: [], lookingForPlayers: false }
    const result = await updateProfile('my-token', data)
    expect(result.displayName).toBe('Player One')
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/profiles/me'),
      expect.objectContaining({ method: 'PUT', headers: expect.objectContaining({ Authorization: 'Bearer my-token' }) })
    )
  })

  it('throws ApiError on 400', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 400, json: async () => ({ error: 'Display name is required.' }) })
    await expect(updateProfile('tok', { displayName: '', languages: [], platforms: [], externalLinks: {}, currentlyPlayingGames: [], lookingForPlayers: false })).rejects.toBeInstanceOf(ApiError)
  })
})
```

- [ ] **Step 3: Run tests — expect FAIL**

```
npm test -- profilesApi
```
Expected: FAIL — `profilesApi` not found.

- [ ] **Step 4: Implement profilesApi.ts**

`src/api/profilesApi.ts`:
```ts
import { API_BASE_URL, ApiError, parseErrorMessage } from './http'
import type { PostFeedItem } from './postsApi'

export interface ProfileData {
  userId: string
  username: string
  displayName: string
  bio: string | null
  avatarUrl: string | null
  region: string | null
  languages: string[]
  platforms: string[]
  externalLinks: Record<string, string>
  currentlyPlayingGames: string[]
  lookingForPlayers: boolean
  createdAt: string
  updatedAt: string
}

export interface UpdateProfileData {
  displayName: string
  bio?: string | null
  avatarUrl?: string | null
  region?: string | null
  languages: string[]
  platforms: string[]
  externalLinks: Record<string, string>
  currentlyPlayingGames: string[]
  lookingForPlayers: boolean
}

export async function getProfile(username: string): Promise<ProfileData> {
  const response = await fetch(`${API_BASE_URL}/api/profiles/${username}`)
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Profile not found.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}

export async function getProfilePosts(username: string): Promise<PostFeedItem[]> {
  const response = await fetch(`${API_BASE_URL}/api/profiles/${username}/posts`)
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to load posts.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}

export async function updateProfile(token: string, data: UpdateProfileData): Promise<ProfileData> {
  const response = await fetch(`${API_BASE_URL}/api/profiles/me`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(data),
  })
  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Failed to update profile.')
    throw new ApiError(response.status, message)
  }
  return response.json()
}
```

- [ ] **Step 5: Run tests — expect PASS**

```
npm test -- profilesApi
```
Expected: 5 tests pass.

- [ ] **Step 6: Full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 7: Commit**

```
git add src/api/profilesApi.ts src/api/profilesApi.test.ts
git commit -m "feat: add profilesApi (getProfile, getProfilePosts, updateProfile)"
```

---

### Task 4: Frontend — ProfileHeader component

**Files:**
- Create: `src/components/ProfileHeader.tsx`
- Create: `src/components/ProfileHeader.test.tsx`

**Interfaces:**
- Consumes: `Avatar` from `./ui/Avatar`, `Badge` from `./ui/Badge`, `Button` from `./ui/Button`, `ProfileData` from `../api/profilesApi`.
- Produces: `ProfileHeader({ profile: ProfileData, isOwner: boolean, onEditClick: () => void })` — renders avatar, names, bio, region, platforms as badges, external links as clickable anchors, and "Edit Profile" button (only if `isOwner`).

- [ ] **Step 1: Write failing test**

`src/components/ProfileHeader.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProfileHeader } from './ProfileHeader'
import type { ProfileData } from '../api/profilesApi'

const profile: ProfileData = {
  userId: 'u1', username: 'nexusnova', displayName: 'NexusNova', bio: 'Gaming is life',
  avatarUrl: null, region: 'EU', languages: ['English', 'Swedish'],
  platforms: ['PC', 'PlayStation'], externalLinks: { Steam: 'https://steamcommunity.com/id/nexusnova' },
  currentlyPlayingGames: [], lookingForPlayers: false,
  createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
}

describe('ProfileHeader', () => {
  it('renders displayName and username', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    expect(screen.getByText('NexusNova')).toBeInTheDocument()
    expect(screen.getByText('@nexusnova')).toBeInTheDocument()
  })

  it('renders bio', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    expect(screen.getByText('Gaming is life')).toBeInTheDocument()
  })

  it('renders region', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    expect(screen.getByText('EU')).toBeInTheDocument()
  })

  it('renders platform badges', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    expect(screen.getByText('PC')).toBeInTheDocument()
    expect(screen.getByText('PlayStation')).toBeInTheDocument()
  })

  it('renders external links as anchors', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    const link = screen.getByRole('link', { name: /steam/i })
    expect(link).toHaveAttribute('href', 'https://steamcommunity.com/id/nexusnova')
    expect(link).toHaveAttribute('target', '_blank')
  })

  it('shows Edit Profile button when isOwner', () => {
    render(<ProfileHeader profile={profile} isOwner={true} onEditClick={vi.fn()} />)
    expect(screen.getByRole('button', { name: /edit profile/i })).toBeInTheDocument()
  })

  it('hides Edit Profile button when not isOwner', () => {
    render(<ProfileHeader profile={profile} isOwner={false} onEditClick={vi.fn()} />)
    expect(screen.queryByRole('button', { name: /edit profile/i })).not.toBeInTheDocument()
  })

  it('calls onEditClick when Edit Profile is clicked', async () => {
    const onEditClick = vi.fn()
    const user = userEvent.setup()
    render(<ProfileHeader profile={profile} isOwner={true} onEditClick={onEditClick} />)
    await user.click(screen.getByRole('button', { name: /edit profile/i }))
    expect(onEditClick).toHaveBeenCalledOnce()
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- ProfileHeader
```
Expected: FAIL — `./ProfileHeader` not found.

- [ ] **Step 3: Implement ProfileHeader**

`src/components/ProfileHeader.tsx`:
```tsx
import { Globe } from 'lucide-react'
import { Avatar } from './ui/Avatar'
import { Badge } from './ui/Badge'
import { Button } from './ui/Button'
import type { ProfileData } from '../api/profilesApi'

interface ProfileHeaderProps {
  profile: ProfileData
  isOwner: boolean
  onEditClick: () => void
}

export function ProfileHeader({ profile, isOwner, onEditClick }: ProfileHeaderProps) {
  return (
    <div className="flex flex-col gap-4 rounded-xl border border-border bg-surface p-6">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          <Avatar
            src={profile.avatarUrl ?? undefined}
            alt={profile.displayName}
            size="lg"
          />
          <div>
            <h1 className="text-xl font-bold text-text">{profile.displayName}</h1>
            <p className="text-sm text-muted">@{profile.username}</p>
          </div>
        </div>
        {isOwner && (
          <Button variant="secondary" size="sm" onClick={onEditClick}>
            Edit Profile
          </Button>
        )}
      </div>

      {profile.bio && (
        <p className="text-sm text-text leading-relaxed">{profile.bio}</p>
      )}

      {profile.region && (
        <div className="flex items-center gap-1.5 text-sm text-muted">
          <Globe className="h-4 w-4" aria-hidden="true" />
          <span>{profile.region}</span>
        </div>
      )}

      {profile.platforms.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {profile.platforms.map((platform) => (
            <Badge key={platform} variant="tag">{platform}</Badge>
          ))}
        </div>
      )}

      {Object.entries(profile.externalLinks).length > 0 && (
        <div className="flex flex-col gap-1">
          {Object.entries(profile.externalLinks).map(([key, value]) => (
            <a
              key={key}
              href={value}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm text-primary hover:underline"
            >
              {key}
            </a>
          ))}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 4: Run test — expect PASS**

```
npm test -- ProfileHeader
```
Expected: 8 tests pass.

- [ ] **Step 5: Full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/components/ProfileHeader.tsx src/components/ProfileHeader.test.tsx
git commit -m "feat: add ProfileHeader component"
```

---

### Task 5: Frontend — EditProfileForm component

**Files:**
- Create: `src/components/EditProfileForm.tsx`
- Create: `src/components/EditProfileForm.test.tsx`

**Interfaces:**
- Consumes: `Button` from `./ui/Button`, `updateProfile`, `ProfileData`, `UpdateProfileData` from `../api/profilesApi`, `ApiError` from `../api/http`.
- Produces: `EditProfileForm({ profile: ProfileData, token: string, onSave: (updated: ProfileData) => void, onCancel: () => void })`.

Platform options (exact): `['PC', 'Xbox', 'PlayStation', 'Switch']`.
Max external links: 10.
ExternalLinks state: `Array<{ key: string; value: string }>` internally, converted to `Record<string, string>` on submit.

- [ ] **Step 1: Write failing test**

`src/components/EditProfileForm.test.tsx`:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EditProfileForm } from './EditProfileForm'
import type { ProfileData } from '../api/profilesApi'
import * as profilesApi from '../api/profilesApi'

vi.mock('../api/profilesApi')

const profile: ProfileData = {
  userId: 'u1', username: 'player', displayName: 'Player One', bio: 'Hello',
  avatarUrl: null, region: 'EU', languages: ['English'], platforms: ['PC'],
  externalLinks: { Steam: 'https://steam.com' }, currentlyPlayingGames: [],
  lookingForPlayers: false, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
}

const updated: ProfileData = { ...profile, displayName: 'Updated Name' }

beforeEach(() => {
  vi.mocked(profilesApi.updateProfile).mockResolvedValue(updated)
})

describe('EditProfileForm', () => {
  it('pre-fills displayName field', () => {
    render(<EditProfileForm profile={profile} token="tok" onSave={vi.fn()} onCancel={vi.fn()} />)
    expect(screen.getByDisplayValue('Player One')).toBeInTheDocument()
  })

  it('toggles PC platform off when clicked', async () => {
    const user = userEvent.setup()
    render(<EditProfileForm profile={profile} token="tok" onSave={vi.fn()} onCancel={vi.fn()} />)
    const pcButton = screen.getByRole('button', { name: 'PC' })
    expect(pcButton).toHaveAttribute('aria-pressed', 'true')
    await user.click(pcButton)
    expect(pcButton).toHaveAttribute('aria-pressed', 'false')
  })

  it('calls updateProfile and onSave on submit', async () => {
    const onSave = vi.fn()
    const user = userEvent.setup()
    render(<EditProfileForm profile={profile} token="tok" onSave={onSave} onCancel={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /save/i }))
    await waitFor(() => expect(onSave).toHaveBeenCalledWith(updated))
    expect(profilesApi.updateProfile).toHaveBeenCalledWith('tok', expect.objectContaining({ displayName: 'Player One' }))
  })

  it('calls onCancel when Cancel is clicked', async () => {
    const onCancel = vi.fn()
    const user = userEvent.setup()
    render(<EditProfileForm profile={profile} token="tok" onSave={vi.fn()} onCancel={onCancel} />)
    await user.click(screen.getByRole('button', { name: /cancel/i }))
    expect(onCancel).toHaveBeenCalledOnce()
  })

  it('shows error message on updateProfile failure', async () => {
    const { ApiError } = await import('../api/http')
    vi.mocked(profilesApi.updateProfile).mockRejectedValueOnce(new ApiError(400, 'Display name is required.'))
    const user = userEvent.setup()
    render(<EditProfileForm profile={profile} token="tok" onSave={vi.fn()} onCancel={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /save/i }))
    await waitFor(() => expect(screen.getByText('Display name is required.')).toBeInTheDocument())
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- EditProfileForm
```
Expected: FAIL — `./EditProfileForm` not found.

- [ ] **Step 3: Implement EditProfileForm**

`src/components/EditProfileForm.tsx`:
```tsx
import { useState } from 'react'
import { Button } from './ui/Button'
import { updateProfile, type ProfileData } from '../api/profilesApi'
import { ApiError } from '../api/http'

const PLATFORMS = ['PC', 'Xbox', 'PlayStation', 'Switch']

interface EditProfileFormProps {
  profile: ProfileData
  token: string
  onSave: (updated: ProfileData) => void
  onCancel: () => void
}

interface LinkRow { key: string; value: string }

export function EditProfileForm({ profile, token, onSave, onCancel }: EditProfileFormProps) {
  const [displayName, setDisplayName] = useState(profile.displayName)
  const [bio, setBio] = useState(profile.bio ?? '')
  const [avatarUrl, setAvatarUrl] = useState(profile.avatarUrl ?? '')
  const [region, setRegion] = useState(profile.region ?? '')
  const [platforms, setPlatforms] = useState<string[]>(profile.platforms)
  const [links, setLinks] = useState<LinkRow[]>(
    Object.entries(profile.externalLinks).map(([key, value]) => ({ key, value }))
  )
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  function togglePlatform(platform: string) {
    setPlatforms((prev) =>
      prev.includes(platform) ? prev.filter((p) => p !== platform) : [...prev, platform]
    )
  }

  function updateLink(index: number, field: 'key' | 'value', val: string) {
    setLinks((prev) => prev.map((row, i) => (i === index ? { ...row, [field]: val } : row)))
  }

  function removeLink(index: number) {
    setLinks((prev) => prev.filter((_, i) => i !== index))
  }

  function addLink() {
    if (links.length >= 10) return
    setLinks((prev) => [...prev, { key: '', value: '' }])
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setIsSaving(true)
    const externalLinks: Record<string, string> = {}
    for (const { key, value } of links) {
      if (key.trim() && value.trim()) externalLinks[key.trim()] = value.trim()
    }
    try {
      const updated = await updateProfile(token, {
        displayName: displayName.trim(),
        bio: bio.trim() || null,
        avatarUrl: avatarUrl.trim() || null,
        region: region.trim() || null,
        languages: profile.languages,
        platforms,
        externalLinks,
        currentlyPlayingGames: profile.currentlyPlayingGames,
        lookingForPlayers: profile.lookingForPlayers,
      })
      onSave(updated)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.')
    } finally {
      setIsSaving(false)
    }
  }

  const inputClass = 'rounded-lg border border-border bg-surface-raised px-3 py-2 text-sm text-text outline-none focus:border-primary w-full'

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4 rounded-xl border border-border bg-surface p-6">
      <h2 className="text-base font-semibold text-text">Edit Profile</h2>

      <label className="flex flex-col gap-1 text-sm text-muted">
        Display name
        <input className={inputClass} value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
      </label>

      <label className="flex flex-col gap-1 text-sm text-muted">
        Bio
        <textarea
          className={`${inputClass} resize-none h-24`}
          value={bio}
          maxLength={500}
          onChange={(e) => setBio(e.target.value)}
        />
        <span className="text-xs self-end">{bio.length} / 500</span>
      </label>

      <label className="flex flex-col gap-1 text-sm text-muted">
        Avatar URL
        <input className={inputClass} value={avatarUrl} onChange={(e) => setAvatarUrl(e.target.value)} placeholder="https://..." />
      </label>

      <label className="flex flex-col gap-1 text-sm text-muted">
        Region
        <input className={inputClass} value={region} onChange={(e) => setRegion(e.target.value)} placeholder="e.g. EU, NA, AS" />
      </label>

      <div className="flex flex-col gap-2">
        <span className="text-sm text-muted">Platforms</span>
        <div className="flex flex-wrap gap-2">
          {PLATFORMS.map((platform) => (
            <button
              key={platform}
              type="button"
              aria-pressed={platforms.includes(platform)}
              onClick={() => togglePlatform(platform)}
              className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                platforms.includes(platform) ? 'bg-primary text-white' : 'bg-surface-raised text-muted hover:text-text'
              }`}
            >
              {platform}
            </button>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <span className="text-sm text-muted">External links</span>
        {links.map((row, i) => (
          <div key={i} className="flex gap-2 items-center">
            <input
              className="rounded-lg border border-border bg-surface-raised px-3 py-2 text-sm text-text outline-none focus:border-primary w-32"
              placeholder="Name"
              value={row.key}
              onChange={(e) => updateLink(i, 'key', e.target.value)}
            />
            <input
              className="rounded-lg border border-border bg-surface-raised px-3 py-2 text-sm text-text outline-none focus:border-primary flex-1"
              placeholder="URL or username"
              value={row.value}
              onChange={(e) => updateLink(i, 'value', e.target.value)}
            />
            <Button type="button" variant="ghost" size="sm" onClick={() => removeLink(i)}>✕</Button>
          </div>
        ))}
        {links.length < 10 && (
          <Button type="button" variant="ghost" size="sm" onClick={addLink} className="self-start">
            + Add link
          </Button>
        )}
      </div>

      {error && <p className="text-frustrated text-sm">{error}</p>}

      <div className="flex gap-2">
        <Button type="submit" disabled={isSaving}>{isSaving ? 'Saving…' : 'Save'}</Button>
        <Button type="button" variant="ghost" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  )
}
```

- [ ] **Step 4: Run test — expect PASS**

```
npm test -- EditProfileForm
```
Expected: 5 tests pass.

- [ ] **Step 5: Full suite + build**

```
npm test
npm run build
```
Expected: all pass, zero TS errors.

- [ ] **Step 6: Commit**

```
git add src/components/EditProfileForm.tsx src/components/EditProfileForm.test.tsx
git commit -m "feat: add EditProfileForm component"
```

---

### Task 6: Frontend — ProfilePage + routing + sidebar fix

**Files:**
- Modify: `src/pages/ProfilePage.tsx` (replace placeholder)
- Create: `src/pages/ProfilePage.test.tsx`
- Modify: `src/App.tsx`
- Modify: `src/components/layout/Sidebar.tsx`

**Interfaces:**
- Consumes: `useParams`, `useNavigate` from `react-router-dom`; `useAuth`; `getProfile`, `getProfilePosts`, `ProfileData` from `../api/profilesApi`; `PostFeedItem` from `../api/postsApi`; `ProfileHeader` (Task 4); `EditProfileForm` (Task 5); `PostCard` from `../components/PostCard`.
- Produces:
  - `ProfilePage` — loads profile + posts, shows ProfileHeader, toggles EditProfileForm, shows PostCard list.
  - `/profile/:username` added to AppShell outside ProtectedRoute.
  - Sidebar "Profile" link navigates to `/profile/${user.username}` when logged in.

- [ ] **Step 1: Write failing test**

`src/pages/ProfilePage.test.tsx`:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import ProfilePage from './ProfilePage'
import * as profilesApi from '../api/profilesApi'

vi.mock('../api/profilesApi')
vi.mock('../api/postsApi', () => ({ getFeed: vi.fn(), createPost: vi.fn(), updatePost: vi.fn(), deletePost: vi.fn(), getProfilePosts: vi.fn() }))
vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({ user: { id: 'u1', username: 'player', displayName: 'Player', email: 'p@p.com' }, token: 'tok', isLoading: false }),
}))

const profile: profilesApi.ProfileData = {
  userId: 'u1', username: 'player', displayName: 'Player One', bio: 'My bio',
  avatarUrl: null, region: 'EU', languages: [], platforms: ['PC'],
  externalLinks: {}, currentlyPlayingGames: [], lookingForPlayers: false,
  createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
}

beforeEach(() => {
  vi.mocked(profilesApi.getProfile).mockResolvedValue(profile)
  const postsApi = vi.mocked(await import('../api/postsApi'))
  postsApi.getProfilePosts = vi.fn().mockResolvedValue([])
})

function renderProfile(username = 'player') {
  return render(
    <MemoryRouter initialEntries={[`/profile/${username}`]}>
      <Routes>
        <Route path="/profile/:username" element={<ProfilePage />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('ProfilePage', () => {
  it('renders the profile header with displayName', async () => {
    renderProfile()
    await waitFor(() => expect(screen.getByText('Player One')).toBeInTheDocument())
  })

  it('shows Edit Profile button for own profile', async () => {
    renderProfile('player')
    await waitFor(() => expect(screen.getByRole('button', { name: /edit profile/i })).toBeInTheDocument())
  })

  it('hides Edit Profile button for other profiles', async () => {
    vi.mocked(profilesApi.getProfile).mockResolvedValueOnce({ ...profile, userId: 'other-user', username: 'other' })
    renderProfile('other')
    await waitFor(() => expect(screen.queryByRole('button', { name: /edit profile/i })).not.toBeInTheDocument())
  })

  it('shows not found message on 404', async () => {
    const { ApiError } = await import('../api/http')
    vi.mocked(profilesApi.getProfile).mockRejectedValueOnce(new ApiError(404, 'Profile was not found.'))
    renderProfile('nobody')
    await waitFor(() => expect(screen.getByText(/not found/i)).toBeInTheDocument())
  })

  it('toggles edit form on Edit Profile click', async () => {
    const user = userEvent.setup()
    renderProfile()
    await waitFor(() => screen.getByRole('button', { name: /edit profile/i }))
    await user.click(screen.getByRole('button', { name: /edit profile/i }))
    expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /cancel/i }))
    expect(screen.queryByRole('button', { name: /save/i })).not.toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

```
npm test -- ProfilePage
```
Expected: FAIL — old placeholder has none of this.

- [ ] **Step 3: Replace ProfilePage**

`src/pages/ProfilePage.tsx`:
```tsx
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ProfileHeader } from '../components/ProfileHeader'
import { EditProfileForm } from '../components/EditProfileForm'
import { PostCard } from '../components/PostCard'
import { getProfile, getProfilePosts, type ProfileData } from '../api/profilesApi'
import { type PostFeedItem } from '../api/postsApi'
import { ApiError } from '../api/http'
import { useAuth } from '../context/AuthContext'

export default function ProfilePage() {
  const { username } = useParams<{ username: string }>()
  const { user, token } = useAuth()

  const [profile, setProfile] = useState<ProfileData | null>(null)
  const [posts, setPosts] = useState<PostFeedItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isEditing, setIsEditing] = useState(false)

  useEffect(() => {
    if (!username) return
    setIsLoading(true)
    setNotFound(false)
    setError(null)
    setIsEditing(false)

    Promise.all([getProfile(username), getProfilePosts(username)])
      .then(([p, ps]) => { setProfile(p); setPosts(ps) })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 404) setNotFound(true)
        else setError('Failed to load profile.')
      })
      .finally(() => setIsLoading(false))
  }, [username])

  const isOwner = !!user && !!profile && user.id === profile.userId

  if (isLoading) return <p className="text-muted">Loading…</p>
  if (notFound) return <p className="text-muted">Profile not found.</p>
  if (error) return <p className="text-frustrated">{error}</p>
  if (!profile) return null

  return (
    <div className="flex flex-col gap-4">
      <ProfileHeader
        profile={profile}
        isOwner={isOwner}
        onEditClick={() => setIsEditing((prev) => !prev)}
      />

      {isEditing && token && (
        <EditProfileForm
          profile={profile}
          token={token}
          onSave={(updated) => { setProfile(updated); setIsEditing(false) }}
          onCancel={() => setIsEditing(false)}
        />
      )}

      <h2 className="text-lg font-semibold text-text">Posts</h2>
      {posts.length === 0 ? (
        <p className="text-muted">No posts yet.</p>
      ) : (
        posts.map((post) => (
          <PostCard
            key={post.id}
            post={post}
            currentUserId={user?.id}
            onUpdate={(updated) => setPosts((prev) => prev.map((p) => (p.id === updated.id ? updated : p)))}
            onDelete={(postId) => setPosts((prev) => prev.filter((p) => p.id !== postId))}
          />
        ))
      )}
    </div>
  )
}
```

- [ ] **Step 4: Update App.tsx — add public /profile/:username route**

Replace entire `src/App.tsx`:
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

      {/* Public routes inside AppShell (no auth required) */}
      <Route element={<AppShell />}>
        <Route path="/profile/:username" element={<ProfilePage />} />
      </Route>

      {/* Protected routes inside AppShell */}
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
        <Route path="/create-post" element={<CreatePostPage />} />
      </Route>
    </Routes>
  )
}

export default App
```

- [ ] **Step 5: Update Sidebar — dynamic Profile link**

In `src/components/layout/Sidebar.tsx`, find the `navItems` array. The Profile item currently has `to: '/profile'`. Change it so it's dynamic based on `user`:

Remove the Profile entry from the static `navItems` array:
```tsx
  { to: '/profile', label: 'Profile', icon: User, end: false },
```

Then inside the `Sidebar` function, after `const navigate = useNavigate()`, add:
```tsx
  const profilePath = user ? `/profile/${user.username}` : '/login'
```

And render the Profile link separately after the `nav` block with the other nav items, or simply replace the static entry in the map. The cleanest approach: keep the nav items array but override Profile's `to` dynamically. Replace the Profile entry with a special case in the render:

Inside the `<nav>` map, when rendering the Profile item, use `profilePath` instead of `to`. Replace the existing static `navItems` array Profile entry:

Change the `navItems` array item `{ to: '/profile', label: 'Profile', icon: User, end: false }` to `{ to: '__profile__', label: 'Profile', icon: User, end: false }` and in the NavLink render:
```tsx
            <NavLink
              key={to}
              to={to === '__profile__' ? profilePath : to}
              end={end}
              ...
```

Actually the simplest correct approach: remove Profile from navItems and add it explicitly after. Replace the `<nav>` section:

```tsx
      <nav className="flex flex-col gap-1">
        {navItems.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                isActive
                  ? 'bg-primary text-white'
                  : 'text-muted hover:bg-surface-raised hover:text-text'
              }`
            }
          >
            <Icon className="h-5 w-5" aria-hidden="true" />
            {label}
          </NavLink>
        ))}
        <NavLink
          to={profilePath}
          className={({ isActive }) =>
            `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
              isActive
                ? 'bg-primary text-white'
                : 'text-muted hover:bg-surface-raised hover:text-text'
            }`
          }
        >
          <User className="h-5 w-5" aria-hidden="true" />
          Profile
        </NavLink>
      </nav>
```

And remove `{ to: '/profile', label: 'Profile', icon: User, end: false }` from the `navItems` array.

- [ ] **Step 6: Run all tests — expect PASS**

```
npm test
```
Expected: all pass. (The existing Sidebar test checks for nav links including `/find-players` and `/threads` — those still work. Profile link test is not in the existing suite so no regression.)

- [ ] **Step 7: Full build**

```
npm run build
```
Expected: zero TS errors.

- [ ] **Step 8: Commit**

```
git add src/pages/ProfilePage.tsx src/pages/ProfilePage.test.tsx src/App.tsx src/components/layout/Sidebar.tsx
git commit -m "feat: real ProfilePage with edit form, public routing, dynamic sidebar profile link"
```

---

### Task 7: Frontend — merge to main + push both repos

- [ ] **Step 1: Run full test suite**

```
npm test
```
Expected: all pass.

- [ ] **Step 2: Merge frontend**

```
git checkout main
git merge --no-ff feature/profile-page -m "Merge feature/profile-page: ProfilePage, EditProfileForm, ProfileHeader, profilesApi"
git branch -d feature/profile-page
git push origin main
```

- [ ] **Step 3: Push backend**

```
cd C:\NoBackup\development\Playr
git push origin main
```

---

## Self-Review

**Spec coverage:**
- `GetByUsernameAsync` (case-insensitive, empty list for unknown user, newest-first) → Task 1 ✓
- `GET /api/profiles/{username}/posts` public → Task 1 ✓
- `ProfileData` interface → Task 3 ✓
- `getProfile`, `getProfilePosts`, `updateProfile` → Task 3 ✓
- `ProfileHeader`: avatar, displayName, @username, bio, region, platform badges, external links, Edit button owner-only → Task 4 ✓
- `EditProfileForm`: displayName, bio, avatarUrl, region, platform toggles, external links dynamic, save/cancel → Task 5 ✓
- `ProfilePage`: loads profile+posts, isOwner check, edit toggle, PostCard list, loading/notFound/error → Task 6 ✓
- `/profile/:username` public route in AppShell outside ProtectedRoute → Task 6 ✓
- Sidebar dynamic Profile link → Task 6 ✓
- Merges + pushes → Tasks 2 + 7 ✓

**Placeholder scan:** No TBD/TODO. Task 6 Step 5 Sidebar change is described in detail with exact code. The `ProfilePage.test.tsx` mocks `postsApi` with explicit `getProfilePosts: vi.fn()` to avoid the real fetch.

**Type consistency:**
- `ProfileData.userId` (string) compared to `user.id` (string from `UserResponse`) ✓
- `EditProfileForm` receives `token: string` — `useAuth().token` is `string | null`, ProfilePage guards `token &&` before rendering EditProfileForm ✓
- `getProfilePosts` returns `PostFeedItem[]` — same type `PostCard` and FeedPage use ✓
- `updateProfile(token, UpdateProfileData)` — `EditProfileForm` constructs `UpdateProfileData` correctly including `languages`, `currentlyPlayingGames`, `lookingForPlayers` from existing profile ✓
