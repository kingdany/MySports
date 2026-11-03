import { useCallback, useEffect, useState } from 'react';

import { useEvents } from './data';

export type SeasonRef = { competition: string; season: string };

const message = (e: unknown) => (e instanceof Error ? e.message : 'Impossibile caricare la stagione');

// Carica (se serve) la stagione scelta e ne espone file, errore e aggiornamento manuale.
export function useSelectedSeason(selection: SeasonRef | null) {
  const { getSeason, loadSeason } = useEvents();
  const competition = selection?.competition;
  const season = selection?.season;
  const key = selection ? `${competition}/${season}` : null;

  // L'errore è legato alla stagione per cui si è verificato: cambiando stagione sparisce da solo.
  const [failure, setFailure] = useState<{ key: string; message: string } | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    if (!competition || !season) return;
    loadSeason(competition, season).catch((e) => setFailure({ key: `${competition}/${season}`, message: message(e) }));
  }, [competition, season, loadSeason]);

  const refresh = useCallback(async () => {
    if (!competition || !season || !key) return;
    setRefreshing(true);
    try {
      await loadSeason(competition, season, true);
      setFailure(null);
    } catch (e) {
      setFailure({ key, message: message(e) });
    }
    setRefreshing(false);
  }, [competition, season, key, loadSeason]);

  return {
    file: competition && season ? getSeason(competition, season) : undefined,
    error: failure && failure.key === key ? failure.message : null,
    refreshing,
    refresh,
  };
}
