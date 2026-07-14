# Configuration reference

All module behaviour is driven from the `Modules:SqlDataManager` configuration
section. A full annotated sample lives at
[`appsettings.sample.json`](appsettings.sample.json).

## Top-level

| Key | Description |
|-----|-------------|
| `Enabled` | Master on/off switch for the whole module. |
| `DisplayName` | Product name (so a parent app can rebrand). |
| `ApiRoutePrefix` | Default API prefix; overridable at `MapSqlDataManagerEndpoints`. |

## `Connection`

SQL Server connection details. **Never returned to the browser.**

| Key | Description |
|-----|-------------|
| `Server` / `Database` | Server and database name. |
| `AuthenticationType` | `Windows` (integrated) or `SqlPassword`. |
| `Username` / `Password` | Used only for `SqlPassword`. Supply via secrets. |
| `Encrypt` / `TrustServerCertificate` | TLS options. |
| `ConnectionTimeoutSeconds` / `CommandTimeoutSeconds` | Timeouts. |
| `MetadataCacheSeconds` | How long discovered metadata is cached. |

## `DataDisplay`

`DefaultPageSize`, `AllowedPageSizes`, `MaximumPageSize` (enforced server-side),
`MaximumDisplayedTextLength` (long text is truncated for preview),
`ExactRowCountMode` (`Exact` / `Estimated` / `OnDemand` / `Disabled`).

## `Crud`

Enable flags and confirmation requirements for create/update/delete,
`RequireTypedDeleteConfirmation`, and bulk-delete guardrails
(`AllowBulkDelete`, `MaximumBulkDeleteRows`).

## `ObjectAccess`

Access gates applied in strict precedence (deny always wins):

1. `DeniedSchemas` → `AllowedSchemas`
2. `DeniedObjects` → `AllowedObjects`
3. `DefaultTableAccess` / `DefaultViewAccess` (`ReadOnly` or `ReadWrite`)

## `ObjectPermissions`

Per-object overrides keyed by `schema.object`, each with
`Read` / `Create` / `Update` / `Delete` booleans. These are intersected with
what the object structurally supports and the global CRUD flags.

## `ObjectSensitivity`

Per-object `SensitiveColumns` map of column → behaviour
(`Masked`, `Hidden`, `ReadOnly`). Masked values are replaced with a token;
hidden columns are never returned or searched.

## `Security`

`RequireAuthentication` plus the policy names for
Read/Create/Update/Delete/ManageSettings/RefreshMetadata. Policies are only
enforced when authentication is required (see the integration guide for wiring
a real `ICurrentUser`).

## `SettingsStorage`

`Provider` (`JsonFile`), `Directory`, `AutoSave`, `SaveDebounceMilliseconds`.

## Secret management

Sensitive values (username/password) should come from environment variables or
a secret store, **not** committed config. ASP.NET Core configuration overrides
apply automatically, for example:

```bash
export Modules__SqlDataManager__Connection__Password="…"
```

Or use user-secrets in development:

```bash
dotnet user-secrets set "Modules:SqlDataManager:Connection:Password" "…"
```
