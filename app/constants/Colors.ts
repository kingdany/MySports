const tintColorLight = '#2f95dc';
const tintColorDark = '#fff';

export default {
  light: {
    text: '#000',
    muted: '#6b7280',
    background: '#fff',
    card: '#f3f4f6',
    border: '#e5e7eb',
    tint: tintColorLight,
    tabIconDefault: '#ccc',
    tabIconSelected: tintColorLight,
  },
  dark: {
    text: '#fff',
    muted: '#9ca3af',
    background: '#000',
    card: '#1c1c1e',
    border: '#2c2c2e',
    tint: tintColorDark,
    tabIconDefault: '#ccc',
    tabIconSelected: tintColorDark,
  },
};

export const LiveColor = '#e11d48';

// Zone della classifica del calcio, nell'ordine della legenda.
export const StandingZones: { id: string; label: string; color: string }[] = [
  { id: 'champions', label: 'Champions', color: '#2563eb' },
  { id: 'europa', label: 'Europa League', color: '#f97316' },
  { id: 'conference', label: 'Conference', color: '#16a34a' },
  { id: 'relegation', label: 'Retrocessione', color: '#dc2626' },
];

// Colore distintivo di ogni competizione (badge nelle liste).
export const CompetitionColors: Record<string, string> = {
  'serie-a': '#0a5cad',
  f1: '#e10600',
  motogp: '#3d3d3d',
};
