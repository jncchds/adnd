# ADnD — Event & LLM Interaction Flow Diagrams

> All diagrams use [Mermaid](https://mermaid.js.org/) syntax.

---

## 1. Full Player-Message → GM-Response Flow

The canonical game loop — from a player typing a message to the AI GM's narration appearing on every client.

```mermaid
sequenceDiagram
    participant P  as Player (Browser)
    participant HR as GameHub (SignalR)
    participant DB as PostgreSQL
    participant EB as EventBus (Wolverine)
    participant PW as PlotWeaverHandler
    participant GA as GameActionHandler
    participant AB as AgentBus
    participant SA as AgentSaga (Wolverine)
    participant LD as LLMDispatchHandler
    participant LM as LLM Provider
    participant TE as ToolExecutionHandler
    participant NH as NarrativeHandler
    participant GL as GameLifecycleHandler

    P->>HR: invoke("SendMessage", gameId, content)
    HR->>DB: INSERT Message (type=Player) + embed
    HR->>P: broadcast NewMessage (game group)
    HR->>EB: PublishAsync(MessageSent)

    par EventBus fans out
        EB->>PW: HandleAsync(MessageSent)
        Note over PW: Increment per-game counter<br/>Every N msgs → PlotWeaver.ReviewAndAdaptAsync()
    and
        EB->>GA: HandleAsync(MessageSent)
        GA->>AB: SendCallAsync(AgentCall{Action=Narrate})
    end

    AB->>DB: INSERT AgentCall (Status=Pending)
    AB->>EB: PublishAsync(AgentCallQueued)

    EB->>SA: Start(AgentCallQueued)
    Note over SA: Saga keyed on AgentCall.Id<br/>Schedules 5-min timeout

    SA->>LD: Emit LLMDispatchRequested
    LD->>DB: Load Game.LLMPreset
    LD->>LM: CompleteWithToolsAsync(systemPrompt, userPrompt, tools)
    LM-->>LD: LLMToolCallResult

    alt No tool calls
        LD->>NH: Emit NarrativeReady (direct)
    else Has tool calls
        LD->>TE: Emit ToolCallRequested[0]
        loop Each tool call
            TE->>TE: Execute via GMToolRegistry
            alt Requires player confirmation
                TE->>HR: Broadcast PlayerRollRequested
                P->>HR: invoke("ConfirmPlayerRoll", ...)
                HR->>TE: Resume saga
            end
            TE->>SA: Emit ToolCallCompleted
            SA->>TE: Emit ToolCallRequested[n+1] (if more)
        end
        SA->>LD: Emit LLMFollowUpRequested
        LD->>LM: CompleteAsync(tool results → follow-up prompt)
        LM-->>LD: Narrative text
        LD->>NH: Emit NarrativeReady
    end

    NH->>DB: INSERT Message (type=GM) + embed
    NH->>HR: broadcast NewMessage (game group)
    NH->>GL: Emit GameNarrationStarted

    GL->>DB: UPDATE Game.Status = Active (if Starting)
    GL->>HR: broadcast GameStatusChanged
    HR->>P: NewMessage (GM narration rendered via react-markdown)
```

---

## 2. Wolverine AgentSaga State Machine

The durable saga tracks each AI GM work unit from queued → completed.

```mermaid
stateDiagram-v2
    [*] --> Pending : AgentCall saved

    Pending --> Init : AgentCallQueued received
    Init --> LLMDispatch : SagaOrchestratorHandler emits LLMDispatchRequested

    LLMDispatch --> LLMResponse : LLMDispatchHandler calls provider

    LLMResponse --> NarrativeReady : No tool calls
    LLMResponse --> ToolExecution : Has tool calls

    ToolExecution --> ToolCoordination : ToolCallCompleted
    ToolCoordination --> ToolExecution : More tools remain
    ToolCoordination --> LLMFollowUp : All tools done

    LLMFollowUp --> NarrativeReady : Follow-up LLM call complete

    NarrativeReady --> Completed : Message persisted + broadcast

    Init --> Failed : Exception / timeout
    LLMDispatch --> Failed : Provider error
    ToolExecution --> Failed : Tool error
    LLMFollowUp --> Failed : Provider error

    Failed --> [*] : DeadLetterQueue (3 retries)
    Completed --> [*]
```

---

## 3. LLM Provider Selection & Execution

How a raw `AgentCall` maps to an actual LLM API call.

```mermaid
flowchart TD
    AC[AgentCall.Input\nGMDispatchOptions] --> LD[LLMDispatchHandler]

    LD --> PS[Load LLMPreset\nfrom Game.LLMPresetId]
    PS --> DK[ApiKeyEncryptionService\nDecrypt API key]
    DK --> PF[LLMProviderFactory\n.CreateFromPreset]

    PF -->|providerType = ollama| OL[OllamaLLMProvider\nOllamaSharp client]
    PF -->|providerType = openaicompatible| LC[OpenAICompatibleLLMProvider\nOpenAI-style HTTP]
    PF -->|providerType = openai| OA[OpenAILLMProvider\nOpenAI SDK]
    PF -->|providerType = google| GA[GoogleAIStudioLLMProvider\nREST API]

    OL & LC & OA & GA --> RP[ResiliencePolicies\nPolly Retry + Circuit Breaker]
    RP --> LLM[[External LLM API]]
    LLM --> TR[LLMToolCallResult\ntext + tool_calls]

    TR --> LOG[LLMInteractionLogger\nINSERT LLMInteractionLog]
    LOG --> RESP[LLMResponseReceived\nemitted to saga]

    subgraph Fallback ["Tool-call fallback (providers without native support)"]
        FB1[Append tool schema as JSON\nto system prompt]
        FB2[ParseToolCallsFromResponse\n3-pass: direct → fence → bracket]
        FB1 --> FB2
    end

    OL -.->|no native tools| Fallback
```

---

## 4. SignalR Event Topology

All real-time events the server broadcasts to connected clients.

```mermaid
flowchart LR
    subgraph Server ["Server (GameHub)"]
        CH[ChatMethods]
        CB[Combat.*]
        AG[AgentMethods]
        NH[NarrativeHandler]
        PH[PlayerDisconnectHandler]
    end

    subgraph Group ["SignalR Group: game-{id}"]
        direction TB
        NM[NewMessage]
        GN[GameNarration]
        GS[GameStatusChanged]
        GM[GMStatusChanged]
        PD[PlayerDisconnected]
        PR[PlayerReconnected]
        CS[CombatStarted]
        CE[CombatEnded]
        TA[TurnAdvanced]
        PA[ParticipantAdded]
        PM[ParticipantRemoved]
        DD[CombatDamageDealt]
        CA[CombatConditionApplied]
        CR[CombatConditionRemoved]
        RR[PlayerRollRequested]
        GT[GMThinking]
        GD[GMDoneThinking]
        GE[GMError]
    end

    CH -->|player/ooc/whisper msg| NM
    NH -->|AI narration persisted| NM
    NH -->|narration text| GN
    AG -->|GMStatus change| GM
    AG -->|composing indicator| GT & GD
    AG -->|saga failed| GE
    CB -->|InitiateCombat| CS
    CB -->|EndCombat| CE
    CB -->|NextTurn| TA
    CB -->|AddParticipant| PA
    CB -->|RemoveParticipant| PM
    CB -->|DealDamage| DD
    CB -->|ApplyCondition| CA
    CB -->|RemoveCondition| CR
    CB -->|requestPlayerRoll tool| RR
    PH -->|OnDisconnected| PD
    PH -->|OnConnected| PR
    NH -->|GameNarrationStarted → Active| GS

    Group --> C1[Client A\nChatPanel + CombatPanel]
    Group --> C2[Client B\nChatPanel + CombatPanel]
    Group --> C3[Client N...]
```

---

## 5. PlotWeaver & RAG Context Flow

How plot intelligence feeds the GM's context window.

```mermaid
flowchart TD
    MS[MessageSent event] --> PW[PlotWeaverHandler\nper-game counter]
    PW -->|every N messages| RV[PlotWeaver.ReviewAndAdaptAsync]
    PW -->|CombatEnded| RV
    PW -->|SessionCreated| RV
    PW -->|PlayerLeft/death| RV
    PW -->|StorySwayed| RV

    RV --> S1[PlotThreadGenerationStrategy\nLLM generates new threads]
    RV --> S2[PlotThreadAdaptationStrategy\nLLM adapts momentum + milestones]
    RV --> S3[PlotMilestoneSpawningStrategy\nspawns MilestoneEvents]
    RV --> S4[PlotOpportunityDetectionStrategy\ndetects story opportunities]

    S1 & S2 & S3 & S4 --> DB[(PlotThreads table\n+ vector embeddings)]

    DB --> RAG[RAGService.GeneratePlotContextAsync]
    subgraph Context Assembly
        RAG --> R1[Recent N messages]
        RAG --> R2[Active NPCs]
        RAG --> R3[Active plot threads\nby momentum]
        RAG --> R4[FindSimilarPlotThreadsAsync\npgvector cosine ⟨=⟩]
    end

    R1 & R2 & R3 & R4 --> CTX[Context string\nappended to GM system prompt]
    CTX --> LD[LLMDispatchHandler\nCompleteWithToolsAsync]
```

---

## 6. Combat System Event Flow

Turn-by-turn combat lifecycle from hub to database.

```mermaid
sequenceDiagram
    participant P  as Player (Hub)
    participant GH as GameHub.Combat
    participant CS as CombatService (facade)
    participant CL as CombatLifecycleService
    participant CI as CombatInitiativeService
    participant CT as CombatTurnService
    participant CP as CombatParticipantService
    participant DB as PostgreSQL
    participant EB as EventBus

    P->>GH: StartCombat(gameId, name)
    GH->>CL: StartCombatAsync
    CL->>DB: INSERT Combat (Status=Active)
    CL->>EB: PublishAsync(CombatStarted)
    EB->>P: broadcast CombatStarted

    P->>GH: AddParticipant(combatId, characterId/npcId)
    GH->>CP: AddParticipantAsync
    CP->>DB: INSERT CombatParticipant
    CP->>EB: PublishAsync(ParticipantAdded)
    EB->>P: broadcast ParticipantAdded

    P->>GH: RollInitiativeForAll(combatId)
    GH->>CI: RollInitiativeForAllAsync
    CI->>CI: DiceEngine.Roll("1d20+DEX")
    CI->>DB: UPDATE CombatParticipants (Initiative)
    CI->>DB: ORDER BY Initiative DESC
    CI->>EB: PublishAsync(InitiativeRolledForAll)

    loop Combat Round
        P->>GH: DealDamage(participantId, amount, type)
        GH->>CS: DealDamageAsync
        CS->>DB: UPDATE CombatParticipant.HP
        CS->>DB: INSERT CombatEvent (type=Damage)
        CS->>EB: PublishAsync(CombatDamageDealt)
        EB->>P: broadcast CombatDamageDealt

        alt HP ≤ 0 and is PC (D&D 5e)
            CS->>DB: UPDATE DeathSaveState = {successes:0, failures:0}
            P->>GH: RecordDeathSave(participantId, isSuccess, roll)
            GH->>CS: RecordDeathSaveAsync
            CS->>CS: 3 successes → Stable<br/>3 failures → Dead<br/>nat20 → 1 HP<br/>nat1 → 2 failures
        end

        P->>GH: NextTurn(combatId)
        GH->>CT: AdvanceTurnAsync
        CT->>CT: Reset action economy for new participant
        CT->>DB: UPDATE Combat.CurrentTurnIndex
        CT->>EB: PublishAsync(TurnAdvanced)
        EB->>P: broadcast TurnAdvanced
    end

    P->>GH: EndCombat(combatId)
    GH->>CL: EndCombatAsync
    CL->>DB: UPDATE Combat.Status = Finished
    CL->>EB: PublishAsync(CombatEnded)
    EB->>P: broadcast CombatEnded
    Note over EB: CombatEnded also triggers<br/>PlotWeaver + auto-loot (S11)
```

---

## 7. Auth & Session Flow

```mermaid
sequenceDiagram
    participant B  as Browser
    participant AC as AuthController
    participant AS as AuthService
    participant DB as PostgreSQL

    B->>AC: POST /api/auth/register {email, password}
    AC->>AS: RegisterAsync
    AS->>AS: BCrypt.HashPassword(password)
    AS->>DB: INSERT User
    AS->>AS: Generate JWT (HS256, 60 min)
    AS->>DB: INSERT RefreshToken (30 day)
    AC-->>B: {accessToken, refreshToken}

    Note over B: JWT stored in localStorage<br/>Sent as Authorization: Bearer header

    B->>AC: POST /api/auth/refresh {refreshToken}
    AC->>AS: RefreshAsync
    AS->>DB: Validate RefreshToken (not revoked, not expired)
    AS->>DB: DELETE old RefreshToken (rotation)
    AS->>AS: Issue new JWT + RefreshToken
    AS->>DB: INSERT new RefreshToken
    AC-->>B: {accessToken, refreshToken}

    B->>AC: POST /api/auth/logout {refreshToken}
    AC->>DB: UPDATE RefreshToken.IsRevoked = true
    AC-->>B: 204 No Content
```

---

## 8. Improvement Suggestions

These are architectural notes on gaps or enhancements worth considering as the remaining phases are built.

| # | Area | Current State | Suggestion |
|---|------|--------------|------------|
| I1 | **AgentSaga** | Wolverine saga class not yet implemented | `AgentSaga.cs` must be the first file added in Phase 6 gap-fill — the handlers reference saga state transitions but have no saga tracking them |
| I2 | **Strategy Layer** | `IAdndLlmStrategy` and 4 strategy classes referenced in design but missing from `Services/Llm/` | Add thin strategy classes that format request bodies per-provider; keeps providers interchangeable |
| I3 | **GMThinking indicator** | Designed (S1) but no server emission yet | Emit `GMThinking` at saga `LLMDispatch` step and `GMDoneThinking` at `NarrativeReady`; trivial addition |
| I4 | **Rate limiting** | CORS + auth wired, but `AddRateLimiter` not registered | Add `builder.Services.AddRateLimiter(...)` in Program.cs during Phase 9 polish |
| I5 | **Frontend** | Only `App.tsx` + `main.tsx` scaffolded | Phase 8 is the largest remaining chunk; recommend spawning a dedicated UI agent with full design context |
| I6 | **Missing controllers** | ~12 controllers from Phase 5 list absent | Build alongside Phase 6/7 services as they become available (LLMLogsController requires Phase 7 RAGService, etc.) |
| I7 | **Hangfire** | Listed in Phase 7 but not in `Program.cs` yet | Add `Hangfire.AspNetCore` + `Hangfire.PostgreSql` registration; PlotWeaver scheduled jobs land here |
| I8 | **Context trim** | `MaxContextMessages`/`MaxContextTokenEstimate` on LLMPreset designed (S5) | Hook into `AgentBus.HandleGMCall()` before building the system prompt; simple slice of message list |
```
