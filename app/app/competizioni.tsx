import { ScrollView, StyleSheet, Switch } from 'react-native';

import { CompetitionLogo } from '@/components/CompetitionLogo';
import { SportIcon } from '@/components/SportIcon';
import { Text, View, useThemeColor } from '@/components/Themed';
import { usePreferences } from '@/lib/preferences';
import { groupBySport, resultsCompetition, upcomingChoice, useCompetitions, visibleCompetitions } from '@/lib/sports';

// Le competizioni da mostrare in Prossimi, Risultati e Classifiche. Si salvano quelle nascoste, così una competizione
// nuova compare da sola; ne resta sempre visibile almeno una, quindi l'ultimo interruttore acceso è bloccato.
export default function CompetitionsScreen() {
  const { all } = useCompetitions();
  const {
    hiddenCompetitions: hidden, setHiddenCompetitions, upcomingCompetition, setUpcomingCompetition,
    competition, setCompetition, competitionBySport,
  } = usePreferences();
  const tint = useThemeColor({}, 'tint');
  const card = useThemeColor({}, 'card');
  const border = useThemeColor({}, 'border');
  const muted = useThemeColor({}, 'muted');

  const isVisible = (id: string) => !hidden.includes(id);
  const visibleCount = all.filter((c) => isVisible(c.id)).length;

  // Nascondendo la competizione scelta in una tab, la tab passa alla più vicina rimasta (vedi upcomingChoice e
  // resultsCompetition) e la salva: riattivandola dopo, la tab non ci torna da sola.
  const apply = (next: string[]) => {
    setHiddenCompetitions(next);
    const visible = visibleCompetitions(all, next);
    const upcoming = upcomingChoice(upcomingCompetition, visible, all).choice;
    if (upcoming !== upcomingCompetition) setUpcomingCompetition(upcoming);
    if (competition !== null) {
      const results = resultsCompetition(competition, visible, all, competitionBySport);
      if (results && results.id !== competition) setCompetition(results.id);
    }
  };
  const show = (ids: string[]) => apply(hidden.filter((id) => !ids.includes(id)));
  const hide = (ids: string[]) => apply([...hidden, ...ids.filter((id) => !hidden.includes(id))]);

  const switchColors = { trackColor: { true: tint, false: border } };

  // Su iOS l'header è trasparente: "automatic" fa aggiungere a iOS lo spazio per non finirci sotto. Android lo ignora.
  return (
    <ScrollView contentInsetAdjustmentBehavior="automatic" contentContainerStyle={styles.content}>
      {groupBySport(all).map((sport) => {
        const ids = sport.competitions.map((c) => c.id);
        const sportOn = ids.some(isVisible);
        // Spegnere lo sport nasconderebbe tutto se le sole competizioni visibili sono sue.
        const sportLocked = sportOn && visibleCount === ids.filter(isVisible).length;
        return (
          <View key={sport.id} style={styles.section}>
            <View style={styles.sportRow}>
              <SportIcon sport={sport.id} size={20} color={muted} />
              <Text style={styles.sportName}>{sport.name}</Text>
              {ids.length > 1 && (
                <Switch
                  {...switchColors}
                  value={sportOn}
                  disabled={sportLocked}
                  onValueChange={(on) => (on ? show(ids) : hide(ids))}
                  accessibilityLabel={sport.name}
                />
              )}
            </View>
            <View style={[styles.card, { backgroundColor: card }]}>
              {sport.competitions.map((c, i) => {
                const on = isVisible(c.id);
                return (
                  <View
                    key={c.id}
                    style={[styles.row, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: border }]}>
                    <View style={styles.logo}>
                      <CompetitionLogo id={c.id} height={22} maxWidth={56} />
                    </View>
                    <Text style={styles.name} numberOfLines={1}>{c.name}</Text>
                    <Switch
                      {...switchColors}
                      value={on}
                      disabled={on && visibleCount === 1}
                      onValueChange={(next) => (next ? show([c.id]) : hide([c.id]))}
                      accessibilityLabel={c.name}
                    />
                  </View>
                );
              })}
            </View>
          </View>
        );
      })}
      <Text style={[styles.note, { color: muted }]}>
        Le competizioni nascoste non compaiono in Prossimi, Risultati e Classifiche. Ne resta sempre visibile almeno una.
      </Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, gap: 24 },
  section: { gap: 8, backgroundColor: 'transparent' },
  // Stesso margine delle righe nel riquadro (12), così interruttori, icone e loghi stanno in colonna. Provati sfasati,
  // per distinguere l'interruttore dello sport: l'utente li preferisce allineati.
  sportRow: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12, backgroundColor: 'transparent' },
  sportName: { flex: 1, fontSize: 17, fontWeight: '700' },
  card: { borderRadius: 10, overflow: 'hidden' },
  row: {
    flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 12, minHeight: 52,
    backgroundColor: 'transparent',
  },
  logo: { width: 56, alignItems: 'flex-start', backgroundColor: 'transparent' }, // a sinistra, sotto l'icona dello sport
  name: { flex: 1, fontSize: 15 },
  note: { fontSize: 13, paddingHorizontal: 12 },
});
