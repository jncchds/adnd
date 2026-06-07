# Stage 1: Build the React Client (omit dev deps to keep image smaller)
FROM node:20-alpine AS build-client
WORKDIR /app

COPY src/Adnd.Client/package.json src/Adnd.Client/package-lock.json* ./
RUN npm ci

COPY src/Adnd.Client/ ./
RUN npm run build

# Stage 2: Build the .NET Server (Alpine SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-server
WORKDIR /src

COPY src/Adnd.Server/Adnd.Server.csproj ./
RUN dotnet restore

COPY src/Adnd.Server/ .
RUN dotnet publish "Adnd.Server.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:SymbolPackageFormat=none

# Stage 3: Minimal runtime image (Alpine)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Copy published server
COPY --from=build-server /app/publish .

# Copy built client into wwwroot
COPY --from=build-client /app/dist ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
