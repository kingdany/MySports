import { useCallback, useDeferredValue, useMemo, useState } from 'react';

import { CompetitionBar } from '@/components/CompetitionBar';
import { EventList } from '@/components/EventList';
import { TeamPicker } from '@/components/TeamPicker';
import { View } from '@/components/Themed';
import { useEvents } from '@/lib/data';
import { isPractice } from '@/lib/format';
import { usePreferences } from '@/lib/preferences';
import { sportChoice, upcomingChoice, useCompetitions } from '@/lib/sports';
import { useTeamFilter } from '@/lib/useTeamFilter';

// Con "Tutti" e con "Tutte" le competizioni di uno sport si mostrano solo i prossimi giorni, altrimenti la lista cresce
// con ogni competizione aggiunta. Scegliendo una competizione si vede invece tutto il resto della stagione.
const ALL_DAYS = 30;

export default function UpcomingScreen() {
  const { events, loading, error, refreshing, refresh } = useEvents();
  const { all, visible: competitions } = useCompetitions(); // le nascoste non compaiono, nemmeno in "Tutti" e "Tutte"
  const { upcomingCompetition, setUpcomingCompetition } = usePreferences();

  // "Tutti", uno sport o una competizione; una scelta salvata nascosta o che non esiste più vale come la più vicina.
  const current = upcomingChoice(upcomingCompetition, competitions, all);
  const selected = current.choice;

  // Uno sport con una competizione visibile sola non ha "Tutte": si sceglie direttamente la competizione.
  const selectSport = useCallback((sport: string | null) => {
    const inSport = competitions.filter((c) => c.sport === sport);
    setUpcomingCompetition(sport === null ? null : inSport.length === 1 ? inSport[0].id : sportChoice(sport));
  }, [competitions, setUpcomingCompetition]);
  const selectCompetition = useCallback((id: string | null) => {
    setUpcomingCompetition(id ?? (current.sport ? sportChoice(current.sport) : null));
  }, [current.sport, setUpcomingCompetition]);

  // Il pulsante cambia subito, con la lista in caricamento; poi arriva la lista filtrata, che richiede più tempo.
  const shown = useDeferredValue(selected);
  const shownChoice = upcomingChoice(shown, competitions, all);
  const limited = shownChoice.competition === null; // "Tutti" o "Tutte"

  // Ora letta all'apertura della schermata e a ogni refresh, che sposta in avanti anche il limite dei giorni.
  const [openedAt, setOpenedAt] = useState(Date.now);
  const onRefresh = useCallback(() => {
    setOpenedAt(Date.now());
    return refresh();
  }, [refresh]);

  // Dal più vicino, senza prove libere: con "Tutti" e "Tutte" fino a ALL_DAYS giorni, altrimenti tutto il resto della
  // stagione.
  const { sport: shownSport, competition: shownCompetition } = shownChoice;
  const upcoming = useMemo(() => {
    const limit = openedAt + ALL_DAYS * 24 * 60 * 60 * 1000;
    const included = new Set(
      competitions.filter((c) => shownSport === null || c.sport === shownSport).map((c) => c.id),
    );
    return events?.filter(
      (e) =>
        (e.status === 'scheduled' || e.status === 'live') &&
        !isPractice(e.session) &&
        (shownCompetition !== null
          ? e.competition === shownCompetition
          : new Date(e.startsAt).getTime() <= limit && included.has(e.competition)),
    );
  }, [events, competitions, shownSport, shownCompetition, openedAt]);

  // Il filtro per squadra vale dentro uno sport ("Tutte" del calcio: l'Inter in Serie A e in Champions League): con
  // "Tutti" non compare e non si applica. Nei motori non ci sono squadre, quindi non compare.
  const team = useTeamFilter(shownSport === null ? undefined : upcoming);

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar
        competitions={all}
        sport={current.sport}
        competition={current.competition}
        onSelectSport={selectSport}
        onSelectCompetition={selectCompetition}
        showAll
      />
      <TeamPicker teams={team.teams} teamId={team.teamId} onSelect={team.selectTeam} searchable={limited} />
      <EventList
        events={shownSport === null ? upcoming : team.events}
        loading={loading || shown !== selected || team.switching}
        error={error}
        refreshing={refreshing}
        onRefresh={onRefresh}
        emptyMessage={
          limited ? `Nessun evento nei prossimi ${ALL_DAYS} giorni` : 'Nessun evento in programma per questa stagione'
        }
        linked={false}
      />
    </View>
  );
}
