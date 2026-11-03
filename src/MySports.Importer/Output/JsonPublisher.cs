using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MySports.Importer.Models;

namespace MySports.Importer.Output;

// Scrive i file JSON statici letti dall'app:
//   meta.json             competizioni disponibili e data di generazione
//   upcoming.json         eventi programmati o in corso nei prossimi giorni
//   results.json          eventi conclusi negli ultimi giorni
//   {competizione}/{stagione}.json   stagione completa
public sealed class JsonPublisher(string outputDir)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // accenti leggibili ("Autódromo" invece di "Autódromo")
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed record SeasonFile(string Competition, string Season, DateTimeOffset GeneratedAt, IReadOnlyList<SportEvent> Events);
    private sealed record EventsFile(DateTimeOffset GeneratedAt, IReadOnlyList<SportEvent> Events);
    private sealed record MetaFile(DateTimeOffset GeneratedAt, IReadOnlyList<CompetitionInfo> Competitions);
    private sealed record CompetitionInfo(string Id, string Sport, string Name, string ShortName, string CurrentSeason);

    public async Task<IReadOnlyList<SportEvent>?> LoadSeasonAsync(string competition, string season, CancellationToken ct)
    {
        var path = SeasonPath(competition, season);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<SeasonFile>(stream, Options, ct);
        return file?.Events;
    }

    public Task WriteSeasonAsync(string competition, string season, IReadOnlyList<SportEvent> events, DateTimeOffset now, CancellationToken ct) =>
        WriteAsync(SeasonPath(competition, season), new SeasonFile(competition, season, now, events), ct);

    public async Task WriteIndexesAsync(
        IReadOnlyList<(Competition Competition, string Season)> competitions,
        IReadOnlyList<SportEvent> events,
        DateTimeOffset now,
        TimeSpan window,
        CancellationToken ct)
    {
        var upcoming = events
            .Where(e => e.Status is EventStatus.Scheduled or EventStatus.Live && e.StartsAt <= now + window)
            .OrderBy(e => e.StartsAt)
            .ToList();
        var results = events
            .Where(e => e.Status == EventStatus.Finished && e.StartsAt >= now - window)
            .OrderByDescending(e => e.StartsAt)
            .ToList();
        var meta = competitions
            .Select(c => new CompetitionInfo(c.Competition.Id, c.Competition.Sport, c.Competition.Name, c.Competition.ShortName, c.Season))
            .ToList();

        await WriteAsync(Path.Combine(outputDir, "upcoming.json"), new EventsFile(now, upcoming), ct);
        await WriteAsync(Path.Combine(outputDir, "results.json"), new EventsFile(now, results), ct);
        await WriteAsync(Path.Combine(outputDir, "meta.json"), new MetaFile(now, meta), ct);
    }

    private string SeasonPath(string competition, string season) => Path.Combine(outputDir, competition, $"{season}.json");

    private static async Task WriteAsync<T>(string path, T content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, content, Options, ct);
    }
}
