import { STAGES, stageLabel } from './format';
import type { SportEvent, Team } from './types';

export type Tie = {
  id: string;
  teams: [Team, Team]; // la prima è quella che gioca in casa la prima partita
  winner: 0 | 1 | null; // indice in teams, null finché la sfida non è conclusa
};

export type BracketRound = { stage: string; title: string; ties: Tie[] };

// Una sfida nell'albero: column è il turno, row la posizione verticale (a metà tra le sfide che la alimentano).
export type TreeNode = { tie: Tie; column: number; row: number; children: TreeNode[] };

export type BracketTree = { columns: string[]; nodes: TreeNode[]; rows: number };

type KnownEvent = SportEvent & { homeTeam: Team; awayTeam: Team };

const hasTeams = (event: SportEvent): event is KnownEvent => !!event.homeTeam && !!event.awayTeam;

// Gol dal punto di vista della sfida: la squadra di casa cambia tra andata e ritorno.
function goals(event: KnownEvent, first: Team, home?: number, away?: number): [number, number] | null {
  if (home == null || away == null) return null;
  return event.homeTeam.id === first.id ? [home, away] : [away, home];
}

function buildTie(stage: string, legs: KnownEvent[]): Tie {
  const first = legs[0].homeTeam;
  const played = legs.flatMap((e) => {
    const score = e.status === 'finished' ? goals(e, first, e.homeScore, e.awayScore) : null;
    return score ? [score] : [];
  });

  const shootout = legs.find((e) => e.homePenalties != null && e.awayPenalties != null);
  const penalties = shootout ? goals(shootout, first, shootout.homePenalties, shootout.awayPenalties) : null;

  // La finale è una partita sola, le altre fasi sono andata e ritorno.
  let winner: Tie['winner'] = null;
  if (played.length === (stage === 'final' ? 1 : 2)) {
    const [a, b] = played.reduce((total, s) => [total[0] + s[0], total[1] + s[1]], [0, 0]);
    if (a !== b) winner = a > b ? 0 : 1;
    else if (penalties && penalties[0] !== penalties[1]) winner = penalties[0] > penalties[1] ? 0 : 1;
  }

  return { id: `${stage}-${legs[0].id}`, teams: [first, legs[0].awayTeam], winner };
}

// Sfide dell'eliminazione diretta ricavate dalle partite, nell'ordine del torneo.
// Le partite con una squadra ancora da definire non si mostrano: non si saprebbe a quale sfida appartengono.
export function buildBracket(events: SportEvent[] | undefined): BracketRound[] {
  const known = (events ?? [])
    .filter((e) => e.stage && hasTeams(e))
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt)) as KnownEvent[];

  return STAGES.flatMap((stage) => {
    const ties = new Map<string, KnownEvent[]>();
    for (const event of known) {
      if (event.stage !== stage) continue;
      const key = [event.homeTeam.id, event.awayTeam.id].sort().join('-');
      ties.set(key, [...(ties.get(key) ?? []), event]);
    }
    if (ties.size === 0) return [];
    return [{
      stage,
      title: stageLabel(stage),
      ties: [...ties.values()].map((legs) => buildTie(stage, legs)),
    }];
  });
}

// Collega ogni sfida a quelle del turno prima da cui arrivano le sue squadre.
// L'API non dice dove porta una sfida non ancora conclusa: senza collegamento resta su una riga propria,
// sotto l'albero, e i turni futuri non si disegnano per non suggerire accoppiamenti inventati.
export function buildTree(rounds: BracketRound[]): BracketTree {
  const columns = rounds.map((round, column) =>
    round.ties.map((tie): TreeNode => ({ tie, column, row: 0, children: [] })));

  const linked = new Set<TreeNode>();
  columns.forEach((nodes, column) => {
    if (column === 0) return;
    for (const node of nodes) {
      node.children = columns[column - 1].filter((child) => {
        const { teams, winner } = child.tie;
        return winner !== null && node.tie.teams.some((t) => t.id === teams[winner].id);
      });
      node.children.forEach((child) => linked.add(child));
    }
  });

  let rows = 0;
  const place = (node: TreeNode) => {
    node.children.forEach(place);
    node.row = node.children.length
      ? node.children.reduce((sum, child) => sum + child.row, 0) / node.children.length
      : rows++;
  };
  // Dai turni più avanzati: prima l'albero principale, poi le sfide ancora senza collegamento.
  for (const nodes of [...columns].reverse()) nodes.filter((n) => !linked.has(n)).forEach(place);

  return { columns: rounds.map((r) => r.title), nodes: columns.flat(), rows };
}
