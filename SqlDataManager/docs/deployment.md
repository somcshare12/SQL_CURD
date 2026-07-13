# Deployment

## Single-origin (recommended)

1. Build the SPA into the API's wwwroot:
   ```bash
   cd client/sql-data-manager-client && npm ci && npm run build
   ```
2. Publish the API:
   ```bash
   dotnet publish src/SqlDataManager.Api -c Release -o out
   ```
3. Run `out/SqlDataManager.Api` behind your reverse proxy. The backend serves
   both the API and the SPA (with SPA fallback for deep links).

Provide the connection password via environment variable or secret store, e.g.
`Modules__SqlDataManager__Connection__Password`.

## Docker

The [`docker/`](../docker) folder contains a backend `Dockerfile` and a
`docker-compose.yml` that brings up SQL Server plus the API:

```bash
cd docker
docker compose up --build
```

The compose file mounts a volume for the JSON settings directory and includes a
health check. Do **not** bake production passwords into images — pass them as
environment variables at runtime.

## IIS (Windows)

1. Install the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0).
2. `dotnet publish -c Release` and copy the output to the site folder.
3. Create an IIS site/app pool (No Managed Code). The `web.config` produced by
   publish wires the ASP.NET Core Module.
4. Use Windows Authentication for `AuthenticationType: "Windows"`, or supply SQL
   credentials via environment variables / a secret store.
