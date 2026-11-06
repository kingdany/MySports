import { Link } from 'expo-router';
import { memo } from 'react';
import { Pressable, StyleSheet } from 'react-native';

import { Text, View, useThemeColor } from '@/components/Themed';
import { CompetitionColors, LiveColor } from '@/constants/Colors';
import { competitionLabel, formatTime, penaltiesLabel, roundLabel, sessionLabel } from '@/lib/format';
import type { SportEvent } from '@/lib/types';

// Con linked = false la riga è solo informativa: il tocco non apre il dettaglio.
export const EventRow = memo(function EventRow({ event, linked = true }: { event: SportEvent; linked?: boolean }) {
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');

  const row = (pressed: boolean) => (
    <View style={[styles.row, { borderBottomColor: border, opacity: pressed ? 0.6 : 1 }]}>
      <View style={styles.left}>
        {event.status === 'live' ? (
          <Text style={[styles.live, { backgroundColor: LiveColor }]}>LIVE</Text>
        ) : (
          <Text style={styles.time}>{event.dateOnly ? '–' : formatTime(event.startsAt)}</Text>
        )}
        <Text style={[styles.badge, { backgroundColor: CompetitionColors[event.competition] ?? muted }]}>
          {competitionLabel(event.competition)}
        </Text>
      </View>
      <View style={styles.body}>
        <Text style={styles.title} numberOfLines={1}>{event.title}</Text>
        <Text style={[styles.subtitle, { color: muted }]} numberOfLines={1}>
          {event.session === 'match' ? roundLabel(event) : sessionLabel(event.session)}{event.city ? ` · ${event.city}` : ''}
        </Text>
        <Outcome event={event} />
      </View>
      {event.videos.length > 0 && <Text style={[styles.videoMark, { color: muted }]}>▶</Text>}
    </View>
  );

  if (!linked) return row(false);

  return (
    <Link href={{ pathname: '/event/[id]', params: { id: event.id } }} asChild>
      <Pressable>{({ pressed }) => row(pressed)}</Pressable>
    </Link>
  );
});

// Risultato sintetico: punteggio per il calcio, podio per i motori.
function Outcome({ event }: { event: SportEvent }) {
  const muted = useThemeColor({}, 'muted');
  if (event.status !== 'finished') return null;

  if (event.homeScore != null && event.awayScore != null) {
    const penalties = penaltiesLabel(event);
    return <Text style={styles.outcome}>{event.homeScore} - {event.awayScore}{penalties ? ` (${penalties})` : ''}</Text>;
  }
  if (event.result?.length) {
    const podium = event.result.slice(0, 3).map((r) => `${r.pos}. ${r.code ?? r.driver}`).join('   ');
    return <Text style={styles.outcome}>{podium}</Text>;
  }
  return <Text style={[styles.subtitle, { color: muted }]}>Conclusa</Text>;
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    gap: 12,
  },
  left: { width: 64, alignItems: 'center', gap: 6, backgroundColor: 'transparent' },
  time: { fontSize: 16, fontWeight: '600', fontVariant: ['tabular-nums'] },
  live: {
    color: '#fff', fontSize: 12, fontWeight: '700', paddingHorizontal: 6, paddingVertical: 2,
    borderRadius: 4, overflow: 'hidden',
  },
  badge: {
    color: '#fff', fontSize: 11, fontWeight: '700', paddingHorizontal: 6, paddingVertical: 2,
    borderRadius: 4, overflow: 'hidden',
  },
  body: { flex: 1, gap: 2, backgroundColor: 'transparent' },
  title: { fontSize: 16, fontWeight: '600' },
  subtitle: { fontSize: 14 },
  outcome: { fontSize: 14, fontWeight: '600', marginTop: 2, fontVariant: ['tabular-nums'] },
  videoMark: { alignSelf: 'center', fontSize: 16 }, // l'evento ha un video
});
