namespace MySports.Importer.Models;

// Evento nel formato comune a tutti gli sport: è ciò che l'app legge dai file JSON.
public sealed record SportEvent
{
    public required string Id { get; init; }              // es. "f1-2026-19-race", "serie-a-2026-27-558629"
    public required string Competition { get; init; }     // "serie-a" | "champions-league" | "f1" | "motogp"
    public required string Season { get; init; }          // "2026" oppure "2026-27" per il calcio
    public int? Round { get; init; }                      // giornata / numero del GP; nell'eliminazione diretta 1 = andata, 2 = ritorno
    public string? Stage { get; init; }                   // eliminazione diretta: "playoffs" | "last_16" | "quarter_finals" | "semi_finals" | "final"
    public required string Title { get; init; }           // "Mexico City Grand Prix", "Inter - Milan"
    public required string Session { get; init; }         // "match" | "fp1" | "fp2" | "fp3" | "sprint_qualifying" | "sprint" | "qualifying" | "race"; MotoGP anche "fp4", "practice" (pre-qualifiche) e "warm_up"
    public required DateTimeOffset StartsAt { get; init; }
    public bool? DateOnly { get; init; }                  // true se la fonte non ha l'orario (stagioni vecchie, partite ancora da fissare)
    public string? Venue { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public required EventStatus Status { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public int? HomePenalties { get; init; }              // rigori, non compresi nel punteggio
    public int? AwayPenalties { get; init; }
    public IReadOnlyList<ClassificationRow>? Result { get; init; }  // classifica delle sessioni motoristiche
    public IReadOnlyList<Video> Videos { get; init; } = [];
    public MatchDetails? Details { get; init; }           // calcio, solo partite finite: cronologia e formazioni
}

// Dettaglio di una partita finita (da ESPN). Incidents in ordine di tempo; Home/Away assenti se la fonte non ha le formazioni.
// Version: formato del dettaglio; uno salvato con un formato più vecchio si riscarica (assente = 1, senza Short).
public sealed record MatchDetails
{
    public int? Version { get; init; }
    public IReadOnlyList<MatchIncident> Incidents { get; init; } = [];
    public Lineup? Home { get; init; }
    public Lineup? Away { get; init; }
}

// Type: "goal" | "penalty" | "ownGoal" | "missedPenalty" | "yellow" | "secondYellow" | "red".
// Side: "home" | "away", la squadra a cui va l'evento: per l'autogol quella che segna, non quella del giocatore.
// Minute e AddedTime: 45 e 2 per "45'+2'". Player: nome completo; Short: cognome, assente se uguale al nome completo.
public sealed record MatchIncident(int Minute, int? AddedTime, string Type, string Side, string Player, string? Short = null);

public sealed record Lineup(IReadOnlyList<LineupPlayer> Starters, IReadOnlyList<Substitution> Substitutions);

// Name: nome completo; Short: cognome, assente se uguale al nome completo (giocatori noti con un nome solo, es. "Bremer").
public sealed record LineupPlayer(int? Number, string Name, string? Short = null);

public sealed record Substitution(int Minute, int? AddedTime, LineupPlayer In, LineupPlayer Out);

public enum EventStatus { Scheduled, Live, Finished, Postponed, Cancelled }

public sealed record Team(string Id, string Name, string? ShortName, string? LogoUrl);

public sealed record ClassificationRow
{
    public required int Pos { get; init; }
    public string? Number { get; init; }
    public required string Driver { get; init; }
    public string? Code { get; init; }
    public required string Team { get; init; }
    public string? Time { get; init; }      // tempo o distacco; stato ("DNF", "+1 Lap") se non classificato
    public double? Points { get; init; }
    public string? Q1 { get; init; }
    public string? Q2 { get; init; }
    public string? Q3 { get; init; }
}

// Source: nome del canale (es. "Formula 1"); Duration: "8:08". Entrambi assenti nei video salvati prima di averli.
// Embeddable: false se il video si può vedere solo su YouTube; assente negli altri casi.
public sealed record Video(string VideoId, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt, string? Source = null, string? Duration = null, bool? Embeddable = null);
