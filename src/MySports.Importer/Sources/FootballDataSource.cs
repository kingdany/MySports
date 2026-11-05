using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Calcio da football-data.org. Serve una chiave; il piano gratuito consente 10 richieste al minuto
// e le stagioni dalla 2023-24 in poi. Le stagioni si chiamano "2026-27", l'API vuole l'anno d'inizio.
public sealed class FootballDataSource(Competition competition, string competitionCode, string? apiKey) : IEventSource
{
    private const string BaseUrl = "https://api.football-data.org/v4/competitions";
    private const int MaxRateLimitRetries = 5;
    private const int ScorersShown = 10;

    // Squadre il cui nome breve sulla fonte non è quello d'uso comune (id football-data.org -> nome).
    private static readonly Dictionary<int, string> ShortNames = new()
    {
        [7397] = "Como",    // "Como 1907"
        [454] = "Venezia",  // "Venezia FC"
        [487] = "Pisa",     // "AC Pisa"
    };

    // Client riservato a questa fonte: la chiave non deve arrivare alle altre API.
    private readonly HttpClient http = CreateClient(apiKey);

    public Competition Competition => competition;

    // Da agosto vale la nuova stagione: prima l'API di solito non ne ha ancora il calendario.
    public string CurrentSeason(DateTimeOffset now) => SeasonName(now.Month >= 8 ? now.Year : now.Year - 1);

    public async Task<IReadOnlyList<SportEvent>> FetchSeasonAsync(string season, CancellationToken ct)
    {
        var data = await GetJsonAsync($"{BaseUrl}/{competitionCode}/matches?season={StartYear(season)}", ct);
        return data["matches"]!.AsArray().Select(m => ToEvent(m!, season)).ToList();
    }

    public async Task<IReadOnlyList<StandingsTable>> FetchStandingsAsync(string season, CancellationToken ct)
    {
        var data = await GetJsonAsync($"{BaseUrl}/{competitionCode}/standings?season={StartYear(season)}", ct);
        var total = data["standings"]?.AsArray().FirstOrDefault(t => Str(t!["type"]) == "TOTAL");
        var rows = total?["table"]?.AsArray().Select(r => ToStandingRow(r!)).ToList() ?? [];

        // Se i marcatori non arrivano l'errore arriva al chiamante, che tiene tutte le classifiche precedenti.
        var scorersData = await GetJsonAsync($"{BaseUrl}/{competitionCode}/scorers?season={StartYear(season)}&limit={ScorersShown}", ct);
        var scorers = ToScorerRows(scorersData["scorers"]?.AsArray().Select(s => s!).ToList() ?? []);

        var tables = new List<StandingsTable>();
        if (rows.Count > 0) tables.Add(new StandingsTable("table", "Classifica", rows));
        if (scorers.Count > 0) tables.Add(new StandingsTable("scorers", "Marcatori", scorers));
        return tables;
    }

    // L'API non indica la posizione: chi ha gli stessi gol ha la stessa posizione (1, 2, 2, 4).
    // Points contiene i gol, così la colonna principale è la stessa delle altre classifiche.
    private static List<StandingRow> ToScorerRows(List<JsonNode> scorers)
    {
        var rows = new List<StandingRow>();
        foreach (var scorer in scorers)
        {
            var goals = scorer["goals"]!.GetValue<int>();
            var pos = rows.Count > 0 && rows[^1].Points == goals ? rows[^1].Pos!.Value : rows.Count + 1;
            rows.Add(new StandingRow
            {
                Pos = pos,
                PosText = pos.ToString(CultureInfo.InvariantCulture),
                Name = Str(scorer["player"]!["name"])!,
                Team = ShortName(scorer["team"]!) ?? Str(scorer["team"]!["name"]),
                Points = goals,
                Played = scorer["playedMatches"]?.GetValue<int>(),
                Assists = scorer["assists"]?.GetValue<int>(),
            });
        }
        return rows;
    }

