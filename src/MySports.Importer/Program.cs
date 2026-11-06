// Importatore MySports: scarica calendari e risultati dalle fonti esterne e genera i file JSON per l'app.
// Uso:
//   dotnet run --project src/MySports.Importer -- [--out <cartella>]
//       aggiorna la stagione corrente di ogni competizione, upcoming.json, results.json e meta.json
//   dotnet run --project src/MySports.Importer -- --competition f1 --seasons 1950-2025 [--out <cartella>]
//       importa stagioni passate (una tantum: non cambiano più) e aggiorna meta.json
//   ... --competition f1 --seasons 1950-2025 --standings-only
//       aggiunge solo le classifiche a stagioni già importate, senza riscaricare gli eventi
//   ... --competition serie-a --seasons 2023-24,2024-25
//       le stagioni del calcio si elencano per nome
using System.Globalization;
using MySports.Importer.Models;
using MySports.Importer.Output;
using MySports.Importer.Sources;

string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

var outputDir = Arg("--out") ?? "out";
var seasonsArg = Arg("--seasons");
var competitionArg = Arg("--competition");
var standingsOnly = args.Contains("--standings-only");
var window = TimeSpan.FromDays(14); // orizzonte di upcoming.json e results.json

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("MySports/0.1");

var footballKey = Environment.GetEnvironmentVariable("FOOTBALL_DATA_API_KEY") ?? ReadDotEnv("FOOTBALL_DATA_API_KEY");

// La F1 resta prima: è la competizione mostrata all'apertura delle classifiche.
IEventSource[] sources =
[
    new JolpicaF1Source(http),
    new FootballDataSource(Competition.SerieA, "SA", footballKey, FootballDataSource.SerieAZones, FootballDataSource.SerieAZoneExceptions),
    new FootballDataSource(Competition.ChampionsLeague, "CL", footballKey, FootballDataSource.ChampionsLeagueZones),
];

// Highlights YouTube per competizione, nell'ordine in cui l'app li mostra (italiano per primo).
// Senza chiave (YOUTUBE_API_KEY) si salta questo passaggio.
var youtubeKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY") ?? ReadDotEnv("YOUTUBE_API_KEY");
var videoSources = new Dictionary<string, YouTubeHighlights[]>();
if (!string.IsNullOrWhiteSpace(youtubeKey))
{
    videoSources[Competition.F1.Id] = [YouTubeHighlights.SkySportF1(http, youtubeKey), YouTubeHighlights.FormulaOne(http, youtubeKey)];
    videoSources[Competition.SerieA.Id] = [YouTubeHighlights.SkySportSerieA(http, youtubeKey), YouTubeHighlights.DaznSerieA(http, youtubeKey)];
    videoSources[Competition.ChampionsLeague.Id] = [YouTubeHighlights.SkySportChampionsLeague(http, youtubeKey)];
}
else
    Console.WriteLine("YOUTUBE_API_KEY non impostata: highlights non aggiornati");

var publisher = new JsonPublisher(outputDir);
var now = DateTimeOffset.UtcNow;
var ct = CancellationToken.None;
var failures = 0;

if (seasonsArg is not null)
{
    // Stagioni passate: solo la competizione richiesta.
    var source = sources.SingleOrDefault(s => s.Competition.Id == competitionArg);
    if (source is null)
    {
        Console.Error.WriteLine($"Con --seasons serve --competition tra: {string.Join(", ", sources.Select(s => s.Competition.Id))}");
        return 2;
    }
    foreach (var season in ParseSeasons(seasonsArg))
    {
        if (await ImportSeasonAsync(source, season) is null) failures++;
    }
}
else
{
    // Stagione corrente di ogni competizione + elenchi dei prossimi eventi e dei risultati recenti.
    var allEvents = new List<SportEvent>();
    foreach (var source in sources)
    {
        var events = await ImportSeasonAsync(source, source.CurrentSeason(now));
        if (events is null)
        {
            failures++;
            // Fonte non raggiungibile o formato cambiato: si tengono i dati dell'esecuzione precedente, se ci sono.
            events = (await publisher.LoadSeasonAsync(source.Competition.Id, source.CurrentSeason(now), ct))?.Events;
        }
        allEvents.AddRange(events ?? []);
    }
    await publisher.WriteFeedsAsync(allEvents, now, window, ct);
}

