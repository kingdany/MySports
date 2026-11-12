import { SymbolView } from 'expo-symbols';
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, type StyleProp, type TextStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

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
  missedPenalty: '(rigore sbagliato)',
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

function IncidentIcon({ type, size = 16 }: { type: IncidentType; size?: number }) {
  const text = useThemeColor({}, 'text');
  const muted = useThemeColor({}, 'muted');
  switch (type) {
    case 'yellow':
    case 'red':
    case 'secondYellow':
      return <CardIcon type={type} height={size} />;
    case 'ownGoal':
      return <Ball color={RED} size={size} />;
    case 'missedPenalty':
      return <Ball color={muted} size={size} />;
    default:
      return <Ball color={text} size={size} />;
  }
}

function Ball({ color, size }: { color: string; size: number }) {
  return (
    <SymbolView
      name={{ ios: 'soccerball', android: 'sports_soccer', web: 'sports_soccer' }}
      tintColor={color}
      size={size}
    />
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
    <SymbolView
      name={up
        ? { ios: 'arrow.up', android: 'arrow_upward', web: 'arrow_upward' }
        : { ios: 'arrow.down', android: 'arrow_downward', web: 'arrow_downward' }}
      tintColor={up ? GREEN : RED}
      size={12}
    />
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
      {card && <CardIcon type={card} height={12} />}
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
  columns: { flexDirection: 'row', paddingHorizontal: 16, gap: 16 },
  column: { flex: 1, gap: 4 },
  teamName: { fontSize: 15, fontWeight: '700', marginBottom: 4 },
  playerRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  substitute: { paddingLeft: 8 },
  number: { width: 22, fontSize: 13, fontWeight: '700', textAlign: 'right', fontVariant: ['tabular-nums'] },
  playerName: { flexShrink: 1, fontSize: 13 },
  subMinute: { fontSize: 11, fontVariant: ['tabular-nums'] },
});
