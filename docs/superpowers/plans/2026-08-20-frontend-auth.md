# Playr Frontend Login/Register Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bootstrap a new Vite + React + TypeScript frontend in `../playr-frontend` with a terminal/HUD-styled Login and Register flow wired to the existing Playr backend auth API, landing on a protected placeholder dashboard.

**Architecture:** A single-page app (Vite + React 19 + TypeScript + React Router 7) with a thin `authApi` fetch layer, a React `AuthContext` holding the JWT in `localStorage`, a `ProtectedRoute` wrapper, and a reusable `TerminalFrame` shell component providing the terminal-window visual chrome for Login/Register. Backend gets one narrow addition: a CORS policy so the Vite dev server (`http://localhost:5173`) can call the API.

**Tech Stack:** Vite 8, React 19, TypeScript, Tailwind CSS 4 (`@tailwindcss/vite` plugin), React Router 7, Vitest 4 + React Testing Library, `.NET` (existing `Playr.Api`) for the CORS change.

## Global Constraints

- Frontend project root: `../playr-frontend` (sibling of this repo, path `C:\NoBackup\development\playr-frontend`).
- Backend dev API base URL: `http://localhost:5258`.
- Vite dev server must run on the default port `5173` (required for the CORS origin to match).
- No card/box-shadow container for auth forms — terminal-frame chrome only, sharp corners, no rounded borders on the form shell (per spec `docs/superpowers/specs/2026-08-20-frontend-auth-design.md`).
- Accent color: neon green `#39ff14` for focus/glow states; background near-black `#0a0e14`.
- Monospace font (`JetBrains Mono`, fallback `monospace`) for all auth UI text.
- Register has no token in its response — client must follow up with a login call using the same credentials (see spec, "Auth flow").
- Every task must leave `npm run build` (frontend) and `dotnet build` (backend, for Task 1 only) passing.

---

### Task 1: Backend CORS policy

**Files:**
- Modify: `src/Playr.Api/Program.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: CORS policy named `"FrontendDev"` allowing origin `http://localhost:5173`, any header, methods `GET, POST, PUT, OPTIONS`, and credentials — used implicitly by the browser, not referenced by any other task's code.

- [ ] **Step 1: Add the CORS service registration**

In `src/Playr.Api/Program.cs`, add this line after `builder.Services.AddInfrastructure(builder.Configuration);` (line 12):

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "OPTIONS")
            .AllowCredentials();
    });
});
```

- [ ] **Step 2: Wire the middleware into the pipeline**

In the same file, add `app.UseCors("FrontendDev");` immediately after `app.UseHttpsRedirection();` and before `app.UseAuthentication();` so the resulting order is:

```csharp
app.UseHttpsRedirection();
app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- [ ] **Step 3: Build the backend to confirm it compiles**

Run (from repo root `C:\NoBackup\development\Playr`):

```powershell
dotnet build src/Playr.Api/Playr.Api.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add src/Playr.Api/Program.cs
git commit -m "Add CORS policy for frontend dev server"
```

---

### Task 2: Scaffold the Vite + React + TypeScript project

**Files:**
- Create: entire `C:\NoBackup\development\playr-frontend` project tree (via `npm create vite@latest`), including `package.json`, `tsconfig.json`, `vite.config.ts`, `index.html`, `src/main.tsx`, `src/App.tsx`.

**Interfaces:**
- Consumes: nothing.
- Produces: a runnable Vite dev server on port 5173, and the base `src/` folder that later tasks add files into.

- [ ] **Step 1: Scaffold the project**

From `C:\NoBackup\development` (the parent of both repos), run:

```powershell
npm create vite@latest playr-frontend -- --template react-ts
```

This writes into the existing (currently near-empty, git-initialized) `playr-frontend` directory. If prompted about the directory not being empty (it contains `.git`), confirm/proceed.

- [ ] **Step 2: Install base dependencies**

```powershell
cd C:\NoBackup\development\playr-frontend
npm install
```

