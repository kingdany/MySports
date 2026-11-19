import { PickerBar } from '@/components/PickerBar';
import { TeamLogo } from '@/components/TeamLogo';
import { usePreferences } from '@/lib/preferences';
import { teamLabel } from '@/lib/useTeamFilter';
import type { Team } from '@/lib/types';

const ALL = 'all';

type Props = {
  teams: Team[];
  teamId: string | null; // null = tutte le squadre
  onSelect: (teamId: string | null) => void;
  // Con "Tutte" le competizioni di uno sport le squadre sono troppe per mostrarle tutte: si cercano per nome e a campo
  // vuoto si vedono solo le ultime scelte.
  searchable?: boolean;
};

// Scelta della squadra; non compare se gli eventi non hanno squadre (es. F1). "Tutte", la scelta predefinita, è
// evidenziata come la stagione corrente, così non sembra una squadra tra le altre.
export function TeamPicker({ teams, teamId, onSelect, searchable = false }: Props) {
  const { recentTeams, rememberTeam } = usePreferences();

  if (teams.length === 0) return null;

  const selected = teams.find((t) => t.id === teamId);

  // Le ultime scelte che sono anche in questo elenco (una squadra senza partite nei prossimi giorni non c'è).
  const suggested = recentTeams
    .map((label) => teams.find((t) => teamLabel(t) === label)?.id)
    .filter((id) => id !== undefined);

  const select = (key: string) => {
    const team = teams.find((t) => t.id === key);
    if (searchable && team) rememberTeam(teamLabel(team));
    onSelect(team ? team.id : null);
  };

  return (
    <PickerBar
      label="Squadra"
      value={selected ? teamLabel(selected) : 'Tutte'}
      title="Scegli la squadra"
      options={[
        // Toglie il filtro. Nell'elenco completo è sempre la prima riga; con la ricerca c'è solo se una squadra è scelta,
        // perché da solo sopra il campo vuoto sembrava un altro "Chiudi".
        ...(!searchable || selected ? [{ key: ALL, label: 'Tutte le squadre', highlighted: true, pinned: true }] : []),
        ...teams.map((t) => ({
          key: t.id,
          label: teamLabel(t),
          icon: <TeamLogo uri={t.logoUrl} name={teamLabel(t)} size={18} />,
          searchText: t.name,
        })),
      ]}
      selectedKey={teamId ?? ALL}
      onSelect={select}
      rows={!searchable} // una squadra per riga; con la ricerca le poche voci restano affiancate
      search={
        searchable
          ? {
              placeholder: 'Cerca una squadra',
              suggested,
              suggestedTitle: 'Ultime scelte',
              hint: suggested.length > 0
                ? 'Scrivi il nome per cercare tra tutte le squadre'
                : 'Scrivi il nome di una squadra per cercarla tra tutte le competizioni. Le ultime squadre scelte compariranno qui.',
              empty: 'Nessuna squadra contiene questo testo',
            }
          : undefined
      }
    />
  );
}
