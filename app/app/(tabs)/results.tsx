import { useState } from 'react';

import { EventList } from '@/components/EventList';
import { SeasonPicker, type SeasonSelection } from '@/components/SeasonPicker';
import { View } from '@/components/Themed';
import { useEvents } from '@/lib/data';
import { useSelectedSeason } from '@/lib/useSelectedSeason';

export default function ResultsScreen() {
  const { competitions, events, loading, error, refreshing, refresh } = useEvents();
  const [selected, setSelected] = useState<SeasonSelection>(null);
  const past = useSelectedSeason(selected);

  const source = selected ? past.file?.events : events ?? undefined;

  // Dal più recente. Le prove libere non hanno risultati da mostrare: restano solo partite e sessioni con classifica.
  const results = source
    ?.filter((e) => e.status === 'finished' && (e.result?.length || e.homeScore != null))
    .reverse();

  return (
    <View style={{ flex: 1 }}>
      <SeasonPicker competitions={competitions} selected={selected} onSelect={setSelected} />
      <EventList
        events={results}
        loading={selected ? !past.file && !past.error : loading}
        error={selected ? past.error : error}
        refreshing={selected ? past.refreshing : refreshing}
        onRefresh={selected ? past.refresh : refresh}
        emptyMessage="Nessun risultato in questa stagione"
      />
    </View>
  );
}
