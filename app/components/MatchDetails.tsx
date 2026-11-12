import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, type StyleProp, type TextStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Path } from 'react-native-svg';

import { Text, View, useThemeColor } from '@/components/Themed';
import { minuteLabel } from '@/lib/format';
import type { IncidentType, Lineup, LineupPlayer, MatchIncident, SportEvent, Substitution } from '@/lib/types';

const YELLOW = '#facc15';
const RED = '#dc2626';
const GREEN = '#16a34a';
const TOAST_MS = 2000;

const FLAGS: Partial<Record<IncidentType, string>> = {
  penalty: '(R)',
  ownGoal: '(A)',
  missedPenalty: '(R)', // il pallone barrato dice che è sbagliato
  secondYellow: '(2G)',
};

// Si mostra il cognome; toccandolo compare per qualche secondo il nome completo in un fumetto in fondo allo schermo.
const ShowFullName = createContext<(name: string) => void>(() => {});

// Va intorno alla pagina (fuori dallo ScrollView), perché il fumetto resti fermo mentre si scorre.
export function FullNameToast({ children }: { children: ReactNode }) {
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
          <View pointerEvents="none" style={[styles.toastWrap, { bottom: insets.bottom + 24 }]}>
            <Text style={[styles.toast, { backgroundColor: background, color }]}>{name}</Text>
          </View>
        )}
      </View>
    </ShowFullName.Provider>
  );
}

// Cognome del giocatore (più eventuale testo dopo, es. il flag), toccabile se il nome completo è diverso.
function PlayerName({ name, short, style, children }: {
  name: string; short?: string; style?: StyleProp<TextStyle>; children?: ReactNode;
}) {
  const show = useContext(ShowFullName);
  const text = <Text style={style} numberOfLines={1}>{short ?? name}{children}</Text>;
  if (!short) return text;
  return (
    <Pressable onPress={() => show(name)} hitSlop={6} style={styles.pressable}>
      {text}
    </Pressable>
  );
}

// Cronologia di una partita finita: gol, rigori sbagliati, cartellini.
export function MatchTimeline({ event }: { event: SportEvent }) {
  const muted = useThemeColor({}, 'muted');
  const incidents = event.details?.incidents ?? [];
  if (incidents.length === 0) return null;

  return (
    <View style={styles.section}>
      <Text style={[styles.sectionTitle, { color: muted }]}>Cronologia</Text>
      {incidents.map((i, n) => <IncidentRow key={n} incident={i} />)}
    </View>
  );
}

// Formazioni di una partita finita: titolari con numero di maglia, chi è entrato e chi è uscito.
export function MatchLineups({ event }: { event: SportEvent }) {
  const muted = useThemeColor({}, 'muted');
  const details = event.details;
  if (!details?.home && !details?.away) return null;

  return (
    <View style={styles.section}>
      <Text style={[styles.sectionTitle, { color: muted }]}>Formazioni</Text>
      <View style={styles.columns}>
        <LineupColumn
          team={event.homeTeam?.shortName ?? event.homeTeam?.name ?? 'Casa'}
          lineup={details.home}
          incidents={details.incidents.filter((i) => playerSide(i) === 'home')}
        />
        <LineupColumn
          team={event.awayTeam?.shortName ?? event.awayTeam?.name ?? 'Trasferta'}
          lineup={details.away}
          incidents={details.incidents.filter((i) => playerSide(i) === 'away')}
        />
      </View>
    </View>
  );
}

// Squadra del giocatore: l'autogol è registrato per la squadra che segna.
const playerSide = (i: MatchIncident) =>
  i.type === 'ownGoal' ? (i.side === 'home' ? 'away' : 'home') : i.side;

// Eventi della squadra di casa a sinistra, della squadra in trasferta a destra, il minuto al centro.
function IncidentRow({ incident }: { incident: MatchIncident }) {
  const muted = useThemeColor({}, 'muted');
  const flag = FLAGS[incident.type];
  const label = (
    <PlayerName name={incident.player} short={incident.short} style={styles.incidentText}>
      {flag ? <Text style={{ color: muted }}> {flag}</Text> : null}
    </PlayerName>
  );
  const icon = <IncidentIcon type={incident.type} />;
  const home = incident.side === 'home';

  return (
    <View style={styles.incidentRow}>
      <View style={[styles.incidentSide, styles.incidentHome]}>{home && <>{label}{icon}</>}</View>
      <Text style={[styles.minute, { color: muted }]}>{minuteLabel(incident.minute, incident.addedTime)}</Text>
      <View style={styles.incidentSide}>{!home && <>{icon}{label}</>}</View>
    </View>
  );
}

