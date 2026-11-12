using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Dettaglio delle partite finite (marcatori, rigori sbagliati, cartellini, formazioni) dall'API pubblica non documentata
// di ESPN, senza chiave. football-data.org li ha solo a pagamento.
// Le partite si abbinano a quelle di football-data per giorno, risultato e squadre: una chiamata "scoreboard" per giorno
// dà gli id ESPN, poi una chiamata "summary" per partita. Ogni partita si legge una volta: il dettaglio resta
// dall'esecuzione precedente, come i video.
// Le squadre si confrontano per parole in comune tra i nomi (ESPN "RB Leipzig", football-data "RasenBallsport Leipzig" e
// "Lipsia"), perché i nomi diversi sono tanti: in Champions League 37 su 3 stagioni. Verificato il 12/11/2026 su tutte le
// stagioni pubblicate: Champions League 2024-25, 2025-26, 2026-27 e Serie A 2026-27, tutte le partite abbinate.
public sealed partial class EspnMatchDetails(string league)
{
    private const string BaseUrl = "https://site.api.espn.com/apis/site/v2/sports/soccer";

    // Formato del dettaglio salvato: un dettaglio di una versione diversa si riscarica. 2: cognome dei giocatori (Short).
    private const int CurrentVersion = 2;

    // Client proprio: ESPN a volte risponde compresso con gzip anche senza chiederlo.
    private static readonly HttpClient Http = CreateClient();

