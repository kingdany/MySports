import { router } from 'expo-router';
import { Pressable, StyleSheet } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { useThemeColor } from '@/components/Themed';

// "tune" di Material Symbols (Outlined, peso 400; Google, licenza Apache 2.0), disegnato in SVG come le icone degli sport
// (vedi SportIcon): occupa 120-840 del riquadro 0-960.
const TUNE_PATH = 'M440-120v-240h80v80h320v80H520v80h-80Zm-320-80v-80h240v80H120Zm160-160v-80H120v-80h160v-80h80v240h-80Zm160-80v-80h400v80H440Zm160-160v-240h80v80h160v80H680v80h-80Zm-480-80v-80h400v80H120Z';

// Pulsante nell'header delle tab che apre la schermata delle competizioni da mostrare. Con router.push e non con
// <Link asChild>, che non passava al pulsante lo stile scritto come funzione, e quindi il margine dal bordo.
export function CompetitionsButton() {
  const text = useThemeColor({}, 'text');
  return (
    <Pressable
      onPress={() => router.push('/competizioni')}
      accessibilityRole="button"
      accessibilityLabel="Scegli le competizioni"
      hitSlop={8}
      style={({ pressed }) => [styles.button, pressed && { opacity: 0.5 }]}>
      <Svg width={24} height={24} viewBox="120 -840 720 720">
        <Path d={TUNE_PATH} fill={text} />
      </Svg>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: { marginRight: 16 },
});
