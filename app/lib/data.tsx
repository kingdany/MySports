import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';

import type { CompetitionInfo, MetaFile, SeasonFile, SportEvent } from './types';

// File JSON pubblicati su GitHub Pages dal workflow "Aggiorna dati".
export const DATA_URL = 'https://kingdany.github.io/MySports';

async function fetchJson<T>(path: string): Promise<T> {
  // Il parametro evita che la cache HTTP restituisca un file vecchio dopo un aggiornamento.
  const res = await fetch(`${DATA_URL}/${path}?t=${Date.now()}`);
  if (!res.ok) throw new Error(`Errore ${res.status} nel caricamento dei dati`);
  return res.json();
}

const byStart = (a: SportEvent, b: SportEvent) => Date.parse(a.startsAt) - Date.parse(b.startsAt);

const seasonKey = (competition: string, season: string) => `${competition}/${season}`;

// Scarica la stagione corrente di ogni competizione elencata in meta.json e unisce gli eventi.
async function fetchCurrentSeasons(): Promise<{ competitions: CompetitionInfo[]; events: SportEvent[] }> {
  const meta = await fetchJson<MetaFile>('meta.json');
  const seasons = await Promise.all(
    meta.competitions.map((c) => fetchJson<SeasonFile>(`${seasonKey(c.id, c.currentSeason)}.json`)),
  );
  return { competitions: meta.competitions, events: seasons.flatMap((s) => s.events).sort(byStart) };
}

type EventsState = {
  competitions: CompetitionInfo[];
  events: SportEvent[] | null; // stagioni correnti di tutte le competizioni
  error: string | null;
  loading: boolean;
  refreshing: boolean;
  refresh: () => Promise<void>;
  // Stagioni passate: scaricate su richiesta e tenute in memoria.
  pastSeason: (competition: string, season: string) => SportEvent[] | undefined;
  loadPastSeason: (competition: string, season: string, force?: boolean) => Promise<void>;
  getEvent: (id: string) => SportEvent | undefined;
};

const EventsContext = createContext<EventsState | null>(null);

// Dati condivisi da tutte le schermate: si scaricano una volta e si aggiornano insieme.
export function EventsProvider({ children }: { children: ReactNode }) {
  const [competitions, setCompetitions] = useState<CompetitionInfo[]>([]);
  const [events, setEvents] = useState<SportEvent[] | null>(null);
  const [pastSeasons, setPastSeasons] = useState<Record<string, SportEvent[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await fetchCurrentSeasons();
      setCompetitions(data.competitions);
      setEvents(data.events);
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

  const pastSeason = useCallback(
    (competition: string, season: string) => pastSeasons[seasonKey(competition, season)],
    [pastSeasons],
  );

  // Lancia un errore se il download fallisce: lo gestisce la schermata che l'ha chiesto.
  const loadPastSeason = useCallback(async (competition: string, season: string, force = false) => {
    const key = seasonKey(competition, season);
    if (!force && pastSeasons[key]) return;
    const file = await fetchJson<SeasonFile>(`${key}.json`);
    setPastSeasons((prev) => ({ ...prev, [key]: file.events.sort(byStart) }));
  }, [pastSeasons]);

  const getEvent = useCallback(
    (id: string) =>
      events?.find((e) => e.id === id) ??
      Object.values(pastSeasons).flat().find((e) => e.id === id),
    [events, pastSeasons],
  );

  return (
    <EventsContext.Provider
      value={{
        competitions, events, error, refreshing, refresh, pastSeason, loadPastSeason, getEvent,
        loading: events === null && error === null,
      }}>
      {children}
    </EventsContext.Provider>
  );
}

export function useEvents(): EventsState {
  const state = useContext(EventsContext);
  if (!state) throw new Error('useEvents va usato dentro EventsProvider');
  return state;
}
