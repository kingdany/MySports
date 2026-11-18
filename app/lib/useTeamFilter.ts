import { useDeferredValue, useMemo, useState } from 'react';

import type { SportEvent, Team } from './types';

export const teamLabel = (team: Team) => team.shortName ?? team.name;

// Stemmi per nome della squadra (teamLabel), ricavati dalle partite: le classifiche hanno solo il nome, che l'importatore
// scrive allo stesso modo (nome breve, altrimenti completo).
export function teamLogos(events: SportEvent[] | undefined) {
  const logos = new Map<string, string>();
  for (const e of events ?? [])
    for (const team of [e.homeTeam, e.awayTeam])
      if (team?.logoUrl) logos.set(teamLabel(team), team.logoUrl);
  return logos;
}

// Filtro per squadra su un elenco di eventi. Le squadre sono quelle presenti nell'elenco, in ordine alfabetico.
// Se la squadra scelta non c'è (es. una stagione in cui non era in Serie A) vale come "tutte".
// Le squadre si riconoscono per nome (teamLabel), non per id: le fonti hanno id diversi per lo stesso club (la Roma di
// football-data in Serie A e quella di ESPN in Europa League), che con "Tutte" del calcio sono nello stesso elenco.
export function useTeamFilter(events: SportEvent[] | undefined) {
  const [chosen, setChosen] = useState<string | null>(null);

  // Una squadra per nome: la prima trovata, con il suo id e il suo stemma.
  const teams = useMemo(() => {
    const byLabel = new Map<string, Team>();
    for (const e of events ?? [])
      for (const team of [e.homeTeam, e.awayTeam])
        if (team && !byLabel.has(teamLabel(team))) byLabel.set(teamLabel(team), team);
    return [...byLabel.values()].sort((a, b) => teamLabel(a).localeCompare(teamLabel(b), 'it'));
  }, [events]);

  const selected = teams.find((t) => t.id === chosen);
  const teamId = selected ? selected.id : null;
  const label = selected ? teamLabel(selected) : null;

  // La barra cambia subito, con la lista in caricamento; poi arriva la lista filtrata.
  const shown = useDeferredValue(label);

  const filtered = useMemo(
    () =>
      shown === null
        ? events
        : events?.filter((e) => (e.homeTeam && teamLabel(e.homeTeam) === shown) || (e.awayTeam && teamLabel(e.awayTeam) === shown)),
    [events, shown],
  );

  return { teams, teamId, selectTeam: setChosen, events: filtered, switching: shown !== label };
}
