# ADnD — Ideas & Roadmap

> **Advanced Dungeon Network** — A multi-system TTRPG web framework with LLM-powered Game Master assistance, real-time chat, and custom system support.

This document captures improvement ideas, feature requests, and architectural enhancements for the project. Items are categorized by priority and area.

---

## 🔴 Critical / High Priority

### 1. Distributed Connection State (Production Readiness)

**Problem:** `_playerConnections` in `GameHub` is an in-memory `ConcurrentDictionary`. In a multi-instance deployment, disconnected players won't be detected correctly.

**Ideas:**
- Replace with Redis-backed connection tracking
- Use `IDistributedCache` for player-to-connection mapping
- Add SignalR scaleout with Redis backplane
- Consider Azure SignalR Service for managed scaling

### 2. Security Hardening

**Ideas:**
- Encrypt `LLMPreset.ApiKey` at rest (AES-256 with per-tenant key)
- Add rate limiting to API endpoints (e.g., 100 req/min per user)
- Add CSRF protection for state-changing endpoints
- Implement API key rotation for LLM providers
- Add input sanitization for narrative content (XSS prevention)
- Audit JWT token claims for privilege escalation vectors
- Add `X-Content-Type-Options: nosniff` and other security headers
- Add audit logging for admin actions (create/delete LLM presets, etc.)

### 3. Error Handling & Resilience

**Ideas:**
- Add retry policies for LLM calls (Polly)
- Add circuit breaker for LLM provider failures
- Graceful degradation when LLM is unavailable (fallback to narrative templates)
- Add health check endpoints (`/health`, `/health/ready`)
- Add graceful shutdown for GameAgent recovery
- Add database connection retry with exponential backoff

### 4. Database Performance

**Ideas:**
- Add indexes on frequently queried columns (`GameSessions.GameId`, `Messages.SessionId`, `CombatParticipants.CombatId`)
- Add full-text search index on `PlotThreads.Title` and `PlotThreads.Description`
- Partition `Messages` and `LLMInteractionLogs` by date for large games
- Add connection pooling configuration for PostgreSQL
- Add database migration versioning with rollback support

---

## ✅ Recently Completed

### 🛡️ Security Hardening (Batch 1 — 2026-06-09)
- [x] API key encryption (AES-256-GCM via `IApiKeyEncryptionService`)
- [x] Rate limiting middleware (IP-based: 10/min auth, 30/min LLM presets, 100/min default)
- [x] Security headers (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy, Cache-Control)

### 🏥 Error Handling & Resilience (Batch 2 — 2026-06-09)
- [x] Polly retry policy (3 attempts, exponential backoff for HTTP 5xx and timeouts)
- [x] Polly circuit breaker (opens after 5 consecutive failures, half-opens after 30s)
- [x] `IResiliencePolicies` interface + `ResiliencePolicies` implementation
- [x] `ResilienceExtensions.ExecuteWithResilienceAsync` extension for easy policy application
- [x] Health check endpoints: `/health` (liveness) and `/health/ready` (readiness)
- [x] `DatabaseHealthCheck` — verifies PostgreSQL connectivity
- [x] `LlmProvidersHealthCheck` — checks all registered LLM providers
- [x] Configurable via `Resilience` section in appsettings.json
- [x] Graceful degradation: circuit breaker prevents cascading failures

### 🗄️ Database Performance (Batch 3 — 2026-06-09)
- [x] New indexes: `PlotThreads(GameId, Status)`, `Combats(GameId, Status)`, `LLMInteractionLogs(StartedAt)`, `Messages(SessionId, CreatedAt)`
- [x] `IX_PlotThreads_GameId_Title` composite index for fast title lookups
- [x] `AuditLogs` table with `(UserId, CreatedAt)` index — tracks admin actions
- [x] `IAuditLogService` + `AuditLog` model for audit logging
- [x] Connection pooling: `MaxPoolSize=100;MinPoolSize=10;Connection Idle Lifetime=300`
- [x] EF query logging enabled in dev via `ConfigureWarnings`

### 🏗️ Architectural Improvements

### 21. Code Quality & Refactoring

