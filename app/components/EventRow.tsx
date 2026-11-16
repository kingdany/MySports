import { Link } from 'expo-router';
import { memo } from 'react';
import { Pressable, StyleSheet } from 'react-native';

import { CompetitionLogo, hasLogo } from '@/components/CompetitionLogo';
import { TeamLogo } from '@/components/TeamLogo';
import { Text, View, useThemeColor } from '@/components/Themed';
import { CompetitionColors, LiveColor } from '@/constants/Colors';
import { competitionLabel, formatTime, penaltiesLabel, roundLabel, sessionLabel } from '@/lib/format';
import type { SportEvent } from '@/lib/types';
import { teamLabel } from '@/lib/useTeamFilter';

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
        {hasLogo(event.competition) ? (
          // Logo bianco sul colore della competizione; i loghi larghi (MotoGP) si rimpiccioliscono per stare nella colonna.
          <View style={[styles.logoBadge, { backgroundColor: CompetitionColors[event.competition] ?? muted }]}>
            <CompetitionLogo id={event.competition} height={13} maxWidth={48} mono="#fff" label={competitionLabel(event.competition)} />
          </View>
        ) : (
          <Text style={[styles.badge, { backgroundColor: CompetitionColors[event.competition] ?? muted }]}>
            {competitionLabel(event.competition)}
          </Text>
        )}
      </View>
      <View style={styles.body}>
        {event.homeTeam && event.awayTeam ? (
          // Partita: stemma di casa, titolo, stemma della trasferta, come nel tabellone del dettaglio.
          <View style={styles.match}>
            <TeamLogo uri={event.homeTeam.logoUrl} name={teamLabel(event.homeTeam)} size={18} />
            <Text style={[styles.title, styles.matchTitle]} numberOfLines={1}>{event.title}</Text>
            <TeamLogo uri={event.awayTeam.logoUrl} name={teamLabel(event.awayTeam)} size={18} />
          </View>
        ) : (
          <Text style={styles.title} numberOfLines={1}>{event.title}</Text>
        )}
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
  // Alta come il riquadro con la sigla (testo 11 più i margini), così le righe non cambiano altezza.
  logoBadge: { height: 18, paddingHorizontal: 6, borderRadius: 4, alignItems: 'center', justifyContent: 'center' },
  body: { flex: 1, gap: 2, backgroundColor: 'transparent' },
  title: { fontSize: 16, fontWeight: '600' },
  match: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'transparent' },
  matchTitle: { flexShrink: 1 },
  subtitle: { fontSize: 14 },
  outcome: { fontSize: 14, fontWeight: '600', marginTop: 2, fontVariant: ['tabular-nums'] },
  videoMark: { alignSelf: 'center', fontSize: 16 }, // l'evento ha un video
});
