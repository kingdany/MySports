import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

// Id di competizione scelto dall'utente; null = nessuna scelta (o "Tutti" in Prossimi).
type Choice = string | null;

type Preferences = {
  competition: Choice; // condivisa da Risultati e Classifiche
  setCompetition: (id: Choice) => void;
  upcomingCompetition: Choice; // filtro di Prossimi, separato perché lì si parte da tutte le competizioni
  setUpcomingCompetition: (id: Choice) => void;
};

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

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [competition, setCompetition] = useStoredChoice('competition');
  const [upcomingCompetition, setUpcomingCompetition] = useStoredChoice('upcomingCompetition');

  const value = useMemo(
    () => ({ competition, setCompetition, upcomingCompetition, setUpcomingCompetition }),
    [competition, setCompetition, upcomingCompetition, setUpcomingCompetition],
  );

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences(): Preferences {
  const preferences = useContext(PreferencesContext);
  if (!preferences) throw new Error('usePreferences va usato dentro PreferencesProvider');
  return preferences;
}