Expected: installs without errors, creates `node_modules` and `package-lock.json`.

- [ ] **Step 3: Add a `.gitignore` entry check**

Open `C:\NoBackup\development\playr-frontend\.gitignore` (created by the Vite scaffold) and confirm it already ignores `node_modules` and `dist`. Vite's template includes these by default — no edit needed if present.

- [ ] **Step 4: Verify the dev server starts**

```powershell
npm run dev -- --port 5173
```

Expected: console prints `Local: http://localhost:5173/`. Stop the server (Ctrl+C) once confirmed.

- [ ] **Step 5: Verify the production build works**

```powershell
npm run build
```

Expected: completes with a `dist/` output and no TypeScript errors.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Scaffold Vite + React + TypeScript project"
```

---

### Task 3: Install and configure Tailwind CSS 4 + routing dependencies

**Files:**
- Modify: `playr-frontend/vite.config.ts`
- Modify: `playr-frontend/src/index.css`
- Modify: `playr-frontend/package.json` (via npm install)

**Interfaces:**
- Consumes: the scaffolded project from Task 2.
- Produces: Tailwind utility classes available in all components; `react-router-dom` available for Task 4+ to import `BrowserRouter`, `Routes`, `Route`, `Link`, `useNavigate`.

- [ ] **Step 1: Install Tailwind, its Vite plugin, and React Router**

```powershell
cd C:\NoBackup\development\playr-frontend
npm install tailwindcss @tailwindcss/vite
npm install react-router-dom
```

- [ ] **Step 2: Register the Tailwind Vite plugin**

Replace the contents of `playr-frontend/vite.config.ts` with:

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
  },
})
```

- [ ] **Step 3: Import Tailwind in the global stylesheet**

Replace the entire contents of `playr-frontend/src/index.css` with:

```css
@import "tailwindcss";

:root {
  color-scheme: dark;
}

html, body, #root {
  height: 100%;
}

body {
  margin: 0;
  background-color: #0a0e14;
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
```

- [ ] **Step 4: Simplify `src/App.tsx` to a placeholder**

Replace the entire contents of `playr-frontend/src/App.tsx` with:

```tsx
function App() {
  return (
    <div className="flex min-h-screen items-center justify-center text-[#39ff14]">
      <p>playr_frontend booting_</p>
    </div>
  )
}

export default App
```

- [ ] **Step 5: Verify Tailwind classes are applied**

```powershell
npm run dev -- --port 5173
```

Open `http://localhost:5173` in a browser (or just confirm via curl that the page loads):

```powershell
curl.exe http://localhost:5173
```

Expected: HTML response containing `<div id="root">`. Stop the dev server after checking.

- [ ] **Step 6: Verify the build still passes**

```powershell
npm run build
```

Expected: success, no errors.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "Add Tailwind CSS 4 and React Router"
```

---

### Task 4: `authApi` fetch layer with tests

**Files:**
- Create: `playr-frontend/src/api/authApi.ts`
- Create: `playr-frontend/src/api/authApi.test.ts`
- Modify: `playr-frontend/package.json` (via npm install, adds test deps)
- Create: `playr-frontend/vitest.config.ts`

**Interfaces:**
- Consumes: nothing (talks directly to the backend via `fetch`).
- Produces (used by Task 5's `AuthContext`):
  - `const API_BASE_URL: string` (exported, value `'http://localhost:5258'`)
  - `interface UserResponse { id: string; email: string; username: string; displayName: string | null }`
  - `interface LoginResponse { accessToken: string; expiresAt: string }`
  - `class ApiError extends Error { status: number }`
  - `async function register(email: string, username: string, password: string): Promise<UserResponse>`
  - `async function login(usernameOrEmail: string, password: string): Promise<LoginResponse>`
  - `async function getMe(token: string): Promise<UserResponse>`

- [ ] **Step 1: Install test dependencies**

```powershell
cd C:\NoBackup\development\playr-frontend
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom @testing-library/user-event
```

- [ ] **Step 2: Add the Vitest config**

Create `playr-frontend/vitest.config.ts`:

```ts
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test-setup.ts',
  },
})
```

Create `playr-frontend/src/test-setup.ts`:

```ts
import '@testing-library/jest-dom'
```

- [ ] **Step 3: Add a `test` script to `package.json`**

In `playr-frontend/package.json`, add to the `"scripts"` section:

```json
"test": "vitest run"
```

- [ ] **Step 4: Write the failing test for `login`**

Create `playr-frontend/src/api/authApi.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { login, register, getMe, ApiError, API_BASE_URL } from './authApi'

