# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Multi-stage build for the SQL Data Manager standalone host.
#   1. Build the React SPA with Node.
#   2. Publish the ASP.NET Core API with the .NET SDK, embedding the SPA in
#      wwwroot so a single image serves both API and UI.
#   3. Run on the lean ASP.NET runtime image.
# Build from the repository's SqlDataManager/ directory:
#   docker build -f docker/Dockerfile -t sql-data-manager .
# ---------------------------------------------------------------------------

# ---- 1. Frontend build ----------------------------------------------------
FROM node:22-bookworm-slim AS client
WORKDIR /client
COPY client/sql-data-manager-client/package*.json ./
RUN npm ci
COPY client/sql-data-manager-client/ ./
# Emit the SPA into a local dist we copy into the API wwwroot next stage.
RUN npm run build -- --outDir dist --emptyOutDir

# ---- 2. Backend publish ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/SqlDataManager.Api/SqlDataManager.Api.csproj
COPY --from=client /client/dist ./src/SqlDataManager.Api/wwwroot
RUN dotnet publish src/SqlDataManager.Api/SqlDataManager.Api.csproj -c Release -o /app/publish

# ---- 3. Runtime -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
# The settings directory is a mounted volume so JSON layout files persist.
VOLUME ["/app/AppData"]
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=5 \
  CMD wget -qO- http://localhost:8080/api/sql-data-manager/health || exit 1
ENTRYPOINT ["dotnet", "SqlDataManager.Api.dll"]
