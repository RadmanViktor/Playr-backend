# PLAYR Backend MVP Design

## Summary

PLAYR is a social gaming platform where users document their gaming journey and later discover other players with similar interests. The first backend MVP is intentionally strict: authentication and basic user profiles only.

This phase builds the backend first. Angular, posts, discovery, discussions, friends, private chat, and media upload are out of scope for this MVP.

## Decisions

- Architecture: simple layered architecture.
- Backend: ASP.NET Core Web API.
- Database: PostgreSQL.
- ORM: Entity Framework Core.
- Authentication: ASP.NET Core Identity for users and password handling.
- API authentication: JWT bearer tokens.
- Local development database: Docker Compose.
- API style: REST.
- Future work tracking: `docs/FUTURE.md`.

## Architecture

The backend will use four projects:

```text
Playr.Api
Playr.Application
Playr.Domain
Playr.Infrastructure
```

`Playr.Api` contains controllers, JWT setup, request/response DTOs, Swagger, validation wiring, dependency injection, and API configuration.

`Playr.Application` contains use cases and services for authentication and profiles, such as register, login, get current user, get profile, and update profile.

`Playr.Domain` contains core models and domain concepts that should not depend on the database or API layer.

`Playr.Infrastructure` contains EF Core, Identity configuration, PostgreSQL integration, migrations, and data-access implementations.

This structure keeps the MVP understandable for a solo developer while leaving room for later features.

## Authentication

Authentication uses ASP.NET Core Identity to manage user accounts and password hashing. The API exposes custom REST endpoints instead of relying on generated UI.

Endpoints:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
```

`POST /api/auth/register` creates an Identity user and a related basic profile.

Example request:

```json
{
  "email": "user@example.com",
  "username": "playerOne",
  "password": "StrongPassword123!"
}
```

Example response:

```json
{
  "id": "user-id",
  "email": "user@example.com",
  "username": "playerOne",
  "displayName": "playerOne"
}
```

`POST /api/auth/login` verifies credentials through Identity and returns a JWT.

Example response:

```json
{
  "accessToken": "jwt-token",
  "expiresAt": "2026-06-29T12:00:00Z"
}
```

`GET /api/auth/me` requires a valid JWT and returns the current user with basic profile data.

JWT claims should stay minimal:

```text
sub = user id
email
username
```

Out of scope for MVP:

- Email confirmation.
- Refresh tokens.
- Roles.
- Password reset.
- External login providers.

## Profile

Each user has one profile linked to their Identity user.

Profile fields:

```text
UserId
Username
DisplayName
Bio
AvatarUrl
Region
Languages
Platforms
ExternalLinks
CurrentlyPlayingGames
LookingForPlayers
CreatedAt
UpdatedAt
```

For the MVP, `Languages`, `Platforms`, `ExternalLinks`, and `CurrentlyPlayingGames` can be stored as JSON-backed values in PostgreSQL. This avoids extra tables before discovery and filtering exist.

Profile endpoints:

```text
GET /api/profiles/{username}
PUT /api/profiles/me
```

`GET /api/profiles/{username}` is public and returns a public user profile.

`PUT /api/profiles/me` requires JWT authentication and updates the current user's profile.

Example update request:

```json
{
  "displayName": "Viktor",
  "bio": "Mostly RPGs and co-op games.",
  "avatarUrl": "https://example.com/avatar.png",
  "region": "EU",
  "languages": ["Swedish", "English"],
  "platforms": ["PC", "PlayStation"],
  "currentlyPlayingGames": ["Helldivers 2", "Baldur's Gate 3"],
  "externalLinks": {
    "steam": "https://steamcommunity.com/example",
    "discord": "viktor#1234"
  },
  "lookingForPlayers": true
}
```

## Data Flow

The backend is designed so Angular can be added later without changing the API shape.

```text
Future Angular frontend
  -> REST API
  -> Application services
  -> Identity and profile logic
  -> EF Core
  -> PostgreSQL via Docker Compose
```

Controllers should stay thin. Business rules and orchestration belong in application services. EF Core and Identity implementation details stay in infrastructure.

## Error Handling

The API should return consistent HTTP responses:

- `400 Bad Request` for validation errors.
- `401 Unauthorized` for missing or invalid JWTs.
- `404 Not Found` when a public profile does not exist.
- `409 Conflict` when email or username already exists.

Validation rules:

- Email is required and must be valid.
- Username is required, unique, and length-limited.
- Password follows ASP.NET Core Identity password rules.
- Profile string fields have max lengths.
- Arrays such as languages, platforms, and currently playing games have max item counts.
- `CurrentlyPlayingGames` is a string array for MVP, with a suggested max of 20 games.

## Testing Strategy

The first implementation should include tests where they provide clear value without slowing down the MVP.

Recommended verification:

- Unit tests for application services where practical.
- Integration tests for auth/profile endpoints when the project structure is in place.
- `dotnet test` as the standard verification command.
- Manual API verification through Swagger or HTTP files during early development.
- EF Core migrations verified against the Docker Compose PostgreSQL database.

## MVP Scope

Included:

- Backend solution and projects.
- PostgreSQL via Docker Compose.
- EF Core and Identity setup.
- Register, login, and current-user endpoints.
- Public profile read endpoint.
- Authenticated profile update endpoint.
- Basic validation and error handling.

Excluded:

- Angular frontend.
- Posts/logs.
- Image or video upload.
- Discovery/search/filtering.
- Threads/discussions.
- Friends.
- Private chat.
- External provider login.
- Password reset and email confirmation.
