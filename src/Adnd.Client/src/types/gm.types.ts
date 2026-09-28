import type { GMStatus } from './game.types'

/** Matches GMStatusController.GetStatus, which returns { gameId, status }. */
export interface GMStatusResponse {
  gameId: string
  status: GMStatus
}

/** Pushed over SignalR ("GMActivity") as an agent call moves through its saga steps. */
export interface GMActivity {
  gameId: string
  step: string
  detail: string | null
}

/**
 * The creator's direction for a story the LLM runs (StoryNudgeDto). Put in front of the
 * narrator for `turnsRemaining` more GM turns, or until withdrawn when that is null.
 */
export interface StoryNudge {
  id: string
  text: string
  turnsRemaining: number | null
  createdAt: string
  expiredAt: string | null
}
