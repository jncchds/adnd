import { useParams } from 'react-router-dom';
import { usePlotWeaver } from '../api/hooks/usePlot';
import PlotBoardAdminTab from './PlotBoardAdminTab';

export default function AdminPlotBoardPage() {
  const { id } = useParams<{ id: string }>();
  const { threads, isLoading } = usePlotWeaver(id);

  if (!id) return null;
  return <PlotBoardAdminTab threads={threads} isLoading={isLoading} gameId={id} />;
}
