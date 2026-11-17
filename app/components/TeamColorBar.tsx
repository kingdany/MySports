import { StyleSheet, View } from 'react-native';

// Barretta con il colore della squadra accanto al pilota (classifiche di F1 e MotoGP), alta quanto il nome e la squadra.
// Senza colore resta lo spazio vuoto, così i nomi restano allineati con le altre righe della stessa lista.
export function TeamColorBar({ color }: { color: string | undefined }) {
  return <View style={[styles.bar, { backgroundColor: color ?? 'transparent' }]} />;
}

const styles = StyleSheet.create({
  // Le righe hanno 12 di spazio tra gli elementi: la barretta lo riduce a 8, così resta vicina al nome.
  bar: { width: 4, alignSelf: 'stretch', borderRadius: 2, marginHorizontal: -4 },
});
