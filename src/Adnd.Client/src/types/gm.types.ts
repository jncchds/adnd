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

export interface SwayRequest {
  direction: string
  intensity: number
  content: string
}
