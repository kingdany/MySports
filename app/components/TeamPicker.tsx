import { PickerBar } from '@/components/PickerBar';
import { teamLabel } from '@/lib/useTeamFilter';
import type { Team } from '@/lib/types';

const ALL = 'all';

type Props = {
  teams: Team[];
  teamId: string | null; // null = tutte le squadre
  onSelect: (teamId: string | null) => void;
};

// Scelta della squadra; non compare se gli eventi non hanno squadre (es. F1).
export function TeamPicker({ teams, teamId, onSelect }: Props) {
  if (teams.length === 0) return null;

  const selected = teams.find((t) => t.id === teamId);

  return (
    <PickerBar
      label="Squadra"
      value={selected ? teamLabel(selected) : 'Tutte'}
      title="Scegli la squadra"
      options={[{ key: ALL, label: 'Tutte' }, ...teams.map((t) => ({ key: t.id, label: teamLabel(t) }))]}
      selectedKey={teamId ?? ALL}
      onSelect={(key) => onSelect(key === ALL ? null : key)}
    />
  );
}
