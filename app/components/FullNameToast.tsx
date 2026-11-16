import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Text, View, useThemeColor } from '@/components/Themed';

const TOAST_MS = 2000;

// Dove c'è poco spazio si mostra un nome breve o troncato; toccandolo compare per qualche secondo il nome completo in un
// fumetto in fondo allo schermo.
const ShowFullName = createContext<(name: string) => void>(() => {});

export const useShowFullName = () => useContext(ShowFullName);

// Va intorno alla pagina (fuori dallo ScrollView), perché il fumetto resti fermo mentre si scorre. overSystemBar: la
// pagina arriva fino in fondo allo schermo (es. il dettaglio dell'evento), quindi il fumetto sale sopra i tasti di
// sistema; nelle schede la barra delle schede li copre già.
export function FullNameToast({ children, overSystemBar = false }: { children: ReactNode; overSystemBar?: boolean }) {
  const [name, setName] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const insets = useSafeAreaInsets();
  // Colori del tema invertiti, perché il fumetto si stacchi dalla pagina sia in chiaro sia in scuro.
  const background = useThemeColor({}, 'text');
  const color = useThemeColor({}, 'background');

  const show = useCallback((full: string) => {
    clearTimeout(timer.current);
    setName(full);
    timer.current = setTimeout(() => setName(null), TOAST_MS);
  }, []);
  useEffect(() => () => clearTimeout(timer.current), []);

  return (
    <ShowFullName.Provider value={show}>
      <View style={styles.fill}>
        {children}
        {name && (
          <View pointerEvents="none" style={[styles.toastWrap, { bottom: (overSystemBar ? insets.bottom : 0) + 24 }]}>
            <Text style={[styles.toast, { backgroundColor: background, color }]}>{name}</Text>
          </View>
        )}
      </View>
    </ShowFullName.Provider>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  toastWrap: { position: 'absolute', left: 16, right: 16, alignItems: 'center', backgroundColor: 'transparent' },
  toast: {
    fontSize: 15, fontWeight: '600', textAlign: 'center',
    paddingHorizontal: 16, paddingVertical: 10, borderRadius: 20, overflow: 'hidden',
  },
});