// Ogni icona sta in uno spazio largo quanto il pallone, centrata: il cartellino, più stretto, ha lo stesso centro del
// pallone, così nella cronologia le icone stanno in colonna.
function IncidentIcon({ type, size = 16 }: { type: IncidentType; size?: number }) {
  return (
    <View style={[styles.iconSlot, { width: size, height: size }]}>
      <IncidentGlyph type={type} size={size} />
    </View>
  );
}

function IncidentGlyph({ type, size }: { type: IncidentType; size: number }) {
  const text = useThemeColor({}, 'text');
  switch (type) {
    case 'yellow':
    case 'red':
    case 'secondYellow':
      return <CardIcon type={type} height={size} />;
    case 'ownGoal':
      return <Ball color={RED} size={size} />;
    case 'missedPenalty':
      return <MissedBall color={text} size={size} />;
    default:
      return <Ball color={text} size={size} />;
  }
}

// Icone disegnate in SVG con i tracciati di Material Symbols (Outlined, peso 400; Google, licenza Apache 2.0), gli stessi
// di expo-symbols su Android. Con expo-symbols su Android il simbolo è un carattere di font: il margine che Android mette
// sopra il testo lo tagliava sotto e a destra e lo spostava in basso rispetto ai cartellini. L'SVG ha un riquadro esatto.
const BALL_PATH = 'M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm200-500 54-18 16-54q-32-48-77-82.5T574-786l-54 38v56l160 112Zm-400 0 160-112v-56l-54-38q-54 17-99 51.5T210-652l16 54 54 18Zm-42 308 46-4 30-54-58-174-56-20-40 30q0 65 18 118.5T238-272Zm242 112q26 0 51-4t49-12l28-60-26-44H378l-26 44 28 60q24 8 49 12t51 4Zm-90-200h180l56-160-146-102-144 102 54 160Zm332 88q42-50 60-103.5T800-494l-40-28-56 18-58 174 30 54 46 4Z';
const ARROW_UP_PATH = 'M440-160v-487L216-423l-56-57 320-320 320 320-56 57-224-224v487h-80Z';
const ARROW_DOWN_PATH = 'M440-800v487L216-537l-56 57 320 320 320-320-56-57-224 224v-487h-80Z';

// Il pallone occupa 80-880 del riquadro 0-960: il viewBox ritagliato lo fa alto quanto `size`, come i cartellini.
function Ball({ color, size }: { color: string; size: number }) {
  return (
    <Svg width={size} height={size} viewBox="80 -880 800 800">
      <Path d={BALL_PATH} fill={color} />
    </Svg>
  );
}

// Rigore sbagliato: pallone barrato da una X rossa.
function MissedBall({ color, size }: { color: string; size: number }) {
  const strike = { width: size, top: size / 2 - 1 };
  return (
    <View style={{ width: size, height: size, backgroundColor: 'transparent' }}>
      <Ball color={color} size={size} />
      <View style={[styles.strike, strike, { transform: [{ rotate: '-45deg' }] }]} />
      <View style={[styles.strike, strike, { transform: [{ rotate: '45deg' }] }]} />
    </View>
  );
}

// Cartellino disegnato: giallo, rosso, oppure doppio giallo (un giallo con un rosso davanti).
function CardIcon({ type, height }: { type: 'yellow' | 'red' | 'secondYellow'; height: number }) {
  const width = Math.round(height * 0.7);
  const card = (color: string, offset = 0) => (
    <View
      style={[styles.card, { width, height, backgroundColor: color, left: offset, top: offset }]}
    />
  );
  if (type !== 'secondYellow') return <View style={{ width, height }}>{card(type === 'red' ? RED : YELLOW)}</View>;
  return (
    <View style={{ width: width + 4, height: height + 4 }}>
      {card(YELLOW)}
      {card(RED, 4)}
    </View>
  );
}

function Arrow({ up }: { up: boolean }) {
  return (
    <Svg width={12} height={12} viewBox="0 -960 960 960">
      <Path d={up ? ARROW_UP_PATH : ARROW_DOWN_PATH} fill={up ? GREEN : RED} />
    </Svg>
  );
}

