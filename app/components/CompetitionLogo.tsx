import { memo, useMemo } from 'react';
import { SvgXml } from 'react-native-svg';

import { View, useThemeColor } from '@/components/Themed';
import { useColorScheme } from '@/components/useColorScheme';
import { CompetitionLogos } from '@/constants/CompetitionLogos';

type Props = {
  id: string; // competizione, es. "serie-a"
  height: number;
  maxWidth?: number; // i loghi larghi (MotoGP) si rimpiccioliscono per starci
  mono?: string; // sul fondo colorato della competizione: tutto di questo colore (es. bianco), o la piastrina (plate)
  label?: string; // per l'accessibilità
};

// Tutti i colori del logo, sfumature comprese, diventano currentColor.
const toMono = (xml: string) => xml.replace(/fill="(?!none)[^"]*"/g, 'fill="currentColor"');

// Logo della competizione; null se non ce l'ha, e chi lo usa mostra il nome.
export const CompetitionLogo = memo(function CompetitionLogo({ id, height, maxWidth, mono, label }: Props) {
  const scheme = useColorScheme();
  const text = useThemeColor({}, 'text');
  const logo = CompetitionLogos[id];
  const plate = !!mono && !!logo?.plate;
  const xml = useMemo(() => (logo && mono && !plate ? toMono(logo.xml) : logo?.xml), [logo, mono, plate]);
  if (!logo || !xml) return null;

  if (plate) {
    // A colori su bianco: il colore del tema chiaro anche in modalità scura. La piastrina è alta height.
    const padV = Math.max(1, Math.round(height * 0.08));
    const inner = height - 2 * padV;
    return (
      <View
        accessibilityLabel={label}
        style={{
          backgroundColor: '#fff', borderRadius: Math.round(height * 0.2),
          paddingVertical: padV, paddingHorizontal: Math.round(height * 0.2),
        }}>
        <SvgXml xml={xml} width={inner * logo.ratio} height={inner} color={logo.color?.light ?? '#000'} />
      </View>
    );
  }

  let width = height * logo.ratio;
  let logoHeight = height;
  if (maxWidth && width > maxWidth) {
    width = maxWidth;
    logoHeight = width / logo.ratio;
  }
  const color = mono ?? logo.color?.[scheme] ?? text;

  return <SvgXml xml={xml} width={width} height={logoHeight} color={color} accessibilityLabel={label} />;
});

export const hasWordmark = (id: string) => CompetitionLogos[id]?.wordmark ?? false;
export const hasLogo = (id: string) => id in CompetitionLogos;
export const hasPlate = (id: string) => CompetitionLogos[id]?.plate ?? false;
export const rowLabel = (id: string) => CompetitionLogos[id]?.rowLabel;
