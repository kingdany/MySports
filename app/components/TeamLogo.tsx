import { Image } from 'expo-image';
import { memo, useState } from 'react';
import { StyleSheet } from 'react-native';

import { Text, View, useThemeColor } from '@/components/Themed';
import { useColorScheme } from '@/components/useColorScheme';

type Props = { uri?: string; name: string; size?: number };

// Stemma della squadra (PNG o SVG di football-data), tenuto nella cache su disco. Senza stemma, o se non si carica, un
// cerchio con l'iniziale della stessa misura, così le righe restano allineate.
export const TeamLogo = memo(function TeamLogo({ uri, name, size = 20 }: Props) {
  const [failed, setFailed] = useState<string | null>(null); // indirizzo che non si è caricato
  const card = useThemeColor({}, 'card');
  const muted = useThemeColor({}, 'muted');
  const scheme = useColorScheme();

  if (!uri || failed === uri) {
    return (
      <View style={[styles.fallback, { width: size, height: size, borderRadius: size / 2, backgroundColor: card }]}>
        <Text style={{ color: muted, fontSize: size * 0.5, fontWeight: '700' }}>{name.charAt(0).toUpperCase()}</Text>
      </View>
    );
  }

  const image = (imageSize: number) => (
    <Image
      source={{ uri }}
      style={{ width: imageSize, height: imageSize }}
      contentFit="contain"
      cachePolicy="memory-disk"
      recyclingKey={uri} // nelle liste la vista si riusa: senza, per un attimo resterebbe lo stemma della riga di prima
      transition={100}
      onError={() => setFailed(uri)}
      accessibilityLabel={name}
    />
  );

  if (scheme !== 'dark') return image(size);

  // Nel tema scuro gli stemmi scuri (es. Juventus) sparirebbero sul nero: stanno in un cerchio chiaro, tutti, perché
  // dall'immagine non si sa se è scura. Lo stemma si rimpicciolisce per non toccare il bordo del cerchio.
  return (
    <View style={[styles.fallback, { width: size, height: size, borderRadius: size / 2, backgroundColor: LOGO_BACKGROUND }]}>
      {image(Math.round(size * 0.72))}
    </View>
  );
});

const LOGO_BACKGROUND = '#f3f4f6';

const styles = StyleSheet.create({
  fallback: { alignItems: 'center', justifyContent: 'center' },
});