// Titolari in ordine, ognuno seguito da chi è entrato al suo posto (anche a catena).
function LineupColumn({ team, lineup, incidents }: { team: string; lineup?: Lineup; incidents: MatchIncident[] }) {
  if (!lineup) return <View style={styles.column} />;
  const replacedBy = new Map(lineup.substitutions.map((s) => [s.out.name, s]));

  const rows: { player: LineupPlayer; cameIn: boolean; wentOut?: Substitution }[] = [];
  const add = (player: LineupPlayer, cameIn: boolean, seen: Set<string>) => {
    if (seen.has(player.name)) return; // una fonte sbagliata non deve creare un ciclo
    seen.add(player.name);
    const wentOut = replacedBy.get(player.name);
    rows.push({ player, cameIn, wentOut });
    if (wentOut) add(wentOut.in, true, seen);
  };
  for (const p of lineup.starters) add(p, false, new Set());

  return (
    <View style={styles.column}>
      <Text style={styles.teamName} numberOfLines={1}>{team}</Text>
      {rows.map(({ player, cameIn, wentOut }) => (
        <PlayerRow
          key={player.name}
          player={player}
          cameIn={cameIn}
          wentOut={wentOut}
          incidents={incidents.filter((i) => i.player === player.name)}
        />
      ))}
    </View>
  );
}

function PlayerRow({ player, cameIn, wentOut, incidents }: {
  player: LineupPlayer; cameIn: boolean; wentOut?: Substitution; incidents: MatchIncident[];
}) {
  const muted = useThemeColor({}, 'muted');
  // Gol e autogol uno per uno, del cartellino solo il più grave.
  const goals = incidents.filter((i) => i.type === 'goal' || i.type === 'penalty' || i.type === 'ownGoal');
  const card = (['red', 'secondYellow', 'yellow'] as const).find((t) => incidents.some((i) => i.type === t));

  return (
    <View style={[styles.playerRow, cameIn && styles.substitute]}>
      {cameIn && <Arrow up />}
      <Text style={[styles.number, { color: muted }]}>{player.number ?? ''}</Text>
      <PlayerName name={player.name} short={player.short} style={styles.playerName} />
      {goals.map((g, n) => <IncidentIcon key={n} type={g.type} size={12} />)}
      {card && <IncidentIcon type={card} size={12} />}
      {wentOut && (
        <>
          <Arrow up={false} />
          <Text style={[styles.subMinute, { color: muted }]}>{minuteLabel(wentOut.minute, wentOut.addedTime)}</Text>
        </>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  toastWrap: { position: 'absolute', left: 16, right: 16, alignItems: 'center', backgroundColor: 'transparent' },
  toast: {
    fontSize: 15, fontWeight: '600',
    paddingHorizontal: 16, paddingVertical: 10, borderRadius: 20, overflow: 'hidden',
  },
  pressable: { flexShrink: 1 },
  section: { paddingBottom: 16 },
  sectionTitle: { fontSize: 13, fontWeight: '700', textTransform: 'uppercase', paddingHorizontal: 16, paddingTop: 8, paddingBottom: 8 },
  incidentRow: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 5 },
  incidentSide: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'transparent' },
  incidentHome: { justifyContent: 'flex-end' },
  incidentText: { fontSize: 14, flexShrink: 1 },
  minute: { width: 56, textAlign: 'center', fontSize: 13, fontVariant: ['tabular-nums'] },
  card: { position: 'absolute', borderRadius: 2 },
  // overflow visibile: il doppio giallo, con il rosso spostato di 4 punti, è più alto dello spazio.
  iconSlot: { alignItems: 'center', justifyContent: 'center', overflow: 'visible', backgroundColor: 'transparent' },
  strike: { position: 'absolute', left: 0, height: 2, backgroundColor: RED },
  columns: { flexDirection: 'row', paddingHorizontal: 16, gap: 16 },
  column: { flex: 1, gap: 4 },
  teamName: { fontSize: 15, fontWeight: '700', marginBottom: 4 },
  playerRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  substitute: { paddingLeft: 8 },
  number: { width: 22, fontSize: 13, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  playerName: { flexShrink: 1, fontSize: 13 },
  subMinute: { fontSize: 11, fontVariant: ['tabular-nums'] },
});
