# PLAYR — Användarsökning Design

## Kontext

TopBar har redan ett `SearchInput`-fält men det är dekorativt. Profilsidan
`/profile/:username` är publik. Det finns ingen sökmöjlighet för att hitta
andra användare.

## Beslut

- **Endpoint:** `GET /api/profiles/search?q={query}` — söker på username och
  displayName (`LIKE %q%`, case-insensitive), max 8 träffar, publik.
- **UX:** Live dropdown i topbaren — debounce 300ms, aktiveras vid ≥ 2 tecken,
  klick navigerar till `/profile/{username}`.

## Ej i scope

- Sökning på bio, region, spel.
- Paginering av sökresultat.
- Sökhistorik.
- Dedikerad /search-sida.

## Backend

### `IProfileService` — ny metod
```
Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken)
```

### `ProfileSearchResult` record (`Playr.Application.Profiles`)
```csharp
public sealed record ProfileSearchResult(
    Guid UserId, string Username, string DisplayName, string? AvatarUrl);
```

### `ProfileService.SearchAsync`
- Trimmar query, returnerar tom lista om tom sträng.
- `WHERE UPPER(Username) LIKE UPPER('%{query}%') OR UPPER(DisplayName) LIKE UPPER('%{query}%')`
- `Take(8)`, `OrderBy(Username)`, `AsNoTracking`.
- Mappar till `ProfileSearchResult`.

### `GET /api/profiles/search?q={query}` (publik)
Ny action i `ProfilesController`. Returnerar `IReadOnlyList<ProfileSearchResponse>`
där `ProfileSearchResponse(Guid UserId, string Username, string DisplayName, string? AvatarUrl)`.
Tom query → `200 []`.

## Frontend

### `src/api/profilesApi.ts` — ny funktion
```ts
interface ProfileSearchResult { userId: string; username: string; displayName: string; avatarUrl: string | null }
searchProfiles(query: string): Promise<ProfileSearchResult[]>
// GET /api/profiles/search?q={query}
```

### `TopBar.tsx` — interaktivt sökfält med dropdown
- State: `query: string`, `results: ProfileSearchResult[]`, `isOpen: boolean`.
- `useEffect` med 300ms debounce: om `query.trim().length >= 2` → anropa
  `searchProfiles(query)` → sätt `results` + `isOpen: true`. Annars stäng.
- Dropdown under sökfältet: varje rad = `Avatar` (sm) + displayName + @username.
  Klick → `navigate('/profile/{username}')` + stäng dropdown.
- "Ingen användare hittades" om `results.length === 0` och query ≥ 2.
- Klick utanför (`mousedown` på `document`) stänger dropdown.
- `useNavigate` från react-router-dom.

## Open questions
Inga.
