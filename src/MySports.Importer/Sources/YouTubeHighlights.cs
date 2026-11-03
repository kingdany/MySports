using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Aggiunge agli eventi gli highlights pubblicati da un canale YouTube ufficiale (YouTube Data API v3).
// I video si abbinano per tipo di sessione (dal titolo) e data di pubblicazione, perché i nomi dei GP
// nei titoli non sono sempre regolari (es. "Bahrain Grand Prix in Malaysia", anno mancante).
public sealed partial class YouTubeHighlights(HttpClient http, string apiKey, string channelId, string sourceName)
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";
    private const int MaxPages = 40;                              // 50 video per pagina, 1 unità di quota ciascuna
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(3); // pubblicazione entro 3 giorni dalla sessione

    // "Race Highlights | 2026 Mexico City Grand Prix" -> race. FP ed "Extended Highlights" (gare storiche) esclusi.
    private static readonly (string Prefix, string Session)[] Titles =
    [
        ("Sprint Qualifying Highlights", "sprint_qualifying"),
        ("Sprint Highlights", "sprint"),
        ("Qualifying Highlights", "qualifying"),
        ("Race Highlights", "race"),
    ];

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    private sealed record Upload(string VideoId, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt);

    public async Task<IReadOnlyList<SportEvent>> AttachAsync(IReadOnlyList<SportEvent> events, CancellationToken ct)
    {
        var sessions = Titles.Select(t => t.Session).ToHashSet();
        bool Needs(SportEvent e) => e.Status == EventStatus.Finished && e.Videos.Count == 0 && sessions.Contains(e.Session);

        var waiting = events.Where(Needs).ToList();
        if (waiting.Count == 0) return events;

        // Si leggono i caricamenti dal più recente fino alla sessione più vecchia ancora senza video.
        var oldest = waiting.Min(e => e.StartsAt);
        var uploads = await ReadUploadsSinceAsync(oldest, ct);

        var found = new Dictionary<string, Upload>();
        foreach (var upload in uploads.OrderBy(u => u.PublishedAt))
        {
            var session = Titles.FirstOrDefault(t => upload.Title.StartsWith(t.Prefix, StringComparison.OrdinalIgnoreCase)).Session;
            if (session is null) continue;

            var match = waiting
                .Where(e => e.Session == session && e.StartsAt <= upload.PublishedAt && upload.PublishedAt - e.StartsAt <= MaxDelay)
                .Where(e => YearPattern().Match(upload.Title) is not { Success: true } year || year.Value == e.Season)
                .MaxBy(e => e.StartsAt);
            if (match is not null) found.TryAdd(match.Id, upload);
        }
        if (found.Count == 0) return events;

        // Durata e disponibilità in Italia: i video bloccati non si mostrano.
        var details = await GetDetailsAsync(found.Values.Select(u => u.VideoId).ToList(), ct);
        var attached = 0;
        var result = events.Select(e =>
        {
            if (!found.TryGetValue(e.Id, out var u) || !details.TryGetValue(u.VideoId, out var d) || d.BlockedInItaly) return e;
            attached++;
            return e with { Videos = [new Video(u.VideoId, u.Title, u.ThumbnailUrl, u.PublishedAt, sourceName, d.Duration)] };
        }).ToList();

        Console.WriteLine($"Video {sourceName}: {attached} highlights abbinati");
        return result;
    }

    private async Task<List<Upload>> ReadUploadsSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        // La playlist "caricamenti" di un canale ha lo stesso id del canale con "UU" al posto di "UC".
        var playlistId = "UU" + channelId[2..];
        var uploads = new List<Upload>();
        string? pageToken = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var data = await HttpJson.GetAsync(http,
                $"{BaseUrl}/playlistItems?part=snippet&maxResults=50&playlistId={playlistId}&key={apiKey}" +
                (pageToken is null ? "" : $"&pageToken={pageToken}"), "YouTube", 0, ct);

            foreach (var item in data["items"]!.AsArray())
            {
                var s = item!["snippet"]!;
                var thumbs = s["thumbnails"];
                uploads.Add(new Upload(
                    s["resourceId"]!["videoId"]!.GetValue<string>(),
                    s["title"]!.GetValue<string>(),
                    (thumbs?["high"] ?? thumbs?["medium"] ?? thumbs?["default"])?["url"]?.GetValue<string>() ?? "",
                    DateTimeOffset.Parse(s["publishedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture)));
            }

            pageToken = data["nextPageToken"]?.GetValue<string>();
            if (pageToken is null || uploads[^1].PublishedAt < since) break;
        }
        return uploads;
    }

    private sealed record Details(string Duration, bool BlockedInItaly);

    private async Task<Dictionary<string, Details>> GetDetailsAsync(List<string> ids, CancellationToken ct)
    {
        var details = new Dictionary<string, Details>();
        foreach (var chunk in ids.Chunk(50))
        {
            var data = await HttpJson.GetAsync(http,
                $"{BaseUrl}/videos?part=contentDetails&id={string.Join(',', chunk)}&key={apiKey}", "YouTube", 0, ct);
            foreach (var v in data["items"]!.AsArray())
            {
                var content = v!["contentDetails"]!;
                var restriction = content["regionRestriction"];
                var blocked = restriction?["blocked"]?.AsArray().Any(c => c?.GetValue<string>() == "IT") == true
                              || restriction?["allowed"] is { } allowed && allowed.AsArray().All(c => c?.GetValue<string>() != "IT");
                details[v["id"]!.GetValue<string>()] = new Details(FormatDuration(content["duration"]!.GetValue<string>()), blocked);
            }
        }
        return details;
    }

    // "PT8M8S" -> "8:08", "PT1H2M3S" -> "1:02:03".
    private static string FormatDuration(string iso)
    {
        var t = XmlConvert.ToTimeSpan(iso);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }
}