**Ideas:**
- **Map/Grid Rendering**: Visual combat grid with token placement (Canvas/SVG)
- **Token Management**: Drag-and-drop tokens, health bars, status icons
- **Dynamic Lighting**: Fog of war, line-of-sight calculations
- **Map Integration**: Import images from Roll20, Foundry VTT, or custom maps
- **Token Library**: Pre-built monster/player token collection
- **API Integration**: Connect to Roll20 API, Foundry VTT, or Fantasy Grounds
- **Character Sheet Overlay**: Click a token to view character sheet
- **Zone Triggers**: Define areas that trigger effects (traps, hazards)

### 6. Multi-Game & Lobby System

**Ideas:**
- Add a "Lobby" concept — pre-game waiting area with character creation
- Game browser with filters (system, player count, status, language)
- Game templates — save game configs for quick recreation
- Watch mode — spectators can watch a game without participating
- Game archiving with replay capability
- Game history timeline with key events

### 7. Advanced Combat System

**Ideas:**
- **Group Initiative**: Teams roll initiative together (alliance-based)
- **Gridded Movement**: Calculate move distance on grid with obstacles
- **Cover System**: Half cover, three-quarter cover, total cover modifiers
- **Environmental Effects**: Terrain types, weather, lighting conditions
- **Status Effects**: More granular conditions with stacking rules
- **Multi-Target Attacks**: Spell attack resolution for AoE effects
- **Lair Actions**: Environment-based actions for specific locations
- **Boss Mechanics**: Phase-based boss encounters with special abilities
- **Tactical Minimap**: Real-time token movement with pathfinding
- **Combat Templates**: Pre-built encounter templates (difficulty, npcs, rewards)

### 8. Player Character Management

**Ideas:**
- **Full Character Sheets**: Complete D&D 5e/PF2e/CoC7e character sheet rendering
- **Character Import**: Import from D&D Beyond, Foundry VTT, or JSON
- **Character Templates**: Pre-built archetypes (level 1 fighter, etc.)
- **Equipment Management**: Inventory with weight, encumbrance, magic items
- **Spell Manager**: Full spell list with casting, upcasting, and ritual casting
- **Proficiency Tracking**: Skill proficiency, saving throw proficiency
- **Background Features**: Track background features and traits
- **Multiclass Support**: Track XP and class levels for multiclass characters
- **Character Archetypes**: Save/load character builds
- **Character History**: Track character progression across sessions

### 9. Advanced AI / LLM Features

**Ideas:**
- **Streaming Responses**: Real-time token streaming for GM narrative (SignalR SSE)
- **Context Window Management**: Smart truncation of conversation history
- **Memory System**: Long-term memory for NPCs and plot threads
- **NPC Voice Profiles**: Each NPC has a unique speaking style/personality
- **Dynamic Difficulty**: AI adjusts encounter difficulty based on party strength
- **Player Behavior Analysis**: Track player patterns and adapt narrative
- **Multi-Agent Architecture**: Separate agents for combat, narrative, world-building
- **Tool Calling Enhancements**: More granular tools (spell effects, NPC reactions)
- **LLM Caching**: Cache LLM responses for repeated queries
- **Prompt Templates**: Configurable system prompts per game/session
- **Fine-Tuning Support**: Support for fine-tuned models per campaign
- **RAG Enhancements**: Cross-game RAG for shared universes
- **Embedding Caching**: Cache embeddings to reduce API calls
- **Fallback Narratives**: Template-based fallback when LLM is down
- **Narrative Style Options**: Choose between dramatic, humorous, dark, etc.

### 10. Plot & Narrative Tools

**Ideas:**
- **Plot Board Visualization**: Interactive plot thread graph (force-directed)
- **Plot Thread Relationships**: Link threads together (prerequisite, parallel, conflicting)
- **Story Arc Tracking**: Track hero's journey, three-act structure
- **NPC Relationship Map**: Visualize NPC connections and alliances
- **World State Tracking**: Track factions, territories, political situations
- **Random Encounter Generator**: Context-aware random encounters
- **Plot Seed Generator**: AI-generated plot seeds based on themes
- **Milestone Tracking**: Track story milestones with progress indicators
- **Narrative Beat System**: Track story beats (inciting incident, climax, etc.)
- **Player Choice Tracking**: Track key decisions and their consequences
- **Reputation System**: Track faction reputation with players

