using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Dettaglio delle partite finite (marcatori, rigori sbagliati, cartellini, formazioni) dall'API pubblica non documentata
// di ESPN, senza chiave. football-data.org li ha solo a pagamento.
// Le partite si abbinano a quelle di football-data per giorno, squadre e risultato: una chiamata "scoreboard" per giorno
// dà gli id ESPN, poi una chiamata "summary" per partita. Ogni partita si legge una volta: il dettaglio resta
// dall'esecuzione precedente, come i video. Verificato sulla Serie A 2026-27 (110 partite su 110 abbinate).
public sealed partial class EspnMatchDetails(string league)
{
    private const string BaseUrl = "https://site.api.espn.com/apis/site/v2/sports/soccer";

    // Client proprio: ESPN a volte risponde compresso con gzip anche senza chiederlo.
    private static readonly HttpClient Http = CreateClient();

    // Nomi ESPN -> nome breve di football-data, dove sono diversi.
    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC Milan"] = "Milan",
        ["AS Roma"] = "Roma",
        ["Internazionale"] = "Inter",
    };

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

    // Lega ESPN: "ita.1" per la Serie A ("uefa.champions" per la Champions League, non ancora usata).
    public static EspnMatchDetails SerieA() => new("ita.1");

    // Aggiunge il dettaglio alle partite finite che non lo hanno ancora.
    public async Task<IReadOnlyList<SportEvent>> AttachAsync(IReadOnlyList<SportEvent> events, CancellationToken ct)
    {
        var waiting = events
            .Where(e => e.Session == "match" && e.Status == EventStatus.Finished && e.Details is null
                        && e.HomeScore is not null && e.AwayScore is not null && e.HomeTeam is not null && e.AwayTeam is not null)
            .ToList();
        if (waiting.Count == 0) return events;

        var found = new Dictionary<string, MatchDetails>();
        var missing = new List<string>();
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
                if (espnId is null)
                {
                    missing.Add($"{match.Title} del {match.StartsAt:yyyy-MM-dd}: partita non trovata su ESPN");
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

    // La partita ESPN dello stesso giorno con le stesse squadre in casa e in trasferta e lo stesso risultato, già conclusa.
    private static string? FindEspnId(JsonArray espnEvents, SportEvent match)
    {
        foreach (var e in espnEvents)
        {
            var competition = e?["competitions"]?[0];
            if (competition?["status"]?["type"]?["completed"]?.GetValue<bool>() != true) continue;
            var competitors = competition["competitors"]?.AsArray() ?? [];
            var home = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "home");
            var away = competitors.FirstOrDefault(c => Str(c?["homeAway"]) == "away");
            if (home is null || away is null) continue;
            if (SameTeam(home, match.HomeTeam!) && SameTeam(away, match.AwayTeam!)
                && Score(home) == match.HomeScore && Score(away) == match.AwayScore)
                return Str(e!["id"]);
        }
        return null;
    }

    private static bool SameTeam(JsonNode competitor, Team team)
    {
        var name = Str(competitor["team"]?["displayName"]) ?? "";
        if (TeamAliases.TryGetValue(name, out var alias)) name = alias;
        return string.Equals(Plain(name), Plain(team.ShortName ?? team.Name), StringComparison.OrdinalIgnoreCase);
    }

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
            var player = Str(e["participants"]?[0]?["athlete"]?["displayName"]);
            if (type is null || player is null) continue;
            var (minute, added) = Minute(e);
            incidents.Add(new MatchIncident(minute, added, type, SideOf(e["team"]), player));
        }

        var lineups = new Dictionary<string, Lineup>();
        foreach (var roster in summary["rosters"]?.AsArray() ?? [])
        {
            if (roster is null) continue;
            var side = SideOf(roster["team"]);
            var players = (roster["roster"]?.AsArray() ?? []).Where(p => p is not null).Select(p => p!).ToList();
            var byId = players.GroupBy(p => Str(p["athlete"]?["id"]) ?? "").ToDictionary(g => g.Key, g => Player(g.First()));
            var starters = players.Where(p => p["starter"]?.GetValue<bool>() == true).Select(Player).ToList();
            if (starters.Count == 0) continue;

            // Chi entra è il primo partecipante della sostituzione, chi esce il secondo.
            var substitutions = keyEvents
                .Where(e => Str(e["type"]?["type"]) == "substitution" && SideOf(e["team"]) == side)
                .Select(e =>
                {
                    var (minute, added) = Minute(e);
                    var playerIn = PlayerOf(e["participants"]?[0], byId);
                    var playerOut = PlayerOf(e["participants"]?[1], byId);
                    return playerIn is null || playerOut is null ? null : new Substitution(minute, added, playerIn, playerOut);
                })
                .OfType<Substitution>()
                .ToList();
            lineups[side] = new Lineup(starters, substitutions);
        }

        return new MatchDetails
        {
            Incidents = incidents,
            Home = lineups.GetValueOrDefault("home"),
            Away = lineups.GetValueOrDefault("away"),
        };
    }

    private static LineupPlayer Player(JsonNode p) => new(
        int.TryParse(Str(p["jersey"]), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null,
        Str(p["athlete"]?["displayName"]) ?? "");

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
