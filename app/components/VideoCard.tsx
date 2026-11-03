import { Image, Linking, Pressable, StyleSheet } from 'react-native';

import { Text, View, useThemeColor } from '@/components/Themed';
import type { Video } from '@/lib/types';

// Anteprima di un video: toccandola si apre YouTube (app, se installata, altrimenti browser).
export function VideoCard({ video }: { video: Video }) {
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');

  return (
    <Pressable onPress={() => Linking.openURL(`https://www.youtube.com/watch?v=${video.videoId}`)}>
      {({ pressed }) => (
        <View style={[styles.container, { opacity: pressed ? 0.7 : 1 }]}>
          <View style={[styles.thumbnail, { backgroundColor: card }]}>
            <Image source={{ uri: video.thumbnailUrl }} style={StyleSheet.absoluteFill} resizeMode="cover" />
            <View style={styles.play}>
              <Text style={styles.playIcon}>▶</Text>
            </View>
            {video.duration ? <Text style={styles.duration}>{video.duration}</Text> : null}
          </View>
          <Text style={styles.title} numberOfLines={2}>{video.title}</Text>
          {video.source ? <Text style={[styles.source, { color: muted }]}>{video.source} · YouTube</Text> : null}
        </View>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: { gap: 6, backgroundColor: 'transparent' },
  thumbnail: {
    aspectRatio: 16 / 9, borderRadius: 10, overflow: 'hidden', alignItems: 'center', justifyContent: 'center',
  },
  play: {
    width: 56, height: 56, borderRadius: 28, backgroundColor: 'rgba(0,0,0,0.6)',
    alignItems: 'center', justifyContent: 'center',
  },
  playIcon: { color: '#fff', fontSize: 22, marginLeft: 3 },
  duration: {
    position: 'absolute', right: 8, bottom: 8, color: '#fff', fontSize: 12, fontWeight: '600',
    backgroundColor: 'rgba(0,0,0,0.75)', paddingHorizontal: 5, paddingVertical: 1, borderRadius: 4, overflow: 'hidden',
  },
  title: { fontSize: 15, fontWeight: '600' },
  source: { fontSize: 13 },
});
