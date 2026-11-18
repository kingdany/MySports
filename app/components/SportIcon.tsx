import Svg, { Path } from 'react-native-svg';

// Tracciati di Material Symbols (Outlined, peso 400; Google, licenza Apache 2.0), disegnati in SVG e non con expo-symbols,
// che su Android li disegna come un carattere di font tagliato e spostato in basso (vedi MatchDetails).
// sports_soccer: il pallone occupa 80-880 del riquadro 0-960.
export const BALL_PATH = 'M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm200-500 54-18 16-54q-32-48-77-82.5T574-786l-54 38v56l160 112Zm-400 0 160-112v-56l-54-38q-54 17-99 51.5T210-652l16 54 54 18Zm-42 308 46-4 30-54-58-174-56-20-40 30q0 65 18 118.5T238-272Zm242 112q26 0 51-4t49-12l28-60-26-44H378l-26 44 28 60q24 8 49 12t51 4Zm-90-200h180l56-160-146-102-144 102 54 160Zm332 88q42-50 60-103.5T800-494l-40-28-56 18-58 174 30 54 46 4Z';
// sports_motorsports, il casco, scelto per i motori al posto della bandiera a scacchi (sports_score): occupa 80-880 in
// larghezza e 160-800 in altezza.
const HELMET_PATH = 'M480-480Zm80 240q100 0 170-70t70-170q0-101-73.5-170.5T550-720q-48 0-93 11t-87 33l100 40q41 17 65.5 52.5T560-504q0 60-41.5 102T418-360H162q-2 24-2 54.5v65.5h400ZM176-440h240q27 0 45.5-18.5T480-504q0-19-10.5-34.5T440-562l-148-60q-42 37-71.5 84T176-440Zm384 280H160q-33 0-56.5-23.5T80-240v-90q0-98 37-183.5t100.5-149Q281-726 367-763t183-37q68 0 128 25t105 68.5Q828-663 854-605t26 125q0 66-25 124.5t-68.5 102Q743-210 684.5-185T560-160Z';

// Riquadri ritagliati sul disegno, quadrati e centrati, così l'icona è alta quanto `size`.
const ICONS: Record<string, { path: string; viewBox: string }> = {
  football: { path: BALL_PATH, viewBox: '80 -880 800 800' },
  motorsport: { path: HELMET_PATH, viewBox: '80 -880 800 800' }, // più largo che alto: centrato in verticale
};

export const hasSportIcon = (sport: string) => sport in ICONS;

export function SportIcon({ sport, size, color }: { sport: string; size: number; color: string }) {
  const icon = ICONS[sport];
  if (!icon) return null;
  return (
    <Svg width={size} height={size} viewBox={icon.viewBox}>
      <Path d={icon.path} fill={color} />
    </Svg>
  );
}
