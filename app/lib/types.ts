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

export type Video = {
  videoId: string;
  title: string;
  thumbnailUrl: string;
  publishedAt: string;
  source?: string; // canale, es. "Formula 1"
  duration?: string; // "8:08"
  embeddable?: boolean; // false se il video si può vedere solo su YouTube
};

export type SportEvent = {
  id: string;
  competition: string;
  season: string;
  round?: number; // nell'eliminazione diretta 1 = andata, 2 = ritorno
  stage?: string; // eliminazione diretta: "playoffs" | "last_16" | "quarter_finals" | "semi_finals" | "final"
  title: string;
  session: string;
  startsAt: string;
  dateOnly?: boolean; // la fonte non ha l'orario (stagioni vecchie, partite ancora da fissare)
  venue?: string;
  city?: string;
  country?: string;
  status: EventStatus;
  homeTeam?: Team;
  awayTeam?: Team;
  homeScore?: number;
  awayScore?: number;
  homePenalties?: number; // rigori, non compresi nel punteggio
  awayPenalties?: number;
  result?: ClassificationRow[];
  videos: Video[];
};

export type StandingRow = {
  pos?: number; // assente se non classificato (es. squalifica dal campionato)
  posText: string; // "1", "2", ... oppure "D" (squalificato)
  name: string;
  code?: string;
  team?: string;
  points: number; // gol nella classifica marcatori
  wins?: number;
  played?: number; // questi ultimi solo per il calcio
  draws?: number;
  losses?: number;
  goalDifference?: number;
  zone?: string; // "champions" | "europa" | "conference" | "relegation" | "round_of_16" | "playoff"
};

export type StandingsTable = { id: string; title: string; rows: StandingRow[] };

export type SeasonFile = {
  competition: string;
  season: string;
  generatedAt: string;
  events: SportEvent[];
  standings?: StandingsTable[];
};

export type CompetitionInfo = {
  id: string;
  sport: string;
  name: string;
  shortName: string;
  currentSeason: string;
  seasons: string[]; // tutte le stagioni disponibili, dalla più recente
};

export type MetaFile = { generatedAt: string; competitions: CompetitionInfo[] };
