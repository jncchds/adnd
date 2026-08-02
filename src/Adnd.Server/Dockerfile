# ── Stage 1: frontend ────────────────────────────────────────────────────────
FROM node:22-slim AS node-build
WORKDIR /repo

COPY src/Adnd.Client/package*.json src/Adnd.Client/
RUN npm ci --prefix src/Adnd.Client

# VERSION is read by vite.config.ts at build time
COPY VERSION ./
COPY src/Adnd.Client/ src/Adnd.Client/
RUN cd src/Adnd.Client && npm run build

# ── Stage 2: .NET build ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Adnd.Server/Adnd.Server.csproj src/Adnd.Server/
RUN dotnet restore src/Adnd.Server/Adnd.Server.csproj

COPY src/Adnd.Server/ src/Adnd.Server/
# Bring in the pre-built React assets (wwwroot excluded from host build context)
COPY --from=node-build /repo/src/Adnd.Server/wwwroot/ src/Adnd.Server/wwwroot/

RUN dotnet publish src/Adnd.Server/Adnd.Server.csproj \
    -c Release \
    -o /publish \
    --no-restore

# ── Stage 3: dev tooling ─────────────────────────────────────────────────────
# Only the `sdk` compose profile uses this. Keeping the EF CLI out of the shared build
# stage means production images don't pay for the tool install.
FROM build AS sdk-tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# ── Stage 4: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is needed for the container healthcheck; the aspnet image doesn't ship it.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --chown=app:app --from=build /publish ./
USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
