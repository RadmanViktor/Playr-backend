# PLAYR — Purple Dashboard Design System + App Shell

## Context

The PLAYR frontend is a React + Vite + TypeScript + Tailwind (v4) app living in the
sibling repository `../playr-frontend`. It currently implements auth (Login,
Register, protected Dashboard) in a **terminal / green-neon HUD** style
(`TerminalFrame`, JetBrains Mono, `#0a0e14` bg, neon-green accents).

The product owner supplied a reference image of a **dark purple gaming
dashboard** (sidebar nav, rounded cards, purple accents, avatars, mood badges,
right-hand widget rail) and asked to update the design to match it.

Earlier PLAYR spec text mentioned Angular; this is superseded — the frontend
**stays React** (existing sibling repo). This spec covers a **visual overhaul**
plus a reusable **app shell**, not feature implementation.

## Decisions (locked)

- **Framework:** React (keep existing `../playr-frontend` repo). Ignore the
  "Angular" wording from the product brief.
- **Visual style:** Adopt the purple dashboard reference image **everywhere**,
  including auth pages. Retire the terminal/green-HUD look.
- **Font:** Switch to Inter (modern sans-serif). Drop JetBrains Mono as the app
  font.
- **Icons:** Add `lucide-react`.

## Goals

- Define a design-token system (colors, typography, radii) via Tailwind v4
  `@theme` in `index.css`, matching the reference image.
- Build a reusable set of presentational UI primitives.
- Build a responsive **app shell** (sidebar + top bar + main + optional right
  rail) that authenticated pages render inside.
- Restyle the existing Login/Register pages to the purple style.

## Non-goals (this session)

- Feature widgets: feed posts, currently-playing, looking-to-play cards,
  trending threads, suggested players. These are built later ON the shell.
- Backend changes. This is frontend-only.
- Full mobile polish (basic responsive only).

## Design tokens

Defined in `index.css` via `@theme`. Approximate values (tune during build):

| Token | Value | Use |
|---|---|---|
| `--color-bg` | `#0a0710` | page background (very dark purple-black) |
| `--color-surface` | `#15101f` | cards, sidebar |
| `--color-surface-raised` | `#1e1730` | inputs, nested cards |
| `--color-border` | `#2a2140` | 1px card borders |
| `--color-primary` | `#8b3dff` (hover `#7a2ff0`) | buttons, active nav, accents |
| `--color-text` | `#f2eefb` | primary text |
| `--color-text-muted` | `#a294c0` | secondary text |
| Status colors | enjoying=green, need-help=amber, frustrated=red, completed=blue | mood badges |

- **Radius:** cards `rounded-xl` (12px), inputs `rounded-lg`, avatars/pills
  `rounded-full`.
- **Glow:** purple box-shadow on active/hover for primary elements.
- **Font:** Inter (loaded via Google Fonts or `@fontsource`), monospace reserved
  only for code-like snippets if needed.

## Component architecture

### `src/components/ui/` (presentational primitives)

- **Avatar** — image + online-status dot (green/amber/gray), size variants.
- **Badge** — pill for moods (`Enjoying`/`Need Help`/`Frustrated`/`Completed`)
  and tags (`#EldenRing`); color variants from tokens.
- **Button** — `primary` (filled purple + glow), `secondary` (surface),
  `ghost`; size variants.
- **Card** — rounded bordered surface container; `CardHeader` (icon + title +
  optional "View All" link).
- **IconButton** — square rounded button for header/post actions.
- **SearchInput** — header search field.

### `src/components/layout/`

- **AppShell** — CSS grid `[sidebar | main | (optional right rail)]`. Responsive:
  right rail hides on narrow screens; sidebar collapses toward icons on mobile
  (basic only).
- **Sidebar** — logo, primary nav (Home / Feed / Find Players / Threads /
  Profile) with active-state highlight (React Router `NavLink`), "Create Post"
  primary button, user card, promo card, "Find Players" button.
- **TopBar** — search, notifications bell (badge count), messages icon, avatar
  dropdown trigger.
- **RightRail** — slot container; feature widgets plug in later.

### Other

- **AuthPanel** — replaces `TerminalFrame`; purple-styled centered panel for
  standalone auth pages.
- **ProtectedRoute** — reused as-is.

## Routing

- `/login`, `/register` → standalone (AuthPanel on dark purple bg, no shell).
- Protected routes render inside `AppShell` via a layout route with `<Outlet/>`:

```
/login, /register        → standalone (AuthPanel)
/ (protected, AppShell)
  ├── index → HomePage (placeholder regions)
  ├── /feed         → FeedPage (placeholder)
  ├── /find-players → FindPlayersPage (placeholder)
  ├── /threads      → ThreadsPage (placeholder)
  └── /profile      → ProfilePage (placeholder)
```

`App.tsx` restructured so `ProtectedRoute` wraps an `AppShell` layout route.

## File plan (`../playr-frontend`)

```
src/
  index.css                    # + @theme tokens, Inter font, purple base
  App.tsx                      # restructured routes (layout route)
  components/
    ui/       Avatar, Badge, Button, Card, IconButton, SearchInput (+ tests)
    layout/   AppShell, Sidebar, TopBar, RightRail (+ tests)
    ProtectedRoute.tsx         # reused
    AuthPanel.tsx              # replaces TerminalFrame (purple)
  pages/
    LoginPage, RegisterPage    # restyled
    HomePage.tsx               # placeholder regions inside shell
    FeedPage, FindPlayersPage, ThreadsPage, ProfilePage  # placeholders
```

## Dependencies to add

- `lucide-react` (icons)
- Inter font (Google Fonts link or `@fontsource/inter`)

## Testing

Vitest + React Testing Library (already configured).

- New: AppShell renders sidebar + nav; NavLink active state; Button variants
  render correct classes; Avatar status dot.
- Updated: auth page tests adjusted for `TerminalFrame` → `AuthPanel` rename and
  new class names; auth submit behavior must remain unchanged.

## Open questions

None outstanding.
