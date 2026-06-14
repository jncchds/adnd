// Phase 2e: Agent saga state machine
// Saga requires correct MassTransit 8.x saga data base class.
// Deferred until Phase 2 — consumers are migrated first.
/*
public class AgentSaga :
    MassTransit.SagaStateMachine<AgentSagaData>,
    IConsumer<AgentCallQueued>,
    IConsumer<LLMDispatchRequested>,
    IConsumer<LLMResponseReceived>,
    IConsumer<ToolCallRequested>,
    IConsumer<ToolCallCompleted>,
    IConsumer<NarrativeReady>
{
    // ... saga implementation ...
}
*/
