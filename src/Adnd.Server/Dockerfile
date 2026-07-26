# Stage 1: build — .NET SDK + Node.js
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Install Node.js 22 (LTS)
RUN apt-get update && apt-get install -y curl \
  && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
  && apt-get install -y nodejs \
  && rm -rf /var/lib/apt/lists/*

# Install EF CLI tool for SDK profile (migrations)
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Restore .NET dependencies first (layer-cache friendly)
COPY src/Adnd.Server/Adnd.Server.csproj src/Adnd.Server/
RUN dotnet restore src/Adnd.Server/Adnd.Server.csproj

# Install npm dependencies (layer-cache friendly)
COPY src/Adnd.Client/package*.json src/Adnd.Client/
RUN cd src/Adnd.Client && npm ci

# Copy and build client (outputs to src/Adnd.Server/wwwroot via vite.config.ts)
COPY src/Adnd.Client/ src/Adnd.Client/
RUN cd src/Adnd.Client && npm run build

# Copy server source and publish
COPY src/Adnd.Server/ src/Adnd.Server/
RUN dotnet publish src/Adnd.Server/Adnd.Server.csproj \
    -c Release \
    -o /publish \
    --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