describe('authApi', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('login posts credentials and returns the access token on success', async () => {
    const mockResponse = { accessToken: 'abc123', expiresAt: '2026-01-01T00:00:00Z' }
    ;(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockResponse,
    })

    const result = await login('someone', 'password123')

    expect(fetch).toHaveBeenCalledWith(
      `${API_BASE_URL}/api/auth/login`,
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
        body: JSON.stringify({ usernameOrEmail: 'someone', password: 'password123' }),
      })
    )
    expect(result).toEqual(mockResponse)
  })

  it('login throws ApiError with the server message on 401', async () => {
    ;(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 401,
      json: async () => ({ error: 'Invalid credentials.' }),
    })

    await expect(login('someone', 'wrong')).rejects.toMatchObject({
      message: 'Invalid credentials.',
      status: 401,
    })
  })

  it('register posts the new user payload and returns the created user', async () => {
    const mockUser = { id: '1', email: 'a@b.com', username: 'someone', displayName: null }
    ;(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockUser,
    })

    const result = await register('a@b.com', 'someone', 'password123')

    expect(fetch).toHaveBeenCalledWith(
      `${API_BASE_URL}/api/auth/register`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ email: 'a@b.com', username: 'someone', password: 'password123' }),
      })
    )
    expect(result).toEqual(mockUser)
  })

  it('register throws ApiError with the server message on 409', async () => {
    ;(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => ({ error: 'Username already taken.' }),
    })

    await expect(register('a@b.com', 'someone', 'password123')).rejects.toMatchObject({
      message: 'Username already taken.',
      status: 409,
    })
  })

  it('getMe sends the bearer token and returns the current user', async () => {
    const mockUser = { id: '1', email: 'a@b.com', username: 'someone', displayName: null }
    ;(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockUser,
    })

    const result = await getMe('token-value')

    expect(fetch).toHaveBeenCalledWith(
      `${API_BASE_URL}/api/auth/me`,
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer token-value' }),
      })
    )
    expect(result).toEqual(mockUser)
  })
})
```

- [ ] **Step 5: Run tests to verify they fail**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './authApi'` or similar (file doesn't exist yet).

- [ ] **Step 6: Implement `authApi.ts`**

Create `playr-frontend/src/api/authApi.ts`:

```ts
export const API_BASE_URL = 'http://localhost:5258'

export interface UserResponse {
  id: string
  email: string
  username: string
  displayName: string | null
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
}

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function parseErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body = await response.json()
    if (body && typeof body.error === 'string') {
      return body.error
    }
  } catch {
    // ignore parse failures, fall through to fallback
  }
  return fallback
}

export async function register(
  email: string,
  username: string,
  password: string
): Promise<UserResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, username, password }),
  })

  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Registration failed.')
    throw new ApiError(response.status, message)
  }

  return response.json()
}

export async function login(usernameOrEmail: string, password: string): Promise<LoginResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ usernameOrEmail, password }),
  })

  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Invalid credentials.')
    throw new ApiError(response.status, message)
  }

  return response.json()
}

export async function getMe(token: string): Promise<UserResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  })

  if (!response.ok) {
    const message = await parseErrorMessage(response, 'Not authenticated.')
    throw new ApiError(response.status, message)
  }

  return response.json()
}
```

- [ ] **Step 7: Run tests to verify they pass**

```powershell
npm run test
```

Expected: PASS — all 5 tests green.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "Add authApi fetch layer with tests"
```

---

### Task 5: `AuthContext` with tests

**Files:**
- Create: `playr-frontend/src/context/AuthContext.tsx`
- Create: `playr-frontend/src/context/AuthContext.test.tsx`

**Interfaces:**
- Consumes: `register`, `login`, `getMe`, `ApiError`, `UserResponse` from `../api/authApi` (Task 4).
- Produces (used by Task 6/7/8):
  - `interface AuthContextValue { user: UserResponse | null; token: string | null; isLoading: boolean; login: (usernameOrEmail: string, password: string) => Promise<void>; register: (email: string, username: string, password: string) => Promise<void>; logout: () => void }`
  - `function AuthProvider({ children }: { children: React.ReactNode }): JSX.Element`
  - `function useAuth(): AuthContextValue` (throws if used outside `AuthProvider`)
  - `localStorage` key used: `'playr_token'`

- [ ] **Step 1: Write the failing tests**

Create `playr-frontend/src/context/AuthContext.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from './AuthContext'
import * as authApi from '../api/authApi'

function TestConsumer() {
  const auth = useAuth()
  return (
    <div>
      <span data-testid="user">{auth.user ? auth.user.username : 'none'}</span>
      <span data-testid="loading">{auth.isLoading ? 'loading' : 'idle'}</span>
      <button onClick={() => auth.login('someone', 'password123')}>login</button>
      <button onClick={() => auth.register('a@b.com', 'someone', 'password123')}>register</button>
      <button onClick={() => auth.logout()}>logout</button>
    </div>
  )
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('starts with no user and not loading when there is no stored token', async () => {
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('idle'))
    expect(screen.getByTestId('user')).toHaveTextContent('none')
  })

  it('login stores the token and loads the user', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'abc123',
      expiresAt: '2026-01-01T00:00:00Z',
    })
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('idle'))
    await user.click(screen.getByText('login'))

    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('someone'))
    expect(localStorage.getItem('playr_token')).toBe('abc123')
  })

  it('register calls register then login and loads the user', async () => {
    vi.spyOn(authApi, 'register').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'abc123',
      expiresAt: '2026-01-01T00:00:00Z',
    })
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('idle'))
    await user.click(screen.getByText('register'))

    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('someone'))
    expect(authApi.register).toHaveBeenCalledWith('a@b.com', 'someone', 'password123')
    expect(authApi.login).toHaveBeenCalledWith('someone', 'password123')
  })

  it('logout clears the token and user', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'abc123',
      expiresAt: '2026-01-01T00:00:00Z',
    })
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('idle'))
    await user.click(screen.getByText('login'))
    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('someone'))

    await user.click(screen.getByText('logout'))

    expect(screen.getByTestId('user')).toHaveTextContent('none')
    expect(localStorage.getItem('playr_token')).toBeNull()
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './AuthContext'`.

- [ ] **Step 3: Implement `AuthContext.tsx`**

Create `playr-frontend/src/context/AuthContext.tsx`:

```tsx
import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { getMe, login as apiLogin, register as apiRegister, type UserResponse } from '../api/authApi'

