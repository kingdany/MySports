import { memo, useMemo } from 'react';
import { SvgXml } from 'react-native-svg';

import { useThemeColor } from '@/components/Themed';
import { useColorScheme } from '@/components/useColorScheme';
import { CompetitionLogos } from '@/constants/CompetitionLogos';

type Props = {
  id: string; // competizione, es. "serie-a"
  height: number;
  maxWidth?: number; // i loghi larghi (MotoGP) si rimpiccioliscono per starci
  mono?: string; // tutto di un colore, es. bianco sullo sfondo colorato della competizione
  label?: string; // per l'accessibilità
};

// Tutti i colori del logo, sfumature comprese, diventano currentColor.
const toMono = (xml: string) => xml.replace(/fill="(?!none)[^"]*"/g, 'fill="currentColor"');

// Logo della competizione; null se non ce l'ha, e chi lo usa mostra il nome.
export const CompetitionLogo = memo(function CompetitionLogo({ id, height, maxWidth, mono, label }: Props) {
  const scheme = useColorScheme();
  const text = useThemeColor({}, 'text');
  const logo = CompetitionLogos[id];
  const xml = useMemo(() => (logo && mono ? toMono(logo.xml) : logo?.xml), [logo, mono]);
  if (!logo || !xml) return null;

  let width = height * logo.ratio;
  if (maxWidth && width > maxWidth) {
    width = maxWidth;
    height = width / logo.ratio;
  }
  const color = mono ?? logo.color?.[scheme] ?? text;

  return <SvgXml xml={xml} width={width} height={height} color={color} accessibilityLabel={label} />;
});

export const hasWordmark = (id: string) => CompetitionLogos[id]?.wordmark ?? false;
export const hasLogo = (id: string) => id in CompetitionLogos;