---

## 🟡 Feature Enhancements

### 11. Chat & Communication

**Ideas:**
- **Message Threading**: Reply to specific messages
- **Message Reactions**: Emoji reactions on messages
- **Message Pinning**: Pin important messages
- **Message Search**: Search across all game messages
- **Markdown Support**: Rich text formatting in messages
- **Message Formatting**: Bold, italic, code blocks, etc.
- **Custom Channels**: GM-defined chat channels (strategy, lore, etc.)
- **Scheduled Messages**: Delayed messages for dramatic effect
- **Message Export**: Export game chat to PDF/Markdown
- **Voice Chat Integration**: WebRTC voice chat for players
- **Text-to-Speech**: TTS for GM narration
- **Chat Themes**: Customizable chat appearance

### 12. Session & Game Management

**Ideas:**
- **Session Notes**: GM notes per session (not visible to players)
- **Session Summaries**: Auto-generated session summaries
- **Recap Generation**: AI-generated session recaps for players
- **Campaign Mode**: Multiple sessions grouped into a campaign
- **Session Templates**: Pre-built session templates (heist, investigation, etc.)
- **Auto-Save**: Periodic game state snapshots
- **Game Restoration**: Restore from saved state
- **Session Calendar**: Schedule sessions with reminders
- **Attendance Tracking**: Track player attendance per session
- **XP/Level Tracking**: Per-session XP awards and level progression

### 13. Dice & Mechanics

**Ideas:**
- **Dice Pool System**: Support for dice pool mechanics (WoD, Shadowrun)
- **Advantage/Disadvantage**: Visual dice with advantage/disadvantage
- **Custom Dice Faces**: Custom die types (d4, d6, d8, d10, d12, d20, d100, d1000)
- **Dice History Chart**: Visual chart of dice roll distribution
- **Dice Statistics**: Track roll statistics (avg, min, max, hot/cold numbers)
- **Fudge/Fate Dice**: Support for Fate/Fudge system dice (+, -, blank)
- **Roll Modification**: Post-roll modifications (reroll, modify, etc.)
- **Dice Tray Animation**: Animated dice rolling
- **Shared Dice Pool**: Shared pool of dice for group actions
- **Dice Trust Verification**: Cryptographic proof of fair rolls

### 14. Admin & GM Tools

**Ideas:**
- **GM Dashboard**: Overview of all active games
- **NPC Stat Blocks**: Pre-built NPC stat blocks (SRD monsters)
- **Loot Generator**: Random loot tables by CR/level
- **Map Maker**: Simple grid-based map editor
- **Quest Generator**: AI-generated quests with rewards
- **Monster Manual**: SRD monster database integration
- **Spell Database**: Complete spell database with search
- **Magic Item Database**: Magic item reference
- **World Builder**: Tools for creating campaign worlds
- **Plot Thread Templates**: Pre-built plot thread structures
- **GM Checklist**: Session preparation checklist
- **Random Tables**: Customizable random encounter tables
- **Handout Generator**: Create handouts for players
- **Clue System**: Track clues and their distribution

### 15. User & Account Features

**Ideas:**
- **OAuth2/SAML**: Google, GitHub, Discord login
- **2FA**: Two-factor authentication
- **Profile Page**: User profile with avatar, bio, favorite systems
- **Game History**: View all games the user has participated in
- **Friend System**: Add friends, invite to games
- **User Settings**: Theme, notification preferences
- **API Keys**: Generate API keys for programmatic access
- **Account Deletion**: GDPR-compliant account deletion
- **Email Notifications**: Session reminders, game invites
- **Dark/Light Theme**: Toggle between themes
- **Custom Avatars**: Upload custom avatars for characters

---

## 🟢 Nice-to-Have / Low Priority

### 16. Multi-System Support Expansion

