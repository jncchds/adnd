# Release Notes

## v0.1.0 — 2026-07-25

Initial release.

- feat: multi-user TTRPG platform with cookie/JWT authentication; supports D&D 5e, Pathfinder 2e, Call of Cthulhu 7e and any custom system
- feat: real-time game chat over SignalR (`/gamehub`); players join a game group via `JoinGameGroup` after connecting
- feat: AI Game Master agent powered by WolverineFx saga orchestration (`AgentSaga` — Orchestrate → ExecuteTools → FollowUpLLM states); durable across server restarts via PostgreSQL transport
- feat: pluggable LLM strategy layer — `IAdndLlmStrategy` with four concrete strategies: OllamaStrategy (OllamaSharp, thinking tokens), OpenAIStrategy (OpenAI SDK, native tool calling, ReasoningEffort), GoogleAIStudioStrategy (OpenAI SDK → Google compat endpoint), OpenAICompatibleStrategy (raw SSE reader, captures `reasoning_content`)
- feat: `LLMPreset` model — per-user provider configuration with endpoint, model, API key, temperature, max tokens, embedding settings, `TimeoutMs`, and `ReasoningEffort`
- feat: `LLMInteractionLogger` — every LLM call (prompt, response, tokens, duration, provider, model) persisted to `LLMInteractionLogs` table; queryable from admin panel
- feat: pgvector-backed semantic search for NPC and plot-thread retrieval; embeddings generated via the preset's embedding model/endpoint
- feat: GM tool calling — `GMToolDefinition` with OpenAI-schema serialisation; OpenAI/Google strategies use native tool calls; Ollama/LMStudio fall back to text-based tool schema injection
- feat: PlotWeaver agent for proactive narrative suggestions driven by active plot threads
- feat: admin panel — Dashboard, Plot Board, NPCs, Characters, Consistency Check, LLM Logs, Agent Calls views
- feat: DnD Arcane Dark theme — `--accent: #7c3aed`, `--bg: #0c0a0e`, `--surface: #141019`; Inter font; full dark/light toggle persisted via `localStorage('adnd-theme')`
- feat: CSS sidebar (`<aside class="app-sidebar">`) with version pill (`v0.1.0`) and ALPHA badge in the toggle button; collapse state persisted via `localStorage('adnd-sidebar')`; collapsible before and after login
- feat: MUI v7 theme aligned to DnD palette for dialogs, forms, and page components
- feat: Docker production build — Node 22 builds the Vite SPA into `wwwroot/`; dotnet SDK publishes ASP.NET Core 10; runtime image serves SPA as static files (`UseDefaultFiles + UseStaticFiles + MapFallbackToFile`); no separate frontend container
- infra: WolverineFx 6.8.0 for message bus, saga persistence, and durable outbox (PostgreSQL transport); replaces earlier MassTransit setup
- infra: EF Core 10 + Npgsql + pgvector; migrations include `LLMPresetStrategyFields` (adds `TimeoutMs`, `ReasoningEffort` to `LLMPresets`)
- fix: `AgentSaga.Start()` now assigns `SagaId` so Wolverine can route `LLMResponseReceived` and `ToolCallCompleted` messages back to the correct saga instance
- fix: SignalR client connects to `/gamehub` (matches hub registration); game association handled by `JoinGameGroup` invoke, not URL
- fix: invalid C# format-string slice `$"call_{Guid.NewGuid():N[..8]}"` corrected to `$"call_{Guid.NewGuid():N}"[..8]` in two provider files
