import { Pressable, ScrollView, StyleSheet } from 'react-native';

import { CompetitionLogo, hasLogo, hasWordmark } from '@/components/CompetitionLogo';
import { Text, useThemeColor } from '@/components/Themed';
import { CompetitionColors } from '@/constants/Colors';
import type { CompetitionInfo } from '@/lib/types';

type Props = {
  competitions: CompetitionInfo[];
  selected: string | null; // null = "Tutti"
  onSelect: (id: string | null) => void;
  showAll?: boolean; // offre la voce "Tutti"
};

// Barra scorrevole per scegliere la competizione.
export function CompetitionBar({ competitions, selected, onSelect, showAll = false }: Props) {
  const tint = useThemeColor({}, 'tint');

  if (competitions.length === 0) return null;

  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.bar} contentContainerStyle={styles.content}>
      {showAll && <Chip label="Tutti" active={selected === null} activeColor={tint} onPress={() => onSelect(null)} />}
      {competitions.map((c) => (
        <Chip
          key={c.id}
          id={c.id}
          label={c.shortName}
          active={selected === c.id}
          activeColor={CompetitionColors[c.id] ?? tint}
          onPress={() => onSelect(c.id)}
        />
      ))}
    </ScrollView>
  );
}

// Logo e nome della competizione; per F1 e MotoGP il logo è già il nome e sta da solo. Attiva, sullo sfondo del colore
// della competizione, il logo diventa bianco come il testo.
function Chip({ id, label, active, activeColor, onPress }: {
  id?: string; label: string; active: boolean; activeColor: string; onPress: () => void;
}) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  // "Tutti" attivo usa il colore del tema, che in modalità scura è chiaro: il testo prende il colore dello sfondo.
  const activeText = activeColor === tint ? background : '#fff';
  const logo = id && hasLogo(id) ? id : null;
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: active }}
      style={[styles.chip, { backgroundColor: active ? activeColor : card }]}>
      {logo !== null && <CompetitionLogo id={logo} height={18} mono={active ? activeText : undefined} />}
      {!(logo !== null && hasWordmark(logo)) && <Text style={[styles.chipText, active && { color: activeText }]}>{label}</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: { flexGrow: 0, flexShrink: 0 }, // altrimenti la lista sottostante, più lunga dello schermo, la schiaccia
  content: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingVertical: 10 },
  // Altezza fissa: con o senza logo, i pulsanti sono tutti alti uguali.
  chip: { flexDirection: 'row', alignItems: 'center', gap: 6, height: 36, paddingHorizontal: 14, borderRadius: 8 },
  chipText: { fontSize: 15, fontWeight: '600' },
});
