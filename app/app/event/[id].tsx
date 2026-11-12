import { Stack, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Text, View, useThemeColor } from '@/components/Themed';
import { FullNameToast, MatchLineups, MatchTimeline } from '@/components/MatchDetails';
import { VideoCard } from '@/components/VideoCard';
import { CompetitionColors } from '@/constants/Colors';
import { useEvents } from '@/lib/data';
import { competitionLabel, formatEventDate, penaltiesLabel, roundLabel, sessionLabel } from '@/lib/format';
import type { ClassificationRow, SportEvent } from '@/lib/types';

export default function EventScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const event = useEvents().getEvent(id);
  const muted = useThemeColor({}, 'muted');
  const insets = useSafeAreaInsets();

  if (!event) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: 'Evento' }} />
        <Text style={{ color: muted }}>Evento non trovato</Text>
      </View>
    );
  }

  const place = [event.venue, event.city, event.country].filter(Boolean).join(', ');
  const round = roundLabel(event);
  const penalties = penaltiesLabel(event);

  // Partita di calcio: tabellone centrato con le squadre ai lati del risultato, al posto del titolo.
  const match = event.homeTeam != null && event.awayTeam != null;
  const centered = match && { textAlign: 'center' as const };

  // La pagina è a tutto schermo: lo spazio in fondo evita che l'ultima riga finisca sotto i tasti di sistema.
  return (
    <FullNameToast>
      <ScrollView contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}>
        <Stack.Screen options={{ title: sessionLabel(event.session) }} />
        <View style={[styles.header, match && styles.headerCentered]}>
          <Text style={[styles.badge, match && styles.badgeCentered, { backgroundColor: CompetitionColors[event.competition] ?? muted }]}>
            {competitionLabel(event.competition)}{round ? ` · ${round}` : ''}
          </Text>
          {match ? <Scoreboard event={event} /> : <Text style={styles.title}>{event.title}</Text>}
          <Text style={[styles.info, centered, { color: muted }]}>{sessionLabel(event.session)} · {formatEventDate(event)}</Text>
          {place ? <Text style={[styles.info, centered, { color: muted }]}>{place}</Text> : null}
          {!match && event.homeScore != null && event.awayScore != null && (
            <Text style={styles.score}>{event.homeScore} - {event.awayScore}</Text>
          )}
          {penalties ? <Text style={[styles.info, centered, { color: muted }]}>{penalties}</Text> : null}
        </View>
        <MatchTimeline event={event} />
        {event.videos.length > 0 && (
          <View style={styles.videos}>
            {event.videos.map((v) => <VideoCard key={v.videoId} video={v} />)}
          </View>
        )}
        <MatchLineups event={event} />
        {event.result?.length ? <Classification event={event} /> : null}
      </ScrollView>
    </FullNameToast>
  );
}

// Casa a sinistra, risultato grande al centro, trasferta a destra; prima della partita "-" al posto del risultato.
function Scoreboard({ event }: { event: SportEvent }) {
  const muted = useThemeColor({}, 'muted');
  const played = event.homeScore != null && event.awayScore != null;
  const name = (team: SportEvent['homeTeam']) => team?.shortName ?? team?.name ?? '';

  return (
    <View style={styles.scoreboard}>
      <Text style={[styles.scoreTeam, styles.scoreTeamHome]} numberOfLines={2}>{name(event.homeTeam)}</Text>
      <Text style={[styles.scoreboardScore, !played && { color: muted }]}>
        {played ? `${event.homeScore} - ${event.awayScore}` : '-'}
      </Text>
      <Text style={styles.scoreTeam} numberOfLines={2}>{name(event.awayTeam)}</Text>
    </View>
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
  headerCentered: { alignItems: 'center' },
  badgeCentered: { alignSelf: 'center' },
  scoreboard: { flexDirection: 'row', alignItems: 'center', alignSelf: 'stretch', marginVertical: 8, backgroundColor: 'transparent' },
  scoreTeam: { flex: 1, fontSize: 18, fontWeight: '700' },
  scoreTeamHome: { textAlign: 'right' },
  scoreboardScore: { fontSize: 40, fontWeight: '800', paddingHorizontal: 16, fontVariant: ['tabular-nums'] },
  badge: {
    alignSelf: 'flex-start', color: '#fff', fontSize: 12, fontWeight: '700',
    paddingHorizontal: 8, paddingVertical: 3, borderRadius: 4, overflow: 'hidden',
  },
  title: { fontSize: 24, fontWeight: '700' },
  info: { fontSize: 15 },
  score: { fontSize: 32, fontWeight: '700', marginTop: 8 },
  videos: { paddingHorizontal: 16, paddingBottom: 16, gap: 16 },
  sectionTitle: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase', paddingHorizontal: 16, paddingTop: 8, paddingBottom: 4 },
  resultRow: {
    flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 10,
    borderBottomWidth: 1, gap: 12,
  },
  pos: { width: 24, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  driver: { flex: 1, backgroundColor: 'transparent' },
  driverName: { fontSize: 16, fontWeight: '600' },
  team: { fontSize: 13 },
  right: { alignItems: 'flex-end', backgroundColor: 'transparent' },
  time: { fontSize: 14, fontVariant: ['tabular-nums'] },
});
