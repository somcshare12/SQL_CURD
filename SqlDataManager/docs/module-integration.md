# Module integration guide

The module supports two modes: **standalone** (it provides the whole shell) and
**embedded** (the parent app provides header, navigation, auth, theme, etc.).

## Backend

Register services and map endpoints. Both the config section and the route
prefix are configurable:

```csharp
builder.Services.AddSqlDataManagerModule(
    builder.Configuration.GetSection("Modules:SqlDataManager"));

app.UseSqlDataManagerExceptionHandling();       // optional if parent has its own
app.MapSqlDataManagerEndpoints("/api/modules/sql-data-manager");
```

All default implementations are registered with `TryAdd`, so a parent can
override them by registering its own **before** calling the module:

| Abstraction | Default | Override to… |
|-------------|---------|--------------|
| `ICurrentUser` | anonymous (all policies pass) | plug in real identity + policy checks |
| `ISqlDataManagerSettingsStore` | JSON file | store layout in SQL Server / profile service |
| `ISqlDataManagerAuditWriter` | structured log | persist audit events |

Example of parent-provided identity:

```csharp
builder.Services.AddScoped<ICurrentUser, MyClaimsPrincipalCurrentUser>();
```

## Frontend

Import only from the feature's public entry point:

```ts
import {
  SqlDataManagerPage,
  SqlDataManagerProvider,
  createSqlDataManagerRoutes,
  createSqlDataManagerNavigationNodes,
  HttpSqlDataManagerApiClient,
} from '@your-org/sql-data-manager';
```

### Standalone

```tsx
<SqlDataManagerPage showHeader showNavigationRail showObjectTree />
```

### Embedded

Render inside your own shell and turn off the chrome the parent owns:

```tsx
<SqlDataManagerProvider
  apiBaseUrl="/api/modules/sql-data-manager"
  basePath="/tools/data-manager"
  embedded
  apiClient={myAuthenticatedApiClient}   // optional injected client
  onError={handleError}
  onMutationCompleted={notifySuccess}
>
  <SqlDataManagerPage
    embedded
    showHeader={false}
    showNavigationRail={false}
  />
</SqlDataManagerProvider>
```

* The embedded page does **not** render a second application header/rail, so it
  slots cleanly into a parent frame.
* Theme is inherited: in standalone mode a `data-sdm-theme` attribute is managed
  by the module; in embedded mode set that attribute (or override the CSS custom
  properties) from your parent theme.
* `createSqlDataManagerRoutes(basePath, pageProps)` returns `RouteObject[]` for
  mounting under a parent React Router.
* `createSqlDataManagerNavigationNodes(objects, basePath)` returns nodes the
  parent can merge into its own navigation tree.
