# PLAYR Angular Auth and Profile Design

## Summary

This phase adds the first Angular frontend for PLAYR. It focuses on a thin but working user experience over the backend features that already exist: register, login, current user, public profile, and profile editing.

Games, posts, looking-to-play cards, threads, and rich media remain out of scope for this frontend phase because the backend does not implement them yet.

## Decisions

- Frontend framework: modern Angular with standalone components.
- Frontend location: `src/Playr.Web`.
- API integration: REST calls to the existing ASP.NET Core API.
- Authentication: JWT stored in `localStorage` for the MVP.
- Route protection: functional Angular route guards.
- HTTP authentication: functional Angular HTTP interceptor registered with `provideHttpClient(withInterceptors(...))`.
- Styling: simple custom CSS, responsive enough for desktop and mobile.

## Angular Structure

```text
src/Playr.Web/
  angular.json
  package.json
  src/
    app/
      app.component.*
      app.config.ts
      app.routes.ts
      core/
        api/
          api.config.ts
        auth/
          auth.interceptor.ts
          auth.guard.ts
          auth.service.ts
          auth.models.ts
        profile/
          profile.service.ts
          profile.models.ts
      features/
        auth/
          login-page.*
          register-page.*
        profile/
          my-profile-page.*
          public-profile-page.*
          edit-profile-page.*
      shared/
```

This keeps the app beginner-friendly while still separating app-wide services from feature pages.

## Routes

```text
/login
/register
/me
/profile/:username
/profile/edit
```

Protected routes:

- `/me`
- `/profile/edit`

Unauthenticated users who navigate to protected routes are redirected to `/login`.

## Auth Flow

`AuthService` owns the token and current user state. It calls:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
```

Login stores the JWT in `localStorage`. Logout removes it and clears current user state.

The HTTP interceptor adds `Authorization: Bearer <token>` to API requests when a token exists.

Register creates the backend account, then the user can log in. The backend currently does not return a token from register, so auto-login is intentionally not required in this phase.

## Profile Flow

Profile pages call:

```text
GET /api/profiles/{username}
PUT /api/profiles/me
```

`/me` loads the current auth user, then loads the public profile by username. `/profile/:username` loads any public profile. `/profile/edit` loads the current user's profile and submits editable profile fields.

Editable fields:

- Display name
- Bio
- Avatar URL
- Region
- Languages
- Platforms
- External links
- Currently playing games
- Looking for players

For this phase, list and dictionary fields can use simple textarea inputs rather than complex custom controls.

## Error Handling

The UI should show simple inline errors for failed login, failed register, missing profile, invalid profile updates, and network errors. The first version does not need global toast notifications.

## Testing and Verification

Verification should include:

- `npm install` in `src/Playr.Web` after scaffolding.
- Angular build command from the generated project.
- Existing backend tests via `dotnet test Playr.sln` when backend-facing assumptions are changed.

Manual smoke test:

- Register a user.
- Log in.
- Open `/me`.
- Edit profile.
- Open `/profile/{username}`.

## Out of Scope

- Games UI.
- Feed UI.
- Posts/play logs UI.
- Looking-to-play UI.
- Threads UI.
- File uploads.
- Refresh tokens.
- Complex design system.