**Ideas:**
- **More Systems**: Warhammer 40k, Cyberpunk 2020, Vampire: The Masquerade, GURPS, Savage Worlds, Stars Without Number, Fate Core, Cortex Plus, Powered by the Apocalypse
- **System-Specific Combat**: Different combat mechanics per system
- **Cross-System Characters**: Characters that can play across multiple systems
- **System Migration**: Migrate characters between systems
- **Custom Rules Engine**: User-defined rules for custom systems
- **Rulebook Integration**: Link to official rulebooks for reference
- **System Compatibility Layer**: Import characters from other TTRPG platforms

### 17. Analytics & Reporting

**Ideas:**
- **Session Analytics**: Duration, player activity, dice roll distribution
- **Combat Analytics**: Damage dealt, hits/misses, conditions applied
- **Player Performance**: Skill check success rates, dice roll stats
- **LLM Usage Analytics**: Token usage, cost tracking, provider performance
- **Plot Thread Analytics**: Thread resolution rate, momentum trends
- **Game Health Metrics**: Active players, session frequency, churn rate
- **Export Reports**: Export game data as CSV/PDF
- **Trend Analysis**: Track game metrics over time
- **Benchmarking**: Compare game metrics to community averages

### 18. Integration & API

**Ideas:**
- **Discord Bot**: Send game events to Discord channels
- **Twitch Integration**: Stream game events to Twitch
- **YouTube Integration**: Auto-generate game session videos
- **Webhook Support**: HTTP webhooks for game events
- **REST API Documentation**: OpenAPI/Swagger with examples
- **GraphQL API**: Alternative API surface for complex queries
- **Mobile App**: React Native or Flutter mobile app
- **Desktop App**: Electron-based desktop app
- **Plugin System**: Third-party plugin architecture
- **Widget Support**: Embed game widgets on websites

### 19. Accessibility

**Ideas:**
- **Screen Reader Support**: ARIA labels, semantic HTML
- **Keyboard Navigation**: Full keyboard-accessible UI
- **High Contrast Mode**: For visually impaired users
- **Font Size Scaling**: Adjustable text size
- **Color Blind Mode**: Color-blind friendly palettes
- **Reduced Motion**: Option to disable animations
- **Voice Commands**: Voice-controlled game actions
- **Alternative Text**: Alt text for game images/tokens

### 20. Community & Social

**Ideas:**
- **Game Marketplace**: Share and discover game templates
- **GM Directory**: Find GMs for new games
- **Character Marketplace**: Share character builds
- **Forum**: Community forum for TTRPG discussion
- **Leaderboards**: Track GM ratings, game quality
- **Achievement System**: In-game achievements for milestones
- **Clan/Guild System**: Group games together
- **Tournament Mode**: Organized competitive play
- **Game Review System**: Rate games and GMs
- **Content Creator Tools**: Tools for TTRPG streamers

---

## 🏗️ Architectural Improvements

### 21. Code Quality & Refactoring

**Ideas:**
- **Unit Tests**: Add test coverage for GameEngine, DiceEngine, CombatService
- **Integration Tests**: Test API endpoints with test database
- **E2E Tests**: Playwright/Cypress tests for critical user flows
- **Code Analysis**: Add SonarQube or similar code quality tooling
- **Documentation**: Auto-generate API docs from Swagger
- **API Versioning**: Version the API for backward compatibility
- **Dependency Injection**: Review DI graph for circular dependencies
- **Generic Repository**: Consider adding a generic repository pattern
- **CQRS**: Separate read/write models for complex queries
- **Event Sourcing**: Consider event sourcing for game state

### 22. Performance Optimization

**Ideas:**
- **Caching Layer**: Redis/Memcached for frequently accessed data
- **Query Optimization**: Add EF Core query logging and optimization
- **Lazy Loading**: Review EF Core lazy/eager loading patterns
- **Pagination**: Add pagination for large result sets (messages, logs)
- **Compression**: Gzip/Brotli compression for API responses
- **CDN**: Serve static assets from CDN
- **Database Read Replicas**: For read-heavy workloads
- **Background Processing**: Use Hangfire for heavy operations (embeddings, RAG)
- **Batch Operations**: Batch database writes for efficiency
- **Connection Pooling**: Optimize PostgreSQL connection pool settings

### 23. Deployment & DevOps

