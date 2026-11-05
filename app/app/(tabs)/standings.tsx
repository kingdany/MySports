import { useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet } from 'react-native';

import { SeasonPicker, type SeasonSelection } from '@/components/SeasonPicker';
import { Text, View, useThemeColor } from '@/components/Themed';
import { useEvents } from '@/lib/data';
import type { StandingRow } from '@/lib/types';
import { useSelectedSeason } from '@/lib/useSelectedSeason';

const formatPoints = (points: number) => points.toLocaleString('it-IT');

const signed = (value: number | undefined) => (value != null && value > 0 ? `+${value}` : value ?? '');

// Colonne del calcio: partite giocate, vittorie, pareggi, sconfitte, differenza reti.
const FOOTBALL_COLUMNS: { label: string; value: (r: StandingRow) => number | string | undefined }[] = [
  { label: 'G', value: (r) => r.played },
  { label: 'V', value: (r) => r.wins },
  { label: 'N', value: (r) => r.draws },
  { label: 'P', value: (r) => r.losses },
  { label: 'DR', value: (r) => signed(r.goalDifference) },
];

export default function StandingsScreen() {
  const { competitions, error: currentError } = useEvents();
  const [selected, setSelected] = useState<SeasonSelection>(null);
  const [tableId, setTableId] = useState('drivers');
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');

  // Le classifiche non si mescolano: all'apertura si vede la stagione in corso della prima competizione.
  const first = competitions[0];
  const effective = selected ?? (first ? { competition: first.id, season: first.currentSeason } : null);
  const { file, error, refreshing, refresh } = useSelectedSeason(effective);

  const tables = file?.standings ?? [];
  const table = tables.find((t) => t.id === tableId) ?? tables[0];
  const shownError = error ?? (first ? null : currentError);
  const isFootball = table?.rows[0]?.played != null;

  return (
    <View style={{ flex: 1 }}>
      <SeasonPicker competitions={competitions} selected={effective} onSelect={setSelected} showAll={false} />

      {tables.length > 1 && (
        <View style={styles.segments}>
          {tables.map((t) => {
            const active = t.id === table?.id;
            return (
              <Pressable
                key={t.id}
                onPress={() => setTableId(t.id)}
                style={[styles.segment, { backgroundColor: active ? tint : card }]}>
                <Text style={[styles.segmentText, active && { color: background }]}>{t.title}</Text>
              </Pressable>
            );
          })}
        </View>
      )}

      {!file && !shownError ? (
        <View style={styles.center}>
          <ActivityIndicator />
        </View>
      ) : (
        <FlatList
          data={table?.rows ?? []}
          keyExtractor={(r, i) => `${r.posText}-${r.name}-${i}`}
          renderItem={({ item }) => <Row row={item} showTeam={table?.id === 'drivers'} isFootball={isFootball} />}
          ListHeaderComponent={
            table ? (
              <View style={[styles.headerRow, isFootball && styles.compact]}>
                <Text style={[styles.headerText, { color: muted, flex: 1 }]}>{table.title}</Text>
                {isFootball ? (
                  <>
                    {FOOTBALL_COLUMNS.map((c) => (
                      <Text key={c.label} style={[styles.headerText, styles.stat, { color: muted }]}>{c.label}</Text>
                    ))}
                    <Text style={[styles.headerText, styles.footballPoints, { color: muted }]}>Pt</Text>
                  </>
                ) : (
                  <>
                    <Text style={[styles.headerText, styles.wins, { color: muted }]}>Vitt.</Text>
                    <Text style={[styles.headerText, styles.points, { color: muted }]}>Punti</Text>
                  </>
                )}
              </View>
            ) : undefined
          }
          ListEmptyComponent={
            <Text style={[styles.message, { color: muted }]}>
              {shownError ? `${shownError}. Trascina in basso per riprovare.` : 'Classifica non disponibile per questa stagione'}
            </Text>
          }
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} />}
        />
      )}
    </View>
  );
}

function Row({ row, showTeam, isFootball }: { row: StandingRow; showTeam: boolean; isFootball: boolean }) {
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  return (
    <View style={[styles.row, isFootball && styles.compact, { borderBottomColor: border }]}>
      <Text style={[styles.pos, row.pos == null && { color: muted }]}>{row.posText}</Text>
      <View style={styles.name}>
        <Text style={styles.nameText} numberOfLines={1}>{row.name}</Text>
        {showTeam && row.team ? (
          <Text style={[styles.team, { color: muted }]} numberOfLines={1}>{row.team}</Text>
        ) : null}
      </View>
      {isFootball ? (
        <>
          {FOOTBALL_COLUMNS.map((c) => (
            <Text key={c.label} style={[styles.stat, { color: muted }]}>{c.value(row)}</Text>
          ))}
          <Text style={styles.footballPoints}>{formatPoints(row.points)}</Text>
        </>
      ) : (
        <>
          <Text style={[styles.wins, { color: muted }]}>{row.wins ?? ''}</Text>
          <Text style={styles.points}>{formatPoints(row.points)}</Text>
        </>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  segments: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingVertical: 10 },
  segment: { flex: 1, paddingVertical: 8, borderRadius: 8, alignItems: 'center' },
  segmentText: { fontSize: 15, fontWeight: '600' },
  headerRow: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 6, gap: 12 },
  headerText: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase' },
  row: {
    flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth, gap: 12,
  },
  pos: { width: 28, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  name: { flex: 1, backgroundColor: 'transparent' },
  nameText: { fontSize: 16, fontWeight: '600' },
  team: { fontSize: 13 },
  wins: { width: 40, fontSize: 14, textAlign: 'right', fontVariant: ['tabular-nums'] },
  points: { width: 56, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  compact: { gap: 6 }, // la classifica del calcio ha più colonne
  stat: { width: 24, fontSize: 13, textAlign: 'right', fontVariant: ['tabular-nums'] },
  footballPoints: { width: 30, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  message: { textAlign: 'center', padding: 24, fontSize: 15 },
});
