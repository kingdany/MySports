import { Pressable, ScrollView, StyleSheet } from 'react-native';

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
          label={c.shortName}
          active={selected === c.id}
          activeColor={CompetitionColors[c.id] ?? tint}
          onPress={() => onSelect(c.id)}
        />
      ))}
    </ScrollView>
  );
}

function Chip({ label, active, activeColor, onPress }: {
  label: string; active: boolean; activeColor: string; onPress: () => void;
}) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  // "Tutti" attivo usa il colore del tema, che in modalità scura è chiaro: il testo prende il colore dello sfondo.
  const activeText = activeColor === tint ? background : '#fff';
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
      style={[styles.chip, { backgroundColor: active ? activeColor : card }]}>
      <Text style={[styles.chipText, active && { color: activeText }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: { flexGrow: 0, flexShrink: 0 }, // altrimenti la lista sottostante, più lunga dello schermo, la schiaccia
  content: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingVertical: 10 },
  chip: { paddingHorizontal: 16, paddingVertical: 8, borderRadius: 8, alignItems: 'center' },
  chipText: { fontSize: 15, fontWeight: '600' },
});
