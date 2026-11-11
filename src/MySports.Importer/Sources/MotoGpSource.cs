using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// MotoGP dalle API pubbliche del sito motogp.com (Pulselive): gratuite, senza chiave, ma non documentate.
// Il calendario (/events) dà gli orari ufficiali delle sessioni; l'archivio dei risultati (/results) dà le classifiche
// delle sessioni e del campionato. Nell'archivio gli orari delle sessioni future sono provvisori (a novembre 2026
// sbagliati di una settimana) e le ore sono locali scritte come UTC, quindi gli orari vengono sempre dal calendario.
// Le stagioni vanno dal 1949: fino al 2001 la classe regina era la 500cc, che nel calendario ha la stessa sigla.
public sealed partial class MotoGpSource(HttpClient http) : IEventSource
{
    private const string BaseUrl = "https://api.motogp.pulselive.com/motogp/v1";
    private const string CalendarCategory = "MGP"; // sigla della classe regina nel calendario (le altre: MT2, MT3, 250, 125, ...)
    private const int MaxRateLimitRetries = 10;

    // Prima del 2001 il calendario ha orari finti (tutte le gare alle 14:30:27 o a mezzanotte): si salva solo la data.
    private const int FirstSeasonWithTimes = 2001;

    // Sessioni del calendario -> nostre. Le qualifiche diventano una sola sessione: Q1 e Q2 dal 2013, con i tempi
    // di entrambe come in F1; prima una sessione unica (QP) o due giornate (QP1 e QP2, senza classifiche nell'archivio).
    private static readonly Dictionary<string, string> Sessions = new()
    {
        ["FP1"] = "fp1", ["PR"] = "practice", ["FP2"] = "fp2", ["FP3"] = "fp3", ["FP4"] = "fp4",
        ["Q1"] = "qualifying", ["Q2"] = "qualifying", ["QP"] = "qualifying", ["QP1"] = "qualifying", ["QP2"] = "qualifying",
        ["SPR"] = "sprint", ["WUP"] = "warm_up", ["RAC"] = "race",
    };

    // GP con gli orari del calendario sbagliati: (stagione, sigla del GP) -> correzione da sommare a ogni sessione.
    // Jerez 2024: tutto 2 ore più tardi del vero (Q1 alle 12:50 invece delle 10:50, gara alle 16:00 invece delle 14:00).
    private static readonly Dictionary<(string Season, string Gp), TimeSpan> TimeCorrections = new()
    {
        [("2024", "SPA")] = TimeSpan.FromHours(-2),
    };

    // Durata indicativa, per capire se una sessione senza risultati è in corso o finita (la qualifica va da Q1 a fine Q2).
    private static readonly Dictionary<string, double> DurationHours = new()
    {
        ["race"] = 1.5, ["sprint"] = 1, ["qualifying"] = 1,
    };

    // Dati dell'archivio risultati che servono sia agli eventi sia alle classifiche: si leggono una volta per stagione.
    private sealed record Archive(string SeasonId, string CategoryId, Dictionary<string, JsonNode> EventsByCalendarId, Dictionary<string, JsonNode> EventsByShortName);
    private readonly Dictionary<string, Archive> archives = [];
    private JsonArray? seasonList; // elenco delle stagioni dell'archivio, uguale per tutte: si legge una volta

    public Competition Competition => Competition.MotoGP;

    public string CurrentSeason(DateTimeOffset now) => now.Year.ToString(CultureInfo.InvariantCulture);

