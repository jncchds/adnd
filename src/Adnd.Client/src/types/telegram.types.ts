// Wire DTOs of /api/telegram and /api/games/{id}/telegram (Controllers/TelegramController.cs).

export interface TelegramBotStatus {
  enabled: boolean
  ready: boolean
  problem: string | null
  botUsername: string | null
  webhook: boolean
}

export interface TelegramAccount {
  bot: TelegramBotStatus
  linkedUsername: string | null
  linkedAt: string | null
}

export interface TelegramCode {
  code: string
  expiresAt: string
  link: string | null
  command: string | null
}

export interface TelegramBinding {
  id: string
  chatType: string
  chatTitle: string
  createdAt: string
}

export interface GameTelegram {
  bot: TelegramBotStatus
  bindings: TelegramBinding[]
}
