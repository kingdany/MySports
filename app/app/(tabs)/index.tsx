import { EventList } from '@/components/EventList';
import { useEvents } from '@/lib/data';
import { isPractice } from '@/lib/format';

export default function UpcomingScreen() {
  const { events, loading, error, refreshing, refresh } = useEvents();

  // Tutto il resto della stagione, dal più vicino, senza prove libere.
  const upcoming = events?.filter(
    (e) => (e.status === 'scheduled' || e.status === 'live') && !isPractice(e.session),
  );

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
