import { useMemo } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet } from 'react-native';

import { useShowFullName } from '@/components/FullNameToast';
import { TeamLogo } from '@/components/TeamLogo';
import { Text, View, useThemeColor } from '@/components/Themed';
import { buildTree, type BracketRound, type Tie, type TreeNode } from '@/lib/bracket';
import { teamLabel } from '@/lib/useTeamFilter';

const BOX_WIDTH = 140;
const BOX_HEIGHT = 56;
const ROW_HEIGHT = 68;
const COLUMN_GAP = 28;
const HEADER_HEIGHT = 32;
const PADDING = 16;

const boxLeft = (column: number) => PADDING + column * (BOX_WIDTH + COLUMN_GAP);
const boxTop = (row: number) => HEADER_HEIGHT + row * ROW_HEIGHT;
const boxMiddle = (row: number) => boxTop(row) + BOX_HEIGHT / 2;

type Props = { rounds: BracketRound[]; refreshing: boolean; onRefresh: () => void };

// Tabellone dell'eliminazione diretta come albero, da scorrere in orizzontale.
// Solo chi passa il turno, senza punteggi: i risultati e i video stanno nella scheda Risultati.
export function Bracket({ rounds, refreshing, onRefresh }: Props) {
  const muted = useThemeColor({}, 'muted');
  const tree = useMemo(() => buildTree(rounds), [rounds]);

  return (
    <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View
          style={{
            width: boxLeft(tree.columns.length) - COLUMN_GAP + PADDING,
            height: boxTop(tree.rows) + PADDING,
          }}>
          {tree.columns.map((title, column) => (
            <Text key={title} numberOfLines={1} style={[styles.title, { color: muted, left: boxLeft(column) }]}>{title}</Text>
          ))}
          {tree.nodes.map((node) => <Links key={node.tie.id} node={node} />)}
          {tree.nodes.map((node) => <TieBox key={node.tie.id} node={node} />)}
        </View>
      </ScrollView>
    </ScrollView>
  );
}

// Linee verso le sfide del turno prima: un tratto per ognuna, uno verticale che le unisce e uno verso questa.
function Links({ node }: { node: TreeNode }) {
  const color = useThemeColor({}, 'border');
  if (node.children.length === 0) return null;

  const left = boxLeft(node.column);
  const elbow = left - COLUMN_GAP / 2;
  const middles = [...node.children.map((c) => boxMiddle(c.row)), boxMiddle(node.row)];
  const top = Math.min(...middles);

  return (
    <>
      {node.children.map((child) => (
        <View
          key={child.tie.id}
          style={[styles.line, { backgroundColor: color, left: elbow - COLUMN_GAP / 2, top: boxMiddle(child.row), width: COLUMN_GAP / 2, height: 1 }]}
        />
      ))}
      <View style={[styles.line, { backgroundColor: color, left: elbow, top, width: 1, height: Math.max(...middles) - top + 1 }]} />
      <View style={[styles.line, { backgroundColor: color, left: elbow, top: boxMiddle(node.row), width: COLUMN_GAP / 2, height: 1 }]} />
    </>
  );
}

function TieBox({ node }: { node: TreeNode }) {
  const card = useThemeColor({}, 'card');
  return (
    <View style={[styles.box, { backgroundColor: card, left: boxLeft(node.column), top: boxTop(node.row) }]}>
      <TeamLine tie={node.tie} index={0} />
      <TeamLine tie={node.tie} index={1} />
    </View>
  );
}

function TeamLine({ tie, index }: { tie: Tie; index: 0 | 1 }) {
  const showFullName = useShowFullName();
  const eliminated = tie.winner !== null && tie.winner !== index;
  const team = tie.teams[index];
  // Il riquadro è stretto: toccando la squadra compare il nome per intero, come in classifica (nome breve, non quello
  // ufficiale della fonte: "Inter", non "FC Internazionale Milano").
  return (
    <Pressable onPress={() => showFullName(teamLabel(team))} hitSlop={4} style={[styles.teamLine, eliminated && styles.eliminated]}>
      <TeamLogo uri={team.logoUrl} name={teamLabel(team)} size={16} />
      <Text numberOfLines={1} style={[styles.team, tie.winner === index && styles.winner]}>
        {teamLabel(team)}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  title: {
    position: 'absolute', top: 8, width: BOX_WIDTH, fontSize: 13, fontWeight: '700', textTransform: 'uppercase',
  },
  line: { position: 'absolute' },
  box: {
    position: 'absolute', width: BOX_WIDTH, height: BOX_HEIGHT, borderRadius: 8,
    paddingHorizontal: 10, justifyContent: 'center', gap: 2,
  },
  teamLine: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'transparent' },
  eliminated: { opacity: 0.45 }, // chi è eliminato è più chiaro, stemma compreso
  team: { flexShrink: 1, fontSize: 14 },
  winner: { fontWeight: '700' },
});
