import type { SportEvent } from './types';

const SESSION_LABELS: Record<string, string> = {
  fp1: 'Prove libere 1',
  fp2: 'Prove libere 2',
  fp3: 'Prove libere 3',
  sprint_qualifying: 'Qualifiche Sprint',
  sprint: 'Sprint',
  qualifying: 'Qualifiche',
  race: 'Gara',
  match: 'Partita',
};

const COMPETITION_LABELS: Record<string, string> = {
  'serie-a': 'Serie A',
  'champions-league': 'UCL', // il badge è stretto: "Champions" andrebbe a capo
  f1: 'F1',
  motogp: 'MotoGP',
};

const STAGE_LABELS: Record<string, string> = {
  playoffs: 'Spareggi',
  last_16: 'Ottavi',
  quarter_finals: 'Quarti',
  semi_finals: 'Semifinali',
  final: 'Finale',
};

// Fasi dell'eliminazione diretta, nell'ordine del torneo.
export const STAGES = Object.keys(STAGE_LABELS);

export const stageLabel = (stage: string) => STAGE_LABELS[stage] ?? stage;

export const sessionLabel = (session: string) => SESSION_LABELS[session] ?? session;

// Prove libere: presenti nei dati ma non mostrate nell'app.
export const isPractice = (session: string) => session === 'fp1' || session === 'fp2' || session === 'fp3';

export const competitionLabel = (competition: string) => COMPETITION_LABELS[competition] ?? competition;

// "Giornata 11" per il calcio, "Ottavi · andata" o "Finale" per l'eliminazione diretta, "Round 19" per i motori.
export function roundLabel(event: SportEvent): string | null {
  const stage = event.stage ? STAGE_LABELS[event.stage] : undefined;
  if (stage) return event.round === 1 ? `${stage} · andata` : event.round === 2 ? `${stage} · ritorno` : stage;
  return event.round ? `${event.session === 'match' ? 'Giornata' : 'Round'} ${event.round}` : null;
}

// "4-3 ai rigori", oppure null se la partita non è finita ai rigori.
export const penaltiesLabel = (event: SportEvent) =>
  event.homePenalties != null && event.awayPenalties != null
    ? `${event.homePenalties}-${event.awayPenalties} ai rigori`
    : null;

export const formatTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

const isThisYear = (date: Date) => date.getFullYear() === new Date().getFullYear();

// Data e ora complete per il dettaglio; anno solo se diverso dal corrente, ora solo se la fonte la conosce.
export function formatEventDate(event: SportEvent): string {
  const date = new Date(event.startsAt);
  return date.toLocaleString('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    ...(isThisYear(date) ? {} : { year: 'numeric' }),
    ...(event.dateOnly ? {} : { hour: '2-digit', minute: '2-digit' }),
  });
}

// Chiave del giorno nel fuso orario del telefono, es. "2026-11-06".
const dayKey = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

function dayTitle(date: Date): string {
  const today = new Date();
  const offset = (days: number) => dayKey(new Date(today.getFullYear(), today.getMonth(), today.getDate() + days));
  const key = dayKey(date);
  if (key === offset(0)) return 'Oggi';
  if (key === offset(1)) return 'Domani';
  if (key === offset(-1)) return 'Ieri';
  const label = date.toLocaleDateString('it-IT', {
    weekday: 'long', day: 'numeric', month: 'long', ...(isThisYear(date) ? {} : { year: 'numeric' }),
  });
  return label.charAt(0).toUpperCase() + label.slice(1);
}

export type DaySection = { title: string; data: SportEvent[] };

// Raggruppa gli eventi per giorno locale, mantenendo l'ordine ricevuto.
export function groupByDay(events: SportEvent[]): DaySection[] {
  const sections: (DaySection & { key: string })[] = [];
  for (const event of events) {
    const date = new Date(event.startsAt);
    const key = dayKey(date);
    const last = sections.at(-1);
    if (last?.key === key) last.data.push(event);
    else sections.push({ key, title: dayTitle(date), data: [event] });
  }
  return sections;
}
