namespace MySports.Importer.Models;

// Una classifica della stagione (es. "Piloti", "Costruttori"; per il calcio "Classifica", "Marcatori").
public sealed record StandingsTable(string Id, string Title, IReadOnlyList<StandingRow> Rows);

public sealed record StandingRow
{
    public int? Pos { get; init; }            // null se non classificato (es. squalifica dal campionato)
    public required string PosText { get; init; }  // "1", "2", ... oppure "D" (squalificato), "-" ...
    public required string Name { get; init; }
    public string? Code { get; init; }
    public string? Team { get; init; }        // scuderia del pilota (più di una se ha cambiato durante l'anno)
    public required double Points { get; init; }   // gol nella classifica marcatori
    public int? Wins { get; init; }
    public int? Played { get; init; }         // questi ultimi solo per il calcio
    public int? Draws { get; init; }
    public int? Losses { get; init; }
    public int? GoalDifference { get; init; }
    public string? Zone { get; init; }        // "champions" | "europa" | "conference" | "relegation"
}
