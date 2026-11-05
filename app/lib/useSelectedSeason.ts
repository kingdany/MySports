import { useCallback, useDeferredValue, useEffect, useState } from 'react';

import { useEvents } from './data';
import { usePreferences } from './preferences';

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

// Competizione e stagione mostrate da Risultati e Classifiche. La competizione è condivisa e resta salvata
// (senza una scelta valida vale la prima di meta.json); la stagione riparte da quella corrente a ogni cambio.
export function useCompetitionSeason() {
  const events = useEvents();
  const { competition: chosen, setCompetition } = usePreferences();
  const competition = events.competitions.find((c) => c.id === chosen) ?? events.competitions[0] ?? null;

  const [picked, setPicked] = useState<SeasonRef | null>(null);
  if (picked && picked.competition !== competition?.id) setPicked(null);

  const season = picked?.competition === competition?.id && picked ? picked.season : competition?.currentSeason ?? null;
  const selected = useSelectedSeason(competition && season ? { competition: competition.id, season } : null);

  // Disegnare una lista lunga richiede tempo: prima si aggiornano i pulsanti, con la schermata in caricamento
  // (file assente), e subito dopo arrivano i dati.
  const key = competition && season ? `${competition.id}/${season}` : null;
  const switching = useDeferredValue(key) !== key;

  return {
    competitions: events.competitions,
    competition,
    season,
    selectCompetition: setCompetition,
    selectSeason: (next: string) => {
      if (competition) setPicked({ competition: competition.id, season: next });
    },
    file: switching ? undefined : selected.file,
    // Finché non c'è una competizione l'unico errore possibile è quello del caricamento iniziale, da cui si riparte.
    error: switching ? null : competition ? selected.error : events.error,
    refreshing: competition ? selected.refreshing : events.refreshing,
    refresh: competition ? selected.refresh : events.refresh,
  };
}
