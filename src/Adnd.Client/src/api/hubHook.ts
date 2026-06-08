import { useRef, useCallback, useState } from 'react';

interface HubConnection {
  isConnected: boolean;
  connect: (url: string, token?: string) => Promise<void>;
  disconnect: () => Promise<void>;
  on: (event: string, handler: (...args: any[]) => void) => void;
  off: (event: string, handler?: (...args: any[]) => void) => void;
  invoke: (method: string, ...args: any[]) => Promise<any>;
  get connectionId(): string | null;
}

// Whisper response from server
export interface WhisperResponse {
  id: string;
  fromPlayerId: string;
  fromCharacter: string;
  fromRole: string;
  content: string;
  type: number;
  targets: string;
  createdAt: string;
  isSent?: boolean;
}

// Agent call events from server
export interface AgentCallStartedEvent {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  createdAt: string;
}

export interface AgentCallCompletedEvent {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  status: number;
  output?: string;
  outputMessage?: string;
  durationMs: number;
  completedAt: string;
  error?: string;
}

// ==================== Tool Call Events ====================

export interface ToolCallNotificationEvent {
  toolCallId: string;
  toolName: string;
  outputMessage: string;
  requiresConfirmation: boolean;
  timestamp: string;
}

export interface ToolCallConfirmedEvent {
  toolCallId: string;
  toolName: string;
  approved: boolean;
  outputMessage?: string;
  result?: string;
}

export interface PlayerRollRequestedEvent {
  toolCallId: string;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
  approved: boolean;
}

export interface PlayerRollConfirmedEvent {
  toolCallId: string;
  playerId: string;
  playerName: string;
  skill: string;
  formula: string;
  dc: number;
}

export interface PlayerRollDeclinedEvent {
  toolCallId: string;
  playerId: string;
  playerName: string;
  skill: string;
}

// Combat events from server
export interface CombatStartedEvent {
  combatId: string;
  name?: string;
  currentRound: number;
  participants: CombatParticipantEvent[];
  startedAt: string;
}

export interface CombatParticipantEvent {
  id: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
}

export interface CombatEndedEvent {
  combatId: string;
  result?: string;
  endedAt: string;
}

export interface CombatParticipantAddedEvent {
  participantId: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
}

export interface CombatParticipantRemovedEvent {
  participantId: string;
}

export interface InitiativeRolledEvent {
  participantId: string;
  displayName: string;
  initiative: number;
  rolls: number[];
}

export interface InitiativeCompleteEvent {
  turnOrder: string;
  participants: CombatParticipantEvent[];
}

export interface TurnAdvancedEvent {
  currentRound: number;
  participantId: string;
  displayName: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
}

export interface CombatAttackEvent {
  attacker: string;
  weapon: string;
  target: string;
  hit: boolean;
  isCritical: boolean;
  isFumble: boolean;
  attackRoll: number;
  attackDice: number;
  ac: number;
  damageDice: number;
  damageTotal: number;
  damageInfo: string;
  targetHP: number;
  targetMaxHP: number;
}

export interface CombatDamageEvent {
  participantId: string;
  displayName?: string;
  damage: number;
  hp?: number;
  maxHP?: number;
  source?: string;
}

export interface CombatHealEvent {
  participantId: string;
  displayName?: string;
  amount: number;
  hp?: number;
  maxHP?: number;
  source?: string;
}

export interface CombatSaveThrowEvent {
  participant: string;
  saveType: string;
  diceRoll: number;
  dc: number;
  success: boolean;
  rolledAt: string;
}

export interface CombatDeathSaveEvent {
  participant: string;
  success: boolean;
  successes: number;
  failures: number;
  isStabilized: boolean;
  isDead: boolean;
}

export interface ConditionAppliedEvent {
  participantId: string;
  conditionName: string;
  duration?: number;
}

export interface ConditionRemovedEvent {
  participantId: string;
  conditionName: string;
}

export interface CombatLogUpdatedEvent {
  combatLog: CombatLog;
}

