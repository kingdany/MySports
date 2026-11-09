import { useCallback, useDeferredValue, useMemo, useState } from 'react';

import { CompetitionBar } from '@/components/CompetitionBar';
import { EventList } from '@/components/EventList';
import { TeamPicker } from '@/components/TeamPicker';
import { View } from '@/components/Themed';
import { useEvents } from '@/lib/data';
import { isPractice } from '@/lib/format';
import { usePreferences } from '@/lib/preferences';
import { useTeamFilter } from '@/lib/useTeamFilter';

// Con "Tutti" si mostrano solo i prossimi giorni, altrimenti la lista cresce con ogni sport aggiunto.
// Scegliendo una competizione si vede invece tutto il resto della stagione.
const ALL_DAYS = 30;

export default function UpcomingScreen() {
  const { competitions, events, loading, error, refreshing, refresh } = useEvents();
  const { upcomingCompetition, setUpcomingCompetition } = usePreferences();

  // Una competizione salvata che non esiste più vale come "Tutti".
  const selected = competitions.some((c) => c.id === upcomingCompetition) ? upcomingCompetition : null;

  // Il pulsante cambia subito, con la lista in caricamento; poi arriva la lista filtrata, che richiede più tempo.
  const shown = useDeferredValue(selected);

  // Ora letta all'apertura della schermata e a ogni refresh, che sposta in avanti anche il limite dei giorni.
  const [openedAt, setOpenedAt] = useState(Date.now);
  const onRefresh = useCallback(() => {
    setOpenedAt(Date.now());
    return refresh();
  }, [refresh]);

  // Dal più vicino, senza prove libere: con "Tutti" fino a ALL_DAYS giorni, altrimenti tutto il resto della stagione.
  const upcoming = useMemo(() => {
    const limit = openedAt + ALL_DAYS * 24 * 60 * 60 * 1000;
    return events?.filter(
      (e) =>
        (e.status === 'scheduled' || e.status === 'live') &&
        !isPractice(e.session) &&
        (shown === null ? new Date(e.startsAt).getTime() <= limit : e.competition === shown),
    );
  }, [events, shown, openedAt]);

  // Il filtro per squadra vale dentro una competizione: con "Tutti" non compare e non si applica.
  const team = useTeamFilter(shown === null ? undefined : upcoming);

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar competitions={competitions} selected={selected} onSelect={setUpcomingCompetition} showAll />
      <TeamPicker teams={team.teams} teamId={team.teamId} onSelect={team.selectTeam} />
      <EventList
        events={shown === null ? upcoming : team.events}
        loading={loading || shown !== selected || team.switching}
        error={error}
        refreshing={refreshing}
        onRefresh={onRefresh}
        emptyMessage={
          shown === null ? `Nessun evento nei prossimi ${ALL_DAYS} giorni` : 'Nessun evento in programma per questa stagione'
        }
        linked={false}
      />
    </View>
  );
}
