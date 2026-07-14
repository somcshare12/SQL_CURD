# CRUD safety & security model

## No raw SQL from the client

The frontend only ever sends **structured** requests (page, sort, filters as a
closed operator vocabulary, structured keys and field values). The backend
builds SQL itself:

* **Values** are always SQL parameters (`@p0`, `@k0`, …) — never concatenated.
* **Identifiers** (schema/table/column) cannot be parameters, so they are
  (1) taken from trusted metadata, (2) matched against the permitted-object
  cache, (3) validated, and (4) bracket-quoted with closing brackets doubled
  (`[dbo].[Customers]`). Client-supplied bracketed identifiers are never trusted.
* Sorting only ever emits `ASC`/`DESC` on validated metadata columns.

The module deliberately exposes **no** SQL editor, stored-procedure runner,
raw `WHERE`/`ORDER BY` input, join builder or arbitrary expression filter.

## Stable-key requirement

Update and delete require a stable unique key, chosen by priority: primary key →
non-null unique constraint → non-null unique index. Without one, update/delete
are disabled (read/create may still be allowed) and the UI explains why. The
module never builds a key from "all displayed columns", and always targets
exactly one row.

## Transactions & concurrency

Every create/update/delete runs inside a short transaction opened **after** the
user confirms. Updates/deletes require exactly one affected row. Optimistic
concurrency uses `rowversion` when present; a zero-row result with a token
returns **HTTP 409** rather than silently overwriting newer data.

## Authorization

Access is resolved in strict precedence (module enabled → schema gates → object
gates → per-object permissions → structural capability → user authorization).
Deny always beats allow. Denied objects never appear in the tree **and** cannot
be reached by calling the API route directly. UI visibility is never treated as
authorization — the backend re-checks permission on every operation.

## Sensitive data & logging

Columns can be configured `Masked` / `Hidden` / `ReadOnly`. Masked values are
replaced with a token before leaving the server; hidden columns are never
returned or searched. Structured logs record operation metadata only — never
passwords, connection strings, tokens, unmasked sensitive values or full record
payloads.
