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
  f1: 'F1',
  motogp: 'MotoGP',
};

export const sessionLabel = (session: string) => SESSION_LABELS[session] ?? session;

export const competitionLabel = (competition: string) => COMPETITION_LABELS[competition] ?? competition;

export const formatTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString('it-IT', {
    weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit',
  });

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
  const label = date.toLocaleDateString('it-IT', { weekday: 'long', day: 'numeric', month: 'long' });
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
