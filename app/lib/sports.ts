import { useMemo } from 'react';

import { useEvents } from './data';
import { usePreferences } from './preferences';
import type { CompetitionInfo } from './types';

// Sport della barra di sopra, dal campo "sport" di meta.json, in quest'ordine. Uno sport che non è qui va in fondo, con il
// suo id come nome, finché non lo si aggiunge.
const SPORTS = [
  { id: 'football', name: 'Calcio' },
  { id: 'motorsport', name: 'Motori' }, // F1 e MotoGP
];

export type Sport = { id: string; name: string; competitions: CompetitionInfo[] };

// Le competizioni divise per sport, ognuna nell'ordine di meta.json.
export function groupBySport(competitions: CompetitionInfo[]): Sport[] {
  const rank = (id: string) => {
    const i = SPORTS.findIndex((s) => s.id === id);
    return i < 0 ? SPORTS.length : i;
  };
  return [...new Set(competitions.map((c) => c.sport))]
    .sort((a, b) => rank(a) - rank(b))
    .map((id) => ({
      id,
      name: SPORTS.find((s) => s.id === id)?.name ?? id,
      competitions: competitions.filter((c) => c.sport === id),
    }));
}

// Le competizioni non nascoste dall'utente. Ne resta sempre almeno una: se fossero nascoste tutte (la schermata delle
// competizioni non lo permette) si mostrano tutte, perché Risultati e Classifiche non resterebbero vuote.
export function visibleCompetitions(competitions: CompetitionInfo[], hidden: string[]): CompetitionInfo[] {
  if (hidden.length === 0) return competitions;
  const visible = competitions.filter((c) => !hidden.includes(c.id));
  return visible.length > 0 ? visible : competitions;
}

// Tutte le competizioni di meta.json e quelle visibili.
export function useCompetitions(): { all: CompetitionInfo[]; visible: CompetitionInfo[] } {
  const { competitions } = useEvents();
  const { hiddenCompetitions } = usePreferences();
  const visible = useMemo(() => visibleCompetitions(competitions, hiddenCompetitions), [competitions, hiddenCompetitions]);
  return { all: competitions, visible };
}

// La scelta di Prossimi, salvata come testo: null è "Tutti", "sport:football" sono "Tutte" le competizioni del calcio,
// altrimenti l'id di una competizione.
const SPORT_PREFIX = 'sport:';
export const sportChoice = (sport: string) => SPORT_PREFIX + sport;

export type UpcomingChoice = {
  choice: string | null; // la scelta valida più vicina a quella salvata; null è "Tutti"
  sport: string | null; // sport attivo nella barra di sopra
  competition: string | null; // competizione attiva in quella di sotto; null con "Tutti" o "Tutte"
};

// La scelta salvata, se è ancora visibile. Altrimenti (competizione nascosta o sparita da meta.json) si resta nel suo
// sport: "Tutte", o la sua sola competizione visibile, che non ha "Tutte". Se lo sport non ha più competizioni visibili
// si torna a "Tutti".
export function upcomingChoice(
  stored: string | null,
  visible: CompetitionInfo[],
  all: CompetitionInfo[],
): UpcomingChoice {
  let sport: string | undefined;
  if (stored?.startsWith(SPORT_PREFIX)) {
    sport = stored.slice(SPORT_PREFIX.length);
  } else {
    const competition = visible.find((c) => c.id === stored);
    if (competition) return { choice: competition.id, sport: competition.sport, competition: competition.id };
    sport = all.find((c) => c.id === stored)?.sport;
  }
  const inSport = visible.filter((c) => c.sport === sport);
  if (sport && inSport.length > 1) return { choice: sportChoice(sport), sport, competition: null };
  if (inSport.length === 1) return { choice: inSport[0].id, sport: inSport[0].sport, competition: inSport[0].id };
  return { choice: null, sport: null, competition: null };
}

// La competizione di Risultati e Classifiche: quella scelta, se è visibile. Altrimenti un'altra del suo sport (l'ultima
// scelta lì, o la prima), o la prima visibile di meta.json.
export function resultsCompetition(
  chosen: string | null,
  visible: CompetitionInfo[],
  all: CompetitionInfo[],
  bySport: Record<string, string>,
): CompetitionInfo | null {
  const competition = visible.find((c) => c.id === chosen);
  if (competition) return competition;
  const sport = all.find((c) => c.id === chosen)?.sport;
  const inSport = visible.filter((c) => c.sport === sport);
  return inSport.find((c) => c.id === bySport[sport ?? '']) ?? inSport[0] ?? visible[0] ?? null;
}
