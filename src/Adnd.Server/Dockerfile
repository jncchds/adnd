# Stage 1: Build the React Client
FROM node:20-alpine AS build-client
WORKDIR /app

# Copy package files and install dependencies
COPY src/Adnd.Client/package.json src/Adnd.Client/package-lock.json* ./
RUN npm ci

# Copy source and build
COPY src/Adnd.Client/ ./
RUN npm run build

# Stage 2: Build the .NET Server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
WORKDIR /src

# Copy project files and restore
COPY src/Adnd.Server/Adnd.Server.csproj ./
RUN dotnet restore

# Copy everything and publish
COPY src/Adnd.Server/ .
RUN dotnet publish "Adnd.Server.csproj" -c Release -o /app/publish

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published server
COPY --from=build-server /app/publish .

# Copy built client into wwwroot (ASP.NET serves static files here)
COPY --from=build-client /app/dist ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
