import { memo, useCallback, useMemo } from 'react';
import { ActivityIndicator, RefreshControl, SectionList, StyleSheet, type ListRenderItem } from 'react-native';

import { EventRow } from '@/components/EventRow';
import { Text, View, useThemeColor } from '@/components/Themed';
import { groupByDay } from '@/lib/format';
import type { SportEvent } from '@/lib/types';

type Props = {
  events: SportEvent[] | undefined;
  loading: boolean;
  error: string | null;
  refreshing: boolean;
  onRefresh: () => void;
  emptyMessage: string;
  linked?: boolean; // il tocco su una riga apre il dettaglio dell'evento
};

// Schermate tenute montate attorno a quella visibile (predefinito 21). Con le intestazioni fisse, quando
// la lista smonta le prime righe lo scorrimento salta su e giù in loop: dopo circa 10 schermate, quindi
// con la Serie A. Così le righe di una stagione restano montate e quel punto non si raggiunge.
const WINDOW_SIZE = 121;

// Lista di eventi divisa per giorno, con aggiornamento trascinando verso il basso.
// Le righe restano tutte montate: memo evita di ridisegnarle quando cambia qualcosa che non le riguarda
// (le schede restano montate anche quando non sono visibili).
export const EventList = memo(function EventList({
  events, loading, error, refreshing, onRefresh, emptyMessage, linked = true,
}: Props) {
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');
  const sections = useMemo(() => groupByDay(events ?? []), [events]);
  const renderItem = useCallback<ListRenderItem<SportEvent>>(
    ({ item }) => <EventRow event={item} linked={linked} />,
    [linked],
  );

  if (loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <SectionList
      sections={sections}
      keyExtractor={keyExtractor}
      renderItem={renderItem}
      windowSize={WINDOW_SIZE}
      renderSectionHeader={({ section }) => (
        <Text style={[styles.header, { backgroundColor: card }]}>{section.title}</Text>
      )}
      ListHeaderComponent={
        error ? <Text style={[styles.message, { color: muted }]}>{error}. Trascina in basso per riprovare.</Text> : undefined
      }
      ListEmptyComponent={
        error ? undefined : <Text style={[styles.message, { color: muted }]}>{emptyMessage}</Text>
      }
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
      stickySectionHeadersEnabled
    />
  );
});

const keyExtractor = (e: SportEvent) => e.id;

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  header: { fontSize: 14, fontWeight: '700', paddingHorizontal: 16, paddingVertical: 6 },
  message: { textAlign: 'center', padding: 24, fontSize: 15 },
});
