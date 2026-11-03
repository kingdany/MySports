import { EventList } from '@/components/EventList';
import { useEvents } from '@/lib/data';

export default function ResultsScreen() {
  const { events, loading, error, refreshing, refresh } = useEvents();

  // Tutta la stagione già disputata, dal più recente. Le prove libere non hanno risultati da mostrare:
  // restano solo partite e sessioni con classifica.
  const results = events
    ?.filter((e) => e.status === 'finished' && (e.result?.length || e.homeScore != null))
    .reverse();

  return (
    <EventList
      events={results}
      loading={loading}
      error={error}
      refreshing={refreshing}
      onRefresh={refresh}
      emptyMessage="Nessun risultato in questa stagione"
    />
  );
}
