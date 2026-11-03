import { Stack, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet } from 'react-native';

import { Text, View, useThemeColor } from '@/components/Themed';
import { CompetitionColors } from '@/constants/Colors';
import { useEvents } from '@/lib/data';
import { competitionLabel, formatDateTime, sessionLabel } from '@/lib/format';
import type { ClassificationRow, SportEvent } from '@/lib/types';

export default function EventScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const event = useEvents().getEvent(id);
  const muted = useThemeColor({}, 'muted');

  if (!event) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: 'Evento' }} />
        <Text style={{ color: muted }}>Evento non trovato</Text>
      </View>
    );
  }

  const place = [event.venue, event.city, event.country].filter(Boolean).join(', ');

  return (
    <ScrollView>
      <Stack.Screen options={{ title: sessionLabel(event.session) }} />
      <View style={styles.header}>
        <Text style={[styles.badge, { backgroundColor: CompetitionColors[event.competition] ?? muted }]}>
          {competitionLabel(event.competition)}{event.round ? ` · Round ${event.round}` : ''}
        </Text>
        <Text style={styles.title}>{event.title}</Text>
        <Text style={[styles.info, { color: muted }]}>{sessionLabel(event.session)} · {formatDateTime(event.startsAt)}</Text>
        {place ? <Text style={[styles.info, { color: muted }]}>{place}</Text> : null}
        {event.homeScore != null && event.awayScore != null && (
          <Text style={styles.score}>{event.homeScore} - {event.awayScore}</Text>
        )}
      </View>
      {event.result?.length ? <Classification event={event} /> : null}
    </ScrollView>
  );
}

function Classification({ event }: { event: SportEvent }) {
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  const isQualifying = event.session === 'qualifying' || event.session === 'sprint_qualifying';

  // In qualifica si mostra il miglior tempo raggiunto (Q3, altrimenti Q2, altrimenti Q1).
  const detail = (r: ClassificationRow) => (isQualifying ? r.q3 ?? r.q2 ?? r.q1 : r.time) ?? '';

  return (
    <View>
      <Text style={[styles.sectionTitle, { color: muted }]}>Classifica</Text>
      {event.result!.map((r) => (
        // La posizione da sola non basta come chiave: la fonte può avere posizioni duplicate.
        <View key={`${r.pos}-${r.number ?? r.driver}`} style={[styles.resultRow, { borderBottomColor: border }]}>
          <Text style={styles.pos}>{r.pos}</Text>
          <View style={styles.driver}>
            <Text style={styles.driverName} numberOfLines={1}>{r.driver}</Text>
            <Text style={[styles.team, { color: muted }]} numberOfLines={1}>{r.team}</Text>
          </View>
          <View style={styles.right}>
            <Text style={styles.time}>{detail(r)}</Text>
            {!isQualifying && r.points ? <Text style={[styles.team, { color: muted }]}>{r.points} pt</Text> : null}
          </View>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  header: { padding: 16, gap: 6 },
  badge: {
    alignSelf: 'flex-start', color: '#fff', fontSize: 12, fontWeight: '700',
    paddingHorizontal: 8, paddingVertical: 3, borderRadius: 4, overflow: 'hidden',
  },
  title: { fontSize: 24, fontWeight: '700' },
  info: { fontSize: 15 },
  score: { fontSize: 32, fontWeight: '700', marginTop: 8 },
  sectionTitle: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase', paddingHorizontal: 16, paddingTop: 8, paddingBottom: 4 },
  resultRow: {
    flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth, gap: 12,
  },
  pos: { width: 24, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  driver: { flex: 1, backgroundColor: 'transparent' },
  driverName: { fontSize: 16, fontWeight: '600' },
  team: { fontSize: 13 },
  right: { alignItems: 'flex-end', backgroundColor: 'transparent' },
  time: { fontSize: 14, fontVariant: ['tabular-nums'] },
});
