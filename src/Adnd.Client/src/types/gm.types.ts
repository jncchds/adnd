import type { GMStatus } from './game.types'

/** Matches GMStatusController.GetStatus, which returns { gameId, status }. */
export interface GMStatusResponse {
  gameId: string
  status: GMStatus
}

export interface SwayRequest {
  direction: string
  intensity: number
  content: string
}
