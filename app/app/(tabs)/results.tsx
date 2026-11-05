import { useMemo } from 'react';

import { CompetitionBar } from '@/components/CompetitionBar';
import { EventList } from '@/components/EventList';
import { SeasonPicker } from '@/components/SeasonPicker';
import { View } from '@/components/Themed';
import { useCompetitionSeason } from '@/lib/useSelectedSeason';

export default function ResultsScreen() {
  const {
    competitions, competition, season, selectCompetition, selectSeason, file, error, refreshing, refresh,
  } = useCompetitionSeason();

  // Dal più recente. Le prove libere non hanno risultati da mostrare: restano solo partite e sessioni con classifica.
  const results = useMemo(
    () => file?.events.filter((e) => e.status === 'finished' && (e.result?.length || e.homeScore != null)).reverse(),
    [file],
  );

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar competitions={competitions} selected={competition?.id ?? null} onSelect={selectCompetition} />
      <SeasonPicker competition={competition} season={season} onSelect={selectSeason} />
      <EventList
        events={results}
        loading={!file && !error}
        error={error}
        refreshing={refreshing}
        onRefresh={refresh}
        emptyMessage="Nessun risultato in questa stagione"
      />
    </View>
  );
}