    private SportEvent ToEvent(JsonNode match, string season)
    {
        var home = ToTeam(match["homeTeam"]!);
        var away = ToTeam(match["awayTeam"]!);
        var status = ToStatus(Str(match["status"])!);
        var startsAt = DateTimeOffset.Parse(Str(match["utcDate"])!, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        // Finché l'orario non è fissato l'API indica mezzanotte UTC e una data provvisoria della giornata.
        var dateOnly = startsAt.TimeOfDay == TimeSpan.Zero;
        if (dateOnly) startsAt = startsAt.AddHours(12);

        // Punteggio solo a fine partita: tra un aggiornamento e l'altro quello parziale sarebbe già vecchio.
        var score = status == EventStatus.Finished ? match["score"]?["fullTime"] : null;

        return new SportEvent
        {
            // L'id della partita sulla fonte non cambia, nemmeno se viene rinviata.
            Id = $"{competition.Id}-{season}-{match["id"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture)}",
            Competition = competition.Id,
            Season = season,
            Round = match["matchday"]?.GetValue<int>(),
            Title = $"{home.ShortName ?? home.Name} - {away.ShortName ?? away.Name}",
            Session = "match",
            StartsAt = startsAt,
            DateOnly = dateOnly ? true : null,
            Status = status,
            HomeTeam = home,
            AwayTeam = away,
            HomeScore = score?["home"]?.GetValue<int>(),
            AwayScore = score?["away"]?.GetValue<int>(),
        };
    }

    private static Team ToTeam(JsonNode team) => new(
        team["id"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture),
        Str(team["name"])!,
        ShortName(team),
        Str(team["crest"]));

    private static string? ShortName(JsonNode team) =>
        ShortNames.GetValueOrDefault(team["id"]!.GetValue<int>()) ?? Str(team["shortName"]);

    private static StandingRow ToStandingRow(JsonNode row)
    {
        var pos = row["position"]!.GetValue<int>();
        return new StandingRow
        {
            Pos = pos,
            PosText = pos.ToString(CultureInfo.InvariantCulture),
            Name = ShortName(row["team"]!) ?? Str(row["team"]!["name"])!,
            Code = Str(row["team"]!["tla"]),
            Points = row["points"]!.GetValue<int>(),
            Played = row["playedGames"]!.GetValue<int>(),
            Wins = row["won"]!.GetValue<int>(),
            Draws = row["draw"]!.GetValue<int>(),
            Losses = row["lost"]!.GetValue<int>(),
            GoalDifference = row["goalDifference"]!.GetValue<int>(),
        };
    }

    private static EventStatus ToStatus(string status) => status switch
    {
        "IN_PLAY" or "PAUSED" or "EXTRA_TIME" or "PENALTY_SHOOTOUT" => EventStatus.Live,
        "FINISHED" or "AWARDED" => EventStatus.Finished,
        "POSTPONED" or "SUSPENDED" => EventStatus.Postponed,
        "CANCELLED" or "CANCELED" => EventStatus.Cancelled,
        _ => EventStatus.Scheduled, // "SCHEDULED", "TIMED"
    };

    private Task<JsonNode> GetJsonAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("FOOTBALL_DATA_API_KEY non impostata");
        return HttpJson.GetAsync(http, url, "football-data.org", MaxRateLimitRetries, ct);
    }

    private static HttpClient CreateClient(string? apiKey)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MySports/0.1");
        if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
        return client;
    }

    // 2026 -> "2026-27".
    private static string SeasonName(int startYear) =>
        string.Create(CultureInfo.InvariantCulture, $"{startYear}-{(startYear + 1) % 100:00}");

    // "2026-27" -> "2026".
    private static string StartYear(string season) =>
        Regex.IsMatch(season, @"^\d{4}-\d{2}$")
            ? season[..4]
            : throw new ArgumentException($"Stagione \"{season}\" non valida: il formato è 2026-27");

    private static string? Str(JsonNode? node) => node?.GetValue<string>();
}
