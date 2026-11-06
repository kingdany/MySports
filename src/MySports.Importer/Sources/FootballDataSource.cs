using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Calcio da football-data.org. Serve una chiave; il piano gratuito consente 10 richieste al minuto
// e le stagioni dalla 2023-24 in poi. Le stagioni si chiamano "2026-27", l'API vuole l'anno d'inizio.
public sealed class FootballDataSource(
    Competition competition, string competitionCode, string? apiKey,
    IReadOnlyList<FootballDataSource.ZoneRule> zones,
    IReadOnlyDictionary<string, IReadOnlyList<FootballDataSource.ZoneRule>>? zoneExceptions = null) : IEventSource
{
    // Posizioni di classifica (estremi inclusi) che danno accesso a una zona, es. "champions".
    public sealed record ZoneRule(int From, int To, string Zone);

    // L'API non indica le zone, quindi si ricavano dalla posizione. Valgono per ogni stagione senza eccezioni.
    public static readonly IReadOnlyList<ZoneRule> SerieAZones =
    [
        new(1, 4, "champions"),
        new(5, 5, "europa"),
        new(6, 6, "conference"),
        new(18, 20, "relegation"),
    ];

    // Stagioni in cui i posti europei effettivi sono stati diversi, per Coppa Italia e ranking UEFA.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<ZoneRule>> SerieAZoneExceptions =
        new Dictionary<string, IReadOnlyList<ZoneRule>>
        {
            ["2025-26"] = [new(1, 4, "champions"), new(5, 6, "europa"), new(7, 7, "conference"), new(18, 20, "relegation")],
            // Bologna nono in Europa League da vincitore della Coppa Italia.
            ["2024-25"] = [new(1, 4, "champions"), new(5, 5, "europa"), new(6, 6, "conference"), new(9, 9, "europa"), new(18, 20, "relegation")],
            // Quinto posto in Champions dal ranking UEFA.
            ["2023-24"] = [new(1, 5, "champions"), new(6, 7, "europa"), new(8, 8, "conference"), new(18, 20, "relegation")],
        };

    // Fase campionato della Champions (dal 2024-25): le prime 8 agli ottavi, dalla 9ª alla 24ª agli spareggi.
    public static readonly IReadOnlyList<ZoneRule> ChampionsLeagueZones =
    [
        new(1, 8, "round_of_16"),
        new(9, 24, "playoff"),
        new(25, 36, "eliminated"),
    ];

    private const string BaseUrl = "https://api.football-data.org/v4/competitions";
    private const int MaxRateLimitRetries = 5;
    private const int ScorersShown = 10;
    private const int ScorersFetched = 100; // di più di quelli mostrati, per non tagliare i pari merito

    // Squadre il cui nome breve sulla fonte non è quello d'uso comune (id football-data.org -> nome).
    private static readonly Dictionary<int, string> ShortNames = new()
    {
        [7397] = "Como",    // "Como 1907"
        [454] = "Venezia",  // "Venezia FC"
        [487] = "Pisa",     // "AC Pisa"
        [1899] = "AEK Atene",          // "PAE AEK"
        [81] = "Barcellona",           // "Barça"
        [78] = "Atletico Madrid",      // "Atleti"
        [721] = "Lipsia",              // "RB Leipzig"
        [10] = "Stoccarda",            // "Stuttgart"
        [930] = "Slavia Praga",        // "Slavia Praha"
        [7509] = "Slovan Bratislava",  // "Sl. Bratislava"
        [1887] = "Shakhtar",           // "Shaktar"
        [546] = "Lens",                // "RC Lens"
        [10233] = "Sabah",             // "Sabah FK"
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

        var seasonZones = zoneExceptions?.GetValueOrDefault(season) ?? zones;
        rows = rows.Select(r => r with { Zone = seasonZones.FirstOrDefault(z => r.Pos >= z.From && r.Pos <= z.To)?.Zone }).ToList();

        // Se i marcatori non arrivano l'errore arriva al chiamante, che tiene tutte le classifiche precedenti.
        var scorersData = await GetJsonAsync($"{BaseUrl}/{competitionCode}/scorers?season={StartYear(season)}&limit={ScorersFetched}", ct);
        var scorers = ToScorerRows(scorersData["scorers"]?.AsArray().Select(s => s!).ToList() ?? []);

        var tables = new List<StandingsTable>();
        if (rows.Count > 0) tables.Add(new StandingsTable("table", "Classifica", rows));
        if (scorers.Count > 0) tables.Add(new StandingsTable("scorers", "Marcatori", scorers));
        return tables;
    }

    // L'API non indica la posizione: chi ha gli stessi gol ha lo stesso numero, senza salti (1, 2, 2, 3).
    // L'elenco si chiude quando i nomi sono almeno 10, completando il gruppo di pari merito in corso,
    // quindi l'ultimo numero può essere basso e i nomi più di 10.
    // Points contiene i gol, così la colonna principale è la stessa delle altre classifiche.
    private static List<StandingRow> ToScorerRows(List<JsonNode> scorers)
    {
        var rows = new List<StandingRow>();
        var pos = 0;
        foreach (var scorer in scorers)
        {
            var goals = scorer["goals"]!.GetValue<int>();
            if (rows.Count == 0 || rows[^1].Points != goals)
            {
                if (rows.Count >= ScorersShown) break;
                pos++;
            }
            rows.Add(new StandingRow
            {
                Pos = pos,
                PosText = pos.ToString(CultureInfo.InvariantCulture),
                Name = Str(scorer["player"]!["name"])!,
                Team = ShortName(scorer["team"]!) ?? Str(scorer["team"]!["name"]),
                Points = goals,
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
        var score = status == EventStatus.Finished ? match["score"] : null;
        var fullTime = score?["fullTime"];
        var penalties = score?["penalties"];
        var homePenalties = penalties?["home"]?.GetValue<int>();
        var awayPenalties = penalties?["away"]?.GetValue<int>();

        return new SportEvent
        {
            // L'id della partita sulla fonte non cambia, nemmeno se viene rinviata.
            Id = $"{competition.Id}-{season}-{match["id"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture)}",
            Competition = competition.Id,
            Season = season,
            Round = match["matchday"]?.GetValue<int>(),
            Stage = ToStage(Str(match["stage"])),
            Title = $"{TeamLabel(home)} - {TeamLabel(away)}",
            Session = "match",
            StartsAt = startsAt,
            DateOnly = dateOnly ? true : null,
            Status = status,
            HomeTeam = home,
            AwayTeam = away,
            // Il fullTime della fonte comprende i rigori (un 1-1 finito 4-3 ai rigori vale 5-4).
            HomeScore = fullTime?["home"]?.GetValue<int>() - (homePenalties ?? 0),
            AwayScore = fullTime?["away"]?.GetValue<int>() - (awayPenalties ?? 0),
            HomePenalties = homePenalties,
            AwayPenalties = awayPenalties,
        };
    }

    // Solo per l'eliminazione diretta: campionato e gironi si mostrano come semplici giornate.
    private static string? ToStage(string? stage) =>
        stage is null or "REGULAR_SEASON" or "LEAGUE_STAGE" or "GROUP_STAGE" ? null : stage.ToLowerInvariant();

    private static string TeamLabel(Team? team) => team?.ShortName ?? team?.Name ?? "Da definire";

    // null se la squadra non è ancora nota (eliminazione diretta prima del sorteggio).
    private static Team? ToTeam(JsonNode team) => team["id"] is null ? null : new(
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
