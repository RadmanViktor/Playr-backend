# PLAYR Purple Design System + App Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the terminal/green-HUD frontend look with a dark-purple gaming-dashboard design system and a reusable app shell (sidebar + top bar + right rail), and restyle the auth pages to match.

**Architecture:** Presentational UI primitives in `src/components/ui/`, layout components in `src/components/layout/`, design tokens declared via Tailwind v4 `@theme` in `index.css`. Authenticated pages render inside an `AppShell` layout route; auth pages render standalone in an `AuthPanel`.

**Tech Stack:** React 18, TypeScript, Vite, Tailwind CSS v4 (CSS-config via `@theme`), React Router 7, lucide-react, Inter font, Vitest + React Testing Library.

## Global Constraints

- All frontend work happens in the sibling repo `../playr-frontend` (NOT the `Playr` backend repo). All paths below are relative to `../playr-frontend`.
- Tailwind v4 is configured via CSS (`@import "tailwindcss"` in `src/index.css`); there is NO `tailwind.config.js`. Custom tokens go in an `@theme { ... }` block.
- Font: Inter. Monospace is retired as the app font.
- Icons: use `lucide-react`.
- Existing auth behavior (login/register submit, validation, error handling, protected redirect) MUST remain functionally unchanged; only visuals change.
- `UserResponse` shape (from `src/api/authApi.ts`): `{ id: string; email: string; username: string; displayName: string | null }`.
- Run tests with `npm test` (`vitest run`) from `../playr-frontend`.
- Commit after each task.

---

### Task 1: Install dependencies (lucide-react + Inter font)

**Files:**
- Modify: `package.json` (via npm install)

**Interfaces:**
- Consumes: nothing.
- Produces: `lucide-react` importable; `@fontsource/inter` importable.

- [ ] **Step 1: Install packages**

Run in `../playr-frontend`:
```bash
npm install lucide-react @fontsource/inter
```
Expected: both added to `dependencies` in `package.json`, no errors.

- [ ] **Step 2: Verify install**

Run: `npm ls lucide-react @fontsource/inter`
Expected: both listed with resolved versions.

- [ ] **Step 3: Commit**

```bash
git add package.json package-lock.json
git commit -m "chore: add lucide-react and Inter font"
```

---

### Task 2: Design tokens + base styles in index.css

**Files:**
- Modify: `src/index.css`

**Interfaces:**
- Consumes: nothing.
- Produces: Tailwind utility classes backed by theme tokens: `bg-bg`, `bg-surface`, `bg-surface-raised`, `border-border`, `bg-primary`, `text-primary`, `text-text`, `text-muted`, plus status colors `enjoying`, `need-help`, `frustrated`, `completed` (usable as `bg-enjoying`, `text-enjoying`, etc.). Inter is the default body font.

- [ ] **Step 1: Replace index.css contents**

Replace the entire file `src/index.css` with:
```css
@import "tailwindcss";
@import "@fontsource/inter/400.css";
@import "@fontsource/inter/500.css";
@import "@fontsource/inter/600.css";
@import "@fontsource/inter/700.css";

@theme {
  --color-bg: #0a0710;
  --color-surface: #15101f;
  --color-surface-raised: #1e1730;
  --color-border: #2a2140;
  --color-primary: #8b3dff;
  --color-primary-hover: #7a2ff0;
  --color-text: #f2eefb;
  --color-muted: #a294c0;
  --color-enjoying: #34d399;
  --color-need-help: #fbbf24;
  --color-frustrated: #f87171;
  --color-completed: #60a5fa;

  --font-sans: 'Inter', ui-sans-serif, system-ui, sans-serif;
}

:root {
  color-scheme: dark;
}

html, body, #root {
  height: 100%;
}

body {
  margin: 0;
  background-color: var(--color-bg);
  color: var(--color-text);
  font-family: var(--font-sans);
}
```

- [ ] **Step 2: Verify build compiles**

Run: `npm run build`
Expected: build succeeds with no CSS/TS errors.

- [ ] **Step 3: Commit**

```bash
git add src/index.css
git commit -m "feat: add purple design tokens and Inter base styles"
```

---

### Task 3: Button primitive

**Files:**
- Create: `src/components/ui/Button.tsx`
- Test: `src/components/ui/Button.test.tsx`

**Interfaces:**
- Consumes: token classes from Task 2.
- Produces: `Button` component. Props: `variant?: 'primary' | 'secondary' | 'ghost'` (default `'primary'`), `size?: 'sm' | 'md'` (default `'md'`), plus all native `button` attributes (`...props`). Renders a `<button>` with `data-variant={variant}` and combined className.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { Button } from './Button'

