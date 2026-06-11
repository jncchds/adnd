import { PlotThreadCategory } from '../types/plot.types';

export const CATEGORY_LABELS: Record<PlotThreadCategory, string> = {
  [PlotThreadCategory.General]: 'General',
  [PlotThreadCategory.Faction]: 'Faction',
  [PlotThreadCategory.Mystery]: 'Mystery',
  [PlotThreadCategory.Personal]: 'Personal',
  [PlotThreadCategory.Threat]: 'Threat',
  [PlotThreadCategory.WorldEvent]: 'World Event',
  [PlotThreadCategory.Relationship]: 'Relationship',
};

export const CATEGORY_COLORS: Record<PlotThreadCategory, 'default' | 'primary' | 'secondary' | 'error' | 'warning' | 'success' | 'info'> = {
  [PlotThreadCategory.General]: 'default',
  [PlotThreadCategory.Faction]: 'primary',
  [PlotThreadCategory.Mystery]: 'info',
  [PlotThreadCategory.Personal]: 'success',
  [PlotThreadCategory.Threat]: 'error',
  [PlotThreadCategory.WorldEvent]: 'warning',
  [PlotThreadCategory.Relationship]: 'secondary',
};

const MOMENTUM_COLORS: Record<string, string> = {
  urgent: '#f44336',
  high: '#ff9800',
  moderate: '#ffeb3b',
  low: '#4caf50',
  abandoned: '#9e9e9e',
};

export function getMomentumLabel(momentum: number): string {
  if (momentum >= 7) return 'Urgent';
  if (momentum >= 4) return 'High';
  if (momentum >= 1) return 'Moderate';
  if (momentum >= -2) return 'Low';
  return 'Fading';
}

export function getMomentumColor(momentum: number): string {
  if (momentum >= 7) return MOMENTUM_COLORS.urgent;
  if (momentum >= 4) return MOMENTUM_COLORS.high;
  if (momentum >= 1) return MOMENTUM_COLORS.moderate;
  if (momentum >= -2) return MOMENTUM_COLORS.low;
  return MOMENTUM_COLORS.abandoned;
}
