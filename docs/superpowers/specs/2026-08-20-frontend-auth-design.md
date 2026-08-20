# Playr Frontend – Login & Register Design

## Context

The `playr-frontend` repository (`../playr-frontend` relative to this repo) currently
contains only an empty initial commit. This spec covers bootstrapping a new React
frontend from scratch, starting with the Login and Register flows against the
existing Playr backend API (`src/Playr.Api`).

## Goals

- Stand up a new Vite + React + TypeScript project in `../playr-frontend`.
- Implement Login and Register pages with a modern, creative visual style
  (terminal/game-HUD inspired — not a generic centered "card").
- Wire the pages to the existing backend auth endpoints.
- Provide a minimal protected placeholder page to land on after authentication.

## Non-goals

- Building out any feature beyond auth (profiles, matching, etc.).
- Production deployment configuration.
- Password reset / email verification flows (not present in the backend yet).

## Stack

- **Build tool:** Vite
- **Framework:** React 18 + TypeScript
- **Styling:** Tailwind CSS
- **Routing:** React Router (`/login`, `/register`, `/` protected placeholder)
- **State:** A single `AuthContext` (React context) backed by `localStorage` for
  the JWT access token. No external state management library needed at this scope.
- **Testing:** Vitest + React Testing Library for component/behavior tests.

## Backend API (existing, reference only)

- `POST /api/auth/register` — body: `{ email, username, password }` → `201/200`
  with user info, or `409 Conflict` with `{ error }` on duplicate email/username.
- `POST /api/auth/login` — body: `{ usernameOrEmail, password }` → `200` with
  `{ accessToken, expiresAt }`, or `401 Unauthorized` with `{ error }`.
- `GET /api/auth/me` — requires `Authorization: Bearer <token>` → current user
  info, or `401` if missing/invalid.
- Dev backend runs at `http://localhost:5258` (http profile) /
  `https://localhost:7014` (https profile).

## Required backend change

The backend currently has no CORS configuration (`Program.cs`), which will block
browser requests from the Vite dev server. Add a narrowly-scoped CORS policy:

- Allow origin `http://localhost:5173` (Vite default dev port)
- Allow credentials, standard headers, and the methods used by the auth/profile
  controllers (GET/POST/PUT)
- Only wire this into the existing pipeline (`app.UseCors(...)` before
  `UseAuthentication`/`UseAuthorization`), no broader refactor.

## Visual design: Terminal / Game-HUD style

- **Background:** near-black (`#0a0e14`-ish), optional subtle scanline/grid texture.
- **Typography:** monospace font (e.g. JetBrains Mono / Space Mono) for labels,
  headings, and buttons.
- **Accent color:** neon green or cyan glow (e.g. `#39ff14` / `#00f0ff`) used for
  focus states, borders, and button glow-on-hover.
- **No card/box-shadow container.** Instead, a thin 1px accent-colored border
  frames the whole form, with a "title bar" line above it (e.g.
  `> playr_auth --login`), like a terminal window. Sharp corners
  (`border-radius: 0`), no drop shadows.
- **Inputs:** transparent background, accent-colored underline/border, glowing
  focus state, muted gray placeholder text.
- **Buttons:** outline style (no filled background) that fills with a glow
  effect on hover; label text in brackets and uppercase, e.g. `[ LOG IN ]`,
  `[ REGISTER ]`.
- **Details:** optional typing animation on the heading (e.g.
  `Welcome to Playr_` with a blinking cursor block); error messages rendered as
  a terminal-style error line (e.g. `ERROR: invalid credentials`) in
  red/orange glow.
- **Layout:** form is centered on the page but not boxed in a white/shadowed
  card — it sits directly on the dark background, delimited only by the thin
  terminal frame and title bar.
- A small reusable `TerminalFrame` component encapsulates the title bar + thin
  border chrome so both Login and Register (and future auth-adjacent pages)
  share the same shell.

## Pages / Routes

1. **Login** (`/login`)
   - Fields: `usernameOrEmail`, `password`
   - Submit button: `[ LOG IN ]`
   - On success: store access token, redirect to `/`
   - On `401`: show terminal-style error line
   - Link to Register: `> register instead`

2. **Register** (`/register`)
   - Fields: `email`, `username`, `password`
   - Submit button: `[ REGISTER ]`
   - On success: `POST /api/auth/register` returns user info only (no token),
     so the client immediately follows up with `POST /api/auth/login` using
     the same username/password to obtain an access token, stores it, and
     redirects to `/`
   - On `409` (register conflict) or `401` (unlikely follow-up login failure):
     show terminal-style error line
   - Link to Login: `> login instead`

3. **Dashboard placeholder** (`/`, protected)
   - Requires a valid stored token; otherwise redirect to `/login`
   - Fetches `GET /api/auth/me` and displays `Welcome, {username}_` (with the
     typing-cursor visual treatment)
   - Logout button clears the stored token and redirects to `/login`

## Auth flow

- `AuthContext` exposes: current user (or null), loading state, `login()`,
  `register()`, `logout()`.
- Token persisted in `localStorage`; included as `Authorization: Bearer <token>`
  on authenticated requests.
- `ProtectedRoute` wrapper component checks for a valid session (via context)
  and redirects unauthenticated users to `/login`.

## Project structure

```
src/
  api/authApi.ts          // fetch wrappers for register/login/me
  context/AuthContext.tsx
  components/ProtectedRoute.tsx
  components/TerminalFrame.tsx
  pages/LoginPage.tsx
  pages/RegisterPage.tsx
  pages/DashboardPage.tsx
  App.tsx
  main.tsx
```

## Testing

- Vitest + React Testing Library.
- Cover: form validation (empty fields), successful login/register flow
  (mocked API), error handling (401/409 responses), protected route redirect
  behavior.

## Open questions

None outstanding — the register/login token flow is resolved above.