    public async Task<IReadOnlyList<SportEvent>> FetchSeasonAsync(string season, CancellationToken ct)
    {
        var archive = await GetArchiveAsync(season, ct);
        var calendar = (await GetJsonAsync($"{BaseUrl}/events?seasonYear={season}", ct)).AsArray();
        var hasTimes = int.Parse(season, CultureInfo.InvariantCulture) >= FirstSeasonWithTimes;
        var now = DateTimeOffset.UtcNow;
        var events = new List<SportEvent>();

        // Il calendario comprende anche test ed eventi promozionali: si tengono solo i GP, e tra questi quelli corsi dalla
        // classe regina (nelle stagioni vecchie alcuni GP erano solo per le classi minori). Un GP ancora senza orari resta.
        // Il round si conta in ordine di data: il numero del calendario ("sequence") nelle stagioni passate ha doppioni e buchi.
        var gps = calendar
            .Select(g => g!)
            .Where(g => Str(g["kind"]) == "GP")
            .Where(g => !SessionBroadcasts(g).Any() || SessionBroadcasts(g).Any(b => Str(b["category"]?["acronym"]) == CalendarCategory))
            .OrderBy(g => ParseDate(Str(g["date_start"])!))
            .ToList();

        foreach (var (gp, index) in gps.Select((g, i) => (g, i)))
        {
            var round = index + 1;
            var results = archive.EventsByCalendarId.GetValueOrDefault(Str(gp["id"]) ?? "")
                          ?? archive.EventsByShortName.GetValueOrDefault(Str(gp["shortname"]) ?? "");
            var circuit = gp["circuit"];

            // Ogni GP è indipendente: se mancano le classifiche di uno, gli altri si salvano lo stesso
            // (e il confronto con l'esecuzione precedente conserva quelle già scaricate).
            var classifications = new Dictionary<string, JsonArray>();
            if (results is not null && Str(results["status"]) != "NOT-STARTED")
            {
                try
                {
                    classifications = await FetchClassificationsAsync(Str(results["id"])!, archive.CategoryId, ct);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"AVVISO risultati MotoGP {season} round {round}: {ex.Message}");
                }
            }

            SportEvent Create(string session, DateTimeOffset startsAt, bool dateOnly, bool cancelled, IReadOnlyList<ClassificationRow>? result) => new()
            {
                Id = $"motogp-{season}-{round}-{session}",
                Competition = Competition.Id,
                Season = season,
                Round = round,
                Title = GpTitle(Str(results?["name"]), Str(gp["name"])),
                Session = session,
                StartsAt = startsAt,
                DateOnly = dateOnly ? true : null,
                Venue = Str(circuit?["name"]),
                City = Str(circuit?["city"]),
                Country = Str(circuit?["country"]),
                Status = cancelled ? EventStatus.Cancelled : StatusFor(session, startsAt, result is not null, now),
                Result = result,
            };

            var sessions = SessionBroadcasts(gp)
                .Where(b => Str(b["category"]?["acronym"]) == CalendarCategory && SessionName(Str(b["shortname"])) is not null)
                .GroupBy(b => SessionName(Str(b["shortname"]))!)
                .ToList();

            // Senza orari (calendario non ancora completo) il GP compare solo con la gara, alla data dell'ultimo giorno.
            if (sessions.Count == 0)
            {
                events.Add(Create("race", DateOnlyStart(Str(gp["date_end"])!), true, false, null));
                continue;
            }

            var correction = TimeCorrections.GetValueOrDefault((season, Str(gp["shortname"]) ?? ""));
            foreach (var group in sessions)
            {
                var first = group.MinBy(b => ParseDate(Str(b["date_start"])!))!;
                var startsAt = hasTimes ? ParseDate(Str(first["date_start"])!) + correction : DateOnlyStart(Str(first["date_start"])!);
                IReadOnlyList<ClassificationRow>? result = group.Key switch
                {
                    "race" when LastStart(classifications, "RAC") is { } rows => RaceClassification(rows),
                    "sprint" when LastStart(classifications, "SPR") is { } rows => RaceClassification(rows),
                    // Dal 2013 la qualifica è conclusa solo con il Q2.
                    "qualifying" when classifications.TryGetValue("Q2", out var q2) =>
                        QualifyingClassification(classifications.GetValueOrDefault("Q1"), q2),
                    "qualifying" when classifications.TryGetValue("QP", out var qp) => QualifyingClassification(qp, null),
                    _ => null,
                };
                var cancelled = group.All(b => Str(b["status"]) == "CANCELLED");
                events.Add(Create(group.Key, startsAt, !hasTimes, cancelled, result));
            }
        }

