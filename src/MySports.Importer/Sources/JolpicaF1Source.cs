using System.Globalization;
using System.Text.Json.Nodes;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Formula 1 da Jolpica-F1 (https://github.com/jolpica/jolpica-f1), erede di Ergast. Gratuita, senza chiave.
public sealed class JolpicaF1Source(HttpClient http) : IEventSource
{
    private readonly OpenF1Client openF1 = new(http);

    private const string BaseUrl = "https://api.jolpi.ca/ergast/f1";
    private const int PageSize = 100;                                  // massimo consentito da Jolpica
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(300); // limite ~4 richieste/secondo

    // Campi del calendario Jolpica -> nome della sessione nel nostro formato.
    private static readonly (string Field, string Session)[] Sessions =
    [
        ("FirstPractice", "fp1"),
        ("SecondPractice", "fp2"),
        ("ThirdPractice", "fp3"),
        ("SprintQualifying", "sprint_qualifying"),
        ("SprintShootout", "sprint_qualifying"), // nome usato nel 2023
        ("Sprint", "sprint"),
        ("Qualifying", "qualifying"),
    ];

    // Durata indicativa, per capire se una sessione senza risultati è in corso o finita.
    private static readonly Dictionary<string, double> DurationHours = new()
    {
        ["race"] = 3, ["sprint"] = 1.5, ["qualifying"] = 1.5, ["sprint_qualifying"] = 1.5,
    };

    public Competition Competition => Competition.F1;

    public string CurrentSeason(DateTimeOffset now) => now.Year.ToString(CultureInfo.InvariantCulture);

    public async Task<IReadOnlyList<SportEvent>> FetchSeasonAsync(string season, CancellationToken ct)
    {
        var schedule = await GetJsonAsync($"{BaseUrl}/{season}.json?limit={PageSize}", ct);
        var races = schedule["MRData"]!["RaceTable"]!["Races"]!.AsArray();

        var raceResults = await FetchByRoundAsync($"{season}/results", "Results", ct);
        var sprintResults = await FetchByRoundAsync($"{season}/sprint", "SprintResults", ct);
        var qualiResults = await FetchByRoundAsync($"{season}/qualifying", "QualifyingResults", ct);

        var now = DateTimeOffset.UtcNow;
        var sprintQualiResults = await FetchSprintQualifyingAsync(season, races, sprintResults, qualiResults, now, ct);
        var events = new List<SportEvent>();

        foreach (var race in races)
        {
            var round = Str(race!["round"])!;
            var circuit = race["Circuit"]!;

            SportEvent Create(string session, DateTimeOffset startsAt, IReadOnlyList<ClassificationRow>? result) => new()
            {
                Id = $"f1-{season}-{round}-{session}",
                Competition = Competition.Id,
                Season = season,
                Round = int.Parse(round, CultureInfo.InvariantCulture),
                Title = Str(race["raceName"])!,
                Session = session,
                StartsAt = startsAt,
                Venue = Str(circuit["circuitName"]),
                City = Str(circuit["Location"]?["locality"]),
                Country = Str(circuit["Location"]?["country"]),
                Status = StatusFor(session, startsAt, result is not null, now),
                Result = result,
            };

            foreach (var (field, session) in Sessions)
            {
                if (race[field] is not { } s) continue;
                IReadOnlyList<ClassificationRow>? result = session switch
                {
                    "sprint" when sprintResults.TryGetValue(round, out var rows) => RaceClassification(rows),
                    "qualifying" when qualiResults.TryGetValue(round, out var rows) => QualifyingClassification(rows),
                    "sprint_qualifying" when sprintQualiResults.TryGetValue(round, out var rows) => rows,
                    _ => null,
                };
                events.Add(Create(session, ParseStart(s), result));
            }

            events.Add(Create("race", ParseStart(race),
                raceResults.TryGetValue(round, out var raceRows) ? RaceClassification(raceRows) : null));
        }

        return events;
    }

    // Jolpica non ha i risultati delle qualifiche sprint: li prendiamo da OpenF1, abbinando la sessione per orario.
    // Nomi e scuderie vengono dai dati Jolpica dello stesso GP, così restano scritti come nelle altre sessioni.
    // Se OpenF1 non risponde la sessione resta senza risultati, senza bloccare il resto dell'importazione.
    private async Task<Dictionary<string, List<ClassificationRow>>> FetchSprintQualifyingAsync(
        string season,
        JsonArray races,
        Dictionary<string, List<JsonNode>> sprintResults,
        Dictionary<string, List<JsonNode>> qualiResults,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var byRound = new Dictionary<string, List<ClassificationRow>>();
        var finished = races
            .Select(r => (Round: Str(r!["round"])!, Node: r["SprintQualifying"] ?? r["SprintShootout"]))
            .Where(x => x.Node is not null)
            .Select(x => (x.Round, Start: ParseStart(x.Node!)))
            .Where(x => x.Start + TimeSpan.FromHours(DurationHours["sprint_qualifying"]) < now)
            .ToList();
        if (finished.Count == 0) return byRound;

        try
        {
            var sessions = await openF1.GetSessionsAsync(season, "Sprint Qualifying", ct);
            foreach (var (round, start) in finished)
            {
                var session = sessions.FirstOrDefault(s => (s.Start - start).Duration() < TimeSpan.FromHours(2));
                if (session is null) continue;

                var rows = await openF1.GetSessionResultAsync(session.SessionKey, ct);
                if (rows.Count == 0) continue;

                var openF1Drivers = (await openF1.GetDriversAsync(session.SessionKey, ct))
                    .DistinctBy(d => d.Number)
                    .ToDictionary(d => d.Number);
                var jolpicaDrivers = (sprintResults.GetValueOrDefault(round) ?? [])
                    .Concat(qualiResults.GetValueOrDefault(round) ?? [])
                    .DistinctBy(r => Str(r["number"]))
                    .ToDictionary(r => Str(r["number"])!);

                byRound[round] = rows
                    .OrderBy(r => r.Position ?? int.MaxValue)
                    .Select((r, i) =>
                    {
                        var number = r.DriverNumber.ToString(CultureInfo.InvariantCulture);
                        var j = jolpicaDrivers.GetValueOrDefault(number);
                        var o = openF1Drivers.GetValueOrDefault(r.DriverNumber);
                        return new ClassificationRow
                        {
                            Pos = r.Position ?? i + 1,
                            Number = number,
                            Driver = j is not null ? DriverName(j["Driver"]!) : o?.FullName ?? $"#{number}",
                            Code = j is not null ? Str(j["Driver"]!["code"]) : o?.Acronym,
                            Team = j is not null ? Str(j["Constructor"]!["name"])! : o?.Team ?? "",
                            Q1 = FormatLap(r.Duration.ElementAtOrDefault(0)),
                            Q2 = FormatLap(r.Duration.ElementAtOrDefault(1)),
                            Q3 = FormatLap(r.Duration.ElementAtOrDefault(2)),
                        };
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AVVISO qualifiche sprint da OpenF1: {ex.Message}");
        }
        return byRound;
    }

    // 91.52 secondi -> "1:31.520", come i tempi di qualifica di Jolpica.
    private static string? FormatLap(double? seconds) =>
        seconds is { } s ? $"{(int)(s / 60)}:{(s % 60).ToString("00.000", CultureInfo.InvariantCulture)}" : null;

    private async Task<JsonNode> GetJsonAsync(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Jolpica ha risposto {(int)res.StatusCode} su {url}");
        var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!;
        await Task.Delay(Pause, ct);
        return json;
    }

    // Le API paginano per riga di risultato, quindi un GP può essere diviso tra due pagine:
    // ricomponiamo le righe per numero di round.
    private async Task<Dictionary<string, List<JsonNode>>> FetchByRoundAsync(string path, string listKey, CancellationToken ct)
    {
        var byRound = new Dictionary<string, List<JsonNode>>();
        for (int offset = 0, total = int.MaxValue; offset < total; offset += PageSize)
        {
            var data = await GetJsonAsync($"{BaseUrl}/{path}.json?limit={PageSize}&offset={offset}", ct);
            total = int.Parse(Str(data["MRData"]!["total"])!, CultureInfo.InvariantCulture);
            foreach (var race in data["MRData"]!["RaceTable"]!["Races"]!.AsArray())
            {
                var round = Str(race!["round"])!;
                if (!byRound.TryGetValue(round, out var rows)) byRound[round] = rows = [];
                rows.AddRange(race[listKey]?.AsArray().Select(r => r!) ?? []);
            }
        }
        return byRound;
    }

    private static List<ClassificationRow> RaceClassification(List<JsonNode> rows) =>
        rows.Select(r => new ClassificationRow
        {
            Pos = int.Parse(Str(r["position"])!, CultureInfo.InvariantCulture),
            Number = Str(r["number"]),
            Driver = DriverName(r["Driver"]!),
            Code = Str(r["Driver"]!["code"]),
            Team = Str(r["Constructor"]!["name"])!,
            Time = Str(r["Time"]?["time"]) ?? Str(r["status"]),
            Points = double.Parse(Str(r["points"]) ?? "0", CultureInfo.InvariantCulture),
        }).ToList();

    private static List<ClassificationRow> QualifyingClassification(List<JsonNode> rows) =>
        rows.Select(r => new ClassificationRow
        {
            Pos = int.Parse(Str(r["position"])!, CultureInfo.InvariantCulture),
            Number = Str(r["number"]),
            Driver = DriverName(r["Driver"]!),
            Code = Str(r["Driver"]!["code"]),
            Team = Str(r["Constructor"]!["name"])!,
            Q1 = NullIfEmpty(Str(r["Q1"])),
            Q2 = NullIfEmpty(Str(r["Q2"])),
            Q3 = NullIfEmpty(Str(r["Q3"])),
        }).ToList();

    private static string DriverName(JsonNode d) => $"{Str(d["givenName"])} {Str(d["familyName"])}";

    // Jolpica fornisce data e ora separate ("2026-11-01" + "20:00:00Z"); l'ora può mancare nelle stagioni vecchie.
    private static DateTimeOffset ParseStart(JsonNode node)
    {
        var date = Str(node["date"])!;
        var time = Str(node["time"]) ?? "00:00:00Z";
        return DateTimeOffset.Parse($"{date}T{time}", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static EventStatus StatusFor(string session, DateTimeOffset startsAt, bool hasResult, DateTimeOffset now)
    {
        if (hasResult) return EventStatus.Finished;
        if (now < startsAt) return EventStatus.Scheduled;
        var duration = TimeSpan.FromHours(DurationHours.GetValueOrDefault(session, 1));
        return now < startsAt + duration ? EventStatus.Live : EventStatus.Finished;
    }

    private static string? Str(JsonNode? node) => node?.GetValue<string>();

    // Jolpica a volte usa "" invece di omettere il campo (es. tempo di Q1 mancante).
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
