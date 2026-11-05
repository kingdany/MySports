import { useMemo } from 'react';

import { CompetitionBar } from '@/components/CompetitionBar';
import { EventList } from '@/components/EventList';
import { SeasonPicker } from '@/components/SeasonPicker';
import { TeamPicker } from '@/components/TeamPicker';
import { View } from '@/components/Themed';
import { useCompetitionSeason } from '@/lib/useSelectedSeason';
import { useTeamFilter } from '@/lib/useTeamFilter';

export default function ResultsScreen() {
  const {
    competitions, competition, season, selectCompetition, selectSeason, file, error, refreshing, refresh,
  } = useCompetitionSeason();

  // Dal più recente. Le prove libere non hanno risultati da mostrare: restano solo partite e sessioni con classifica.
  const results = useMemo(
    () => file?.events.filter((e) => e.status === 'finished' && (e.result?.length || e.homeScore != null)).reverse(),
    [file],
  );
  const team = useTeamFilter(results);

  return (
    <View style={{ flex: 1 }}>
      <CompetitionBar competitions={competitions} selected={competition?.id ?? null} onSelect={selectCompetition} />
      <SeasonPicker competition={competition} season={season} onSelect={selectSeason} />
      <TeamPicker teams={team.teams} teamId={team.teamId} onSelect={team.selectTeam} />
      <EventList
        events={team.events}
        loading={(!file && !error) || team.switching}
        error={error}
        refreshing={refreshing}
        onRefresh={refresh}
        emptyMessage="Nessun risultato in questa stagione"
      />
    </View>
  );
}
