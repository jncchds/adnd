export enum PlotThreadStatus {
  Active = 0,
  Resolved = 1,
  Abandoned = 2,
}

export enum PlotThreadCategory {
  General = 0,
  Faction = 1,
  Mystery = 2,
  Personal = 3,
  Threat = 4,
  WorldEvent = 5,
  Relationship = 6,
}

export enum MilestoneStatus {
  Pending = 0,
  Triggered = 1,
  Completed = 2,
  Abandoned = 3,
}

export enum OpportunityType {
  NewThread = 0,
  SpawnMilestone = 1,
  AdaptThread = 2,
  MergeThreads = 3,
  EscalateThreat = 4,
}

export interface PlotThreadResponse {
  id: string;
  title: string;
  category: PlotThreadCategory;
  description: string;
  status: PlotThreadStatus;
  momentum: number;
  relevanceScore: number;
  nextMilestone: string | null;
  foreshadowing: string | null;
  adaptationHistory: string[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ThreadUpdate {
  threadId: string;
  threadTitle: string;
  oldMomentum: number | null;
  newMomentum: number | null;
  oldStatus: string | null;
  newStatus: string | null;
  oldDescription: string | null;
  newDescription: string | null;
  oldMilestone: string | null;
  newMilestone: string | null;
  reason: string;
}

export interface PlotReviewResponse {
  id: string;
  trigger: string;
  summary: string;
  updates: ThreadUpdate[];
  reviewedAt: string;
}

export interface AdjustMomentumRequest {
  delta: number;
  reason: string;
}


