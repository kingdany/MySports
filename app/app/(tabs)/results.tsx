import { useEffect, useState } from 'react';

import { EventList } from '@/components/EventList';
import { SeasonPicker, type SeasonSelection } from '@/components/SeasonPicker';
import { View } from '@/components/Themed';
import { useEvents } from '@/lib/data';

export default function ResultsScreen() {
  const { competitions, events, loading, error, refreshing, refresh, pastSeason, loadPastSeason } = useEvents();
  const [selected, setSelected] = useState<SeasonSelection>(null);
  const [pastError, setPastError] = useState<string | null>(null);
  const [pastRefreshing, setPastRefreshing] = useState(false);

  // Le stagioni passate si scaricano solo quando vengono scelte.
  useEffect(() => {
    if (!selected) return;
    loadPastSeason(selected.competition, selected.season).catch((e) =>
      setPastError(e instanceof Error ? e.message : 'Impossibile caricare la stagione'),
    );
  }, [selected, loadPastSeason]);

  const select = (selection: SeasonSelection) => {
    setPastError(null);
    setSelected(selection);
  };

  const refreshPast = async () => {
    if (!selected) return;
    setPastRefreshing(true);
    try {
      await loadPastSeason(selected.competition, selected.season, true);
      setPastError(null);
    } catch (e) {
      setPastError(e instanceof Error ? e.message : 'Impossibile caricare la stagione');
    }
    setPastRefreshing(false);
  };

  const past = selected ? pastSeason(selected.competition, selected.season) : undefined;
  const source = selected ? past : events ?? undefined;

  // Dal più recente. Le prove libere non hanno risultati da mostrare: restano solo partite e sessioni con classifica.
  const results = source
    ?.filter((e) => e.status === 'finished' && (e.result?.length || e.homeScore != null))
    .reverse();

  return (
    <View style={{ flex: 1 }}>
      <SeasonPicker competitions={competitions} selected={selected} onSelect={select} />
      <EventList
        events={results}
        loading={selected ? !past && !pastError : loading}
        error={selected ? pastError : error}
        refreshing={selected ? pastRefreshing : refreshing}
        onRefresh={selected ? refreshPast : refresh}
        emptyMessage="Nessun risultato in questa stagione"
      />
    </View>
  );
}
