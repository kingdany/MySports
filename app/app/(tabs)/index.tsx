import { EventList } from '@/components/EventList';
import { useEvents } from '@/lib/data';

export default function UpcomingScreen() {
  const { events, loading, error, refreshing, refresh } = useEvents();

  // Tutto il resto della stagione, dal più vicino.
  const upcoming = events?.filter((e) => e.status === 'scheduled' || e.status === 'live');

  return (
    <EventList
      events={upcoming}
      loading={loading}
      error={error}
      refreshing={refreshing}
      onRefresh={refresh}
      emptyMessage="Nessun evento in programma per questa stagione"
    />
  );
}
