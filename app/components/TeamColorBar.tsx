import { StyleSheet, View } from 'react-native';

import { useThemeColor } from '@/components/Themed';

// Barretta con il colore della squadra accanto al pilota (classifiche di F1 e MotoGP), alta quanto il nome e la squadra.
// Senza colore resta lo spazio vuoto, così i nomi restano allineati con le altre righe della stessa lista.
export function TeamColorBar({ color }: { color: string | undefined }) {
  const background = useThemeColor({}, 'background');
  const muted = useThemeColor({}, 'muted');
  return (
    <View
      style={[
        styles.bar,
        { backgroundColor: color ?? 'transparent' },
        // Un colore quasi uguale allo sfondo (bianco nel tema chiaro, nero in quello scuro: Haas, Cadillac, LCR...)
        // sparirebbe: gli si dà un bordo.
        color && Math.abs(luminance(color) - luminance(background)) < 40 && { borderWidth: StyleSheet.hairlineWidth, borderColor: muted },
      ]}
    />
  );
}

// Luminosità percepita (0-255) di "#RRGGBB" o "#RGB".
function luminance(color: string) {
  const hex = color.length === 4 ? [...color.slice(1)].map((c) => c + c).join('') : color.slice(1);
  const value = parseInt(hex, 16);
  return 0.299 * ((value >> 16) & 255) + 0.587 * ((value >> 8) & 255) + 0.114 * (value & 255);
}

const styles = StyleSheet.create({
  // Le righe hanno 12 di spazio tra gli elementi: la barretta lo riduce a 8, così resta vicina al nome.
  bar: { width: 4, alignSelf: 'stretch', borderRadius: 2, marginHorizontal: -4 },
});
