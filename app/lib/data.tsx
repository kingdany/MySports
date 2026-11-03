import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';

import type { MetaFile, SeasonFile, SportEvent } from './types';

// File JSON pubblicati su GitHub Pages dal workflow "Aggiorna dati".
export const DATA_URL = 'https://kingdany.github.io/MySports';

async function fetchJson<T>(path: string): Promise<T> {
  // Il parametro evita che la cache HTTP restituisca un file vecchio dopo un aggiornamento.
  const res = await fetch(`${DATA_URL}/${path}?t=${Date.now()}`);
  if (!res.ok) throw new Error(`Errore ${res.status} nel caricamento dei dati`);
  return res.json();
}

// Scarica la stagione corrente di ogni competizione elencata in meta.json e unisce gli eventi.
async function fetchAllSeasons(): Promise<SportEvent[]> {
  const meta = await fetchJson<MetaFile>('meta.json');
  const seasons = await Promise.all(
    meta.competitions.map((c) => fetchJson<SeasonFile>(`${c.id}/${c.currentSeason}.json`)),
  );
  return seasons
    .flatMap((s) => s.events)
    .sort((a, b) => Date.parse(a.startsAt) - Date.parse(b.startsAt));
}

type EventsState = {
  events: SportEvent[] | null;
  error: string | null;
  loading: boolean;
  refreshing: boolean;
  refresh: () => Promise<void>;
  getEvent: (id: string) => SportEvent | undefined;
};

const EventsContext = createContext<EventsState | null>(null);

// Dati condivisi da tutte le schermate: si scaricano una volta e si aggiornano insieme.
export function EventsProvider({ children }: { children: ReactNode }) {
  const [events, setEvents] = useState<SportEvent[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      setEvents(await fetchAllSeasons());
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Impossibile caricare i dati');
    }
  }, []);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }, [load]);

  useEffect(() => {
    load();
  }, [load]);

  const getEvent = useCallback((id: string) => events?.find((e) => e.id === id), [events]);

  return (
    <EventsContext.Provider
      value={{ events, error, refreshing, refresh, getEvent, loading: events === null && error === null }}>
      {children}
    </EventsContext.Provider>
  );
}

export function useEvents(): EventsState {
  const state = useContext(EventsContext);
  if (!state) throw new Error('useEvents va usato dentro EventsProvider');
  return state;
}