describe('Button', () => {
  it('renders children and defaults to primary variant', () => {
    render(<Button>Post</Button>)
    const btn = screen.getByRole('button', { name: 'Post' })
    expect(btn).toHaveAttribute('data-variant', 'primary')
  })

  it('applies the requested variant', () => {
    render(<Button variant="ghost">More</Button>)
    expect(screen.getByRole('button', { name: 'More' })).toHaveAttribute('data-variant', 'ghost')
  })

  it('forwards native button props', () => {
    render(<Button disabled>Nope</Button>)
    expect(screen.getByRole('button', { name: 'Nope' })).toBeDisabled()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- Button`
Expected: FAIL — cannot resolve `./Button`.

- [ ] **Step 3: Write the implementation**

```tsx
import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost'
type Size = 'sm' | 'md'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
}

const variantClasses: Record<Variant, string> = {
  primary:
    'bg-primary text-white hover:bg-primary-hover shadow-[0_0_16px_-4px_var(--color-primary)]',
  secondary: 'bg-surface-raised text-text hover:bg-border',
  ghost: 'bg-transparent text-muted hover:text-text hover:bg-surface-raised',
}

const sizeClasses: Record<Size, string> = {
  sm: 'px-3 py-1.5 text-sm',
  md: 'px-4 py-2 text-sm',
}

export function Button({
  variant = 'primary',
  size = 'md',
  className = '',
  ...props
}: ButtonProps) {
  return (
    <button
      data-variant={variant}
      className={`inline-flex items-center justify-center gap-2 rounded-lg font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${variantClasses[variant]} ${sizeClasses[size]} ${className}`}
      {...props}
    />
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- Button`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/Button.tsx src/components/ui/Button.test.tsx
git commit -m "feat: add Button ui primitive"
```

---

### Task 4: Avatar primitive

**Files:**
- Create: `src/components/ui/Avatar.tsx`
- Test: `src/components/ui/Avatar.test.tsx`

**Interfaces:**
- Consumes: token classes.
- Produces: `Avatar` component. Props: `src?: string`, `alt: string`, `size?: 'sm' | 'md' | 'lg'` (default `'md'`), `status?: 'online' | 'in-game' | 'offline'`. Renders a rounded-full `<img>` (or a fallback initial `<span>` when no `src`); when `status` is set, renders a status dot element with `data-status={status}`.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { Avatar } from './Avatar'

describe('Avatar', () => {
  it('renders an image with alt text', () => {
    render(<Avatar src="/a.png" alt="PlayerOne" />)
    expect(screen.getByRole('img', { name: 'PlayerOne' })).toHaveAttribute('src', '/a.png')
  })

  it('renders a fallback initial when no src', () => {
    render(<Avatar alt="Zoe" />)
    expect(screen.getByText('Z')).toBeInTheDocument()
  })

  it('renders a status dot when status is provided', () => {
    render(<Avatar alt="Zoe" status="online" />)
    expect(screen.getByTestId('avatar-status')).toHaveAttribute('data-status', 'online')
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- Avatar`
Expected: FAIL — cannot resolve `./Avatar`.

- [ ] **Step 3: Write the implementation**

```tsx
type Size = 'sm' | 'md' | 'lg'
type Status = 'online' | 'in-game' | 'offline'

interface AvatarProps {
  src?: string
  alt: string
  size?: Size
  status?: Status
}

const sizeClasses: Record<Size, string> = {
  sm: 'h-8 w-8 text-xs',
  md: 'h-10 w-10 text-sm',
  lg: 'h-12 w-12 text-base',
}

const statusColor: Record<Status, string> = {
  online: 'bg-enjoying',
  'in-game': 'bg-need-help',
  offline: 'bg-muted',
}

export function Avatar({ src, alt, size = 'md', status }: AvatarProps) {
  return (
    <span className={`relative inline-flex shrink-0 ${sizeClasses[size]}`}>
      {src ? (
        <img
          src={src}
          alt={alt}
          className="h-full w-full rounded-full object-cover"
        />
      ) : (
        <span
          aria-label={alt}
          className="flex h-full w-full items-center justify-center rounded-full bg-surface-raised font-semibold text-text uppercase"
        >
          {alt.charAt(0)}
        </span>
      )}
      {status && (
        <span
          data-testid="avatar-status"
          data-status={status}
          className={`absolute bottom-0 right-0 h-2.5 w-2.5 rounded-full border-2 border-surface ${statusColor[status]}`}
        />
      )}
    </span>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- Avatar`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/Avatar.tsx src/components/ui/Avatar.test.tsx
git commit -m "feat: add Avatar ui primitive"
```

---

### Task 5: Badge primitive

**Files:**
- Create: `src/components/ui/Badge.tsx`
- Test: `src/components/ui/Badge.test.tsx`

**Interfaces:**
- Consumes: token classes.
- Produces: `Badge` component. Props: `children: ReactNode`, `variant?: 'enjoying' | 'need-help' | 'frustrated' | 'completed' | 'tag'` (default `'tag'`). Renders a rounded-full `<span>` with `data-variant`.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { Badge } from './Badge'

describe('Badge', () => {
  it('renders children with default tag variant', () => {
    render(<Badge>#EldenRing</Badge>)
    const el = screen.getByText('#EldenRing')
    expect(el).toHaveAttribute('data-variant', 'tag')
  })

  it('applies a mood variant', () => {
    render(<Badge variant="need-help">Need Help</Badge>)
    expect(screen.getByText('Need Help')).toHaveAttribute('data-variant', 'need-help')
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- Badge`
Expected: FAIL — cannot resolve `./Badge`.

- [ ] **Step 3: Write the implementation**

```tsx
import type { ReactNode } from 'react'

type Variant = 'enjoying' | 'need-help' | 'frustrated' | 'completed' | 'tag'

interface BadgeProps {
  children: ReactNode
  variant?: Variant
}

const variantClasses: Record<Variant, string> = {
  enjoying: 'bg-enjoying/15 text-enjoying',
  'need-help': 'bg-need-help/15 text-need-help',
  frustrated: 'bg-frustrated/15 text-frustrated',
  completed: 'bg-completed/15 text-completed',
  tag: 'bg-surface-raised text-muted',
}

export function Badge({ children, variant = 'tag' }: BadgeProps) {
  return (
    <span
      data-variant={variant}
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${variantClasses[variant]}`}
    >
      {children}
    </span>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- Badge`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/Badge.tsx src/components/ui/Badge.test.tsx
git commit -m "feat: add Badge ui primitive"
```

---

### Task 6: Card + CardHeader primitives

**Files:**
- Create: `src/components/ui/Card.tsx`
- Test: `src/components/ui/Card.test.tsx`

**Interfaces:**
- Consumes: token classes.
- Produces: `Card` component (props: `children`, `className?`) rendering a bordered rounded surface `<div>`. `CardHeader` component (props: `title: string`, `icon?: ReactNode`, `action?: ReactNode`) rendering title + optional leading icon + optional right-aligned action node.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { Card, CardHeader } from './Card'

describe('Card', () => {
  it('renders children', () => {
    render(<Card>content</Card>)
    expect(screen.getByText('content')).toBeInTheDocument()
  })

  it('CardHeader renders title and action', () => {
    render(<CardHeader title="Trending Threads" action={<a href="#">View All</a>} />)
    expect(screen.getByText('Trending Threads')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View All' })).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- Card`
Expected: FAIL — cannot resolve `./Card`.

- [ ] **Step 3: Write the implementation**

```tsx
import type { ReactNode } from 'react'

export function Card({
  children,
  className = '',
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <div className={`rounded-xl border border-border bg-surface p-4 ${className}`}>
      {children}
    </div>
  )
}

export function CardHeader({
  title,
  icon,
  action,
}: {
  title: string
  icon?: ReactNode
  action?: ReactNode
}) {
  return (
    <div className="mb-3 flex items-center justify-between">
      <div className="flex items-center gap-2">
        {icon && <span className="text-primary">{icon}</span>}
        <h2 className="text-sm font-semibold text-text">{title}</h2>
      </div>
      {action && <div className="text-xs text-primary">{action}</div>}
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- Card`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/Card.tsx src/components/ui/Card.test.tsx
git commit -m "feat: add Card and CardHeader ui primitives"
```

---

### Task 7: IconButton + SearchInput primitives

**Files:**
- Create: `src/components/ui/IconButton.tsx`
- Create: `src/components/ui/SearchInput.tsx`
- Test: `src/components/ui/IconButton.test.tsx`
- Test: `src/components/ui/SearchInput.test.tsx`

**Interfaces:**
- Consumes: token classes.
- Produces:
  - `IconButton`: props = all native `button` attributes plus `children: ReactNode` (the icon) and required `aria-label`. Renders a square rounded `<button>`.
  - `SearchInput`: props = all native `input` attributes; default `type="search"`, default `placeholder="Search PLAYR"`, `aria-label="Search PLAYR"`. Renders a search icon + `<input>` inside a rounded surface container.

- [ ] **Step 1: Write the failing tests**

`src/components/ui/IconButton.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react'
import { IconButton } from './IconButton'

describe('IconButton', () => {
  it('renders an accessible icon button', () => {
    render(<IconButton aria-label="Notifications">*</IconButton>)
    expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument()
  })
})
```

`src/components/ui/SearchInput.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react'
import { SearchInput } from './SearchInput'

describe('SearchInput', () => {
  it('renders a search input with default label', () => {
    render(<SearchInput />)
    expect(screen.getByRole('searchbox', { name: 'Search PLAYR' })).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm test -- IconButton SearchInput`
Expected: FAIL — cannot resolve modules.

- [ ] **Step 3: Write the implementations**

`src/components/ui/IconButton.tsx`:
```tsx
import type { ButtonHTMLAttributes, ReactNode } from 'react'

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode
  'aria-label': string
}

export function IconButton({ children, className = '', ...props }: IconButtonProps) {
  return (
    <button
      className={`flex h-9 w-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-raised hover:text-text ${className}`}
      {...props}
    >
      {children}
    </button>
  )
}
```

`src/components/ui/SearchInput.tsx`:
```tsx
import type { InputHTMLAttributes } from 'react'
import { Search } from 'lucide-react'

export function SearchInput({
  className = '',
  placeholder = 'Search PLAYR',
  ...props
}: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-border bg-surface-raised px-3 py-2">
      <Search className="h-4 w-4 text-muted" aria-hidden="true" />
      <input
        type="search"
        aria-label="Search PLAYR"
        placeholder={placeholder}
        className={`w-full bg-transparent text-sm text-text outline-none placeholder:text-muted ${className}`}
        {...props}
      />
    </div>
  )
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm test -- IconButton SearchInput`
Expected: PASS (2 tests total).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/IconButton.tsx src/components/ui/IconButton.test.tsx src/components/ui/SearchInput.tsx src/components/ui/SearchInput.test.tsx
git commit -m "feat: add IconButton and SearchInput ui primitives"
```

---

### Task 8: Sidebar layout component

**Files:**
- Create: `src/components/layout/Sidebar.tsx`
- Test: `src/components/layout/Sidebar.test.tsx`

**Interfaces:**
- Consumes: `Button` (Task 3), `Avatar` (Task 4), `useAuth` (`src/context/AuthContext.tsx`), `NavLink`/`react-router-dom`, lucide icons.
- Produces: `Sidebar` component (no props). Renders: PLAYR logo, nav links (Home `/`, Feed `/feed`, Find Players `/find-players`, Threads `/threads`, Profile `/profile`) using `NavLink` with active styling, a "Create Post" primary `Button`, a user card (avatar + username from `useAuth`), a promo card, and a "Find Players" `Button`.
- **Test note:** Must be rendered inside a `MemoryRouter` and an `AuthProvider`, OR the test mocks `useAuth`. Use mock (see test).

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from './Sidebar'

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: '1', email: 'a@b.c', username: 'PlayerOne', displayName: null },
    logout: vi.fn(),
  }),
}))

function renderSidebar() {
  return render(
    <MemoryRouter>
      <Sidebar />
    </MemoryRouter>,
  )
}

describe('Sidebar', () => {
  it('renders primary nav links', () => {
    renderSidebar()
    expect(screen.getByRole('link', { name: /home/i })).toHaveAttribute('href', '/')
    expect(screen.getByRole('link', { name: /find players/i })).toHaveAttribute('href', '/find-players')
    expect(screen.getByRole('link', { name: /threads/i })).toHaveAttribute('href', '/threads')
  })

  it('shows the current username', () => {
    renderSidebar()
    expect(screen.getByText('PlayerOne')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- Sidebar`
Expected: FAIL — cannot resolve `./Sidebar`.

- [ ] **Step 3: Write the implementation**

```tsx
import { NavLink } from 'react-router-dom'
import { Home, Rss, Users, MessageSquare, User, Plus } from 'lucide-react'
import { Button } from '../ui/Button'
import { Avatar } from '../ui/Avatar'
import { useAuth } from '../../context/AuthContext'

const navItems = [
  { to: '/', label: 'Home', icon: Home, end: true },
  { to: '/feed', label: 'Feed', icon: Rss, end: false },
  { to: '/find-players', label: 'Find Players', icon: Users, end: false },
  { to: '/threads', label: 'Threads', icon: MessageSquare, end: false },
  { to: '/profile', label: 'Profile', icon: User, end: false },
]

export function Sidebar() {
  const { user } = useAuth()

  return (
    <aside className="flex w-64 shrink-0 flex-col gap-6 border-r border-border bg-surface p-4">
      <div className="flex items-center gap-2 px-2">
        <span className="text-2xl font-bold tracking-tight text-primary">PLAYR</span>
      </div>

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
      </nav>

      <Button className="w-full">
        <Plus className="h-4 w-4" aria-hidden="true" />
        Create Post
      </Button>

      {user && (
        <div className="flex items-center gap-3 rounded-xl border border-border bg-surface-raised p-3">
          <Avatar alt={user.displayName ?? user.username} status="online" />
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-text">{user.username}</p>
            <p className="text-xs text-enjoying">Online</p>
          </div>
        </div>
      )}

      <div className="rounded-xl border border-border bg-surface-raised p-4">
        <p className="text-sm font-semibold text-text">Level up your connections.</p>
        <p className="mt-1 text-xs text-muted">
          Find teammates, share wins, and build your squad.
        </p>
      </div>

      <Button variant="secondary" className="w-full">
        Find Players
      </Button>
    </aside>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- Sidebar`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/layout/Sidebar.tsx src/components/layout/Sidebar.test.tsx
git commit -m "feat: add Sidebar layout component"
```

---

### Task 9: TopBar layout component

**Files:**
- Create: `src/components/layout/TopBar.tsx`
- Test: `src/components/layout/TopBar.test.tsx`

**Interfaces:**
- Consumes: `SearchInput` (Task 7), `IconButton` (Task 7), `Avatar` (Task 4), `useAuth`, lucide icons (`Bell`, `Mail`).
- Produces: `TopBar` component (no props). Renders search input, a notifications `IconButton` (aria-label "Notifications"), a messages `IconButton` (aria-label "Messages"), and the current user's `Avatar`.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { TopBar } from './TopBar'

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: '1', email: 'a@b.c', username: 'PlayerOne', displayName: null },
    logout: vi.fn(),
  }),
}))

describe('TopBar', () => {
  it('renders search and action buttons', () => {
    render(<TopBar />)
    expect(screen.getByRole('searchbox', { name: 'Search PLAYR' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Messages' })).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- TopBar`
Expected: FAIL — cannot resolve `./TopBar`.

- [ ] **Step 3: Write the implementation**

```tsx
import { Bell, Mail } from 'lucide-react'
import { SearchInput } from '../ui/SearchInput'
import { IconButton } from '../ui/IconButton'
import { Avatar } from '../ui/Avatar'
import { useAuth } from '../../context/AuthContext'

export function TopBar() {
  const { user } = useAuth()

  return (
    <header className="flex items-center gap-4 border-b border-border bg-surface px-6 py-3">
      <div className="w-full max-w-md">
        <SearchInput />
      </div>
      <div className="ml-auto flex items-center gap-2">
        <IconButton aria-label="Notifications">
          <Bell className="h-5 w-5" aria-hidden="true" />
        </IconButton>
        <IconButton aria-label="Messages">
          <Mail className="h-5 w-5" aria-hidden="true" />
        </IconButton>
        {user && <Avatar alt={user.displayName ?? user.username} status="online" />}
      </div>
    </header>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- TopBar`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/components/layout/TopBar.tsx src/components/layout/TopBar.test.tsx
git commit -m "feat: add TopBar layout component"
```

---

### Task 10: RightRail + AppShell layout components

**Files:**
- Create: `src/components/layout/RightRail.tsx`
- Create: `src/components/layout/AppShell.tsx`
- Test: `src/components/layout/AppShell.test.tsx`

**Interfaces:**
- Consumes: `Sidebar` (Task 8), `TopBar` (Task 9), `Outlet`/`react-router-dom`.
- Produces:
  - `RightRail`: props = `{ children: ReactNode }`; renders an `<aside>` that hides below the `xl` breakpoint (`hidden xl:flex`).
  - `AppShell`: no props. Renders `Sidebar`, `TopBar`, and a main content area containing `<Outlet />`. This is a React Router layout route element.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { AppShell } from './AppShell'

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: '1', email: 'a@b.c', username: 'PlayerOne', displayName: null },
    logout: vi.fn(),
  }),
}))

describe('AppShell', () => {
  it('renders sidebar nav and routed child content', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<div>Home content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.getByRole('link', { name: /home/i })).toBeInTheDocument()
    expect(screen.getByText('Home content')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- AppShell`
Expected: FAIL — cannot resolve `./AppShell`.

- [ ] **Step 3: Write the implementations**

`src/components/layout/RightRail.tsx`:
```tsx
import type { ReactNode } from 'react'

export function RightRail({ children }: { children: ReactNode }) {
  return (
    <aside className="hidden w-80 shrink-0 flex-col gap-4 border-l border-border bg-surface p-4 xl:flex">
      {children}
    </aside>
  )
}
```

`src/components/layout/AppShell.tsx`:
```tsx
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { TopBar } from './TopBar'

export function AppShell() {
  return (
    <div className="flex min-h-screen bg-bg text-text">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- AppShell`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/components/layout/RightRail.tsx src/components/layout/AppShell.tsx src/components/layout/AppShell.test.tsx
git commit -m "feat: add RightRail and AppShell layout components"
```

---

### Task 11: Placeholder pages (Home, Feed, FindPlayers, Threads, Profile)

**Files:**
- Create: `src/pages/HomePage.tsx`
- Create: `src/pages/FeedPage.tsx`
- Create: `src/pages/FindPlayersPage.tsx`
- Create: `src/pages/ThreadsPage.tsx`
- Create: `src/pages/ProfilePage.tsx`
- Test: `src/pages/HomePage.test.tsx`

**Interfaces:**
- Consumes: `Card`/`CardHeader` (Task 6).
- Produces: five default-exported page components. `HomePage` renders a heading `Home` and a placeholder `Card` with text "Feed coming soon". The other four render a heading and a "coming soon" placeholder each. These are rendered inside `AppShell`'s `<Outlet />`.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import HomePage from './HomePage'

describe('HomePage', () => {
  it('renders the home heading and placeholder', () => {
    render(<HomePage />)
    expect(screen.getByRole('heading', { name: 'Home' })).toBeInTheDocument()
    expect(screen.getByText('Feed coming soon')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- HomePage`
Expected: FAIL — cannot resolve `./HomePage`.

- [ ] **Step 3: Write the implementations**

`src/pages/HomePage.tsx`:
```tsx
import { Card } from '../components/ui/Card'

export default function HomePage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-text">Home</h1>
      <Card>
        <p className="text-muted">Feed coming soon</p>
      </Card>
    </div>
  )
}
```

`src/pages/FeedPage.tsx`:
```tsx
import { Card } from '../components/ui/Card'

export default function FeedPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-text">Feed</h1>
      <Card>
        <p className="text-muted">Feed coming soon</p>
      </Card>
    </div>
  )
}
```

`src/pages/FindPlayersPage.tsx`:
```tsx
import { Card } from '../components/ui/Card'

export default function FindPlayersPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-text">Find Players</h1>
      <Card>
        <p className="text-muted">Find Players coming soon</p>
      </Card>
    </div>
  )
}
```

`src/pages/ThreadsPage.tsx`:
```tsx
import { Card } from '../components/ui/Card'

export default function ThreadsPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-text">Threads</h1>
      <Card>
        <p className="text-muted">Threads coming soon</p>
      </Card>
    </div>
  )
}
```

`src/pages/ProfilePage.tsx`:
```tsx
import { Card } from '../components/ui/Card'

export default function ProfilePage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-text">Profile</h1>
      <Card>
        <p className="text-muted">Profile coming soon</p>
      </Card>
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- HomePage`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/pages/HomePage.tsx src/pages/FeedPage.tsx src/pages/FindPlayersPage.tsx src/pages/ThreadsPage.tsx src/pages/ProfilePage.tsx src/pages/HomePage.test.tsx
git commit -m "feat: add placeholder feature pages"
```

---

### Task 12: Wire routes through AppShell in App.tsx

**Files:**
- Modify: `src/App.tsx`

**Interfaces:**
- Consumes: `AppShell` (Task 10), `ProtectedRoute` (`src/components/ProtectedRoute.tsx`), the five pages (Task 11), `LoginPage`/`RegisterPage`.
- Produces: route tree where `/login` and `/register` are standalone and all other routes render inside `AppShell` behind `ProtectedRoute`. Removes the old `DashboardPage` route.

- [ ] **Step 1: Replace App.tsx contents**

```tsx
import { Routes, Route } from 'react-router-dom'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import HomePage from './pages/HomePage'
import FeedPage from './pages/FeedPage'
import FindPlayersPage from './pages/FindPlayersPage'
import ThreadsPage from './pages/ThreadsPage'
import ProfilePage from './pages/ProfilePage'
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
      </Route>
    </Routes>
  )
}

export default App
```

- [ ] **Step 2: Run full test suite**

Run: `npm test`
Expected: All tests pass EXCEPT possibly `DashboardPage.test.tsx` (removed route) and auth page tests (restyle pending). Note failures to resolve in Tasks 13–14.

- [ ] **Step 3: Remove the obsolete DashboardPage and its test**

```bash
git rm src/pages/DashboardPage.tsx src/pages/DashboardPage.test.tsx
```

- [ ] **Step 4: Run full test suite again**

Run: `npm test`
Expected: only auth page tests may still fail (addressed next). No references to `DashboardPage` remain.

- [ ] **Step 5: Commit**

```bash
git add src/App.tsx
git commit -m "feat: route protected pages through AppShell, remove DashboardPage"
```

---

### Task 13: AuthPanel component (replaces TerminalFrame)

**Files:**
- Create: `src/components/AuthPanel.tsx`
- Test: `src/components/AuthPanel.test.tsx`
- Delete: `src/components/TerminalFrame.tsx`, `src/components/TerminalFrame.test.tsx`

**Interfaces:**
- Consumes: token classes.
- Produces: `AuthPanel` component. Props: `title: string`, `children: ReactNode`. Renders a centered purple-styled panel (rounded `Card`-like surface) with the PLAYR logo, the `title` text, and `children`. Replaces `TerminalFrame`'s role.

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { AuthPanel } from './AuthPanel'

describe('AuthPanel', () => {
  it('renders the title and children', () => {
    render(
      <AuthPanel title="Log in to PLAYR">
        <p>form goes here</p>
      </AuthPanel>,
    )
    expect(screen.getByText('Log in to PLAYR')).toBeInTheDocument()
    expect(screen.getByText('form goes here')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- AuthPanel`
Expected: FAIL — cannot resolve `./AuthPanel`.

- [ ] **Step 3: Write the implementation**

```tsx
import type { ReactNode } from 'react'

export function AuthPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="w-full max-w-md rounded-xl border border-border bg-surface p-8">
      <div className="mb-6 flex flex-col items-center gap-2">
        <span className="text-2xl font-bold tracking-tight text-primary">PLAYR</span>
        <h1 className="text-lg font-semibold text-text">{title}</h1>
      </div>
      {children}
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- AuthPanel`
Expected: PASS (1 test).

- [ ] **Step 5: Delete TerminalFrame**

```bash
git rm src/components/TerminalFrame.tsx src/components/TerminalFrame.test.tsx
```

- [ ] **Step 6: Commit**

```bash
git add src/components/AuthPanel.tsx src/components/AuthPanel.test.tsx
git commit -m "feat: add AuthPanel, remove TerminalFrame"
```

---

### Task 14: Restyle LoginPage and RegisterPage to purple

**Files:**
- Modify: `src/pages/LoginPage.tsx`
- Modify: `src/pages/RegisterPage.tsx`
- Modify: `src/pages/LoginPage.test.tsx` (selector updates only if needed)
- Modify: `src/pages/RegisterPage.test.tsx` (selector updates only if needed)
- Modify: `src/components/ProtectedRoute.tsx` (loading state restyle)

**Interfaces:**
- Consumes: `AuthPanel` (Task 13), `Button` (Task 3), `useAuth`, `ApiError`.
- Produces: restyled login/register pages using `AuthPanel`, purple `Button`, token-colored inputs, and non-terminal error text. Submit/validation/error behavior unchanged.

- [ ] **Step 1: Replace LoginPage.tsx**

```tsx
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AuthPanel } from '../components/AuthPanel'
import { Button } from '../components/ui/Button'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../api/authApi'

interface FieldErrors {
  usernameOrEmail?: string
  password?: string
}

const inputClass =
  'rounded-lg border border-border bg-surface-raised px-3 py-2 text-text outline-none focus:border-primary'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [usernameOrEmail, setUsernameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [generalError, setGeneralError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  function validate(): FieldErrors {
    const errors: FieldErrors = {}
    if (!usernameOrEmail.trim()) {
      errors.usernameOrEmail = 'username or email is required'
    }
    if (!password) {
      errors.password = 'password is required'
    }
    return errors
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setGeneralError(null)

    const errors = validate()
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      return
    }

    setIsSubmitting(true)
    try {
      await login(usernameOrEmail, password)
      navigate('/')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Something went wrong.'
      setGeneralError(message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-4">
      <AuthPanel title="Log in to PLAYR">
        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
          <label className="flex flex-col gap-1 text-sm text-muted">
            Username or email
            <input
              id="usernameOrEmail"
              aria-label="username or email"
              className={inputClass}
              value={usernameOrEmail}
              onChange={(e) => setUsernameOrEmail(e.target.value)}
            />
            {fieldErrors.usernameOrEmail && (
              <span className="text-frustrated">{fieldErrors.usernameOrEmail}</span>
            )}
          </label>
          <label className="flex flex-col gap-1 text-sm text-muted">
            Password
            <input
              id="password"
              aria-label="password"
              type="password"
              className={inputClass}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            {fieldErrors.password && (
              <span className="text-frustrated">{fieldErrors.password}</span>
            )}
          </label>
          {generalError && <p className="text-frustrated">{generalError}</p>}
          <Button type="submit" disabled={isSubmitting} className="mt-2 w-full">
            Log In
          </Button>
        </form>
        <p className="mt-6 text-sm text-muted">
          <Link to="/register" className="text-primary hover:underline">
            Register instead
          </Link>
        </p>
      </AuthPanel>
    </div>
  )
}
```

- [ ] **Step 2: Read RegisterPage.tsx, then replace it applying the same visual pattern**

Read the current file first:
Run: `cat src/pages/RegisterPage.tsx` (or open in editor).

Apply the SAME transformation used for LoginPage: replace `TerminalFrame` with `AuthPanel` (title `"Create your PLAYR account"`), replace the terminal input classes with `inputClass` above, replace terminal error strings (`` `ERROR: ${...}` ``) with plain `text-frustrated` messages, replace the `[ Register ]` `<button>` with `<Button type="submit" disabled={isSubmitting} className="mt-2 w-full">Register</Button>`, and change the `> login instead` link to `text-primary hover:underline` reading `Login instead`. Keep ALL state, validation, and submit logic byte-for-byte identical.

- [ ] **Step 3: Restyle ProtectedRoute loading state**

In `src/components/ProtectedRoute.tsx`, replace the loading `<div>` className `"flex min-h-screen items-center justify-center text-[#39ff14]"` with `"flex min-h-screen items-center justify-center bg-bg text-muted"` and change the text `loading_` to `Loading…`. Leave the rest of the file unchanged.

- [ ] **Step 4: Update auth page tests for new copy/selectors**

Open `src/pages/LoginPage.test.tsx` and `src/pages/RegisterPage.test.tsx`. Update any assertions that matched terminal-specific text — e.g. button names `/log in/i` and `/register/i` still work (case-insensitive), but any assertion on literal `ERROR:` prefixes or `> register instead` link text must change to the new copy (`Register instead` / `Login instead`) and plain error messages (no `ERROR:` prefix). Do not change the behavioral flow of the tests (mocked login/register, navigation).

- [ ] **Step 5: Run the full test suite**

Run: `npm test`
Expected: ALL tests pass.

- [ ] **Step 6: Run the build**

Run: `npm run build`
Expected: build succeeds, no TS/CSS errors.

- [ ] **Step 7: Commit**

```bash
git add src/pages/LoginPage.tsx src/pages/RegisterPage.tsx src/pages/LoginPage.test.tsx src/pages/RegisterPage.test.tsx src/components/ProtectedRoute.tsx
git commit -m "feat: restyle auth pages and protected-route loading to purple theme"
```

---

## Self-Review

**Spec coverage:**
- Design tokens (colors/typography/radii via `@theme`) → Task 2. ✓
- UI primitives (Avatar, Badge, Button, Card, IconButton, SearchInput) → Tasks 3–7. ✓
- Layout (AppShell, Sidebar, TopBar, RightRail) → Tasks 8–10. ✓
- AuthPanel replaces TerminalFrame → Task 13. ✓
- Routing (standalone auth + AppShell layout route with Outlet, placeholder pages) → Tasks 11–12. ✓
- Restyled Login/Register → Task 14. ✓
- Inter font → Tasks 1–2. ✓
- lucide-react → Task 1. ✓
- Auth behavior unchanged → enforced in Task 14 steps. ✓
- Testing updates → Tasks 3–14 each include tests; auth test updates in Task 14. ✓

**Placeholder scan:** No "TBD"/"handle edge cases"/"similar to Task N" left. Task 14 Step 2 references reading RegisterPage first because its current contents aren't quoted here; the transformation is fully specified by reference to the LoginPage pattern, which is shown in full. Acceptable — the pattern is concrete.

**Type consistency:** `Avatar` uses `alt` (required) consistently in Sidebar/TopBar. `Button` variants `primary|secondary|ghost` used consistently. `CardHeader` `action` prop used in Task 6 test only; feature widgets (later) will consume it. `AppShell` is a layout route element consumed in Task 12 exactly as produced in Task 10. `useAuth` returns `user: UserResponse | null` matching the documented shape.

Note: `RightRail` is created (Task 10) but not yet mounted in any page — intentional; feature widgets mount it later. This is per the spec's "features deferred" scope.
