import { useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet } from 'react-native';

import { CompetitionBar } from '@/components/CompetitionBar';
import { SeasonPicker } from '@/components/SeasonPicker';
import { Text, View, useThemeColor } from '@/components/Themed';
import { StandingZones } from '@/constants/Colors';
import type { StandingRow } from '@/lib/types';
import { useCompetitionSeason } from '@/lib/useSelectedSeason';

const formatPoints = (points: number) => points.toLocaleString('it-IT');

const signed = (value: number | undefined) => (value != null && value > 0 ? `+${value}` : value ?? '');

type Column = { label: string; width: number; value: (r: StandingRow) => number | string | undefined };

// Colonne del calcio: partite giocate, vittorie, pareggi, sconfitte, differenza reti.
const FOOTBALL_COLUMNS: Column[] = [
  { label: 'G', width: 20, value: (r) => r.played },
  { label: 'V', width: 20, value: (r) => r.wins },
  { label: 'N', width: 20, value: (r) => r.draws },
  { label: 'P', width: 20, value: (r) => r.losses },
  { label: 'DR', width: 34, value: (r) => signed(r.goalDifference) }, // più larga: ha anche il segno, es. "+16"
];

// Colonne specifiche del calcio: i marcatori hanno solo i gol, nella colonna principale.
// null per le classifiche F1, che mostrano le vittorie.
const columnsOf = (tableId: string | undefined) =>
  tableId === 'table' ? FOOTBALL_COLUMNS : tableId === 'scorers' ? [] : null;

export default function StandingsScreen() {
  const {
    competitions, competition, season, selectCompetition, selectSeason, file, error, refreshing, refresh,
  } = useCompetitionSeason();
  const [tableId, setTableId] = useState('drivers');
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');
  const background = useThemeColor({}, 'background');

  const tables = file?.standings ?? [];
  const table = tables.find((t) => t.id === tableId) ?? tables[0];
  const columns = columnsOf(table?.id);
  const zones = StandingZones.filter((z) => table?.rows.some((r) => r.zone === z.id));

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar competitions={competitions} selected={competition?.id ?? null} onSelect={selectCompetition} />
      <SeasonPicker competition={competition} season={season} onSelect={selectSeason} />

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

      {!file && !error ? (
        <View style={styles.center}>
          <ActivityIndicator />
        </View>
      ) : (
        <FlatList
          data={table?.rows ?? []}
          keyExtractor={(r, i) => `${r.posText}-${r.name}-${i}`}
          renderItem={({ item }) => (
            <Row row={item} showTeam={table?.id === 'drivers' || table?.id === 'scorers'} columns={columns} />
          )}
          ListHeaderComponent={
            table ? (
              <View style={[styles.headerRow, table.id === 'table' && styles.compact]}>
                <Text style={[styles.headerText, { color: muted, flex: 1 }]}>{table.title}</Text>
                {columns ? (
                  <>
                    {columns.map((c) => (
                      <Text key={c.label} numberOfLines={1} style={[styles.headerText, styles.stat, { color: muted, width: c.width }]}>{c.label}</Text>
                    ))}
                    <Text numberOfLines={1} style={[styles.footballPoints, styles.headerText, { color: muted }]}>
                      {table.id === 'scorers' ? 'Gol' : 'Pt'}
                    </Text>
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
              {error ? `${error}. Trascina in basso per riprovare.` : 'Classifica non disponibile per questa stagione'}
            </Text>
          }
          ListFooterComponent={
            zones.length > 0 ? (
              <View style={styles.legend}>
                {zones.map((z) => (
                  <View key={z.id} style={styles.legendItem}>
                    <View style={[styles.legendMark, { backgroundColor: z.color }]} />
                    <Text style={[styles.legendText, { color: muted }]}>{z.label}</Text>
                  </View>
                ))}
              </View>
            ) : undefined
          }
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} />}
        />
      )}
    </View>
  );
}

function Row({ row, showTeam, columns }: { row: StandingRow; showTeam: boolean; columns: Column[] | null }) {
  const muted = useThemeColor({}, 'muted');
  const border = useThemeColor({}, 'border');
  const zoneColor = StandingZones.find((z) => z.id === row.zone)?.color;
  return (
    <View style={[styles.row, columns !== null && columns.length > 0 && styles.compact, { borderBottomColor: border }]}>
      {zoneColor ? <View style={[styles.zone, { backgroundColor: zoneColor }]} /> : null}
      <Text style={[styles.pos, row.pos == null && { color: muted }]}>{row.posText}</Text>
      <View style={styles.name}>
        <Text style={styles.nameText} numberOfLines={1}>{row.name}</Text>
        {showTeam && row.team ? (
          <Text style={[styles.team, { color: muted }]} numberOfLines={1}>{row.team}</Text>
        ) : null}
      </View>
      {columns ? (
        <>
          {columns.map((c) => (
            <Text key={c.label} numberOfLines={1} style={[styles.stat, { color: muted, width: c.width }]}>{c.value(row)}</Text>
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
  compact: { gap: 6 }, // la classifica del calcio ha più colonne; i marcatori no, e tengono lo spazio normale
  stat: { fontSize: 13, textAlign: 'right', fontVariant: ['tabular-nums'] },
  footballPoints: { width: 30, fontSize: 16, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  zone: { position: 'absolute', left: 0, top: 0, bottom: 0, width: 4 }, // striscia sul bordo: non sposta il contenuto
  legend: { flexDirection: 'row', flexWrap: 'wrap', gap: 12, paddingHorizontal: 16, paddingVertical: 14 },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'transparent' },
  legendMark: { width: 4, height: 14 },
  legendText: { fontSize: 13 },
  message: { textAlign: 'center', padding: 24, fontSize: 15 },
});
