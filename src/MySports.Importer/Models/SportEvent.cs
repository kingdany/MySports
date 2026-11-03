namespace MySports.Importer.Models;

// Evento nel formato comune a tutti gli sport: è ciò che l'app legge dai file JSON.
public sealed record SportEvent
{
    public required string Id { get; init; }              // es. "f1-2026-19-race"
    public required string Competition { get; init; }     // "serie-a" | "f1" | "motogp"
    public required string Season { get; init; }          // "2026" oppure "2026-27" per il calcio
    public int? Round { get; init; }                      // giornata / numero del GP
    public required string Title { get; init; }           // "Mexico City Grand Prix", "Inter - Milan"
    public required string Session { get; init; }         // "match" | "fp1" | "fp2" | "fp3" | "sprint_qualifying" | "sprint" | "qualifying" | "race"
    public required DateTimeOffset StartsAt { get; init; }
    public bool? DateOnly { get; init; }                  // true se la fonte non ha l'orario (stagioni vecchie)
    public string? Venue { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public required EventStatus Status { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public IReadOnlyList<ClassificationRow>? Result { get; init; }  // classifica delle sessioni motoristiche
    public IReadOnlyList<Video> Videos { get; init; } = [];
}

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

public sealed record Video(string VideoId, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt);
