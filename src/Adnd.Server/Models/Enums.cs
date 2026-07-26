namespace Adnd.Server.Models;

public enum GameStatus { Draft, Starting, Active, Archived }
public enum GMStatus { Idle, Running, Paused }
public enum PlayerRole { Creator, Player, Spectator, Observer }
public enum PlayerStatus { Active, Inactive, Banned }
public enum PlotThreadCategory { General, Faction, Mystery, Personal, Threat, WorldEvent, Relationship }
public enum PlotThreadStatus { Active, Resolved, Abandoned }
public enum AgentType { Creator, GM, LLM, Dice, RAG, NPC, Player, System }
public enum AgentAction { Query, Generate, Roll, Check, Narrate, Suggest, Execute, Notify, Recall, ManageState, Nudge, CreateCharacter, OpenNarrative, GenerateInitialThreads }
public enum AgentCallStatus { Pending, Running, Completed, Failed, Cancelled }
public enum SagaStep { None = 0, Init = 1, LLMDispatch = 2, LLMResponse = 3, ToolExecution = 4, ToolCoordination = 5, LLMFollowUp = 6, NarrativeReady = 7, Completed = 8, Failed = 9 }
public enum CombatStatus { Active, Paused, Finished }
public enum CombatParticipantType { Character, NPC, Neutral }
public enum CombatEventType { Start, End, TurnChange, Attack, Damage, Healing, Condition, SaveThrow, Initiative, Death, Revival, DeathSave, RoundStart }
public enum EventRecordStatus { Pending, Processing, Completed, Failed }
public enum WhisperType { PlayerToGM, GMToPlayer, PlayerToPlayer, GMBroadcast, SystemMessage, NPCToPlayer, TableTalk }
public enum Attitude { Friendly, Neutral, Unfriendly, Hostile }
