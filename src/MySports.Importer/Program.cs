// Importatore MySports: scarica calendari e risultati dalle fonti esterne e genera i file JSON per l'app.
// Uso: dotnet run --project src/MySports.Importer -- [--out <cartella>]
using MySports.Importer.Models;
using MySports.Importer.Output;
using MySports.Importer.Sources;

var outputDir = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault() ?? "out";
var window = TimeSpan.FromDays(14); // orizzonte di upcoming.json e results.json

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("MySports/0.1");

IEventSource[] sources = [new JolpicaF1Source(http)];

var publisher = new JsonPublisher(outputDir);
var now = DateTimeOffset.UtcNow;
var ct = CancellationToken.None;
var allEvents = new List<SportEvent>();
var published = new List<(Competition, string)>();
var failures = 0;

foreach (var source in sources)
{
    var competition = source.Competition;
    var season = source.CurrentSeason(now);
    var previous = await publisher.LoadSeasonAsync(competition.Id, season, ct);
    try
    {
        var events = await source.FetchSeasonAsync(season, ct);

        // I video trovati nelle esecuzioni precedenti non vanno persi: le fonti non li forniscono.
        var previousVideos = (previous ?? []).ToDictionary(e => e.Id, e => e.Videos);
        events = events
            .Select(e => previousVideos.TryGetValue(e.Id, out var videos) && videos.Count > 0 ? e with { Videos = videos } : e)
            .ToList();

        await publisher.WriteSeasonAsync(competition.Id, season, events, now, ct);
        allEvents.AddRange(events);
        published.Add((competition, season));
        Console.WriteLine($"{competition.Name} {season}: {events.Count} eventi");
    }
    catch (Exception ex)
    {
        // Fonte non raggiungibile o formato cambiato: si tengono i dati dell'esecuzione precedente, se ci sono.
        failures++;
        Console.Error.WriteLine($"ERRORE {competition.Name} {season}: {ex.Message}");
        if (previous is not null)
        {
            allEvents.AddRange(previous);
            published.Add((competition, season));
        }
    }
}

await publisher.WriteIndexesAsync(published, allEvents, now, window, ct);
Console.WriteLine($"File generati in {Path.GetFullPath(outputDir)}");
return failures == 0 ? 0 : 1;
