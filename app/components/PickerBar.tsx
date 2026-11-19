import { useMemo, useState, type ReactNode } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, TextInput } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import Svg, { Path } from 'react-native-svg';

import { Text, View, useThemeColor } from '@/components/Themed';

type Option = {
  key: string;
  label: string;
  highlighted?: boolean; // es. la stagione corrente
  group?: string; // titolo della sezione (es. "Anni 2020"); le voci consecutive con lo stesso gruppo stanno sotto un titolo solo
  icon?: ReactNode; // immagine prima dell'etichetta (es. lo stemma della squadra)
  pinned?: boolean; // con la ricerca: sempre in cima a campo vuoto (es. "Tutte le squadre"), mai tra i risultati
  searchText?: string; // con la ricerca: altro testo in cui cercare oltre all'etichetta (es. il nome completo)
};

// Ricerca nella finestra, per elenchi troppo lunghi da mostrare interi: a campo vuoto si vedono solo le voci `pinned` e
// le `suggested`, scrivendo le voci che contengono il testo in qualunque punto.
type Search = {
  placeholder: string;
  suggested: string[]; // chiavi delle voci suggerite a campo vuoto, in quest'ordine
  suggestedTitle: string; // titolo della sezione dei suggerimenti
  hint: string; // spiega che si può cercare tra tutte le voci, non solo tra quelle suggerite
  empty: string; // nessuna voce contiene il testo
};

type Props = {
  label: string; // es. "Stagione"
  value: string; // voce scelta, mostrata nella barra
  title: string; // titolo della finestra di scelta
  options: Option[]; // senza gruppi le voci sono un elenco unico
  selectedKey: string | null;
  onSelect: (key: string) => void;
  search?: Search;
  rows?: boolean; // una voce per riga invece delle voci affiancate (es. le squadre, con nomi di lunghezza diversa)
};

// Barra "etichetta: valore" che apre una finestra con le voci tra cui scegliere.
export function PickerBar({ label, value, title, options, selectedKey, onSelect, search, rows = false }: Props) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  const tint = useThemeColor({}, 'tint');

  const close = () => {
    setOpen(false);
    setQuery('');
  };
  const choose = (key: string) => {
    close();
    onSelect(key);
  };

  const searching = search !== undefined && query.trim() !== '';
  const groups = useMemo(() => {
    if (!search) return groupOptions(options);
    const text = normalize(query.trim());
    if (text === '') {
      const suggested = search.suggested
        .map((key) => options.find((o) => o.key === key && !o.pinned))
        .filter((o) => o !== undefined)
        .map((o) => ({ ...o, group: search.suggestedTitle }));
      return groupOptions([...options.filter((o) => o.pinned).map((o) => ({ ...o, group: undefined })), ...suggested]);
    }
    // Prima le voci che cominciano con il testo, poi quelle che lo contengono altrove, ognuna nell'ordine ricevuto.
    const matches = options
      .filter((o) => !o.pinned)
      .map((o) => ({ o, at: matchPosition(o, text) }))
      .filter((m) => m.at >= 0);
    const ordered = [...matches.filter((m) => m.at === 0), ...matches.filter((m) => m.at > 0)];
    return groupOptions(ordered.map((m) => ({ ...m.o, group: undefined })));
  }, [options, search, query]);
  const hasSuggestions = groups.some((g) => g.title === search?.suggestedTitle);

  return (
    <>
      <Pressable onPress={() => setOpen(true)} style={[styles.bar, { borderBottomColor: border }]}>
        <Text style={[styles.barLabel, { color: muted }]}>{label}</Text>
        <Text style={styles.barValue}>{value} ▾</Text>
      </Pressable>

      <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={close}>
        <SafeAreaView style={{ flex: 1 }} edges={['top', 'bottom']}>
          <View style={styles.modal}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{title}</Text>
              <Pressable onPress={close} hitSlop={12}>
                <Text style={{ color: tint, fontSize: 16 }}>Chiudi</Text>
              </Pressable>
            </View>
            {search ? <SearchField value={query} onChange={setQuery} placeholder={search.placeholder} /> : null}
            {/* "handled": con la tastiera aperta il primo tocco sceglie la voce, invece di chiudere solo la tastiera. */}
            <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
              {groups.map((g, i) => (
                <View key={g.title ?? i} style={styles.group}>
                  {g.title ? <Text style={[styles.groupTitle, { color: muted }]}>{g.title}</Text> : null}
                  <View style={rows ? styles.rows : styles.chips}>
                    {g.options.map((o) => (
                      <Chip
                        key={o.key}
                        label={o.label}
                        active={o.key === selectedKey}
                        highlighted={o.highlighted ?? false}
                        icon={o.icon}
                        row={rows}
                        onPress={() => choose(o.key)}
                      />
                    ))}
                  </View>
                </View>
              ))}
              {search && !searching ? <SearchHint text={search.hint} large={!hasSuggestions} /> : null}
              {searching && groups.length === 0 ? (
                <Text style={[styles.empty, { color: muted }]}>{search.empty}</Text>
              ) : null}
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>
    </>
  );
}

// Voci consecutive con lo stesso gruppo insieme, nell'ordine in cui arrivano.
function groupOptions(options: Option[]) {
  const groups: { title?: string; options: Option[] }[] = [];
  for (const o of options) {
    const last = groups[groups.length - 1];
    if (last && last.title === o.group) last.options.push(o);
    else groups.push({ title: o.group, options: [o] });
  }
  return groups;
}

