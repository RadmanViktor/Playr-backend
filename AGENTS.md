# AGENTS.md — Playr (backend)

Backend API for PLAYR (social gaming platform). ASP.NET Core (.NET 10) + EF Core + PostgreSQL, Clean Architecture. This repo is `Playr` (backend only). The Angular frontend lives in the sibling repo `Playr-Frontend` (`start-dev.ps1` defaults `-FrontendPath` to `..\Playr-Frontend`).

## Projects (dependency order)
- `src/Playr.Domain` — entities, no dependencies.
- `src/Playr.Application` — use cases/services, depends on Domain only.
- `src/Playr.Infrastructure` — EF Core (`Data/`), migrations, external integrations (Steam, Rawg, Email, Storage). All DI wiring lives in `DependencyInjection.cs`.
- `src/Playr.Api` — ASP.NET Core Web API, controllers, SignalR hubs (`Hubs/`, `Chat/`, feature folders like `Friends/`, `Invitations/`, `Notifications/`, `Steam/` hold API-layer glue e.g. SignalR notifiers).
- `tests/Playr.Application.Tests`, `tests/Playr.IntegrationTests`.

Feature code is organized by folder-per-feature inside each project (e.g. `Posts`, `Friends`, `Games`, `Chat`), not by technical layer within a project.

## Dev environment
- `./start-dev.ps1` starts Postgres (docker compose), the API (`dotnet run --project src/Playr.Api`), and the frontend (`npm run dev` in `Playr-Frontend`) as background processes, logging to `api_stdout.log`/`api_stderr.log` and writing `api.pid`/`frontend.pid`.
- `./stop-dev.ps1` (`-StopPostgres` to also stop the DB container) kills processes by PID file.
- Postgres only (no PID tracking needed): `docker compose up -d` (db: `playr`, user/pass: `playr`/`playr_dev_password`, port 5432).
- API dev URL: `http://localhost:5258`. Frontend dev URL: `http://localhost:5173` (CORS default in `Program.cs` allows this if `Cors:AllowedOrigins` config is absent).
- Dev-only auth shortcut: `appsettings.Development.json` sets `Auth:AutoConfirmEmailOnRegister = true` — registered users don't need email confirmation locally.

## Build / test / migrations
- Build: `dotnet build Playr.sln`
- Test all: `dotnet test Playr.sln`
- Test one project: `dotnet test tests/Playr.Application.Tests/Playr.Application.Tests.csproj`
- Add EF Core migration (run from repo root, targeting the Infrastructure project with Api as startup):
  `dotnet ef migrations add <Name> --project src/Playr.Infrastructure --startup-project src/Playr.Api`
- CI (`.github/workflows/deploy.yml`) runs `dotnet restore/build/test` on the whole solution on every push/PR to `main`, then auto-deploys to production on push to `main` (no PR deploy). Keep the solution buildable and tests green before pushing to `main`.

## Config / secrets
- Never commit real secrets. `deploy/playr-api.env.example` documents the required env vars (`Jwt__SigningKey`, `Steam__ApiKey`, `Rawg__ApiKey`, SMTP creds, etc.) using ASP.NET Core's `Section__Key` double-underscore convention.
- Production env file on the server is regenerated from GitHub Actions secrets on every deploy (see `deploy.yml` "Build environment file" step) — changes made directly on the server are lost on next deploy. If you add a new config key, update it in **three** places: `appsettings.json`/`Development.json`, `deploy/playr-api.env.example`, and the secrets list + env-file generation in `.github/workflows/deploy.yml`.
- `FileStorage__RootPath` must point outside the `dotnet publish` output dir since publish wipes that directory.

## Gotchas
- JWT startup validation (`JwtOptionsValidator.ValidateForStartup`) throws if `Jwt` config/signing key is missing — API won't start without it even locally (dev value is in `appsettings.Development.json`).
- SignalR hubs use a custom `IUserIdProvider` (`ChatUserIdProvider`) — don't assume default `Context.User.Identity.Name` behavior for chat/notification hubs.
