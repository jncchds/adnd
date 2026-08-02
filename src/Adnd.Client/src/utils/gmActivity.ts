import type { GMActivity } from '../types'

// Friendly, reassuring copy for each saga step — shown wherever the GM's activity is
// surfaced (chat footer, admin dashboard) so players/GMs know it hasn't frozen and
// roughly what it's doing while they wait.
export const STEP_LABELS: Record<string, string> = {
  Init: 'Preparing the scene…',
  LLMDispatch: 'The GM is thinking…',
  LLMResponse: 'Reviewing what happened…',
  ToolExecution: 'Resolving an action…',
  ToolCoordination: 'Resolving an action…',
  LLMFollowUp: 'Composing the narration…',
  NarrativeReady: 'Almost done…',
  // Game-start pipeline (GameStartService) — doesn't run through AgentSaga, so these are
  // plain phase names rather than SagaStep values.
  GeneratingPlot: 'Weaving the opening plot…',
  GeneratingRecap: 'Recalling last session…',
  GeneratingNarration: 'Writing the opening scene…',
}

export const TOOL_LABELS: Record<string, string> = {
  rollDice: 'Rolling dice…',
  skillCheck: 'Checking a skill…',
  requestPlayerRoll: 'Waiting on your roll…',
  queryCharacter: 'Looking up a character…',
  queryNPCs: 'Looking up NPCs…',
  searchPlotContext: 'Recalling the story so far…',
  updateGameState: 'Updating the world…',
  sendWhisper: 'Sending a whisper…',
  startCombat: 'Starting combat…',
  addCombatParticipant: 'Adding a combatant…',
  generateLoot: 'Generating loot…',
  narrate: 'Narrating…',
}

export function activityLabel(a: GMActivity): string {
  if (a.step === 'ToolExecution' && a.detail && TOOL_LABELS[a.detail]) return TOOL_LABELS[a.detail]
  return STEP_LABELS[a.step] ?? 'Working…'
}
