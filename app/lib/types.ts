// Formato dei file JSON pubblicati dall'importatore (src/MySports.Importer/Models/SportEvent.cs).

export type EventStatus = 'scheduled' | 'live' | 'finished' | 'postponed' | 'cancelled';

export type Team = { id: string; name: string; shortName?: string; logoUrl?: string };

export type ClassificationRow = {
  pos: number;
  number?: string;
  driver: string;
  code?: string;
  team: string;
  time?: string;
  points?: number;
  q1?: string;
  q2?: string;
  q3?: string;
};

export type Video = { videoId: string; title: string; thumbnailUrl: string; publishedAt: string };

export type SportEvent = {
  id: string;
  competition: string;
  season: string;
  round?: number;
  title: string;
  session: string;
  startsAt: string;
  venue?: string;
  city?: string;
  country?: string;
  status: EventStatus;
  homeTeam?: Team;
  awayTeam?: Team;
  homeScore?: number;
  awayScore?: number;
  result?: ClassificationRow[];
  videos: Video[];
};

export type SeasonFile = { competition: string; season: string; generatedAt: string; events: SportEvent[] };

export type CompetitionInfo = { id: string; sport: string; name: string; shortName: string; currentSeason: string };

export type MetaFile = { generatedAt: string; competitions: CompetitionInfo[] };
