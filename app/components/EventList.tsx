import { ActivityIndicator, RefreshControl, SectionList, StyleSheet } from 'react-native';

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
};

// Lista di eventi divisa per giorno, con aggiornamento trascinando verso il basso.
export function EventList({ events, loading, error, refreshing, onRefresh, emptyMessage }: Props) {
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');

  if (loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <SectionList
      sections={groupByDay(events ?? [])}
      keyExtractor={(e) => e.id}
      renderItem={({ item }) => <EventRow event={item} />}
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
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  header: { fontSize: 14, fontWeight: '700', paddingHorizontal: 16, paddingVertical: 6 },
  message: { textAlign: 'center', padding: 24, fontSize: 15 },
});
