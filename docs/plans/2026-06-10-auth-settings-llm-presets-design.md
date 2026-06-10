# Auth, User Settings, and LLM Presets Design

> Date: 2026-06-10
> Status: Validated

## Overview

This slice implements the foundational user-facing features of ADnD: authentication, user settings, and LLM preset management. Everything in the game system depends on these primitives — players must be able to register, log in, manage their profile, and configure the LLMs that power the AI-GM.

## Architecture Approach

**Clean Architecture organized by vertical slices** rather than technical layers. Each feature (Auth, Users, LlmPresets) owns its controllers, DTOs, services, and handlers. Cross-cutting concerns live in a Shared folder.

```
src/Adnd.Server/
  Features/
    Auth/          (controllers, DTOs, services, handlers)
    Users/         (user settings, profile)
    LlmPresets/    (CRUD, model loading, encryption)
  Shared/          (DbContext, encryption, error handling)
  Program.cs
src/Adnd.Client/
  src/
    pages/
      auth/        (Login, Register)
      settings/    (User settings)
      llm-presets/ (LLM Preset CRUD)
```

## Data Models

### User

```csharp
class User {
  Guid Id
  string Email           // unique
  string DisplayName
  string PasswordHash
  string? RefreshToken   // current active token
  DateTime? RefreshTokenExpiry
  DateTime CreatedAt
  DateTime UpdatedAt
}
```

### LlmPreset

```csharp
class LlmPreset {
  Guid Id
  string Name
  string Provider        // openai, ollama, lmstudio, google
  string BaseModel       // chat model (e.g., gpt-4o)
  string EmbeddingModel  // embedding model (e.g., text-embedding-3-small)
  string ApiKeyEncrypted // AES-256-GCM encrypted
  string SystemPrompt
  float Temperature      // 0.0-2.0
  float? MaxTokens
  bool IsActive          // default true, soft-delete
  string CreatedByUserId
  DateTime CreatedAt
  DateTime UpdatedAt
}
```

**Key design decisions:**
- `Provider` is a string enum (not a separate table) — providers are fixed and known.
- `IsActive` allows soft-deleting presets without losing history (e.g., if a model gets deprecated).
- `ApiKeyEncrypted` stores the AES-256-GCM ciphertext. The encryption key comes from `Encryption__MasterKey` env var.
- Model lists are **optional** — users can always type freely, the dropdown is just a helper.

## API Endpoints

### Auth

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/auth/register` | Register a new account |
| `POST` | `/api/auth/login` | Login, returns JWT + refresh token |
| `POST` | `/api/auth/logout` | Invalidate refresh token |
| `POST` | `/api/auth/refresh` | Refresh JWT token |
| `GET` | `/api/auth/me` | Get current user profile |

### User Settings

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `PUT` | `/api/users/me/display-name` | Update display name |
| `PUT` | `/api/users/me/password` | Change password (requires current + new) |

### LLM Presets

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/llm-presets` | List all presets (filter by `IsActive`) |
| `POST` | `/api/llm-presets` | Create a new preset |
| `PUT` | `/api/llm-presets/{id}` | Update a preset |
| `DELETE` | `/api/llm-presets/{id}` | Soft-delete (set `IsActive = false`) |
| `POST` | `/api/llm-presets/{id}/activate` | Re-activate a preset |
| `GET` | `/api/llm-presets/providers/{provider}/models` | Load available models for a provider |

## Model Loading

| Provider | Endpoint | Model Filtering |
|----------|----------|-----------------|
| OpenAI | `https://api.openai.com/v1/models` | Chat: `gpt-*`, `o-*` | Embedding: `text-embedding-*` |
| Google AI Studio | `https://generativelanguage.googleapis.com/v1beta/models` | Type: `chat` vs `embedding` |
| Ollama | `http://<host>:11434/api/tags` | All local models (user picks type) |
| LMStudio | `http://<host>:1234/v1/models` | Same format as OpenAI |

**Frontend behavior:**
- Provider selection triggers a model list fetch (with caching).
- Model field shows an autocomplete dropdown from the fetched list.
- User can always type freely — no validation that the model exists in the list.

## Frontend Pages

| Route | Page |
|-------|------|
| `/login` | Login form |
| `/register` | Registration form |
| `/settings` | User settings (display name, password change) |
| `/llm-presets` | LLM Preset CRUD + model loading |

## Error Handling

**Backend:**
- FluentValidation for all DTOs (email format, password strength, preset name required, etc.)
- Global exception handler → consistent JSON error responses
- 401 for invalid/expired tokens, 403 for unauthorized access
- 404 for soft-deleted presets
- 409 for duplicate email on register

**Frontend:**
- Axios interceptors handle 401 → auto-logout + redirect to `/login`
- Form-level validation errors displayed inline
- Toast notifications for success/failure on mutations
- Loading states on all async operations

## Security

- Passwords hashed with **BCrypt**
- API keys encrypted at rest with **AES-256-GCM** using env-var-derived master key
- Refresh tokens are **single-use** — rotated on each use, old token invalidated
- JWT tokens: short-lived access (15 min) + longer-lived refresh (7 days)
- CORS configured for `http://localhost:3000` in dev, production origin in prod

## Supported Providers

1. **OpenAI API** — Chat + embedding models via OpenAI API
2. **Ollama** — Local models via `/api/tags`
3. **LMStudio** — Local models via `/v1/models` (OpenAI-compatible)
4. **Google AI Studio** — Gemini models via Google API
