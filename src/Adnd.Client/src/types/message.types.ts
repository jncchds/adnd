export enum WhisperType {
  // === In-game whispers (narrative) ===
  InGamePlayerToGM = 0,
  InGameGMToPlayer = 1,

  // === OOC whispers (non-narrative) ===
  OOCPlayerToGM = 2,
  OOCGMToPlayer = 3,

  // === Legacy (kept for compatibility) ===
  PlayerToPlayer = 4,
  GMToGroup = 5,
  GMToAll = 6,
}
