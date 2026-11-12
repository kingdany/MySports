import { PickerBar } from '@/components/PickerBar';
import type { CompetitionInfo } from '@/lib/types';

type Props = {
  competition: CompetitionInfo | null;
  season: string | null;
  onSelect: (season: string) => void;
};

// Oltre questo numero di stagioni (F1 e MotoGP ne hanno quasi 80) le stagioni si dividono per decennio.
const GROUP_ABOVE = 12;

// Decennio dell'anno d'inizio: "2026" e "2026-27" -> "Anni 2020".
const decade = (season: string) => `Anni ${Math.floor(parseInt(season.slice(0, 4), 10) / 10) * 10}`;

// Scelta tra le stagioni disponibili per la competizione; la stagione corrente è evidenziata.
export function SeasonPicker({ competition, season, onSelect }: Props) {
  if (!competition) return null;
  const seasons = competition.seasons ?? [competition.currentSeason];
  const grouped = seasons.length > GROUP_ABOVE;

  return (
    <PickerBar
      label="Stagione"
      value={season ?? ''}
      title={`${competition.name}: scegli la stagione`}
      options={seasons.map((s) => ({
        key: s,
        label: s,
        highlighted: s === competition.currentSeason,
        group: grouped ? decade(s) : undefined,
      }))}
      selectedKey={season}
      onSelect={onSelect}
    />
  );
}
