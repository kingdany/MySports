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

// La scelta di Prossimi, salvata come testo: null è "Tutti", "sport:football" sono "Tutte" le competizioni del calcio,
// altrimenti l'id di una competizione.
const SPORT_PREFIX = 'sport:';
export const sportChoice = (sport: string) => SPORT_PREFIX + sport;

export type UpcomingChoice = {
  choice: string | null; // la scelta, se esiste ancora; altrimenti null ("Tutti")
  sport: string | null; // sport attivo nella barra di sopra
  competition: string | null; // competizione attiva in quella di sotto; null con "Tutti" o "Tutte"
};

export function upcomingChoice(stored: string | null, competitions: CompetitionInfo[]): UpcomingChoice {
  if (stored?.startsWith(SPORT_PREFIX)) {
    const sport = stored.slice(SPORT_PREFIX.length);
    if (competitions.some((c) => c.sport === sport)) return { choice: stored, sport, competition: null };
  }
  const competition = competitions.find((c) => c.id === stored);
  if (competition) return { choice: competition.id, sport: competition.sport, competition: competition.id };
  return { choice: null, sport: null, competition: null };
}
