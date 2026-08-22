# PLAYR — Profilsida + Redigera profil Design

## Kontext

`UserProfile`-entiteten finns i backend med alla fält (bio, avatarUrl, region,
languages, platforms, externalLinks, currentlyPlayingGames, lookingForPlayers).
`GET /api/profiles/{username}` och `PUT /api/profiles/me` existerar redan.
Frontendrouten `/profile` är en placeholder. Det saknas: en endpoint för en
användares posts, ett frontend-profilsida, och ett redigeringsformulär.

## Beslut (låsta)

- **URL:** `/profile/:username` — publik, delbar. Sidebar "Profile"-länk går till
  inloggad användares `/profile/{username}`.
- **Publik routing:** `/profile/:username` läggs i AppShell men **utanför**
  `ProtectedRoute` — fungerar för icke-inloggade (sidebar utan auth är tom/gömd).
- **Edit-UX:** Inline toggle på profilsidan — "Edit Profile"-knapp visar/döljer
  `EditProfileForm` direkt under `ProfileHeader`. Ingen separat sida.
- **Vem ser Edit-knappen:** Bara inloggad ägare (`currentUser.id === profile.userId`).

## Ej i scope

- Avatar-upload (fortfarande URL-sträng).
- Followers/following.
- Privata profiler.
- Looking-for-players-status (fältet finns i DB men exponeras inte i UI ännu —
  det kommer med "Looking for Players"-featuren).

## Backend — ny endpoint

### `GET /api/profiles/{username}/posts` (publik)

Returnerar senaste 50 posts av användaren, nyast först, som `IReadOnlyList<PostResponse>`.

**`IPostService`** får en ny metod:
```
Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, CancellationToken)
```

**`PostService.GetByUsernameAsync`:** Hämtar `UserProfile` på username (case-insensitive),
returnerar tom lista om profilen inte hittas. Hämtar sedan senaste 50 posts för
`UserId`, `OrderByDescending(CreatedAt)`, `Take(50)`, via `MapToPostDtoAsync`.

**`ProfilesController`** får en ny action:
```
[HttpGet("{username}/posts")]
public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetPostsByUsername(
    string username, CancellationToken)
```

Returnerar alltid 200 (tom lista om användaren inte finns eller har inga posts).

## Frontend

### `src/api/profilesApi.ts`

```ts
interface ProfileData {
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

getProfile(username: string): Promise<ProfileData>       // GET /api/profiles/{username}
getProfilePosts(username: string): Promise<PostFeedItem[]> // GET /api/profiles/{username}/posts
updateProfile(token: string, data: UpdateProfileData): Promise<ProfileData> // PUT /api/profiles/me
```

`UpdateProfileData` speglar `UpdateProfileRequest`:
`{ displayName, bio?, avatarUrl?, region?, languages, platforms, externalLinks, currentlyPlayingGames, lookingForPlayers }`.

### `src/components/ProfileHeader.tsx`

Presentationskomponent (inga API-anrop). Props:
- `profile: ProfileData`
- `isOwner: boolean`
- `onEditClick: () => void`

Renderar:
- Stor `Avatar` (`lg`-storlek), `src={avatarUrl ?? undefined}`, `alt={displayName}`
- `displayName` som rubrik, `@username` som muted text
- `bio` om finns
- `region` med en globikon om finns
- `platforms` som `Badge`-taggar (tag-variant)
- `externalLinks` — klickbara rader med länknamn + ikon, öppnar i ny flik
- "Edit Profile"-knapp (`Button variant="secondary"`) om `isOwner`

### `src/components/EditProfileForm.tsx`

Props: `profile: ProfileData`, `token: string`, `onSave: (updated: ProfileData) => void`, `onCancel: () => void`.

Fält (förfyllda med nuvarande värden):
- `displayName` — textfält, required
- `bio` — textarea, max 500, char-räknare
- `avatarUrl` — textfält, valfritt
- `region` — textfält, valfritt
- `platforms` — toggle-knappar: PC / Xbox / PlayStation / Switch (markerade = i listan)
- `externalLinks` — dynamisk lista av rader `[key-input] [value-input] [ta bort]`
  + "Add link"-knapp. Max 10 rader.

Submit kallar `updateProfile(token, data)`. Vid success kallar `onSave(updatedProfile)`.
Vid error visas felmeddelande. Save/Cancel-knappar.

### `src/pages/ProfilePage.tsx`

Ersätter placeholder. Läser `:username` från `useParams()`.

- Hämtar profil + posts parallellt med `Promise.all`.
- Hämtar `user` från `useAuth()` för att avgöra `isOwner`.
- State: `isEditing` (boolean) — toggle via Edit Profile / Cancel.
- Renderar: `ProfileHeader` → (om `isEditing`) `EditProfileForm` → postlista med `PostCard`.
- Loading / error / not-found (404) states.
- `onSave` i `EditProfileForm`: uppdaterar lokal `profile`-state + stänger formuläret.

### Routing i `App.tsx`

Lägg till `/profile/:username` som en **publik route inuti AppShell men utanför
ProtectedRoute**:

```tsx
// Ny layout-route utan ProtectedRoute:
<Route element={<AppShell />}>
  <Route path="/profile/:username" element={<ProfilePage />} />
</Route>
// Befintlig skyddad layout-route behålls som den är
```

`AppShell` renderar `Sidebar` + `TopBar` — `Sidebar` och `TopBar` hanterar redan
`user === null` (avatar-fallback + tom user card).

### Sidebar "Profile"-länk

I `Sidebar.tsx` — "Profile"-länken pekar idag på `/profile` (statisk). Ändra till
dynamisk länk: om `user` finns → `/profile/${user.username}`, annars `/profile`.

## Testing

### Backend
- `PostService.GetByUsernameAsync`: returnerar posts för känd användare sorterade
  nyast-först; returnerar tom lista för okänd användare.
- Endpoint-konfiguration: `GET /api/profiles/{username}/posts` är publik (ingen `[Authorize]`).

### Frontend (Vitest + RTL)
- `profilesApi`: `getProfile` och `getProfilePosts` kastar `ApiError` vid fel;
  `updateProfile` skickar PUT med bearer token.
- `ProfileHeader`: renderar displayName, @username, bio, plattform-badges, extern länk;
  Edit-knapp visas om `isOwner`, döljs annars.
- `EditProfileForm`: plattforms-toggles fungerar; submit kallar `updateProfile`;
  `onSave` kallas vid success; `onCancel` kallas vid avbryt.
- `ProfilePage`: renderar profil + posts; visar "not found" vid 404;
  Edit-flödet öppnar/stänger formuläret.

## Öppna frågor

Inga.
