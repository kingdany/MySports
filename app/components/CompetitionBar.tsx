import { useMemo } from 'react';
import { Pressable, ScrollView, StyleSheet } from 'react-native';

import { CompetitionLogo, hasLogo, hasWordmark } from '@/components/CompetitionLogo';
import { SportIcon, hasSportIcon } from '@/components/SportIcon';
import { Text, useThemeColor } from '@/components/Themed';
import { CompetitionColors } from '@/constants/Colors';
import { usePreferences } from '@/lib/preferences';
import { groupBySport, visibleCompetitions } from '@/lib/sports';
import type { CompetitionInfo } from '@/lib/types';

type Props = {
  competitions: CompetitionInfo[]; // tutte, anche le nascoste, che la barra toglie da sola
  sport: string | null; // null = "Tutti"
  competition: string | null; // null = "Tutte" le competizioni dello sport (o "Tutti")
  onSelectSport: (sport: string | null) => void;
  onSelectCompetition: (id: string | null) => void;
  showAll?: boolean; // offre "Tutti" sopra e "Tutte" sotto (Prossimi)
};

// Due barre scorrevoli: sopra gli sport (Calcio, Motori), sotto le competizioni dello sport scelto, solo se ne ha più di
// una. Così una riga sola non si allunga con ogni competizione aggiunta. Le competizioni nascoste non compaiono, e
// nemmeno uno sport che le ha nascoste tutte; uno sport con più competizioni ma una sola visibile tiene la riga di sotto,
// con quella sola e senza "Tutte".
export function CompetitionBar({ competitions, sport, competition, onSelectSport, onSelectCompetition, showAll = false }: Props) {
  const tint = useThemeColor({}, 'tint');
  const { hiddenCompetitions } = usePreferences();
  const sports = useMemo(
    () => groupBySport(visibleCompetitions(competitions, hiddenCompetitions)),
    [competitions, hiddenCompetitions],
  );
  const current = sports.find((s) => s.id === sport);
  const subRow = !!current && competitions.filter((c) => c.sport === current.id).length > 1;

  if (sports.length === 0) return null;

  return (
    <>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.bar} contentContainerStyle={styles.content}>
        {showAll && <Chip size={LARGE} label="Tutti" active={sport === null} activeColor={tint} onPress={() => onSelectSport(null)} />}
        {sports.map((s) => (
          <Chip
            key={s.id}
            size={LARGE}
            sport={s.id}
            label={s.name}
            active={sport === s.id}
            activeColor={tint}
            onPress={() => onSelectSport(s.id)}
          />
        ))}
      </ScrollView>
      {current && subRow && (
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          style={styles.bar}
          contentContainerStyle={[styles.content, styles.subContent]}>
          {showAll && current.competitions.length > 1 && (
            <Chip size={SMALL} label="Tutte" active={competition === null} activeColor={tint} onPress={() => onSelectCompetition(null)} />
          )}
          {current.competitions.map((c) => (
            <Chip
              key={c.id}
              size={SMALL}
              logo={c.id}
              label={c.name} // nome completo ("Champions League"): quello breve sembrava un'abbreviazione
              active={competition === c.id}
              activeColor={CompetitionColors[c.id] ?? tint}
              onPress={() => onSelectCompetition(c.id)}
            />
          ))}
        </ScrollView>
      )}
    </>
  );
}

// Pulsanti tutti larghi uguali in ogni barra, così nessuna competizione sembra più importante. Quello che non ci sta si
// rimpicciolisce: i loghi che sono già il nome (F1, MotoGP) stanno nella stessa larghezza, e la MotoGP, più lunga, viene
// più bassa; i nomi lunghi ("Champions League" accanto al pallone) vanno su due righe. I pulsanti delle competizioni hanno
// le misure di quando la barra era una sola: più bassi (32), "Champions League" su due righe non si leggeva bene.
type Size = {
  width: number; height: number; icon: number; logoHeight: number; wordmarkWidth: number;
  fontSize: number; twoLinesFontSize: number; twoLinesLineHeight: number;
};
const LARGE: Size = {
  width: 112, height: 36, icon: 20, logoHeight: 24, wordmarkWidth: 72, fontSize: 15, twoLinesFontSize: 13, twoLinesLineHeight: 15,
};
const SMALL: Size = {
  width: 132, height: 36, icon: 20, logoHeight: 22, wordmarkWidth: 72, fontSize: 14, twoLinesFontSize: 13, twoLinesLineHeight: 15,
};
const TWO_LINES_FROM = 10; // caratteri: "Serie A" sta su una riga, "Champions League" va su due

// Icona dello sport, oppure logo della competizione, e nome; per F1 e MotoGP il logo è già il nome e sta da solo. Attivo,
// sullo sfondo del colore della competizione (o del tema), il disegno diventa del colore del testo.
function Chip({ size, sport, logo: logoId, label, active, activeColor, onPress }: {
  size: Size; sport?: string; logo?: string; label: string; active: boolean; activeColor: string; onPress: () => void;
}) {
  const card = useThemeColor({}, 'card');
  const text = useThemeColor({}, 'text');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');
  // Il colore del tema, in modalità scura, è chiaro: sopra il testo prende il colore dello sfondo.
  const activeText = activeColor === tint ? background : '#fff';
  const logo = logoId && hasLogo(logoId) ? logoId : null;
  const wordmark = logo !== null && hasWordmark(logo);
  const twoLines = label.length > TWO_LINES_FROM;
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: active }}
      style={[styles.chip, { width: size.width, height: size.height, backgroundColor: active ? activeColor : card }]}>
      {sport && hasSportIcon(sport) && <SportIcon sport={sport} size={size.icon} color={active ? activeText : text} />}
      {logo !== null && (
        <CompetitionLogo
          id={logo}
          height={size.logoHeight}
          maxWidth={size.wordmarkWidth} // vale solo per F1 e MotoGP, gli altri sono più stretti
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
          style={[
            styles.chipText,
            { fontSize: size.fontSize },
            twoLines && { fontSize: size.twoLinesFontSize, lineHeight: size.twoLinesLineHeight, textAlign: 'center' },
            active && { color: activeText },
          ]}>
          {label}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: { flexGrow: 0, flexShrink: 0 }, // altrimenti la lista sottostante, più lunga dello schermo, la schiaccia
  content: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingVertical: 10 },
  subContent: { paddingTop: 0 }, // attaccata alla barra degli sport, di cui fa parte
  // Misura fissa: con o senza logo, i pulsanti di una barra sono tutti uguali.
  chip: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, paddingHorizontal: 10, borderRadius: 8 },
  chipText: { flexShrink: 1, fontWeight: '600' },
});
