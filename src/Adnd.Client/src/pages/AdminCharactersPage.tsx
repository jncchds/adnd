import { useParams } from 'react-router-dom';
import { useCharacters } from '../api/hooks/useCharacters';
import AdminCharactersTab from '../components/admin/AdminCharactersTab';

export default function AdminCharactersPage() {
  const { id } = useParams<{ id: string }>();
  const { characters } = useCharacters(id);

  if (!id) return null;
  return <AdminCharactersTab characters={characters} />;
}