        return events;
    }

    private static IEnumerable<JsonNode> SessionBroadcasts(JsonNode gp) =>
        (gp["broadcasts"]?.AsArray() ?? []).Select(b => b!).Where(b => Str(b["type"]) == "SESSION");

    // Senza orario si usa mezzogiorno UTC del giorno locale, così la data resta la stessa in qualsiasi fuso orario.
    private static DateTimeOffset DateOnlyStart(string value) =>
        new(ParseLocalDate(value).Date.AddHours(12), TimeSpan.Zero);

    // Sigla del calendario -> nostra sessione. Una gara interrotta e ripartita ha anche "RAC2" (o "SPR2").
    private static string? SessionName(string? shortName) =>
        shortName is null ? null
        : Sessions.TryGetValue(shortName, out var session) ? session
        : RestartedRace().Match(shortName) is { Success: true } m ? Sessions[m.Groups[1].Value]
        : null;

    [GeneratedRegex(@"^(RAC|SPR)\d+$")]
    private static partial Regex RestartedRace();

    // Dopo una ripartenza vale la classifica dell'ultima partenza ("RAC2" invece di "RAC").
    private static JsonArray? LastStart(Dictionary<string, JsonArray> classifications, string type) =>
        classifications
            .Where(c => c.Key == type || RestartedRace().Match(c.Key) is { Success: true } m && m.Groups[1].Value == type)
            .OrderByDescending(c => c.Key.Length).ThenByDescending(c => c.Key, StringComparer.Ordinal)
            .Select(c => c.Value)
            .FirstOrDefault();

    public async Task<IReadOnlyList<StandingsTable>> FetchStandingsAsync(string season, CancellationToken ct)
    {
        var archive = await GetArchiveAsync(season, ct);
        var data = await GetJsonAsync($"{BaseUrl}/results/standings?seasonUuid={archive.SeasonId}&categoryUuid={archive.CategoryId}", ct);
        var rows = data["classification"]?.AsArray().Select(r => r!).ToList() ?? [];
        if (rows.Count == 0) return [];

        // L'API ha solo la classifica piloti (squadre e costruttori non sono disponibili).
        // Nelle stagioni vecchie manca la squadra: si mostra la moto.
        return
        [
            new StandingsTable("drivers", "Piloti", rows.Select(r =>
            {
                var pos = Int(r["position"]);
                return new StandingRow
                {
                    Pos = pos,
                    PosText = pos?.ToString(CultureInfo.InvariantCulture) ?? "-",
                    Name = Str(r["rider"]?["full_name"]) ?? "",
                    Team = TeamName(r),
                    Points = Double(r["points"]) ?? 0,
                    Wins = Int(r["race_wins"]),
                };
            }).ToList()),
        ];
    }

    private async Task<Archive> GetArchiveAsync(string season, CancellationToken ct)
    {
        if (archives.TryGetValue(season, out var cached)) return cached;

        var year = int.Parse(season, CultureInfo.InvariantCulture);
        seasonList ??= (await GetJsonAsync($"{BaseUrl}/results/seasons", ct)).AsArray();
        var seasonId = Str(seasonList.FirstOrDefault(s => Int(s!["year"]) == year)?["id"])
                       ?? throw new InvalidOperationException($"stagione {season} non presente nell'archivio MotoGP");

        // La classe regina si chiama "MotoGP™" dal 2002 e "500cc" prima.
        var categories = (await GetJsonAsync($"{BaseUrl}/results/categories?seasonUuid={seasonId}", ct)).AsArray();
        var categoryId = Str(categories.FirstOrDefault(c => Str(c!["name"]) is { } name
                             && (name.StartsWith("MotoGP", StringComparison.OrdinalIgnoreCase) || name.StartsWith("500cc", StringComparison.OrdinalIgnoreCase)))?["id"])
                         ?? throw new InvalidOperationException($"classe MotoGP/500cc non presente nella stagione {season}");

        // Gli eventi dell'archivio si collegano al calendario con toad_api_uuid (= id nel calendario), altrimenti con la sigla.
        var gps = (await GetJsonAsync($"{BaseUrl}/results/events?seasonUuid={seasonId}", ct)).AsArray()
            .Select(e => e!)
            .Where(e => e["test"]?.GetValue<bool>() != true)
            .ToList();
        var archive = new Archive(
            seasonId,
            categoryId,
            gps.Where(e => Str(e["toad_api_uuid"]) is not null).DistinctBy(e => Str(e["toad_api_uuid"])).ToDictionary(e => Str(e["toad_api_uuid"])!),
            gps.Where(e => Str(e["short_name"]) is not null).DistinctBy(e => Str(e["short_name"])).ToDictionary(e => Str(e["short_name"])!));
        archives[season] = archive;
        return archive;
    }

    // Classifiche delle sessioni concluse che servono (qualifiche, sprint e gara), per sigla: "Q1", "Q2", "QP", "SPR", "RAC".
    private async Task<Dictionary<string, JsonArray>> FetchClassificationsAsync(string eventId, string categoryId, CancellationToken ct)
    {
        var sessions = (await GetJsonAsync($"{BaseUrl}/results/sessions?eventUuid={eventId}&categoryUuid={categoryId}", ct)).AsArray();
        var byName = new Dictionary<string, JsonArray>();
        foreach (var s in sessions.Select(s => s!))
        {
            var name = $"{Str(s["type"])}{Int(s["number"])}";
            if (name is not ("Q1" or "Q2" or "QP") && SessionName(name) is not ("race" or "sprint") || Str(s["status"]) != "FINISHED") continue;
            var data = await GetJsonAsync($"{BaseUrl}/results/session/{Str(s["id"])}/classification?test=false", ct);
            if (data["classification"] is JsonArray { Count: > 0 } rows) byName[name] = rows;
        }
        return byName;
    }

    // I piloti non classificati hanno la posizione vuota e stanno in fondo: prendono i numeri successivi.
    private static List<ClassificationRow> RaceClassification(JsonArray rows)
    {
        var list = rows.Select(r => r!).ToList();
        var codes = Codes(list);
        return list.Select((r, i) => new ClassificationRow
        {
            Pos = Int(r["position"]) ?? i + 1,
            Number = Int(r["rider"]?["number"])?.ToString(CultureInfo.InvariantCulture),
            Driver = Str(r["rider"]?["full_name"]) ?? "",
            Code = codes[i],
            Team = TeamName(r) ?? "",
            Time = RaceTime(r),
            // Nelle stagioni vecchie l'archivio ha 0 punti per tutti: senza punti non si mostra niente.
            Points = Double(r["points"]) is > 0 and var points ? points : null,
        }).ToList();
    }

    // "41:29.173" per il vincitore, "+3.083" per gli altri, "+1 giro" per i doppiati, "Ritirato" per chi non ha finito.
    // Nelle stagioni vecchie spesso mancano tempi e distacchi (distacco "0.000" anche per il secondo): niente.
    private static string? RaceTime(JsonNode r)
    {
        var status = Str(r["status"]);
        if (status is "OUTSTND" or "NOTFINISHFIRST") return "Ritirato";
        if (status != "INSTND") return "Non classificato";
        var laps = Int(r["gap"]?["lap"]) ?? 0;
        if (laps > 0) return laps == 1 ? "+1 giro" : $"+{laps} giri";
        if (Int(r["position"]) == 1) return LapTime(r["time"]);
        return Str(r["gap"]?["first"]) is { } gap && gap.Trim('0', '.', ':').Length > 0 ? $"+{gap}" : null;
    }

    // Nelle stagioni vecchie la squadra manca: si usa la moto ("Yamaha").
    private static string? TeamName(JsonNode r) =>
        Str(r["team"]?["name"]) is { Length: > 0 } team ? team : Str(r["constructor"]?["name"]);

    // Dal 2013 griglia come la ufficiale: prima i 12 del Q2, poi gli altri nell'ordine del Q1 (i primi due del Q1
    // sono passati al Q2). Prima c'era una sessione sola, che arriva qui come q1 senza q2.
    private static List<ClassificationRow> QualifyingClassification(JsonArray? q1, JsonArray? q2)
    {
        var q1Rows = q1?.Select(r => r!).ToList() ?? [];
        var q2Rows = q2?.Select(r => r!).ToList() ?? [];
        string Rider(JsonNode r) => Str(r["rider"]?["id"]) ?? Str(r["rider"]?["full_name"]) ?? "";
        var inQ2 = q2Rows.Select(Rider).ToHashSet();
        var q1Times = q1Rows.DistinctBy(Rider).ToDictionary(Rider, r => LapTime(r["best_lap"]?["time"]));

        var grid = q2Rows.Concat(q1Rows.Where(r => !inQ2.Contains(Rider(r)))).ToList();
        var codes = Codes(grid);
        return grid.Select((r, i) => new ClassificationRow
        {
            Pos = i + 1,
            Number = Int(r["rider"]?["number"])?.ToString(CultureInfo.InvariantCulture),
            Driver = Str(r["rider"]?["full_name"]) ?? "",
            Code = codes[i],
            Team = TeamName(r) ?? "",
            Q1 =q1Times.GetValueOrDefault(Rider(r)),
            Q2 = inQ2.Contains(Rider(r)) ? LapTime(r["best_lap"]?["time"]) : null,
        }).ToList();
    }

    // "01:50.876" -> "1:50.876", come i tempi di qualifica della F1.
    private static string? LapTime(JsonNode? node) =>
        Str(node) is { Length: > 0 } time ? LeadingZero().Replace(time, "") : null;

    [GeneratedRegex(@"^0(?=\d:)")]
    private static partial Regex LeadingZero();

    // Nome breve per il podio nelle liste: il cognome ("Bezzecchi", "Di Giannantonio"),
    // con l'iniziale del nome se due piloti della sessione hanno lo stesso cognome ("M. Marquez", "A. Marquez").
    private static List<string?> Codes(List<JsonNode> rows)
    {
        var names = rows.Select(r => Str(r["rider"]?["full_name"])?.Trim() ?? "").ToList();
        static string Surname(string name) => name.IndexOf(' ') is var i and > 0 ? name[(i + 1)..] : name;
        var repeated = names.GroupBy(Surname).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        return names.Select(n => n.Length == 0 ? null
            : repeated.Contains(Surname(n)) && n.IndexOf(' ') > 0 ? $"{n[0]}. {Surname(n)}"
            : Surname(n)).ToList();
    }

    // Nome del GP dall'archivio, senza sponsor e scritto in modi diversi negli anni: "GRAND PRIX OF THE UNITED STATES",
    // "JAPANESE GRAND PRIX", "DUTCH TT", "GRAN PREMIO DE ESPAÑA". Se manca si usa il calendario, che ha anche lo sponsor
    // ("QATAR AIRWAYS GRAND PRIX OF QATAR"): lì si parte da "Grand Prix of".
    private static string GpTitle(string? archiveName, string? calendarName)
    {
        var name = archiveName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = calendarName ?? "";
            var start = name.IndexOf("GRAND PRIX OF", StringComparison.OrdinalIgnoreCase);
            if (start > 0) name = name[start..];
        }
        var words = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Trim().ToLowerInvariant()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select((w, i) =>
            Acronyms.Contains(w) ? w.ToUpperInvariant()
            // "D'italia" -> "d'Italia", "Dell'emilia" -> "dell'Emilia"
            : w.IndexOf('\'') is var a and > 0 && a < w.Length - 1 && SmallWords.Contains(w[..a])
                ? $"{w[..(a + 1)].ToLowerInvariant()}{char.ToUpperInvariant(w[a + 1])}{w[(a + 2)..]}"
            : i > 0 && SmallWords.Contains(w) ? w.ToLowerInvariant()
            : w));
    }

    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "of", "the", "and", "de", "la", "di", "d", "del", "dell", "della", "e", "do", "du", "von",
    };
    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase) { "TT", "GP", "U.S." };

    private static EventStatus StatusFor(string session, DateTimeOffset startsAt, bool hasResult, DateTimeOffset now)
    {
        if (hasResult) return EventStatus.Finished;
        if (now < startsAt) return EventStatus.Scheduled;
        var duration = TimeSpan.FromHours(DurationHours.GetValueOrDefault(session, 1));
        return now < startsAt + duration ? EventStatus.Live : EventStatus.Finished;
    }

    private Task<JsonNode> GetJsonAsync(string url, CancellationToken ct) =>
        HttpJson.GetAsync(http, url, "MotoGP", MaxRateLimitRetries, ct);

    // Gli orari arrivano sia come "2026-11-20T10:45:00+00:00" sia come "2026-11-20T10:45:00+0000".
    private static DateTimeOffset ParseDate(string value) => ParseLocalDate(value).ToUniversalTime();

    // Data e ora del luogo della gara, con il suo fuso.
    private static DateTimeOffset ParseLocalDate(string value) =>
        DateTimeOffset.Parse(OffsetWithoutColon().Replace(value, "$1:$2"), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"([+-]\d{2})(\d{2})$")]
    private static partial Regex OffsetWithoutColon();

    private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    // I numeri a volte arrivano come testo.
    private static int? Int(JsonNode? node) =>
        node is not JsonValue v ? null
        : v.TryGetValue<int>(out var i) ? i
        : v.TryGetValue<string>(out var s) && int.TryParse(s, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : null;

    private static double? Double(JsonNode? node) =>
        node is not JsonValue v ? null
        : v.TryGetValue<double>(out var d) ? d
        : v.TryGetValue<string>(out var s) && double.TryParse(s, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : null;
}
