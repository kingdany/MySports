import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

// Id di competizione scelto dall'utente; null = nessuna scelta (o "Tutti" in Prossimi).
type Choice = string | null;

type Preferences = {
  competition: Choice; // condivisa da Risultati e Classifiche
  setCompetition: (id: Choice) => void;
  competitionBySport: Record<string, string>; // ultima competizione scelta in ogni sport, in Risultati e Classifiche
  rememberCompetition: (sport: string, id: string) => void;
  upcomingCompetition: Choice; // filtro di Prossimi, separato perché lì si parte da tutte le competizioni
  setUpcomingCompetition: (id: Choice) => void;
  recentTeams: string[]; // ultime squadre scelte con la ricerca di "Tutte" (teamLabel), dalla più recente
  rememberTeam: (label: string) => void;
};

// Squadre suggerite nella ricerca di "Tutte" prima di scrivere.
const MAX_RECENT_TEAMS = 5;

const PreferencesContext = createContext<Preferences | null>(null);

// Una scelta salvata sul telefono. Se la memoria non funziona l'app resta usabile: la scelta vale fino alla chiusura.
function useStoredChoice(key: string): [Choice, (id: Choice) => void] {
  const [value, setValue] = useState<Choice>(null);

  useEffect(() => {
    AsyncStorage.getItem(key)
      .then((stored) => {
        if (stored) setValue((current) => current ?? stored);
      })
      .catch(() => {});
  }, [key]);

  const choose = useCallback((id: Choice) => {
    setValue(id);
    (id ? AsyncStorage.setItem(key, id) : AsyncStorage.removeItem(key)).catch(() => {});
  }, [key]);

  return [value, choose];
}

// Coppie chiave-valore salvate sul telefono, come useStoredChoice. Quelle scelte prima del caricamento restano.
function useStoredMap(key: string): [Record<string, string>, (name: string, value: string) => void] {
  const [map, setMap] = useState<Record<string, string>>({});
  const changed = useRef(false); // si salva solo dopo una scelta, non il valore vuoto di partenza

  useEffect(() => {
    AsyncStorage.getItem(key)
      .then((stored) => {
        const saved: unknown = stored ? JSON.parse(stored) : null;
        if (saved && typeof saved === 'object') setMap((current) => ({ ...(saved as Record<string, string>), ...current }));
      })
      .catch(() => {});
  }, [key]);

  useEffect(() => {
    if (changed.current) AsyncStorage.setItem(key, JSON.stringify(map)).catch(() => {});
  }, [key, map]);

  const set = useCallback((name: string, value: string) => {
    changed.current = true;
    setMap((current) => (current[name] === value ? current : { ...current, [name]: value }));
  }, []);

  return [map, set];
}

// Elenco salvato sul telefono, dal più recente: una voce scelta di nuovo torna in cima, oltre `max` le più vecchie escono.
// Come useStoredMap, le scelte fatte prima del caricamento restano davanti a quelle salvate.
function useStoredList(key: string, max: number): [string[], (item: string) => void] {
  const [list, setList] = useState<string[]>([]);
  const changed = useRef(false); // si salva solo dopo una scelta, non il valore vuoto di partenza

  useEffect(() => {
    AsyncStorage.getItem(key)
      .then((stored) => {
        const saved: unknown = stored ? JSON.parse(stored) : null;
        if (Array.isArray(saved)) {
          const items = saved.filter((x): x is string => typeof x === 'string');
          setList((current) => [...new Set([...current, ...items])].slice(0, max));
        }
      })
      .catch(() => {});
  }, [key, max]);

  useEffect(() => {
    if (changed.current) AsyncStorage.setItem(key, JSON.stringify(list)).catch(() => {});
  }, [key, list]);

  const add = useCallback((item: string) => {
    changed.current = true;
    setList((current) => (current[0] === item ? current : [item, ...current.filter((x) => x !== item)].slice(0, max)));
  }, [max]);

  return [list, add];
}

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [competition, setCompetition] = useStoredChoice('competition');
  const [competitionBySport, rememberCompetition] = useStoredMap('competitionBySport');
  const [upcomingCompetition, setUpcomingCompetition] = useStoredChoice('upcomingCompetition');
  const [recentTeams, rememberTeam] = useStoredList('recentTeams', MAX_RECENT_TEAMS);

  const value = useMemo(
    () => ({
      competition, setCompetition, competitionBySport, rememberCompetition, upcomingCompetition, setUpcomingCompetition,
      recentTeams, rememberTeam,
    }),
    [
      competition, setCompetition, competitionBySport, rememberCompetition, upcomingCompetition, setUpcomingCompetition,
      recentTeams, rememberTeam,
    ],
  );

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences(): Preferences {
  const preferences = useContext(PreferencesContext);
  if (!preferences) throw new Error('usePreferences va usato dentro PreferencesProvider');
  return preferences;
}
