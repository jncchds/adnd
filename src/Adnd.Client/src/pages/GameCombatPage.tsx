import { useParams } from 'react-router-dom';
import CombatTab from './CombatTab';

export default function GameCombatPage() {
  const { id } = useParams<{ id: string }>();
  if (!id) return null;
  return <CombatTab gameId={id} />;
}
