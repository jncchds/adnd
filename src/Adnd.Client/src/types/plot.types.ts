export type PlotThreadStatus = 'Active' | 'Resolved' | 'Abandoned'
export type PlotThreadCategory =
  | 'General' | 'Faction' | 'Mystery' | 'Personal' | 'Threat' | 'WorldEvent' | 'Relationship'

export interface MilestoneEvent {
  id: string
  title: string
  description: string
  status: 'Pending' | 'Triggered' | 'Completed' | 'Abandoned'
  triggeredAt: string | null
}

export interface PlotThread {
  id: string
  gameId: string
  title: string
  description: string
  category: PlotThreadCategory
  status: PlotThreadStatus
  momentum: number
  relevanceScore: number
  nextMilestone: string | null
  foreshadowing: string | null
  adaptationHistory: string[]
  milestoneEvents: MilestoneEvent[]
  isDynamic: boolean
  isDeleted: boolean
  createdAt?: string
}

export interface PlotContext {
  recentEvents: string
  activeNPCs: string
  activeThreads: string
  fullContext: string
}

export interface ConsistencyReport {
  isConsistent: boolean
  issues: string[]
  suggestions: string[]
}

export interface PlotContinuation {
  suggestions: string[]
  nextMilestones: string[]
}