**Ideas:**
- **Docker Compose**: Multi-container dev environment
- **Kubernetes**: Production deployment with K8s
- **CI/CD Pipeline**: GitHub Actions for build/test/deploy
- **Staging Environment**: Pre-production staging server
- **Database Backups**: Automated PostgreSQL backups
- **Monitoring**: Prometheus + Grafana for metrics
- **Logging**: Centralized logging with ELK/Loki
- **Alerting**: Alert on errors, high latency, disk space
- **Blue/Green Deployment**: Zero-downtime deployments
- **Feature Flags**: Runtime feature toggles
- **Health Checks**: Kubernetes liveness/readiness probes
- **Secrets Management**: HashiCorp Vault or AWS Secrets Manager

### 24. Testing Strategy

**Ideas:**
- **Unit Tests**: 80%+ coverage for business logic
- **Integration Tests**: API endpoint tests with mock database
- **SignalR Tests**: Test hub methods with `TestServer`
- **Load Testing**: k6/locust for concurrent player scenarios
- **Chaos Testing**: Test failure scenarios (LLM down, DB down)
- **Security Testing**: OWASP ZAP or similar for vulnerability scanning
- **Performance Testing**: Benchmark critical paths
- **Contract Testing**: Verify API contract between frontend/backend
- **Visual Regression**: Test UI rendering with Percy/Chromatic

---

## 📊 Priority Matrix

| Priority | Criteria | Items |
|----------|----------|-------|
| 🔴 Critical | Production blockers, security, data integrity | 1, 2, 3, 4 |
| ✅ Done | Completed items | 2 (partial), 3 (partial) |
| 🟠 High | Major features that define the product | 5, 6, 7, 8, 9, 10 |
| 🟡 Medium | Important enhancements to existing features | 11-19 |
| 🟢 Low | Nice-to-have, future exploration | 20-24 |

---

## 📅 Suggested Phases

### Phase 1: Foundation (Current)
- [ ] Fix distributed connection state
- [ ] Security hardening
- [ ] Error handling & resilience
- [ ] Database performance improvements
- [ ] Add unit/integration tests

### Phase 2: Core Features
- [ ] Virtual Tabletop (maps, tokens, grid)
- [ ] Advanced combat system
- [ ] Player character management
- [ ] Advanced AI/LLM features (streaming, context management)
- [ ] Multi-game & lobby system

### Phase 3: Enhancement
- [ ] Chat & communication enhancements
- [ ] Session & game management
- [ ] Dice & mechanics expansion
- [ ] Admin & GM tools
- [ ] User & account features

### Phase 4: Polish
- [ ] Multi-system support expansion
- [ ] Analytics & reporting
- [ ] Integration & API
- [ ] Accessibility improvements
- [ ] Community & social features

### Phase 5: Scale
- [ ] Kubernetes deployment
- [ ] Monitoring & alerting
- [ ] CI/CD pipeline
- [ ] Performance optimization
- [ ] Testing strategy

---

## 🎯 Quick Wins (Low Effort, High Impact)

1. **Add pagination to message lists** — Prevents memory issues with long games
2. **Add SQLite fallback** — Easier local development without Docker
3. **Add message search** — Use PostgreSQL full-text search
4. **Add dice roll statistics** — Simple chart of roll distribution
5. **Add session notes** — GM-only notes per session
6. **Add markdown to chat** — Simple formatting support
7. **Add game templates** — Save/load game configurations
8. **Add LLM prompt templates** — Configure system prompts per game
9. **Add health check endpoints** — Essential for production
10. **Add rate limiting** — Simple middleware for API protection

---

## 📝 Notes

- **LLM Provider Agnostic**: The architecture supports any OpenAI-compatible API. Adding new providers is straightforward.
- **Multi-System**: The `SystemRegistry` pattern makes adding new RPG systems easy.
- **Event-Driven**: MediatR events decouple components. New features can subscribe to existing events.
- **Agent Framework**: The `AgentBus` and `GameAgent` system allows autonomous game play.
- **RAG**: pgvector enables semantic search across game content.
- **SignalR**: Real-time updates are handled via SignalR groups.

---

*Last updated: 2026-06-09*