const TOKEN_STORAGE_KEY = 'playr_token'

export interface AuthContextValue {
  user: UserResponse | null
  token: string | null
  isLoading: boolean
  login: (usernameOrEmail: string, password: string) => Promise<void>
  register: (email: string, username: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(null)
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_STORAGE_KEY))
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let cancelled = false

    async function loadUser() {
      if (!token) {
        setUser(null)
        setIsLoading(false)
        return
      }

      setIsLoading(true)
      try {
        const currentUser = await getMe(token)
        if (!cancelled) {
          setUser(currentUser)
        }
      } catch {
        if (!cancelled) {
          localStorage.removeItem(TOKEN_STORAGE_KEY)
          setToken(null)
          setUser(null)
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false)
        }
      }
    }

    loadUser()

    return () => {
      cancelled = true
    }
  }, [token])

  const login = useCallback(async (usernameOrEmail: string, password: string) => {
    const result = await apiLogin(usernameOrEmail, password)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.accessToken)
    setToken(result.accessToken)
  }, [])

  const register = useCallback(async (email: string, username: string, password: string) => {
    await apiRegister(email, username, password)
    const result = await apiLogin(username, password)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.accessToken)
    setToken(result.accessToken)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    setToken(null)
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
npm run test
```

Expected: PASS — all `AuthContext` tests green, plus the Task 4 tests still passing.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add AuthContext with login/register/logout and tests"
```

---

### Task 6: `TerminalFrame` shell component with tests

**Files:**
- Create: `playr-frontend/src/components/TerminalFrame.tsx`
- Create: `playr-frontend/src/components/TerminalFrame.test.tsx`

**Interfaces:**
- Consumes: nothing.
- Produces (used by Task 7/8): `function TerminalFrame({ title, children }: { title: string; children: React.ReactNode }): JSX.Element` — renders a title bar line and a thin bordered container around `children`.

- [ ] **Step 1: Write the failing test**

Create `playr-frontend/src/components/TerminalFrame.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TerminalFrame } from './TerminalFrame'

describe('TerminalFrame', () => {
  it('renders the title bar and children content', () => {
    render(
      <TerminalFrame title="playr_auth --login">
        <p>form goes here</p>
      </TerminalFrame>
    )

    expect(screen.getByText('> playr_auth --login')).toBeInTheDocument()
    expect(screen.getByText('form goes here')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './TerminalFrame'`.

- [ ] **Step 3: Implement `TerminalFrame.tsx`**

Create `playr-frontend/src/components/TerminalFrame.tsx`:

```tsx
import type { ReactNode } from 'react'

export function TerminalFrame({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="w-full max-w-md border border-[#39ff14] text-[#39ff14]">
      <div className="border-b border-[#39ff14] px-4 py-2 text-sm uppercase tracking-wide">
        {`> ${title}`}
      </div>
      <div className="px-6 py-8">{children}</div>
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

```powershell
npm run test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add TerminalFrame shell component with test"
```

---

### Task 7: `LoginPage` with tests

**Files:**
- Create: `playr-frontend/src/pages/LoginPage.tsx`
- Create: `playr-frontend/src/pages/LoginPage.test.tsx`

**Interfaces:**
- Consumes: `useAuth` from `../context/AuthContext` (Task 5), `TerminalFrame` from `../components/TerminalFrame` (Task 6), `Link`/`useNavigate` from `react-router-dom` (Task 3), `ApiError` from `../api/authApi` (Task 4).
- Produces (used by Task 9's router): `function LoginPage(): JSX.Element`, default export.

- [ ] **Step 1: Write the failing tests**

Create `playr-frontend/src/pages/LoginPage.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import LoginPage from './LoginPage'
import { AuthProvider } from '../context/AuthContext'
import * as authApi from '../api/authApi'

function renderLoginPage() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </MemoryRouter>
  )
}

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('shows a terminal-style error message on invalid credentials', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue(new authApi.ApiError(401, 'Invalid credentials.'))

    const user = userEvent.setup()
    renderLoginPage()

    await user.type(screen.getByLabelText(/username or email/i), 'someone')
    await user.type(screen.getByLabelText(/password/i), 'wrongpass')
    await user.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() =>
      expect(screen.getByText(/ERROR: Invalid credentials\./i)).toBeInTheDocument()
    )
  })

  it('calls login with the entered credentials on submit', async () => {
    const loginSpy = vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'abc123',
      expiresAt: '2026-01-01T00:00:00Z',
    })
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    renderLoginPage()

    await user.type(screen.getByLabelText(/username or email/i), 'someone')
    await user.type(screen.getByLabelText(/password/i), 'password123')
    await user.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(loginSpy).toHaveBeenCalledWith('someone', 'password123'))
  })

  it('has a link to the register page', () => {
    renderLoginPage()
    expect(screen.getByRole('link', { name: /register instead/i })).toHaveAttribute(
      'href',
      '/register'
    )
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './LoginPage'`.

- [ ] **Step 3: Implement `LoginPage.tsx`**

Create `playr-frontend/src/pages/LoginPage.tsx`:

```tsx
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { TerminalFrame } from '../components/TerminalFrame'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../api/authApi'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [usernameOrEmail, setUsernameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(usernameOrEmail, password)
      navigate('/')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Something went wrong.'
      setError(message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#0a0e14] px-4">
      <TerminalFrame title="playr_auth --login">
        <h1 className="mb-6 text-lg">Welcome to Playr_</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <label className="flex flex-col gap-1 text-sm">
            username or email
            <input
              id="usernameOrEmail"
              aria-label="username or email"
              className="border-b border-[#39ff14] bg-transparent px-1 py-1 text-[#39ff14] outline-none focus:shadow-[0_0_8px_#39ff14]"
              value={usernameOrEmail}
              onChange={(e) => setUsernameOrEmail(e.target.value)}
              required
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            password
            <input
              id="password"
              aria-label="password"
              type="password"
              className="border-b border-[#39ff14] bg-transparent px-1 py-1 text-[#39ff14] outline-none focus:shadow-[0_0_8px_#39ff14]"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          {error && <p className="text-orange-400">{`ERROR: ${error}`}</p>}
          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-2 border border-[#39ff14] px-4 py-2 uppercase tracking-wide hover:shadow-[0_0_8px_#39ff14] disabled:opacity-50"
          >
            [ Log In ]
          </button>
        </form>
        <p className="mt-6 text-sm">
          <Link to="/register" className="underline">
            {'> register instead'}
          </Link>
        </p>
      </TerminalFrame>
    </div>
  )
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
npm run test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add LoginPage with tests"
```

---

### Task 8: `RegisterPage` with tests

**Files:**
- Create: `playr-frontend/src/pages/RegisterPage.tsx`
- Create: `playr-frontend/src/pages/RegisterPage.test.tsx`

**Interfaces:**
- Consumes: `useAuth` from `../context/AuthContext` (Task 5), `TerminalFrame` from `../components/TerminalFrame` (Task 6), `Link`/`useNavigate` from `react-router-dom`, `ApiError` from `../api/authApi` (Task 4).
- Produces (used by Task 9's router): `function RegisterPage(): JSX.Element`, default export.

- [ ] **Step 1: Write the failing tests**

Create `playr-frontend/src/pages/RegisterPage.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import RegisterPage from './RegisterPage'
import { AuthProvider } from '../context/AuthContext'
import * as authApi from '../api/authApi'

function renderRegisterPage() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <RegisterPage />
      </AuthProvider>
    </MemoryRouter>
  )
}

describe('RegisterPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('shows a terminal-style error message when the username is taken', async () => {
    vi.spyOn(authApi, 'register').mockRejectedValue(
      new authApi.ApiError(409, 'Username already taken.')
    )

    const user = userEvent.setup()
    renderRegisterPage()

    await user.type(screen.getByLabelText(/email/i), 'a@b.com')
    await user.type(screen.getByLabelText(/^username$/i), 'someone')
    await user.type(screen.getByLabelText(/password/i), 'password123')
    await user.click(screen.getByRole('button', { name: /register/i }))

    await waitFor(() =>
      expect(screen.getByText(/ERROR: Username already taken\./i)).toBeInTheDocument()
    )
  })

  it('calls register with the entered fields on submit', async () => {
    const registerSpy = vi.spyOn(authApi, 'register').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'abc123',
      expiresAt: '2026-01-01T00:00:00Z',
    })
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    renderRegisterPage()

    await user.type(screen.getByLabelText(/email/i), 'a@b.com')
    await user.type(screen.getByLabelText(/^username$/i), 'someone')
    await user.type(screen.getByLabelText(/password/i), 'password123')
    await user.click(screen.getByRole('button', { name: /register/i }))

    await waitFor(() =>
      expect(registerSpy).toHaveBeenCalledWith('a@b.com', 'someone', 'password123')
    )
  })

  it('has a link to the login page', () => {
    renderRegisterPage()
    expect(screen.getByRole('link', { name: /login instead/i })).toHaveAttribute('href', '/login')
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './RegisterPage'`.

- [ ] **Step 3: Implement `RegisterPage.tsx`**

Create `playr-frontend/src/pages/RegisterPage.tsx`:

```tsx
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { TerminalFrame } from '../components/TerminalFrame'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../api/authApi'

export default function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await register(email, username, password)
      navigate('/')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Something went wrong.'
      setError(message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#0a0e14] px-4">
      <TerminalFrame title="playr_auth --register">
        <h1 className="mb-6 text-lg">Create your account_</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <label className="flex flex-col gap-1 text-sm">
            email
            <input
              id="email"
              aria-label="email"
              type="email"
              className="border-b border-[#39ff14] bg-transparent px-1 py-1 text-[#39ff14] outline-none focus:shadow-[0_0_8px_#39ff14]"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            username
            <input
              id="username"
              aria-label="username"
              className="border-b border-[#39ff14] bg-transparent px-1 py-1 text-[#39ff14] outline-none focus:shadow-[0_0_8px_#39ff14]"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            password
            <input
              id="password"
              aria-label="password"
              type="password"
              className="border-b border-[#39ff14] bg-transparent px-1 py-1 text-[#39ff14] outline-none focus:shadow-[0_0_8px_#39ff14]"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          {error && <p className="text-orange-400">{`ERROR: ${error}`}</p>}
          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-2 border border-[#39ff14] px-4 py-2 uppercase tracking-wide hover:shadow-[0_0_8px_#39ff14] disabled:opacity-50"
          >
            [ Register ]
          </button>
        </form>
        <p className="mt-6 text-sm">
          <Link to="/login" className="underline">
            {'> login instead'}
          </Link>
        </p>
      </TerminalFrame>
    </div>
  )
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
npm run test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add RegisterPage with tests"
```

---

### Task 9: `ProtectedRoute`, `DashboardPage`, and app routing wire-up

**Files:**
- Create: `playr-frontend/src/components/ProtectedRoute.tsx`
- Create: `playr-frontend/src/components/ProtectedRoute.test.tsx`
- Create: `playr-frontend/src/pages/DashboardPage.tsx`
- Create: `playr-frontend/src/pages/DashboardPage.test.tsx`
- Modify: `playr-frontend/src/App.tsx`
- Modify: `playr-frontend/src/main.tsx`

**Interfaces:**
- Consumes: `useAuth` from `../context/AuthContext` (Task 5), `AuthProvider` (Task 5), `LoginPage`/`RegisterPage` default exports (Tasks 7/8), `BrowserRouter`/`Routes`/`Route`/`Navigate` from `react-router-dom`.
- Produces: final `App` component wiring `/login`, `/register`, and `/` (protected, rendering `DashboardPage`); no further tasks depend on this one.

- [ ] **Step 1: Write the failing test for `ProtectedRoute`**

Create `playr-frontend/src/components/ProtectedRoute.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './ProtectedRoute'
import { AuthProvider } from '../context/AuthContext'
import * as authApi from '../api/authApi'

function renderWithRoute(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<div>login page</div>} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <div>secret dashboard</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>
  )
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('redirects to /login when there is no stored token', async () => {
    renderWithRoute('/')

    await waitFor(() => expect(screen.getByText('login page')).toBeInTheDocument())
  })

  it('renders children when a valid token/user is present', async () => {
    localStorage.setItem('playr_token', 'abc123')
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    renderWithRoute('/')

    await waitFor(() => expect(screen.getByText('secret dashboard')).toBeInTheDocument())
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './ProtectedRoute'`.

- [ ] **Step 3: Implement `ProtectedRoute.tsx`**

Create `playr-frontend/src/components/ProtectedRoute.tsx`:

```tsx
import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center text-[#39ff14]">
        loading_
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
```

- [ ] **Step 4: Run test to verify it passes**

```powershell
npm run test
```

Expected: PASS for `ProtectedRoute.test.tsx`.

- [ ] **Step 5: Write the failing test for `DashboardPage`**

Create `playr-frontend/src/pages/DashboardPage.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import DashboardPage from './DashboardPage'
import { AuthProvider } from '../context/AuthContext'
import * as authApi from '../api/authApi'

describe('DashboardPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('shows a welcome message with the current username', async () => {
    localStorage.setItem('playr_token', 'abc123')
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    render(
      <MemoryRouter>
        <AuthProvider>
          <DashboardPage />
        </AuthProvider>
      </MemoryRouter>
    )

    await waitFor(() => expect(screen.getByText(/welcome, someone_/i)).toBeInTheDocument())
  })

  it('clears the stored token when logout is clicked', async () => {
    localStorage.setItem('playr_token', 'abc123')
    vi.spyOn(authApi, 'getMe').mockResolvedValue({
      id: '1',
      email: 'a@b.com',
      username: 'someone',
      displayName: null,
    })

    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <AuthProvider>
          <DashboardPage />
        </AuthProvider>
      </MemoryRouter>
    )

    await waitFor(() => expect(screen.getByText(/welcome, someone_/i)).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /logout/i }))

    expect(localStorage.getItem('playr_token')).toBeNull()
  })
})
```

- [ ] **Step 6: Run test to verify it fails**

```powershell
npm run test
```

Expected: FAIL — `Cannot find module './DashboardPage'`.

- [ ] **Step 7: Implement `DashboardPage.tsx`**

Create `playr-frontend/src/pages/DashboardPage.tsx`:

```tsx
import { useAuth } from '../context/AuthContext'

