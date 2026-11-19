using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Calcio dall'API pubblica non documentata di ESPN (senza chiave e senza quote), per le competizioni che football-data.org
// non ha nel piano gratuito, come l'Europa League (lega "uefa.europa"). Verificato a novembre 2026.
// Le partite: "scoreboard?dates=2026" dà un anno solare intero (un intervallo di date invece non dà niente), quindi una
// stagione sono due anni, filtrati per "season.year" (l'anno d'inizio). I preliminari non ci sono.
// Le classifiche: dal 2024-25 una tabella sola ("League Phase"); prima c'erano i gironi, che l'app non gestisce, quindi la
// classifica si salva solo se la tabella è una. I marcatori arrivano da "statistics" (goalsLeaders, 50 nomi).
// Le squadre hanno id ESPN, diversi da quelli di football-data: per questo hanno il prefisso "espn-", e l'app le
// riconosce tra le competizioni per nome (la Roma in Serie A e in Europa League), quindi i nomi delle italiane devono
// essere quelli di football-data.
public sealed partial class EspnFootballSource(Competition competition, string league, IReadOnlyList<FootballDataSource.ZoneRule> zones)
    : IEventSource
{
    private const string BaseUrl = "https://site.api.espn.com/apis";

    // Nomi brevi ESPN da cambiare (id ESPN -> nome): abbreviazioni ("Spurs", "C Palace"), prefissi che in italiano non si
    // usano ("FC Porto"), "AS Roma" per il "Roma" della Serie A, e i nomi italiani già usati per la Champions League
    // (FootballDataSource.ShortNames). I nomi che usano i canali YouTube ("Nizza", "Lione") sono alias in
    // YouTubeHighlights.TeamAliases: le partite hanno il nome della fonte, come in Champions League.
    private static readonly Dictionary<string, string> ShortNames = new()
    {
        ["104"] = "Roma",                 // "AS Roma"
        ["437"] = "Porto",                // "FC Porto"
        ["152"] = "Twente",               // "FC Twente"
        ["153"] = "Utrecht",              // "FC Utrecht"
        ["989"] = "Basilea",              // "FC Basel"
        ["620"] = "Brann",                // "SK Brann"
        ["367"] = "Tottenham",            // "Spurs"
        ["360"] = "Manchester United",    // "Man Utd"
        ["384"] = "Crystal Palace",       // "C Palace"
        ["393"] = "Nottingham Forest",    // "Nottm Forest"
        ["134"] = "Stoccarda",            // "Stuttgart"
        ["494"] = "Slavia Praga",         // "Slavia Prague"
        ["433"] = "Sparta Praga",         // "Sparta"
        ["2290"] = "Stella Rossa",        // "Red Star"
        ["440"] = "Dinamo Kiev",          // "Dynamo Kyiv"
        ["2980"] = "Bodø/Glimt",          // "Bodo/Glimt", come football-data
        ["10414"] = "Qarabağ",            // "FK Qarabag"
        ["13083"] = "Hapoel Be'er Sheva", // "Hapoel Be'er"
        // Conference League (e le stesse squadre in Europa League): i nomi scelti dall'utente, che valgono anche in
        // Champions League (FootballDataSource.ShortNames) per le squadre che hanno giocato in entrambe.
        ["521"] = "Slovan Bratislava",          // "S Bratislava"
        ["22281"] = "Pafos",                    // come ESPN, anche in Champions League
        ["2528"] = "Kairat Almaty",             // come ESPN, anche in Champions League
        ["887"] = "AEK Atene",                  // "AEK Athens"
        ["12124"] = "Astana",                   // "FC Astana"
        ["7672"] = "Lugano",                    // "FC Lugano"
        ["20712"] = "Noah",                     // "FC Noah"
        ["3024"] = "Thun",                      // "FC Thun"
        ["20713"] = "TSC Bačka Topola",         // "FK TSC"
        ["3611"] = "Gent",                      // "KAA Gent"
        ["4411"] = "LASK",                      // "LASK Linz", come football-data
        ["2564"] = "Shamrock Rovers",           // "Shamrock"
        ["101"] = "Rayo Vallecano",             // "Rayo"
        ["17856"] = "Lincoln Red Imps",         // "Red Imps"
        ["19243"] = "Drita",                    // "Drita Gjilan"
        ["8151"] = "Škendija",                  // "KF Shkëndija"
        ["8169"] = "KuPS",                      // "KuPS Kuopio"
        ["6758"] = "Mladá Boleslav",            // "M Boleslav"
        ["5309"] = "Vitória Guimarães",         // "Vitória"
        ["7914"] = "İstanbul Başakşehir",       // "Istanbul BB"
        ["7834"] = "Häcken",                    // "BK Häcken"
        ["7858"] = "Ħamrun Spartans",           // "Hamrun Spartans"
        ["909"] = "Copenaghen",                 // "København"
        ["8089"] = "Universitatea Craiova",     // "CSU Craiova"
        ["936"] = "Sint-Truiden",               // "Sint-Truidense"
        ["489"] = "Hajduk Spalato",             // "Hajduk Split"
        ["126"] = "Friburgo",                   // "Freiburg"
        ["20710"] = "Borac Banja Luka",         // "Borac"
        ["7853"] = "Aarhus",                    // "AGF"
        ["20703"] = "Inter Club d'Escaldes",    // "Inter D'Escaldes"
        ["20028"] = "Kauno Žalgiris",           // "Kauno Zalgiris"
        ["180"] = "Strasburgo",                 // "Strasbourg"
        ["21005"] = "Raków Częstochowa",        // "Raków"
        ["493"] = "Shakhtar Donetsk",           // "Shakhtar"
        ["2950"] = "Magonza",                   // "Mainz"
        ["11551"] = "Losanna",                  // "Lausanne Sports"
        ["2990"] = "Lech Poznań",               // "Lech Poznan"
        ["3362"] = "Celje",                     // "NK Celje"
        ["11505"] = "Jagiellonia Białystok",    // "Jagiellonia"
        ["5239"] = "Zrinjski Mostar",           // "Zrinjski"
        ["555"] = "Legia Varsavia",             // "Legia Warsaw"
        ["8250"] = "Breiðablik",                // "Breidablik"
        ["2497"] = "APOEL",                     // "Apoel"
        ["500"] = "Olimpija Lubiana",           // "Olimpija"
        ["244"] = "Real Betis",                 // "Betis"
        ["8249"] = "Víkingur Reykjavík",        // "Vikingur"
        ["557"] = "San Gallo",                  // "St. Gallen"
        ["19250"] = "Petrocub Hîncești",        // "Petrocub"
        // Europa League: i nomi scelti dall'utente, anche questi uguali in Champions League.
        ["167"] = "Lione",                      // "Lyon"
        ["131"] = "Bayer Leverkusen",           // "Leverkusen"
        ["597"] = "Dinamo Zagabria",            // "Dinamo Zagreb"
        ["2790"] = "Salisburgo",                // "RB Salzburg"
        ["1010"] = "OFI Creta",                 // "OFI Crete"
        ["1895"] = "Beşiktaş",                  // "Besiktas"
        ["176"] = "Marsiglia",                  // "Marseille"
        ["622"] = "Ferencváros",                // "Ferencvaros"
        ["147"] = "NEC Nijmegen",               // "NEC"
        ["987"] = "Lillestrøm",                 // "Lillestrom"
        ["11706"] = "Viktoria Plzeň",           // "Viktoria Plzen"
        ["5807"] = "Union Saint-Gilloise",      // "Union SG"
        ["524"] = "Maccabi Tel Aviv",           // "Maccabi Tel-Aviv"
        ["436"] = "Fenerbahçe",                 // "Fenerbahce"
        ["2502"] = "Nizza",                     // "Nice"
        ["166"] = "Lilla",                      // "Lille"
        ["93"] = "Athletic Bilbao",             // "Athletic"
        ["125"] = "Eintracht Francoforte",      // "Frankfurt"
    };

    // Fasi dell'eliminazione diretta ("season.slug") -> stage dell'app. Fase campionato e gironi restano giornate.
    private static readonly Dictionary<string, string> Stages = new()
    {
        ["knockout-round-playoffs"] = "playoffs",
        ["round-of-16"] = "last_16",
        ["quarterfinals"] = "quarter_finals",
        ["semifinals"] = "semi_finals",
        ["final"] = "final",
    };

    // La fase campionato non ha il numero della giornata: si contano i gruppi di giorni di gioco, separati da più di
    // MatchdayGap (le giornate sono a settimane di distanza, una giornata dura al massimo due giorni).
    private static readonly TimeSpan MatchdayGap = TimeSpan.FromDays(3);

    public Competition Competition => competition;

    // Da agosto vale la nuova stagione, come per football-data.
    public string CurrentSeason(DateTimeOffset now) => SeasonName(now.Month >= 8 ? now.Year : now.Year - 1);

    public async Task<IReadOnlyList<SportEvent>> FetchSeasonAsync(string season, CancellationToken ct)
    {
        var startYear = StartYear(season);
        var matches = new Dictionary<string, JsonNode>();
        foreach (var year in new[] { startYear, startYear + 1 })
        {
            var board = await GetJsonAsync($"{BaseUrl}/site/v2/sports/soccer/{league}/scoreboard?dates={year}&limit=1000", ct);
            foreach (var e in board["events"]?.AsArray() ?? [])
                if (e?["season"]?["year"]?.GetValue<int>() == startYear && Str(e["id"]) is { } id)
                    matches.TryAdd(id, e);
        }

        var matchdays = Matchdays(matches.Values.Where(e => Str(e["season"]?["slug"]) == "league-phase").Select(StartsAt));
        return matches.Values
            .Select(e => ToEvent(e, season, matchdays))
            .OfType<SportEvent>()
            .OrderBy(e => e.StartsAt)
            .ToList();
    }

    public async Task<IReadOnlyList<StandingsTable>> FetchStandingsAsync(string season, CancellationToken ct)
    {
        var startYear = StartYear(season);
        var data = await GetJsonAsync($"{BaseUrl}/v2/sports/soccer/{league}/standings?season={startYear}", ct);
        var groups = data["children"]?.AsArray() ?? [];
        var entries = groups.SelectMany(g => g?["standings"]?["entries"]?.AsArray() ?? []).OfType<JsonNode>().ToList();
        var rows = groups.Count == 1 ? entries.Select(ToStandingRow).OrderBy(r => r.Pos).ToList() : [];
        rows = rows.Select(r => r with { Zone = zones.FirstOrDefault(z => r.Pos >= z.From && r.Pos <= z.To)?.Zone }).ToList();

        // Se i marcatori non arrivano l'errore arriva al chiamante, che tiene tutte le classifiche precedenti.
        // La loro squadra non ha il nome breve ("Ludogorets Razgrad"): si prende quello della classifica, per id.
        var teamNames = entries
            .Select(e => e["team"])
            .Where(t => Str(t?["id"]) is not null)
            .GroupBy(t => Str(t!["id"])!)
            .ToDictionary(g => g.Key, g => ShortName(g.First()!));
        var stats = await GetJsonAsync($"{BaseUrl}/site/v2/sports/soccer/{league}/statistics?season={startYear}", ct);
        var leaders = stats["stats"]?.AsArray().FirstOrDefault(s => Str(s?["name"]) == "goalsLeaders")?["leaders"]?.AsArray() ?? [];
        var scorers = FootballDataSource.ScorerRows(leaders
            .Where(l => l?["athlete"] is not null)
            .Select(l => (
                Str(l!["athlete"]!["displayName"]) ?? "",
                l["athlete"]!["team"] is { } team ? teamNames.GetValueOrDefault(Str(team["id"]) ?? "") ?? ShortName(team) : null,
                (int)(l["value"]?.GetValue<double>() ?? 0))));

        var tables = new List<StandingsTable>();
        if (rows.Count > 0) tables.Add(new StandingsTable("table", "Classifica", rows));
        if (scorers.Count > 0) tables.Add(new StandingsTable("scorers", "Marcatori", scorers));
        return tables;
    }

    private SportEvent? ToEvent(JsonNode match, string season, IReadOnlyList<DateTime> matchdays)
    {
        var competitionNode = match["competitions"]?[0];
        var competitors = competitionNode?["competitors"]?.AsArray() ?? [];
        var homeNode = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "home");
        var awayNode = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "away");
        if (competitionNode is null || Str(match["id"]) is not { } id) return null;

        var home = homeNode?["team"] is { } h ? ToTeam(h) : null;
        var away = awayNode?["team"] is { } a ? ToTeam(a) : null;
        var status = ToStatus(competitionNode["status"]?["type"]);

        // Senza orario fissato ("timeValid": false) si tiene solo la data, a mezzogiorno UTC come per football-data.
        var startsAt = StartsAt(match);
        var dateOnly = competitionNode["timeValid"]?.GetValue<bool>() == false;
        if (dateOnly) startsAt = new DateTimeOffset(startsAt.UtcDateTime.Date.AddHours(12), TimeSpan.Zero);

        var slug = Str(match["season"]?["slug"]);
        var stage = slug is null ? null : Stages.GetValueOrDefault(slug);
        var round = stage is not null
            ? competitionNode["leg"]?["value"]?.GetValue<int>() // andata e ritorno; la finale non ce l'ha
            : slug == "league-phase" ? Matchday(matchdays, startsAt) : null;

        // Punteggio solo a fine partita, come per football-data. I rigori sono a parte ("shootoutScore").
        var finished = status == EventStatus.Finished;
        return new SportEvent
        {
            Id = $"{competition.Id}-{season}-{id}",
            Competition = competition.Id,
            Season = season,
            Round = round,
            Stage = stage,
            Title = $"{TeamLabel(home)} - {TeamLabel(away)}",
            Session = "match",
            StartsAt = startsAt,
            DateOnly = dateOnly ? true : null,
            Status = status,
            HomeTeam = home,
            AwayTeam = away,
            HomeScore = finished ? Score(homeNode?["score"]) : null,
            AwayScore = finished ? Score(awayNode?["score"]) : null,
            HomePenalties = finished ? Score(homeNode?["shootoutScore"]) : null,
            AwayPenalties = finished ? Score(awayNode?["shootoutScore"]) : null,
        };
    }

    private static DateTimeOffset StartsAt(JsonNode match) =>
        DateTimeOffset.Parse(Str(match["date"])!, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    // Primo giorno di ogni giornata della fase campionato, in ordine.
    private static List<DateTime> Matchdays(IEnumerable<DateTimeOffset> dates)
    {
        var days = dates.Select(d => d.UtcDateTime.Date).Distinct().Order().ToList();
        return days.Where((day, i) => i == 0 || day - days[i - 1] > MatchdayGap).ToList();
    }

    private static int? Matchday(IReadOnlyList<DateTime> starts, DateTimeOffset startsAt)
    {
        var day = startsAt.UtcDateTime.Date;
        var index = starts.Count(s => s <= day);
        return index == 0 ? null : index;
    }

    private static EventStatus ToStatus(JsonNode? type) => (Str(type?["name"]), Str(type?["state"])) switch
    {
        ("STATUS_POSTPONED" or "STATUS_SUSPENDED" or "STATUS_DELAYED", _) => EventStatus.Postponed,
        ("STATUS_CANCELED" or "STATUS_ABANDONED", _) => EventStatus.Cancelled,
        (_, "in") => EventStatus.Live,
        (_, "post") => type?["completed"]?.GetValue<bool>() == true ? EventStatus.Finished : EventStatus.Postponed,
        _ => EventStatus.Scheduled,
    };

    private static string TeamLabel(Team? team) => team?.ShortName ?? team?.Name ?? "Da definire";

    private static Team? ToTeam(JsonNode team) => Str(team["id"]) is not { } id ? null : new(
        $"espn-{id}",
        (Str(team["displayName"]) ?? "").Trim(),
        ShortName(team),
        Str(team["logo"]) ?? Str(team["logos"]?[0]?["href"]));

    // Il nome breve di ESPN, salvo quelli in ShortNames. Alcuni hanno spazi in fondo ("Jagiellonia ").
    private static string? ShortName(JsonNode team) =>
        ShortNames.GetValueOrDefault(Str(team["id"]) ?? "")
        ?? (Str(team["shortDisplayName"]) ?? Str(team["displayName"]))?.Trim();

    private static StandingRow ToStandingRow(JsonNode row)
    {
        var stats = (row["stats"]?.AsArray() ?? [])
            .Where(s => Str(s?["name"]) is not null && s!["value"] is JsonValue)
            .GroupBy(s => Str(s!["name"])!)
            .ToDictionary(g => g.Key, g => (int)g.First()!["value"]!.GetValue<double>());
        var pos = stats.GetValueOrDefault("rank");
        return new StandingRow
        {
            Pos = pos,
            PosText = pos.ToString(CultureInfo.InvariantCulture),
            Name = ShortName(row["team"]!) ?? "",
            Code = Str(row["team"]!["abbreviation"]),
            Points = stats.GetValueOrDefault("points"),
            Played = stats.GetValueOrDefault("gamesPlayed"),
            Wins = stats.GetValueOrDefault("wins"),
            Draws = stats.GetValueOrDefault("ties"),
            Losses = stats.GetValueOrDefault("losses"),
            GoalDifference = stats.GetValueOrDefault("pointDifferential"),
        };
    }

    private static int? Score(JsonNode? score) => score switch
    {
        null => null,
        JsonValue v when v.TryGetValue<int>(out var n) => n,
        JsonValue v when v.TryGetValue<double>(out var d) => (int)d,
        _ => int.TryParse(Str(score), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null,
    };

    private static Task<JsonNode> GetJsonAsync(string url, CancellationToken ct) =>
        HttpJson.GetAsync(EspnMatchDetails.Http, url, "ESPN", 2, ct);

    // 2026 -> "2026-27".
    private static string SeasonName(int startYear) =>
        string.Create(CultureInfo.InvariantCulture, $"{startYear}-{(startYear + 1) % 100:00}");

    // "2026-27" -> 2026.
    private static int StartYear(string season) =>
        SeasonPattern().IsMatch(season)
            ? int.Parse(season[..4], CultureInfo.InvariantCulture)
            : throw new ArgumentException($"Stagione \"{season}\" non valida: il formato è 2026-27");

    [GeneratedRegex(@"^\d{4}-\d{2}$")]
    private static partial Regex SeasonPattern();

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node?.ToString();
}