export interface CombatLog {
  combatId: string;
  name?: string;
  status: string;
  currentRound: number;
  currentTurnIndex: number;
  participants: CombatParticipantSummary[];
  events: CombatLogEvent[];
}

export interface CombatParticipantSummary {
  id: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
  conditions: ConditionEntry[];
  isCurrentTurn: boolean;
  isDead: boolean;
}

export interface ConditionEntry {
  name: string;
  duration: number;
  description?: string;
}

export interface CombatLogEvent {
  id: string;
  round: number;
  turnIndex: number;
  type: string;
  actorName: string;
  targetName: string;
  content: string;
  createdAt: string;
}

const HUB_URL = '/gamehub';

function createHubConnection(): HubConnection {
  let connection: any = null;
  let isConnected = false;
  const listeners = new Map<string, Set<(...args: any[]) => void>>();

  const hub: HubConnection = {
    get isConnected() {
      return isConnected;
    },

    get connectionId() {
      return connection?.connectionId ?? null;
    },

    connect: async (url: string, token?: string) => {
      if (connection?.state === 1) {
        isConnected = true;
        return;
      }

      const signalR = await import('@microsoft/signalr');

      const builder = new signalR.HubConnectionBuilder()
        .withUrl(url, {
          accessTokenFactory: () => token || '',
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect()
        .build();

      builder.onclose(() => {
        isConnected = false;
      });

      builder.onreconnecting(() => {
        isConnected = false;
      });

      builder.onreconnected(() => {
        isConnected = true;
      });

      await builder.start();
      connection = builder;
      isConnected = true;
    },

    disconnect: async () => {
      if (connection) {
        await connection.stop();
        connection = null;
        isConnected = false;
      }
    },

    on: (event: string, handler: (...args: any[]) => void) => {
      if (!listeners.has(event)) {
        listeners.set(event, new Set());
      }
      listeners.get(event)!.add(handler);

      if (connection) {
        connection.on(event, (...args: any[]) => {
          listeners.get(event)?.forEach((h) => h(...args));
        });
      }
    },

    off: (event: string, handler?: (...args: any[]) => void) => {
      if (handler && listeners.has(event)) {
        listeners.get(event)!.delete(handler);
      } else if (connection) {
        connection.off(event);
      }
    },

    invoke: async (method: string, ...args: any[]) => {
      if (!connection || connection.state !== 1) {
        throw new Error('Hub connection is not active');
      }
      return connection.invoke(method, ...args);
    },
  };

  return hub;
}

// Custom hook for connecting to the game hub
export function useGameHub() {
  const hubRef = useRef<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  if (!hubRef.current) {
    hubRef.current = createHubConnection();
  }

  const connect = useCallback(async (token?: string) => {
    try {
      await hubRef.current!.connect(HUB_URL, token);
      setIsConnected(true);
    } catch (e) {
      console.error('Failed to connect to game hub:', e);
      setIsConnected(false);
    }
  }, []);

  const disconnect = useCallback(async () => {
    await hubRef.current?.disconnect();
    setIsConnected(false);
  }, []);

  const on = useCallback((event: string, handler: (...args: any[]) => void) => {
    hubRef.current?.on(event, handler);
  }, []);

  const off = useCallback((event: string, handler?: (...args: any[]) => void) => {
    hubRef.current?.off(event, handler);
  }, []);

  const invoke = useCallback(async (method: string, ...args: any[]) => {
    return hubRef.current?.invoke(method, ...args);
  }, []);

  return {
    isConnected,
    connect,
    disconnect,
    on,
    off,
    invoke,
    // New hub methods
    // In-game public message
    sendMessage: async (sessionId: string, content: string) => {
      return hubRef.current?.invoke('SendMessage', sessionId, content);
    },
    // In-game whisper to GM
    sendInGameWhisper: async (sessionId: string, content: string) => {
      return hubRef.current?.invoke('SendInGameWhisper', sessionId, content);
    },
    // OOC public message
    sendOOCMessage: async (sessionId: string, content: string) => {
      return hubRef.current?.invoke('SendOOCMessage', sessionId, content);
    },
    // OOC whisper from player to GM
    sendOOCWhisper: async (sessionId: string, content: string) => {
      return hubRef.current?.invoke('SendOOCWhisper', sessionId, content);
    },
    // OOC whisper from GM to player
    sendOOCWhisperToPlayer: async (targetPlayerId: string, content: string) => {
      return hubRef.current?.invoke('SendOOCWhisperToPlayer', targetPlayerId, content);
    },
    // In-game whisper from GM to player (e.g., divination)
    sendInGameWhisperToPlayer: async (targetPlayerId: string, content: string) => {
      return hubRef.current?.invoke('SendInGameWhisperToPlayer', targetPlayerId, content);
    },
    // Legacy (kept for compatibility)
    sendWhisper: async (targets: string, content: string) => {
      return hubRef.current?.invoke('SendWhisper', targets, content);
    },
    sendGMWhisper: async (targetPlayerId: string, content: string) => {
      return hubRef.current?.invoke('SendGMWhisper', targetPlayerId, content);
    },
    callAgent: async (fromAgent: number, toAgent: number, action: number, input: string, sessionId?: string) => {
      return hubRef.current?.invoke('CallAgent', fromAgent, toAgent, action, input, sessionId);
    },
    getWhisperHistory: async (gameId: string, limit = 50) => {
      return hubRef.current?.invoke('GetWhisperHistory', gameId, limit);
    },
    getAgentCallHistory: async (gameId: string, fromAgent?: number, action?: number, limit = 50) => {
      return hubRef.current?.invoke('GetAgentCallHistory', gameId, fromAgent, action, limit);
    },
    getAgentCall: async (callId: string) => {
      return hubRef.current?.invoke('GetAgentCall', callId);
    },
    updateGameState: async (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => {
      return hubRef.current?.invoke('UpdateGameState', gameId, gameState, plotSeed, gameParameters);
    },
    getSystems: async (gameId: string) => {
      return hubRef.current?.invoke('GetSystems', gameId);
    },
    createSystem: async (gameId: string, name: string, jsonDefinition: string) => {
      return hubRef.current?.invoke('CreateSystem', gameId, name, jsonDefinition);
    },

    // Combat methods
    startCombat: async (gameId: string, sessionId?: string, name?: string) => {
      return hubRef.current?.invoke('StartCombat', gameId, sessionId, name);
    },
    endCombat: async (combatId: string, result?: string) => {
      return hubRef.current?.invoke('EndCombat', combatId, result);
    },
    pauseCombat: async (combatId: string) => {
      return hubRef.current?.invoke('PauseCombat', combatId);
    },
    resumeCombat: async (combatId: string) => {
      return hubRef.current?.invoke('ResumeCombat', combatId);
    },
    addParticipant: async (combatId: string, participantType: string, displayName: string,
      ac: number, currentHP: number, maxHP: number, playerId?: string, npcId?: string) => {
      return hubRef.current?.invoke('AddParticipant', combatId, participantType, displayName, ac, currentHP, maxHP, playerId, npcId);
    },
    removeParticipant: async (combatId: string, participantId: string) => {
      return hubRef.current?.invoke('RemoveParticipant', combatId, participantId);
    },
    rollInitiative: async (combatId: string, participantId: string, formula = '1d20') => {
      return hubRef.current?.invoke('RollInitiative', combatId, participantId, formula);
    },
    rollInitiativeForAll: async (combatId: string, formula = '1d20') => {
      return hubRef.current?.invoke('RollInitiativeForAll', combatId, formula);
    },
    advanceTurn: async (combatId: string) => {
      return hubRef.current?.invoke('AdvanceTurn', combatId);
    },
    retreatTurn: async (combatId: string) => {
      return hubRef.current?.invoke('RetreatTurn', combatId);
    },
    getCurrentTurn: async (combatId: string) => {
      return hubRef.current?.invoke('GetCurrentTurn', combatId);
    },
    setCurrentTurn: async (combatId: string, participantId: string) => {
      return hubRef.current?.invoke('SetCurrentTurn', combatId, participantId);
    },
    combatAttack: async (combatId: string, attackerName: string, weapon: string,
      targetId: string, attackFormula: string, attackBonus?: number,
      damageFormula?: string, damageBonus?: number, description?: string) => {
      return hubRef.current?.invoke('CombatAttack', combatId, attackerName, weapon,
        targetId, attackFormula, attackBonus, damageFormula, damageBonus, description);
    },
    combatSaveThrow: async (combatId: string, participantName: string,
      participantId: string, saveType: string, saveFormula: string, dc: number) => {
      return hubRef.current?.invoke('CombatSaveThrow', combatId, participantName,
        participantId, saveType, saveFormula, dc);
    },
    combatApplyCondition: async (combatId: string, participantId: string,
      conditionName: string, duration?: number, description?: string) => {
      return hubRef.current?.invoke('CombatApplyCondition', combatId, participantId,
        conditionName, duration, description);
    },
    combatRemoveCondition: async (combatId: string, participantId: string, conditionName: string) => {
      return hubRef.current?.invoke('CombatRemoveCondition', combatId, participantId, conditionName);
    },
    combatDealDamage: async (combatId: string, participantId: string, damage: number, source?: string) => {
      return hubRef.current?.invoke('CombatDealDamage', combatId, participantId, damage, source);
    },
    combatHeal: async (combatId: string, participantId: string, amount: number, source?: string) => {
      return hubRef.current?.invoke('CombatHeal', combatId, participantId, amount, source);
    },
    combatDeathSave: async (combatId: string, participantId: string, success: boolean) => {
      return hubRef.current?.invoke('CombatDeathSave', combatId, participantId, success);
    },
    getCombatLog: async (combatId: string) => {
      return hubRef.current?.invoke('GetCombatLog', combatId);
    },
    getActiveCombats: async (gameId: string) => {
      return hubRef.current?.invoke('GetActiveCombats', gameId);
    },

    // Spell combat
    combatCastSpell: async (combatId: string, casterName: string, spellName: string,
      targetId: string, saveFormula: string, saveDC: number,
      damageFormula?: number, damageBonus?: number, description?: string) => {
      return hubRef.current?.invoke('CombatCastSpell', combatId, casterName, spellName,
        targetId, saveFormula, saveDC, damageFormula, damageBonus, description);
    },
    combatCastAreaSpell: async (combatId: string, casterName: string, spellName: string,
      saveFormula: string, saveDC: number, damageFormula?: number,
      damageBonus?: number, description?: string, targetIds?: string[]) => {
      return hubRef.current?.invoke('CombatCastAreaSpell', combatId, casterName, spellName,
        saveFormula, saveDC, damageFormula, damageBonus, description, targetIds);
    },

    // System-specific
    combatApplySystemEffects: async (combatId: string, systemId: string, participantId: string) => {
      return hubRef.current?.invoke('CombatApplySystemEffects', combatId, systemId, participantId);
    },
    combatCalculateProficiency: async (combatId: string, systemId: string, level: number) => {
      return hubRef.current?.invoke('CombatCalculateProficiency', combatId, systemId, level);
    },
    combatCalculateSave: async (combatId: string, systemId: string, saveType: string,
      participantId: string, proficiencyBonus?: number) => {
      return hubRef.current?.invoke('CombatCalculateSave', combatId, systemId, saveType,
        participantId, proficiencyBonus);
    },

    // Progression
    combatAddXP: async (combatId: string, participantId: string, xpAmount: number, reason: string) => {
      return hubRef.current?.invoke('CombatAddXP', combatId, participantId, xpAmount, reason);
    },
    combatLevelUp: async (combatId: string, participantId: string, newLevel: number, systemId: string) => {
      return hubRef.current?.invoke('CombatLevelUp', combatId, participantId, newLevel, systemId);
    },
    combatCalculateXP: async (combatId: string, systemId: string) => {
      return hubRef.current?.invoke('CombatCalculateXP', combatId, systemId);
    },

    // Rest
    combatStartShortRest: async (combatId: string) => {
      return hubRef.current?.invoke('CombatStartShortRest', combatId);
    },
    combatStartLongRest: async (combatId: string) => {
      return hubRef.current?.invoke('CombatStartLongRest', combatId);
    },
    combatEndRest: async (combatId: string) => {
      return hubRef.current?.invoke('CombatEndRest', combatId);
    },
    combatGetRestStatus: async (combatId: string) => {
      return hubRef.current?.invoke('CombatGetRestStatus', combatId);
    },

    // Inventory
    combatAddItem: async (combatId: string, participantId: string, itemName: string,
      itemType: string, quantity = 1) => {
      return hubRef.current?.invoke('CombatAddItem', combatId, participantId, itemName, itemType, quantity);
    },
    combatRemoveItem: async (combatId: string, participantId: string, itemName: string) => {
      return hubRef.current?.invoke('CombatRemoveItem', combatId, participantId, itemName);
    },
    combatEquipItem: async (combatId: string, participantId: string, itemName: string) => {
      return hubRef.current?.invoke('CombatEquipItem', combatId, participantId, itemName);
    },
    combatUnequipItem: async (combatId: string, participantId: string, itemName: string) => {
      return hubRef.current?.invoke('CombatUnequipItem', combatId, participantId, itemName);
    },

    // Grid
    combatSetGridSize: async (combatId: string, width: number, height: number) => {
      return hubRef.current?.invoke('CombatSetGridSize', combatId, width, height);
    },
    combatSetPosition: async (combatId: string, participantId: string, gridX: number, gridY: number) => {
      return hubRef.current?.invoke('CombatSetPosition', combatId, participantId, gridX, gridY);
    },
    combatMoveParticipant: async (combatId: string, participantId: string, newGridX: number, newGridY: number) => {
      return hubRef.current?.invoke('CombatMoveParticipant', combatId, participantId, newGridX, newGridY);
    },
    combatGetPosition: async (combatId: string, participantId: string) => {
      return hubRef.current?.invoke('CombatGetPosition', combatId, participantId);
    },
    combatGetAdjacentPositions: async (combatId: string, gridX: number, gridY: number, range = 1) => {
      return hubRef.current?.invoke('CombatGetAdjacentPositions', combatId, gridX, gridY, range);
    },

    // AI Combat
    combatGetAISuggestions: async (combatId: string) => {
      return hubRef.current?.invoke('CombatGetAISuggestions', combatId);
    },
    combatGetAINPCBehavior: async (combatId: string, npcId: string) => {
      return hubRef.current?.invoke('CombatGetAINPCBehavior', combatId, npcId);
    },
    combatAutoResolve: async (combatId: string, resolutionMode = 'quick') => {
      return hubRef.current?.invoke('CombatAutoResolve', combatId, resolutionMode);
    },

    // Character creation
    createCharacter: async (gameId: string, playerId: string, characterData: any) => {
      return hubRef.current?.invoke('CreateCharacter', gameId, playerId, JSON.stringify(characterData));
    },

    // SAN (CoC)
    combatApplySANLoss: async (combatId: string, participantId: string, sanLoss: number, reason: string) => {
      return hubRef.current?.invoke('CombatApplySANLoss', combatId, participantId, sanLoss, reason);
    },
    combatApplySANRecovery: async (combatId: string, participantId: string, sanRecovery: number) => {
      return hubRef.current?.invoke('CombatApplySANRecovery', combatId, participantId, sanRecovery);
    },
    combatMakeSANCheck: async (combatId: string, participantId: string, dc: number) => {
      return hubRef.current?.invoke('CombatMakeSANCheck', combatId, participantId, dc);
    },
  };
}
