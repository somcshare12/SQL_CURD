# API reference

All routes are relative to the configurable prefix (default
`/api/sql-data-manager`). Responses are JSON (camelCase). A live OpenAPI
document is served at `/openapi/v1.json`.

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/health` | Database connectivity probe. |
| GET | `/info` | Module info (display name, limits, features). No credentials. |
| GET | `/objects` | List permitted tables/views with effective permissions. |
| GET | `/objects/{schema}/{object}/metadata` | Full column/key metadata. |
| POST | `/objects/{schema}/{object}/rows/query` | Paged/sorted/filtered read. |
| POST | `/objects/{schema}/{object}/rows/read` | Read one record by structured key. |
| POST | `/objects/{schema}/{object}/rows` | Create a record. |
| PUT | `/objects/{schema}/{object}/rows` | Update one record (key + changed fields). |
| DELETE | `/objects/{schema}/{object}/rows` | Delete one record (key + concurrency token). |
| GET | `/settings` | Load per-user layout settings. |
| PUT | `/settings` | Save per-user layout settings. |
| DELETE | `/settings/objects/{schema}/{object}` | Reset one object's layout. |
| DELETE | `/settings` | Reset all layout settings. |
| POST | `/metadata/refresh` | Invalidate the metadata cache (admin). |

## Query request

```json
{
  "page": 1,
  "pageSize": 100,
  "selectedColumns": ["CustomerId", "CustomerName", "Email"],
  "sort": [{ "column": "CustomerName", "direction": "asc" }],
  "filters": [{ "column": "CustomerName", "operator": "contains", "value": "Ali" }],
  "includeTotalCount": true
}
```

## Structured record key

```json
{ "keys": [{ "column": "OrderId", "value": 1803 }, { "column": "LineNumber", "value": 4 }] }
```

## Error contract

Every failure returns:

```json
{
  "code": "CONCURRENCY_CONFLICT",
  "message": "This record was changed by another user.",
  "traceId": "00-f82d…",
  "validationErrors": { "Email": ["The email value is too long."] }
}
```

Error codes include `MODULE_DISABLED`, `DATABASE_UNAVAILABLE`, `OBJECT_NOT_FOUND`,
`OBJECT_NOT_ALLOWED`, `COLUMN_NOT_FOUND`, `READ_NOT_ALLOWED`, `CREATE_NOT_ALLOWED`,
`UPDATE_NOT_ALLOWED`, `DELETE_NOT_ALLOWED`, `ROW_KEY_REQUIRED`, `ROW_NOT_FOUND`,
`CONCURRENCY_CONFLICT`, `VALIDATION_FAILED`, `FOREIGN_KEY_CONFLICT`,
`UNIQUE_CONSTRAINT_CONFLICT`, `QUERY_TIMEOUT`, `PAGE_SIZE_EXCEEDED`,
`SETTINGS_SAVE_FAILED`, `UNEXPECTED_ERROR`.

Raw SQL statements and database stack traces are never returned to the browser.
