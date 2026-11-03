import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Text, View, useThemeColor } from '@/components/Themed';
import { CompetitionColors } from '@/constants/Colors';
import type { CompetitionInfo } from '@/lib/types';

// null = stagione corrente di tutte le competizioni.
export type SeasonSelection = { competition: string; season: string } | null;

type Props = {
  competitions: CompetitionInfo[];
  selected: SeasonSelection;
  onSelect: (selection: SeasonSelection) => void;
};

// Barra "Stagione: ..." che apre l'elenco delle stagioni disponibili per ogni competizione.
export function SeasonPicker({ competitions, selected, onSelect }: Props) {
  const [open, setOpen] = useState(false);
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');
  const border = useThemeColor({}, 'border');
  const tint = useThemeColor({}, 'tint');

  const selectedName = competitions.find((c) => c.id === selected?.competition)?.name;
  const label = selected ? `${selectedName ?? selected.competition} · ${selected.season}` : 'Stagione corrente';

  const choose = (selection: SeasonSelection) => {
    setOpen(false);
    onSelect(selection);
  };

  return (
    <>
      <Pressable onPress={() => setOpen(true)} style={[styles.bar, { borderBottomColor: border }]}>
        <Text style={[styles.barLabel, { color: muted }]}>Stagione</Text>
        <Text style={styles.barValue}>{label} ▾</Text>
      </Pressable>

      <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setOpen(false)}>
        <SafeAreaView style={{ flex: 1 }} edges={['top', 'bottom']}>
          <View style={styles.modal}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Scegli la stagione</Text>
              <Pressable onPress={() => setOpen(false)} hitSlop={12}>
                <Text style={{ color: tint, fontSize: 16 }}>Chiudi</Text>
              </Pressable>
            </View>
            <ScrollView contentContainerStyle={styles.content}>
              <Chip label="Stagione corrente" active={selected === null} wide onPress={() => choose(null)} />
              {competitions.map((c) => (
                <View key={c.id} style={styles.section}>
                  <Text style={[styles.sectionTitle, { color: CompetitionColors[c.id] ?? muted }]}>{c.name}</Text>
                  <View style={styles.chips}>
                    {(c.seasons ?? [c.currentSeason]).map((season) => (
                      <Chip
                        key={season}
                        label={season}
                        active={selected?.competition === c.id && selected.season === season}
                        onPress={() => choose(season === c.currentSeason ? null : { competition: c.id, season })}
                        background={card}
                      />
                    ))}
                  </View>
                </View>
              ))}
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>
    </>
  );
}

function Chip({ label, active, wide, onPress, background }: {
  label: string; active: boolean; wide?: boolean; onPress: () => void; background?: string;
}) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const bg = useThemeColor({}, 'background');
  return (
    <Pressable
      onPress={onPress}
      style={[styles.chip, wide && styles.chipWide, { backgroundColor: active ? tint : background ?? card }]}>
      <Text style={[styles.chipText, active && { color: bg }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    paddingHorizontal: 16, paddingVertical: 10, borderBottomWidth: StyleSheet.hairlineWidth,
  },
  barLabel: { fontSize: 14 },
  barValue: { fontSize: 15, fontWeight: '600' },
  modal: { flex: 1 },
  modalHeader: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 16,
  },
  modalTitle: { fontSize: 20, fontWeight: '700' },
  content: { paddingHorizontal: 16, paddingBottom: 32, gap: 16 },
  section: { gap: 8 },
  sectionTitle: { fontSize: 14, fontWeight: '700', textTransform: 'uppercase' },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 8, minWidth: 64, alignItems: 'center' },
  chipWide: { alignSelf: 'flex-start' },
  chipText: { fontSize: 15, fontVariant: ['tabular-nums'] },
});
