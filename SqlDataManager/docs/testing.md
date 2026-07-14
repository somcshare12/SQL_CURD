# Testing guide

## Backend

```bash
dotnet test                     # everything
dotnet test tests/SqlDataManager.UnitTests
dotnet test tests/SqlDataManager.ArchitectureTests
dotnet test tests/SqlDataManager.IntegrationTests
```

* **Unit tests** cover the pure logic with no database: identifier quoting,
  access-rule precedence, value coercion, filter/sort/operator parsing and
  create/update validation.
* **Architecture tests** assert the dependency-direction rules (Domain and
  Application must not reference SQL Server or ASP.NET Core).
* **Integration tests** run the API in-memory (`WebApplicationFactory`) against
  the real demo database and exercise metadata discovery, paging, a full
  create → read → update → delete round trip and the access guardrails. They
  **skip automatically** when SQL Server is not reachable, so they are safe to
  run anywhere.

## Frontend

```bash
cd client/sql-data-manager-client
npm test            # Vitest component/unit tests (jsdom)
npm run test:e2e    # Playwright end-to-end (needs both servers running)
```

Playwright requires the browser once: `npx playwright install chromium`. Start
the backend (`dotnet run`) and the dev server (`npm run dev`) first; the e2e
suite covers tree navigation, sorting, filtering, column hiding + persistence
across reload, the full CRUD cycle with confirmations, a validation failure and
a read-only view.
