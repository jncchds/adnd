# Release Notes

## v0.1.0 — 2026-07-26

- infra: Phase 0 scaffold — ASP.NET Core 10 server project with all NuGet package references, minimal `Program.cs` with health check at `/health`, `MapFallbackToFile("index.html")` for SPA serving
- infra: React 19 + TypeScript + Vite 6 client project — `vite.config.ts` with `build.outDir` targeting `../Adnd.Server/wwwroot`, `__APP_VERSION__` injected from `VERSION` file, dev proxy to `localhost:5010`
- infra: multi-stage `Dockerfile` — Node 22 + .NET SDK 10 build stage (client build → server publish), `aspnet:10.0` runtime stage; EF CLI installed in build stage for SDK profile
- infra: `docker-compose.yml` — `postgres` (pgvector/pgvector:pg17) + `app` services; `sdk` profile service for running `dotnet ef migrations add` commands without local tooling
- infra: `.env.example` — all required environment variables documented with generation instructions
- infra: `VERSION` file (0.1.0) read by Vite to inject `__APP_VERSION__` global
- docs: `PLAN.md` — full build plan with 9 phases, architecture decisions, incorporated improvements (S1–S14), ground rules, and additional architectural rules
