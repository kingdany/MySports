import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Text, View, useThemeColor } from '@/components/Themed';
import type { CompetitionInfo } from '@/lib/types';

type Props = {
  competition: CompetitionInfo | null;
  season: string | null;
  onSelect: (season: string) => void;
};

// Barra "Stagione: ..." che apre l'elenco delle stagioni disponibili per la competizione.
export function SeasonPicker({ competition, season, onSelect }: Props) {
  const [open, setOpen] = useState(false);
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  const tint = useThemeColor({}, 'tint');

  if (!competition) return null;

  const choose = (next: string) => {
    setOpen(false);
    onSelect(next);
  };

  return (
    <>
      <Pressable onPress={() => setOpen(true)} style={[styles.bar, { borderBottomColor: border }]}>
        <Text style={[styles.barLabel, { color: muted }]}>Stagione</Text>
        <Text style={styles.barValue}>{season} ▾</Text>
      </Pressable>

      <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setOpen(false)}>
        <SafeAreaView style={{ flex: 1 }} edges={['top', 'bottom']}>
          <View style={styles.modal}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{competition.name}: scegli la stagione</Text>
              <Pressable onPress={() => setOpen(false)} hitSlop={12}>
                <Text style={{ color: tint, fontSize: 16 }}>Chiudi</Text>
              </Pressable>
            </View>
            <ScrollView contentContainerStyle={styles.content}>
              <View style={styles.chips}>
                {(competition.seasons ?? [competition.currentSeason]).map((s) => (
                  <Chip key={s} label={s} active={s === season} onPress={() => choose(s)} />
                ))}
              </View>
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>
    </>
  );
}

function Chip({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  return (
    <Pressable onPress={onPress} style={[styles.chip, { backgroundColor: active ? tint : card }]}>
      <Text style={[styles.chipText, active && { color: background }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    paddingHorizontal: 16, paddingVertical: 10, borderBottomWidth: 1,
  },
  barLabel: { fontSize: 14 },
  barValue: { fontSize: 15, fontWeight: '600' },
  modal: { flex: 1 },
  modalHeader: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 12, padding: 16,
  },
  modalTitle: { flex: 1, fontSize: 20, fontWeight: '700' },
  content: { paddingHorizontal: 16, paddingBottom: 32 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 8, minWidth: 64, alignItems: 'center' },
  chipText: { fontSize: 15, fontVariant: ['tabular-nums'] },
});
