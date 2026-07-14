# SQL Data Manager

A modular, metadata-driven **Create / Read / Update / Delete** module for
Microsoft SQL Server tables and views.

* **Backend:** ASP.NET Core (.NET 10), `Microsoft.Data.SqlClient`
* **Frontend:** React 19 + TypeScript + Vite, TanStack Query, React Router
* **Database:** Microsoft SQL Server (2019/2022)

The module discovers permitted tables/views from SQL Server metadata, renders a
searchable object tree, a server-side paged/sorted/filtered data grid, and
metadata-driven forms for creating, editing and deleting **records** (never
schema objects). It runs standalone or embeds into a larger React + ASP.NET
Core application with a single registration call on each side.

> CRUD applies to database **records only**. The module never creates, alters,
> renames, truncates or drops tables, columns, indexes, views or procedures.

---

## Solution layout

```
SqlDataManager/
├── src/
│   ├── SqlDataManager.Domain/          # Core concepts (no external deps)
│   ├── SqlDataManager.Application/     # Use cases, access rules, validation
│   ├── SqlDataManager.Infrastructure/  # SQL Server access, JSON settings, health
│   ├── SqlDataManager.Contracts/       # Wire DTOs shared by API and tests
│   └── SqlDataManager.Api/             # Endpoints + standalone host + module registration
├── client/sql-data-manager-client/     # React 19 + Vite frontend (feature module)
├── tests/                              # Unit, Architecture, Integration tests
├── docker/                            # Dockerfile, compose, demo DB seed script
└── docs/                             # Configuration, integration, API, deployment docs
```

The backend follows a clean, one-directional dependency flow
(`Api → Infrastructure → Application → Domain`), enforced by the
[architecture tests](tests/SqlDataManager.ArchitectureTests).

---

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [Node.js 20+](https://nodejs.org) (developed against Node 22)
* A reachable SQL Server instance. The quickest option is the bundled Docker
  demo database (below).

---

## Quick start

### 1. Start SQL Server + seed the demo database

Using Docker directly:

```bash
docker run -d --name sqldm-mssql \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng_Passw0rd!" \
  -e "MSSQL_PID=Developer" -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Seed the demonstration database
docker cp docker/seed/seed-database.sql sqldm-mssql:/tmp/seed.sql
docker exec sqldm-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "Str0ng_Passw0rd!" -C -I -i /tmp/seed.sql
```

The connection details for local development live in
`src/SqlDataManager.Api/appsettings.Development.json` (already pointed at the
container above). **These are dev-only credentials** — see
[Secret management](docs/configuration-reference.md#secret-management) for
production.

### 2. Run the backend (development mode)

```bash
cd src/SqlDataManager.Api
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run
```

The API is now at `http://localhost:8080/api/sql-data-manager` and the OpenAPI
document at `http://localhost:8080/openapi/v1.json`.

### 3. Run the frontend (development mode)

```bash
cd client/sql-data-manager-client
npm install
npm run dev
```

Open `http://localhost:5173/sql-data-manager`. The Vite dev server proxies
`/api/*` to the backend on port 8080 (configurable via `VITE_API_PROXY_TARGET`).

### Single-origin build

`npm run build` compiles the SPA straight into `src/SqlDataManager.Api/wwwroot`,
so running only the backend serves both the API and the UI from one origin.

---

## Commands

| Area     | Command | Purpose |
|----------|---------|---------|
| Backend  | `dotnet build` | Build the solution |
| Backend  | `dotnet run` (in `src/SqlDataManager.Api`) | Run the API + standalone host |
| Backend  | `dotnet test` | Run unit, architecture & integration tests |
| Frontend | `npm run dev` | Vite dev server |
| Frontend | `npm run build` | Type-check + production build into API wwwroot |
| Frontend | `npm run lint` | Lint with oxlint |
| Frontend | `npm test` | Component/unit tests (Vitest) |
| Frontend | `npm run test:e2e` | Playwright end-to-end tests (needs both servers) |

---

## Embedding into a parent application

**Backend** — register the module and map its endpoints under any prefix:

```csharp
builder.Services.AddSqlDataManagerModule(
    builder.Configuration.GetSection("Modules:SqlDataManager"));

app.MapSqlDataManagerEndpoints("/api/modules/sql-data-manager");
```

**Frontend** — render the page inside your own shell, or mount its routes:

```tsx
import { SqlDataManagerPage } from '@your-org/sql-data-manager';

<SqlDataManagerPage
  embedded
  showHeader={false}
  showNavigationRail={false}
  apiBaseUrl="/api/modules/sql-data-manager"
  basePath="/tools/data-manager"
/>
```

See [docs/module-integration.md](docs/module-integration.md) for the full
public interface, provider injection and theme inheritance.

---

## Documentation

* [Configuration reference & secret management](docs/configuration-reference.md)
* [Module integration guide (standalone & embedded)](docs/module-integration.md)
* [API reference](docs/api.md)
* [Deployment (Docker / IIS)](docs/deployment.md)
* [Testing guide](docs/testing.md)
* [CRUD safety & security model](docs/security.md)
* [Known limitations](docs/known-limitations.md)

---

## Safety highlights

* The frontend never sends raw SQL. All values are SQL parameters; all
  identifiers are validated against trusted metadata and bracket-quoted.
* Update/delete require a stable unique key and always target exactly one row.
* Every mutation runs in a transaction, opened only *after* client confirmation.
* Optimistic concurrency via `rowversion` returns **HTTP 409** on conflict.
* Backend authorization is always enforced; hiding UI actions is not security.
* Database credentials never leave the backend.
