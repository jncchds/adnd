# Stage 1: Build frontend
FROM node:22-alpine AS frontend-build
WORKDIR /src/Adnd.Client
COPY src/Adnd.Client/package*.json ./
RUN npm ci
COPY src/Adnd.Client/ ./
# vite.config.ts reads ../../VERSION (resolves to /VERSION from WORKDIR /src/Adnd.Client)
COPY VERSION /VERSION
RUN npm run build

# Stage 2: Build and publish backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /repo
COPY src/Adnd.Server/ ./src/Adnd.Server/
COPY --from=frontend-build /src/Adnd.Server/wwwroot ./src/Adnd.Server/wwwroot
RUN dotnet publish src/Adnd.Server/Adnd.Server.csproj -c Release -o /app

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
