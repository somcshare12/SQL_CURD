# AGENTS.md

This repository contains **SQL Data Manager**, a metadata-driven SQL Server CRUD
module. Everything lives under `SqlDataManager/`:

- Backend: ASP.NET Core (.NET 10) solution in `SqlDataManager/src` (`SqlDataManager.slnx`).
- Frontend: React 19 + Vite + TypeScript in `SqlDataManager/client/sql-data-manager-client`.
- Tests: `SqlDataManager/tests` (unit, architecture, integration) + Playwright e2e in the client.

Standard build/run/test commands are documented in `SqlDataManager/README.md`
and `SqlDataManager/docs/testing.md`. Prefer those as the source of truth.

## Cursor Cloud specific instructions

The VM snapshot already has the toolchain installed; the startup update script
runs `dotnet restore` and `npm install`. You still need to **start services
yourself** — they do not auto-start. Key non-obvious points:

- **.NET SDK** is installed at `~/.dotnet` (not a system package) and is on
  `PATH` via `~/.bashrc` (`dotnet --version` should print `10.0.x`). If `dotnet`
  is not found, run `export PATH="$HOME/.dotnet:$PATH"`.

- **Docker has no systemd**: start the daemon manually before using containers,
  e.g. in a tmux session: `sudo dockerd`. It is configured for `fuse-overlayfs`
  with the containerd snapshotter disabled (`/etc/docker/daemon.json`) and
  `iptables-legacy`; do not change these or Docker-in-Docker breaks.

- **SQL Server** runs as the Docker container `sqldm-mssql` on `localhost:1433`
  (user `sa`, password `Str0ng_Passw0rd!`, database `SqlDataManagerDemo`). After
  starting dockerd: `sudo docker start sqldm-mssql`. If the container/image is
  missing, recreate it with the `docker run` command in `SqlDataManager/README.md`
  and reseed with `SqlDataManager/docker/seed/seed-database.sql`.
  - The seed script **must** be run with `sqlcmd -I` (QUOTED_IDENTIFIER ON) or
    the PERSISTED computed-column tables fail to create.

- **Backend (dev)**: from `SqlDataManager/src/SqlDataManager.Api` run
  `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run`.
  `appsettings.Development.json` already targets the demo container.

- **Frontend (dev)**: from `SqlDataManager/client/sql-data-manager-client` run
  `npm run dev` (port 5173). It proxies `/api/*` to the backend on 8080
  (override with `VITE_API_PROXY_TARGET`). `npm run build` outputs the SPA into
  the API's `wwwroot`, so the backend alone can serve the UI.

- **Tests**: `dotnet test` runs everything; integration tests auto-skip when
  SQL Server is unreachable, so start the container first for full coverage.
  Playwright e2e needs both dev servers running plus a one-time
  `npx playwright install chromium`.

- **Gotchas**:
  - Frontend TS config enables `erasableSyntaxOnly` — do not use TypeScript
    constructor parameter properties; declare and assign fields explicitly.
  - Array config values (e.g. `AllowedPageSizes`) must not be initialised with
    defaults in the options classes, or the config binder appends and duplicates
    them.
  - A benign `NU1903` restore warning is emitted for a transitive
    `Microsoft.OpenApi` version; it does not affect the build or runtime.
