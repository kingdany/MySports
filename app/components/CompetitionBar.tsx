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
          label={c.name} // nome completo ("Champions League"): quello breve sembrava un'abbreviazione
          active={selected === c.id}
          activeColor={CompetitionColors[c.id] ?? tint}
          onPress={() => onSelect(c.id)}
        />
      ))}
    </ScrollView>
  );
}

// Pulsanti tutti larghi uguali, così nessuna competizione sembra più importante. Quello che non ci sta si rimpicciolisce:
// i loghi che sono già il nome (F1, MotoGP) stanno nella stessa larghezza, e la MotoGP, più lunga, viene più bassa; i
// nomi lunghi ("Champions League" accanto al pallone) vanno su due righe.
const CHIP_WIDTH = 132;
const WORDMARK_WIDTH = 72;
const LOGO_HEIGHT = 24;
const TWO_LINES_FROM = 10; // caratteri: "Serie A" sta su una riga, "Champions League" va su due

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
  const wordmark = logo !== null && hasWordmark(logo);
  const twoLines = label.length > TWO_LINES_FROM;
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: active }}
      style={[styles.chip, { backgroundColor: active ? activeColor : card }]}>
      {logo !== null && (
        <CompetitionLogo
          id={logo}
          height={LOGO_HEIGHT}
          maxWidth={WORDMARK_WIDTH} // vale solo per F1 e MotoGP, gli altri sono più stretti
          mono={active ? activeText : undefined}
        />
      )}
      {!wordmark && (
        // I nomi lunghi ("Champions League") vanno su due righe, un po' più piccoli. Se non ci stanno comunque (es. con il
        // testo di sistema ingrandito) si rimpiccioliscono.
        <Text
          numberOfLines={twoLines ? 2 : 1}
          adjustsFontSizeToFit
          minimumFontScale={0.6}
          style={[styles.chipText, twoLines && styles.chipTextTwoLines, active && { color: activeText }]}>
          {label}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: { flexGrow: 0, flexShrink: 0 }, // altrimenti la lista sottostante, più lunga dello schermo, la schiaccia
  content: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingVertical: 10 },
  // Misura fissa: con o senza logo, i pulsanti sono tutti uguali.
  chip: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6,
    width: CHIP_WIDTH, height: 36, paddingHorizontal: 10, borderRadius: 8,
  },
  chipText: { flexShrink: 1, fontSize: 15, fontWeight: '600' },
  chipTextTwoLines: { fontSize: 13, lineHeight: 15, textAlign: 'center' },
});
