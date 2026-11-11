import { PickerBar } from '@/components/PickerBar';
import type { CompetitionInfo } from '@/lib/types';

type Props = {
  competition: CompetitionInfo | null;
  season: string | null;
  onSelect: (season: string) => void;
};

// Scelta tra le stagioni disponibili per la competizione; la stagione corrente è evidenziata.
export function SeasonPicker({ competition, season, onSelect }: Props) {
  if (!competition) return null;

  return (
    <PickerBar
      label="Stagione"
      value={season ?? ''}
      title={`${competition.name}: scegli la stagione`}
      options={(competition.seasons ?? [competition.currentSeason]).map((s) => ({
        key: s,
        label: s,
        highlighted: s === competition.currentSeason,
      }))}
      selectedKey={season}
      onSelect={onSelect}
    />
  );
}