await publisher.WriteMetaAsync(sources.Select(s => (s.Competition, s.CurrentSeason(now))).ToList(), now, ct);
Console.WriteLine($"File generati in {Path.GetFullPath(outputDir)}");
return failures == 0 ? 0 : 1;

// Scarica una stagione e la salva; restituisce null se la fonte ha dato errore.
async Task<IReadOnlyList<SportEvent>?> ImportSeasonAsync(IEventSource source, string season)
{
    var competition = source.Competition;
    try
    {
        var previous = await publisher.LoadSeasonAsync(competition.Id, season, ct);

        IReadOnlyList<SportEvent> events;
        if (standingsOnly && previous is not null)
        {
            events = previous.Events;
        }
        else
        {
            events = await source.FetchSeasonAsync(season, ct);
            if (events.Count == 0)
            {
                Console.WriteLine($"{competition.Name} {season}: nessun evento, file non scritto");
                return events;
            }

            // Dalle esecuzioni precedenti si conservano i video (le fonti non li forniscono) e i risultati
            // che questa volta mancano (es. una fonte secondaria come OpenF1 temporaneamente giù).
            var previousById = (previous?.Events ?? []).ToDictionary(e => e.Id);
            events = events
                .Select(e =>
                {
                    if (!previousById.TryGetValue(e.Id, out var old)) return e;
                    if (old.Videos.Count > 0) e = e with { Videos = old.Videos };
                    if (e.Result is null && old.Result is not null) e = e with { Result = old.Result, Status = old.Status };
                    return e;
                })
                .ToList();
        }

        // Highlights solo per la stagione corrente: lo storico dei video si valuterà a parte.
        if (seasonsArg is null && videoSources.TryGetValue(competition.Id, out var channels))
        {
            foreach (var channel in channels)
            {
                try
                {
                    events = await channel.AttachAsync(events, ct);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"AVVISO video {channel.SourceName} {season}: {ex.Message}");
                }
            }

            // Ordine fisso dei video in ogni evento, anche quando un canale li pubblica dopo l'altro.
            var order = channels.Select(c => c.SourceName).ToList();
            events = events
                .Select(e => e.Videos.Count < 2 ? e : e with { Videos = e.Videos.OrderBy(v => order.IndexOf(v.Source ?? "")).ToList() })
                .ToList();
        }

        // Se le classifiche non arrivano si tengono quelle dell'esecuzione precedente.
        IReadOnlyList<StandingsTable>? standings;
        try
        {
            standings = await source.FetchStandingsAsync(season, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AVVISO classifiche {competition.Name} {season}: {ex.Message}");
            standings = previous?.Standings;
        }

        await publisher.WriteSeasonAsync(competition.Id, season, new JsonPublisher.SeasonData(events, standings), now, ct);
        var standingsInfo = standings is null ? "nessuna classifica" : string.Join(", ", standings.Select(t => $"{t.Title} {t.Rows.Count}"));
        Console.WriteLine($"{competition.Name} {season}: {events.Count} eventi, {standingsInfo}");
        return events;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERRORE {competition.Name} {season}: {ex.Message}");
        return null;
    }
}

// Per le prove in locale le chiavi stanno nel file .env (escluso da Git); su GitHub arrivano dai Secrets.
static string? ReadDotEnv(string name) =>
    File.Exists(".env")
        ? File.ReadLines(".env")
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim() == name)
            .Select(parts => parts[1].Trim())
            .FirstOrDefault()
        : null;

// "1950-2025" oppure "2019,2021,2023". "2025-26" è una stagione sola, non un intervallo di anni.
static IEnumerable<string> ParseSeasons(string value) =>
    value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(part => part.Split('-') is [var from, var to] && from.Length == to.Length
            ? Enumerable.Range(int.Parse(from, CultureInfo.InvariantCulture),
                    int.Parse(to, CultureInfo.InvariantCulture) - int.Parse(from, CultureInfo.InvariantCulture) + 1)
                .Select(y => y.ToString(CultureInfo.InvariantCulture))
            : [part]);
