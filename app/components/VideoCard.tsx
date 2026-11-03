import { useState } from 'react';
import { Image, Linking, Pressable, StyleSheet } from 'react-native';

import { Text, View, useThemeColor } from '@/components/Themed';
import { YouTubePlayer } from '@/components/YouTubePlayer';
import type { Video } from '@/lib/types';

// Canali che non permettono la visione fuori da YouTube (es. Formula 1): scoperti al primo errore del player,
// da lì in poi i loro video si aprono subito nell'app YouTube.
const embedBlockedSources = new Set<string>();

const openInYouTube = (video: Video) => Linking.openURL(`https://www.youtube.com/watch?v=${video.videoId}`);

// Anteprima di un video: toccandola il video parte qui dentro, se il canale lo permette;
// altrimenti si apre l'app YouTube. Il link sotto apre sempre YouTube.
export function VideoCard({ video }: { video: Video }) {
  const [playing, setPlaying] = useState(false);
  const sourceKey = video.source ?? video.videoId;

  const play = () => {
    if (embedBlockedSources.has(sourceKey)) openInYouTube(video);
    else setPlaying(true);
  };

  const onPlayerError = () => {
    embedBlockedSources.add(sourceKey);
    setPlaying(false);
    openInYouTube(video);
  };
  const muted = useThemeColor({}, 'muted');
  const card = useThemeColor({}, 'card');
  const tint = useThemeColor({}, 'tint');

  return (
    <View style={styles.container}>
      <View style={[styles.frame, { backgroundColor: card }]}>
        {playing ? (
          <YouTubePlayer videoId={video.videoId} onError={onPlayerError} />
        ) : (
          <Pressable onPress={play}>
            {({ pressed }) => (
              <View style={[styles.thumbnail, { opacity: pressed ? 0.7 : 1 }]}>
                <Image source={{ uri: video.thumbnailUrl }} style={StyleSheet.absoluteFill} resizeMode="cover" />
                <View style={styles.play}>
                  <Text style={styles.playIcon}>▶</Text>
                </View>
                {video.duration ? <Text style={styles.duration}>{video.duration}</Text> : null}
              </View>
            )}
          </Pressable>
        )}
      </View>
      <Text style={styles.title} numberOfLines={2}>{video.title}</Text>
      <View style={styles.footer}>
        {video.source ? <Text style={[styles.source, { color: muted }]}>{video.source} · YouTube</Text> : <View />}
        <Pressable onPress={() => openInYouTube(video)} hitSlop={8}>
          <Text style={[styles.source, { color: tint }]}>Apri in YouTube</Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 6, backgroundColor: 'transparent' },
  frame: { borderRadius: 10, overflow: 'hidden' },
  thumbnail: { aspectRatio: 16 / 9, alignItems: 'center', justifyContent: 'center', backgroundColor: 'transparent' },
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
  footer: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', backgroundColor: 'transparent' },
  source: { fontSize: 13 },
});
