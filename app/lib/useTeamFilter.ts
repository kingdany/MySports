import { useDeferredValue, useMemo, useState } from 'react';

import type { SportEvent, Team } from './types';

export const teamLabel = (team: Team) => team.shortName ?? team.name;

// Filtro per squadra su un elenco di eventi. Le squadre sono quelle presenti nell'elenco, in ordine alfabetico.
// Se la squadra scelta non c'è (es. una stagione in cui non era in Serie A) vale come "tutte".
export function useTeamFilter(events: SportEvent[] | undefined) {
  const [chosen, setChosen] = useState<string | null>(null);

  const teams = useMemo(() => {
    const byId = new Map<string, Team>();
    for (const e of events ?? []) {
      if (e.homeTeam) byId.set(e.homeTeam.id, e.homeTeam);
      if (e.awayTeam) byId.set(e.awayTeam.id, e.awayTeam);
    }
    return [...byId.values()].sort((a, b) => teamLabel(a).localeCompare(teamLabel(b), 'it'));
  }, [events]);

  const teamId = teams.some((t) => t.id === chosen) ? chosen : null;

  // La barra cambia subito, con la lista in caricamento; poi arriva la lista filtrata.
  const shown = useDeferredValue(teamId);

  const filtered = useMemo(
    () => (shown === null ? events : events?.filter((e) => e.homeTeam?.id === shown || e.awayTeam?.id === shown)),
    [events, shown],
  );

  return { teams, teamId, selectTeam: setChosen, events: filtered, switching: shown !== teamId };
}
