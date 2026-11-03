import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

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

async function fetchSeason(competition: string, season: string): Promise<SeasonFile> {
  const file = await fetchJson<SeasonFile>(`${seasonKey(competition, season)}.json`);
  return { ...file, events: file.events.sort(byStart) };
}

// Scarica meta.json e la stagione corrente di ogni competizione elencata.
async function fetchCurrentSeasons(): Promise<{ competitions: CompetitionInfo[]; seasons: SeasonFile[] }> {
  const meta = await fetchJson<MetaFile>('meta.json');
  const seasons = await Promise.all(meta.competitions.map((c) => fetchSeason(c.id, c.currentSeason)));
  return { competitions: meta.competitions, seasons };
}

type EventsState = {
  competitions: CompetitionInfo[];
  events: SportEvent[] | null; // stagioni correnti di tutte le competizioni
  error: string | null;
  loading: boolean;
  refreshing: boolean;
  refresh: () => Promise<void>;
  // Qualsiasi stagione: le correnti sono già caricate, le passate si scaricano su richiesta e restano in memoria.
  getSeason: (competition: string, season: string) => SeasonFile | undefined;
  loadSeason: (competition: string, season: string, force?: boolean) => Promise<void>;
  getEvent: (id: string) => SportEvent | undefined;
};

const EventsContext = createContext<EventsState | null>(null);

// Dati condivisi da tutte le schermate: si scaricano una volta e si aggiornano insieme.
export function EventsProvider({ children }: { children: ReactNode }) {
  const [competitions, setCompetitions] = useState<CompetitionInfo[]>([]);
  const [currentSeasons, setCurrentSeasons] = useState<SeasonFile[] | null>(null);
  const [pastSeasons, setPastSeasons] = useState<Record<string, SeasonFile>>({});
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await fetchCurrentSeasons();
      setCompetitions(data.competitions);
      setCurrentSeasons(data.seasons);
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

  const events = useMemo(
    () => (currentSeasons ? currentSeasons.flatMap((s) => s.events).sort(byStart) : null),
    [currentSeasons],
  );

  const getSeason = useCallback(
    (competition: string, season: string) =>
      currentSeasons?.find((s) => s.competition === competition && s.season === season) ??
      pastSeasons[seasonKey(competition, season)],
    [currentSeasons, pastSeasons],
  );

  // Lancia un errore se il download fallisce: lo gestisce la schermata che l'ha chiesto.
  const loadSeason = useCallback(async (competition: string, season: string, force = false) => {
    const isCurrent = competitions.some((c) => c.id === competition && c.currentSeason === season);
    if (isCurrent) {
      if (force) await load();
      return;
    }
    const key = seasonKey(competition, season);
    if (!force && pastSeasons[key]) return;
    const file = await fetchSeason(competition, season);
    setPastSeasons((prev) => ({ ...prev, [key]: file }));
  }, [competitions, pastSeasons, load]);

  const getEvent = useCallback(
    (id: string) =>
      events?.find((e) => e.id === id) ??
      Object.values(pastSeasons).flatMap((s) => s.events).find((e) => e.id === id),
    [events, pastSeasons],
  );

  return (
    <EventsContext.Provider
      value={{
        competitions, events, error, refreshing, refresh, getSeason, loadSeason, getEvent,
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