    // Nomi ESPN senza parole in comune con quelli di football-data.
    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Red Star Belgrade"] = "Crvena Zvezda",
        ["Pafos"] = "Paphos",
        ["Olympiacos"] = "Olympiakos",
    };

    // Parole dei nomi che non distinguono una squadra ("FC", "Club"...). "Sporting" no: è il nome dello Sporting.
    private static readonly HashSet<string> CommonWords =
        ["fc", "cf", "ac", "as", "sc", "sk", "fk", "afc", "ssc", "club", "calcio", "de", "of", "the", "sport"];

    // Tipi di evento ESPN che contano come gol. Il tiro (testa, punizione, al volo) non interessa.
    private static readonly Dictionary<string, string> GoalTypes = new()
    {
        ["goal"] = "goal",
        ["goal---header"] = "goal",
        ["goal---free-kick"] = "goal",
        ["goal---volley"] = "goal",
        ["penalty---scored"] = "penalty",
        ["own-goal"] = "ownGoal",
    };

    // Leghe ESPN.
    public static EspnMatchDetails SerieA() => new("ita.1");
    public static EspnMatchDetails ChampionsLeague() => new("uefa.champions");

    // Aggiunge il dettaglio alle partite finite che non lo hanno ancora o lo hanno in un formato vecchio. Se non si riesce
    // a riscaricarlo, il dettaglio vecchio resta.
    public async Task<IReadOnlyList<SportEvent>> AttachAsync(IReadOnlyList<SportEvent> events, CancellationToken ct)
    {
        var waiting = events
            .Where(e => e.Session == "match" && e.Status == EventStatus.Finished && (e.Details?.Version ?? 1) != CurrentVersion
                        && e.HomeScore is not null && e.AwayScore is not null && e.HomeTeam is not null && e.AwayTeam is not null)
            .ToList();
        if (waiting.Count == 0) return events;

        var found = new Dictionary<string, MatchDetails>();
        var missing = new List<string>();
        var used = new HashSet<string>(); // una partita ESPN non deve finire su due partite
        foreach (var day in waiting.GroupBy(e => e.StartsAt.UtcDateTime.Date))
        {
            JsonArray espnEvents;
            try
            {
                var board = await GetJsonAsync($"{BaseUrl}/{league}/scoreboard?dates={day.Key:yyyyMMdd}", ct);
                espnEvents = board["events"]?.AsArray() ?? [];
            }
            catch (Exception ex)
            {
                missing.AddRange(day.Select(e => $"{e.Title} del {e.StartsAt:yyyy-MM-dd}: calendario ESPN non letto ({ex.Message})"));
                continue;
            }

            foreach (var match in day)
            {
                var espnId = FindEspnId(espnEvents, match);
                if (espnId is null || !used.Add(espnId))
                {
                    missing.Add($"{match.Title} del {match.StartsAt:yyyy-MM-dd}: " +
                                (espnId is null ? "partita non trovata su ESPN" : $"partita ESPN {espnId} già abbinata a un'altra"));
                    continue;
                }
                try
                {
                    var summary = await GetJsonAsync($"{BaseUrl}/{league}/summary?event={espnId}", ct);
                    var details = ToDetails(summary);
                    // Appena finita la partita ESPN può essere ancora incompleto: si riprova al giro dopo.
                    var homeGoals = details.Incidents.Count(i => i.Side == "home" && IsGoal(i));
                    var awayGoals = details.Incidents.Count(i => i.Side == "away" && IsGoal(i));
                    if (homeGoals != match.HomeScore || awayGoals != match.AwayScore)
                        missing.Add($"{match.Title} del {match.StartsAt:yyyy-MM-dd}: gol ESPN {homeGoals}-{awayGoals}, risultato {match.HomeScore}-{match.AwayScore}");
                    else
                        found[match.Id] = details;
                }
                catch (Exception ex)
                {
                    missing.Add($"{match.Title} del {match.StartsAt:yyyy-MM-dd}: dettaglio ESPN non letto ({ex.Message})");
                }
            }
        }

        Console.WriteLine($"Dettaglio partite ESPN: {found.Count} su {waiting.Count} aggiunte");
        if (missing.Count > 0)
        {
            Console.WriteLine($"Dettaglio partite ESPN, mancanti: {missing.Count}");
            foreach (var m in missing.Take(10)) Console.WriteLine($"  {m}");
            if (missing.Count > 10) Console.WriteLine($"  ... e altri {missing.Count - 10}");
        }
        return found.Count == 0 ? events : events.Select(e => found.TryGetValue(e.Id, out var d) ? e with { Details = d } : e).ToList();
    }

    private static bool IsGoal(MatchIncident i) => i.Type is "goal" or "penalty" or "ownGoal";

    // Tra le partite ESPN concluse dello stesso giorno con lo stesso risultato, quella con entrambe le squadre uguali; se
    // non c'è, l'unica con almeno una squadra uguale (un nome non riconosciuto non basta a perdere la partita).
    private static string? FindEspnId(JsonArray espnEvents, SportEvent match)
    {
        var candidates = new List<(string Id, bool Home, bool Away)>();
        foreach (var e in espnEvents)
        {
            var competition = e?["competitions"]?[0];
            if (competition?["status"]?["type"]?["completed"]?.GetValue<bool>() != true) continue;
            var competitors = competition["competitors"]?.AsArray() ?? [];
            var home = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "home");
            var away = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "away");
            if (home is null || away is null || Str(e!["id"]) is not { } id) continue;
            if (Score(home) != match.HomeScore || Score(away) != match.AwayScore) continue;
            candidates.Add((id, SameTeam(home, match.HomeTeam!), SameTeam(away, match.AwayTeam!)));
        }
        var both = candidates.Where(c => c.Home && c.Away).ToList();
        if (both.Count > 0) return both.Count == 1 ? both[0].Id : null;
        var one = candidates.Where(c => c.Home || c.Away).ToList();
        return one.Count == 1 ? one[0].Id : null;
    }

    // Stessa squadra se i nomi hanno una parola in comune (senza accenti e senza parole come "FC").
    private static bool SameTeam(JsonNode competitor, Team team)
    {
        var name = Str(competitor["team"]?["displayName"]) ?? "";
        var espn = Words(TeamAliases.GetValueOrDefault(name, name), Str(competitor["team"]?["shortDisplayName"]));
        return Words(team.Name, team.ShortName).Overlaps(espn);
    }

    private static HashSet<string> Words(params string?[] names) =>
        names.Where(n => !string.IsNullOrWhiteSpace(n))
            .SelectMany(n => WordSeparator().Split(Plain(n!).ToLowerInvariant()))
            .Where(w => w.Length >= 3 && !CommonWords.Contains(w))
            .ToHashSet();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex WordSeparator();

    private static int? Score(JsonNode competitor) =>
        int.TryParse(Str(competitor["score"]), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;

    private static MatchDetails ToDetails(JsonNode summary)
    {
        var competitors = summary["header"]?["competitions"]?[0]?["competitors"]?.AsArray() ?? [];
        var homeId = Str(competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "home")?["team"]?["id"]);
        string SideOf(JsonNode? team) => Str(team?["id"]) == homeId ? "home" : "away";

        // I rigori della lotteria finale non sono eventi della partita.
        var keyEvents = (summary["keyEvents"]?.AsArray() ?? [])
            .Where(e => e is not null && e["shootout"]?.GetValue<bool>() != true)
            .Select(e => e!)
            .ToList();

        // Giocatori delle formazioni per id: hanno numero di maglia e cognome, che negli eventi mancano.
        var rosters = (summary["rosters"]?.AsArray() ?? []).Where(r => r is not null).Select(r => r!).ToList();
        var athletes = rosters
            .SelectMany(r => r["roster"]?.AsArray() ?? [])
            .Where(p => p is not null)
            .GroupBy(p => Str(p!["athlete"]?["id"]) ?? "")
            .ToDictionary(g => g.Key, g => Player(g.First()!));

        var incidents = new List<MatchIncident>();
        foreach (var e in keyEvents)
        {
            var espnType = Str(e["type"]?["type"]) ?? "";
            var type = GoalTypes.TryGetValue(espnType, out var goal) ? goal
                : espnType switch
                {
                    "penalty---missed" or "penalty---saved" => "missedPenalty",
                    "yellow-card" => "yellow",
                    // Il doppio giallo è un rosso come gli altri: si riconosce solo dal testo.
                    "red-card" => (Str(e["text"]) ?? "").StartsWith("Second yellow", StringComparison.OrdinalIgnoreCase) ? "secondYellow" : "red",
                    _ => null,
                };
            if (type is null || PlayerOf(e["participants"]?[0], athletes) is not { } player) continue;
            var (minute, added) = Minute(e);
            incidents.Add(new MatchIncident(minute, added, type, SideOf(e["team"]), player.Name, player.Short));
        }

        var lineups = new Dictionary<string, Lineup>();
        foreach (var roster in rosters)
        {
            var side = SideOf(roster["team"]);
            var players = (roster["roster"]?.AsArray() ?? []).Where(p => p is not null).Select(p => p!).ToList();
            var starters = players.Where(p => p["starter"]?.GetValue<bool>() == true).Select(Player).ToList();
            if (starters.Count == 0) continue;

            // Chi entra è il primo partecipante della sostituzione, chi esce il secondo.
            var substitutions = keyEvents
                .Where(e => Str(e["type"]?["type"]) == "substitution" && SideOf(e["team"]) == side)
                .Select(e =>
                {
                    var (minute, added) = Minute(e);
                    var playerIn = PlayerOf(e["participants"]?[0], athletes);
                    var playerOut = PlayerOf(e["participants"]?[1], athletes);
                    return playerIn is null || playerOut is null ? null : new Substitution(minute, added, playerIn, playerOut);
                })
                .OfType<Substitution>()
                .ToList();
            lineups[side] = new Lineup(starters, substitutions);
        }

        return new MatchDetails
        {
            Version = CurrentVersion,
            Incidents = incidents,
            Home = lineups.GetValueOrDefault("home"),
            Away = lineups.GetValueOrDefault("away"),
        };
    }

    // Il cognome è "lastName" di ESPN, che include quelli composti ("De Bruyne", "Inao Oulaï"); i giocatori noti con un
    // nome solo ("Bremer", "Vitinha") non lo hanno e restano con quello.
    private static LineupPlayer Player(JsonNode p)
    {
        var name = Str(p["athlete"]?["displayName"]) ?? "";
        var lastName = Str(p["athlete"]?["lastName"]);
        return new LineupPlayer(
            int.TryParse(Str(p["jersey"]), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null,
            name,
            string.IsNullOrWhiteSpace(lastName) || lastName == name ? null : lastName);
    }

    // Il giocatore dalla formazione, che ha il numero di maglia; altrimenti solo il nome dell'evento.
    private static LineupPlayer? PlayerOf(JsonNode? participant, Dictionary<string, LineupPlayer> byId)
    {
        var athlete = participant?["athlete"];
        if (athlete is null) return null;
        return byId.TryGetValue(Str(athlete["id"]) ?? "", out var p) ? p
            : Str(athlete["displayName"]) is { } name ? new LineupPlayer(null, name) : null;
    }

    // "45'+2'" -> (45, 2); "4'" -> (4, null). Senza testo si ricava dai secondi di gioco.
    private static (int Minute, int? Added) Minute(JsonNode e)
    {
        var m = MinutePattern().Match(Str(e["clock"]?["displayValue"]) ?? "");
        if (m.Success)
            return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : null);
        var seconds = e["clock"]?["value"]?.GetValue<double>() ?? 0;
        return ((int)(seconds / 60) + 1, null);
    }

    [GeneratedRegex(@"^(\d+)'(?:\+(\d+)')?")]
    private static partial Regex MinutePattern();

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node?.ToString();

    // Senza accenti, per confrontare i nomi delle squadre.
    private static string Plain(string text) =>
        string.Concat(text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    private static Task<JsonNode> GetJsonAsync(string url, CancellationToken ct) =>
        HttpJson.GetAsync(Http, url, "ESPN", 2, ct);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MySports/0.1");
        return client;
    }
}
