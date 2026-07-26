import type { GMStatus } from './game.types'

export interface GMStatusResponse {
  gameId: string
  gmStatus: GMStatus
  lastAction: string | null
  lastActionAt: string | null
  isPaused: boolean
}

export interface SwayRequest {
  direction: string
  intensity: number
  content: string
}
