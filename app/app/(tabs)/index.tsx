import { useDeferredValue, useMemo } from 'react';

import { CompetitionBar } from '@/components/CompetitionBar';
import { EventList } from '@/components/EventList';
import { View } from '@/components/Themed';
import { useEvents } from '@/lib/data';
import { isPractice } from '@/lib/format';
import { usePreferences } from '@/lib/preferences';

export default function UpcomingScreen() {
  const { competitions, events, loading, error, refreshing, refresh } = useEvents();
  const { upcomingCompetition, setUpcomingCompetition } = usePreferences();

  // Una competizione salvata che non esiste più vale come "Tutti".
  const selected = competitions.some((c) => c.id === upcomingCompetition) ? upcomingCompetition : null;

  // Il pulsante cambia subito, con la lista in caricamento; poi arriva la lista filtrata, che richiede più tempo.
  const shown = useDeferredValue(selected);

  // Tutto il resto della stagione, dal più vicino, senza prove libere.
  const upcoming = useMemo(
    () =>
      events?.filter(
        (e) =>
          (e.status === 'scheduled' || e.status === 'live') &&
          !isPractice(e.session) &&
          (shown === null || e.competition === shown),
      ),
    [events, shown],
  );

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar competitions={competitions} selected={selected} onSelect={setUpcomingCompetition} showAll />
      <EventList
        events={upcoming}
        loading={loading || shown !== selected}
        error={error}
        refreshing={refreshing}
        onRefresh={refresh}
        emptyMessage="Nessun evento in programma per questa stagione"
        linked={false}
      />
    </View>
  );
}
