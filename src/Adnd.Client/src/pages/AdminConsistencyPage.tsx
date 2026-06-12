import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useConsistency } from '../api/hooks/useCharacters';
import AdminConsistencyTab from '../components/admin/AdminConsistencyTab';
export default function AdminConsistencyPage() {
  const { id } = useParams<{ id: string }>();
  const { report, isLoading, check } = useConsistency(id);
  const [checking, setChecking] = useState(false);

  const handleCheck = async () => {
    setChecking(true);
    await check(50);
    setChecking(false);
  };

  if (!id) return null;
  return <AdminConsistencyTab report={report} isLoading={isLoading || checking} onCheck={handleCheck} />;
}
