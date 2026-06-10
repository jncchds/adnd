# ---- Frontend build stage ----
FROM node:20-alpine AS frontend

WORKDIR /app

COPY package.json tsconfig.json vite.config.ts ./
COPY src/Adnd.Client/package.json ./
RUN npm install

COPY src/Adnd.Client/ ./src/Adnd.Client/
RUN npm run build

# ---- Backend build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend

WORKDIR /app

COPY src/Adnd.Server/Adnd.Server.csproj ./
RUN dotnet restore
COPY src/Adnd.Server/ ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Copy published backend
COPY --from=backend /app/publish .

# Copy built frontend
COPY --from=frontend /app/src/Adnd.Client/dist ./wwwroot

EXPOSE 5010

ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
