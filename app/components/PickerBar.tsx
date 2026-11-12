import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Text, View, useThemeColor } from '@/components/Themed';

type Props = {
  label: string; // es. "Stagione"
  value: string; // voce scelta, mostrata nella barra
  title: string; // titolo della finestra di scelta
  // highlighted: es. la stagione corrente. group: titolo della sezione (es. "Anni 2020"); le voci consecutive con lo
  // stesso gruppo stanno sotto un titolo solo. Senza gruppi le voci sono un elenco unico.
  options: { key: string; label: string; highlighted?: boolean; group?: string }[];
  selectedKey: string | null;
  onSelect: (key: string) => void;
};

// Barra "etichetta: valore" che apre una finestra con le voci tra cui scegliere.
export function PickerBar({ label, value, title, options, selectedKey, onSelect }: Props) {
  const [open, setOpen] = useState(false);
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  const tint = useThemeColor({}, 'tint');

  const choose = (key: string) => {
    setOpen(false);
    onSelect(key);
  };

  return (
    <>
      <Pressable onPress={() => setOpen(true)} style={[styles.bar, { borderBottomColor: border }]}>
        <Text style={[styles.barLabel, { color: muted }]}>{label}</Text>
        <Text style={styles.barValue}>{value} ▾</Text>
      </Pressable>

      <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setOpen(false)}>
        <SafeAreaView style={{ flex: 1 }} edges={['top', 'bottom']}>
          <View style={styles.modal}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{title}</Text>
              <Pressable onPress={() => setOpen(false)} hitSlop={12}>
                <Text style={{ color: tint, fontSize: 16 }}>Chiudi</Text>
              </Pressable>
            </View>
            <ScrollView contentContainerStyle={styles.content}>
              {groupOptions(options).map((g, i) => (
                <View key={g.title ?? i} style={styles.group}>
                  {g.title ? <Text style={[styles.groupTitle, { color: muted }]}>{g.title}</Text> : null}
                  <View style={styles.chips}>
                    {g.options.map((o) => (
                      <Chip
                        key={o.key}
                        label={o.label}
                        active={o.key === selectedKey}
                        highlighted={o.highlighted ?? false}
                        onPress={() => choose(o.key)}
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

// Voci consecutive con lo stesso gruppo insieme, nell'ordine in cui arrivano.
function groupOptions(options: Props['options']) {
  const groups: { title?: string; options: Props['options'] }[] = [];
  for (const o of options) {
    const last = groups[groups.length - 1];
    if (last && last.title === o.group) last.options.push(o);
    else groups.push({ title: o.group, options: [o] });
  }
  return groups;
}

// Voce scelta: piena del colore principale. Voce evidenziata: bordo dello stesso colore e testo in grassetto, così si
// riconosce anche quando è scelta un'altra voce. Le altre hanno un bordo trasparente, per avere tutte la stessa misura.
function Chip({ label, active, highlighted, onPress }: { label: string; active: boolean; highlighted: boolean; onPress: () => void }) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  return (
    <Pressable
      onPress={onPress}
      style={[styles.chip, { backgroundColor: active ? tint : card, borderColor: highlighted ? tint : 'transparent' }]}
    >
      <Text style={[styles.chipText, highlighted && styles.chipTextHighlighted, active && { color: background }]}>{label}</Text>
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
  group: { marginBottom: 16 },
  groupTitle: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase', marginBottom: 8 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 8, borderWidth: 2, minWidth: 64, alignItems: 'center' },
  chipText: { fontSize: 15, fontVariant: ['tabular-nums'] },
  chipTextHighlighted: { fontWeight: '700' },
});