export default function DashboardPage() {
  const { user, logout } = useAuth()

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-6 bg-[#0a0e14] text-[#39ff14]">
      <h1 className="text-xl">{`Welcome, ${user?.username ?? ''}_`}</h1>
      <button
        onClick={logout}
        className="border border-[#39ff14] px-4 py-2 uppercase tracking-wide hover:shadow-[0_0_8px_#39ff14]"
      >
        [ Logout ]
      </button>
    </div>
  )
}
```

- [ ] **Step 8: Run test to verify it passes**

```powershell
npm run test
```

Expected: PASS for `DashboardPage.test.tsx`.

- [ ] **Step 9: Wire up routing in `App.tsx`**

Replace the entire contents of `playr-frontend/src/App.tsx` with:

```tsx
import { Routes, Route } from 'react-router-dom'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import { ProtectedRoute } from './components/ProtectedRoute'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  )
}

export default App
```

- [ ] **Step 10: Wire up `AuthProvider` and `BrowserRouter` in `main.tsx`**

Replace the entire contents of `playr-frontend/src/main.tsx` with:

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './context/AuthContext.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>
)
```

- [ ] **Step 11: Run the full test suite**

```powershell
npm run test
```

Expected: PASS — every test file green.

- [ ] **Step 12: Run the production build**

```powershell
npm run build
```

Expected: success, no TypeScript errors.

- [ ] **Step 13: Manual smoke test against the real backend**

With the backend running (see Task 1; start via `dotnet run --project src/Playr.Api/Playr.Api.csproj` from the `Playr` repo) and the docker container for the database already up:

```powershell
npm run dev -- --port 5173
```

Open `http://localhost:5173/register`, register a new user, confirm redirect to `/` showing `Welcome, <username>_`, click `[ Logout ]`, confirm redirect to `/login`, then log back in with the same credentials and confirm the dashboard loads again.

- [ ] **Step 14: Commit**

```powershell
git add -A
git commit -m "Wire up ProtectedRoute, DashboardPage, and app routing"
```