// Minuscolo e senza accenti, così "atle" trova "Atlético". Alcune lettere non sono una lettera più un accento e restano
// dopo NFD: si cambiano a mano, così "bialystok" trova "Białystok" e "hamrun" trova "Ħamrun".
const LETTERS: Record<string, string> = { ł: 'l', đ: 'd', ð: 'd', ħ: 'h', ø: 'o', ı: 'i', æ: 'ae', œ: 'oe', ß: 'ss', þ: 'th' };
const normalize = (s: string) =>
  s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[łđðħøıæœßþ]/g, (c) => LETTERS[c]);

// Dove compare il testo nell'etichetta (0 = all'inizio), oppure nell'altro testo, che conta come "altrove"; -1 se manca.
function matchPosition(option: Option, text: string) {
  const at = normalize(option.label).indexOf(text);
  if (at >= 0) return at;
  return option.searchText && normalize(option.searchText).includes(text) ? 1 : -1;
}

// search di Material Symbols (Outlined, peso 400; Google, licenza Apache 2.0), come le icone di SportIcon.
const SEARCH_PATH = 'M784-120 532-372q-30 24-69 38t-83 14q-109 0-184.5-75.5T120-580q0-109 75.5-184.5T380-840q109 0 184.5 75.5T640-580q0 44-14 83t-38 69l252 252-56 56ZM380-400q75 0 127.5-52.5T560-580q0-75-52.5-127.5T380-760q-75 0-127.5 52.5T320-580q0 75 52.5 127.5T380-400Z';

function SearchIcon({ size, color }: { size: number; color: string }) {
  return (
    <Svg width={size} height={size} viewBox="120 -840 720 720">
      <Path d={SEARCH_PATH} fill={color} />
    </Svg>
  );
}

function SearchField({ value, onChange, placeholder }: {
  value: string;
  onChange: (text: string) => void;
  placeholder: string;
}) {
  const card = useThemeColor({}, 'card');
  const text = useThemeColor({}, 'text');
  const muted = useThemeColor({}, 'muted');
  return (
    <View style={[styles.field, { backgroundColor: card }]}>
      <SearchIcon size={18} color={muted} />
      <TextInput
        value={value}
        onChangeText={onChange}
        placeholder={placeholder}
        placeholderTextColor={muted}
        style={[styles.input, { color: text }]}
        autoFocus
        autoCorrect={false}
        autoCapitalize="none"
        returnKeyType="search"
      />
      {value !== '' ? (
        <Pressable onPress={() => onChange('')} hitSlop={12} accessibilityLabel="Cancella">
          <Text style={[styles.clear, { color: muted }]}>✕</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// Spiega che le voci a campo vuoto sono solo suggerimenti. Senza suggerimenti (all'inizio) è più grande, al centro,
// perché è l'unica cosa nella finestra sotto il campo.
function SearchHint({ text, large }: { text: string; large: boolean }) {
  const muted = useThemeColor({}, 'muted');
  return (
    <View style={large ? styles.hintLarge : styles.hint}>
      <SearchIcon size={large ? 48 : 18} color={muted} />
      <Text style={[large ? styles.hintLargeText : styles.hintText, { color: muted }]}>{text}</Text>
    </View>
  );
}

// Voce scelta: piena del colore principale. Voce evidenziata: bordo dello stesso colore e testo in grassetto, così si
// riconosce anche quando è scelta un'altra voce. Le altre hanno un bordo trasparente, per avere tutte la stessa misura.
// Con `row` la voce occupa tutta la larghezza, allineata a sinistra.
function Chip({ label, active, highlighted, icon, row, onPress }: {
  label: string;
  active: boolean;
  highlighted: boolean;
  icon?: ReactNode;
  row: boolean;
  onPress: () => void;
}) {
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  return (
    <Pressable
      onPress={onPress}
      style={[styles.chip, row && styles.chipRow, { backgroundColor: active ? tint : card, borderColor: highlighted ? tint : 'transparent' }]}
    >
      {icon}
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
  field: {
    flexDirection: 'row', alignItems: 'center', gap: 8,
    marginHorizontal: 16, marginBottom: 16, paddingHorizontal: 12, borderRadius: 10,
  },
  input: { flex: 1, fontSize: 16, paddingVertical: 10 },
  clear: { fontSize: 16 },
  content: { paddingHorizontal: 16, paddingBottom: 32 },
  group: { marginBottom: 16 },
  groupTitle: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase', marginBottom: 8 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  rows: { gap: 6 },
  chipRow: { justifyContent: 'flex-start', gap: 10, paddingVertical: 10 },
  chip: {
    flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: 6,
    paddingHorizontal: 12, paddingVertical: 8, borderRadius: 8, borderWidth: 2, minWidth: 64,
  },
  chipText: { fontSize: 15, fontVariant: ['tabular-nums'] },
  chipTextHighlighted: { fontWeight: '700' },
  hint: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  hintText: { flex: 1, fontSize: 14 },
  hintLarge: { alignItems: 'center', gap: 12, paddingTop: 32, paddingHorizontal: 24 },
  hintLargeText: { fontSize: 15, textAlign: 'center' },
  empty: { fontSize: 15, textAlign: 'center', paddingTop: 32 },
});
