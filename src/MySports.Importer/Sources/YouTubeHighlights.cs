using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Aggiunge agli eventi gli highlights pubblicati da un canale YouTube ufficiale (YouTube Data API v3).
// I video si abbinano con una regola sul titolo, diversa per ogni canale, e per data di pubblicazione.
// F1: per tipo di sessione, perché i nomi dei GP nei titoli non sono regolari (es. "Bahrain Grand Prix in Malaysia").
// Calcio: per squadre di casa e trasferta.
// matcher: titolo -> condizione sull'evento, oppure null se il video non interessa.
public sealed partial class YouTubeHighlights(
    HttpClient http, string apiKey, string channelId, string sourceName, Func<string, Func<SportEvent, bool>?> matcher)
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";
    // 50 video per pagina, 1 unità di quota ciascuna. I canali Sky Sport e DAZN pubblicano circa 1000 video al mese.
    private const int MaxPages = 200;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(3); // pubblicazione entro 3 giorni dalla sessione
    private static readonly HashSet<string> Sessions = ["race", "qualifying", "sprint", "sprint_qualifying", "match"];

    public string SourceName => sourceName;

    // classify: titolo -> sessione ("race", "qualifying", "sprint", "sprint_qualifying") oppure null.
    private static Func<string, Func<SportEvent, bool>?> BySession(Func<string, string?> classify) =>
        title => classify(title) is { } session ? e => e.Session == session : null;

    // La regola deve catturare le due squadre, nell'ordine casa-trasferta.
    private static Func<string, Func<SportEvent, bool>?> ByTeams(Regex rule) =>
        title => rule.Match(title) is { Success: true } m
            ? e => IsTeam(e.HomeTeam, m.Groups[1].Value) && IsTeam(e.AwayTeam, m.Groups[2].Value)
            : null;

    private static bool IsTeam(Team? team, string name)
    {
        name = name.Trim();
        return team?.ShortName is { } shortName
            && string.Equals(Plain(shortName), Plain(TeamAliases.GetValueOrDefault(name, name)), StringComparison.OrdinalIgnoreCase);
    }

    // Senza accenti: i canali scrivono sia "Atlético" sia "Atletico", e "Fenerbahce" per "Fenerbahçe".
    private static string Plain(string text) =>
        new(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    // Nomi usati nei titoli dei canali -> nome breve della squadra nei dati.
    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Manchester City"] = "Man City",
        ["Manchester United"] = "Man United",
        ["B. Dortmund"] = "Dortmund",
        ["Borussia Dortmund"] = "Dortmund",
        ["Bayern Monaco"] = "Bayern",
        ["Betis"] = "Real Betis",
        ["Sporting"] = "Sporting CP",
        ["Bodo/Glimt"] = "Bodø/Glimt",
    };

    // Sky Sport (Italia): "Como-Venezia 1-0: gol e highlights | Serie A". A volte senza punteggio o con
    // "gli highlights". Il canale ha anche altri sport ("Basket Serie A, ...") e clip delle singole azioni.
    public static YouTubeHighlights SkySportSerieA(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCAvNr5DMVncoJnJAotXOjOQ", "Sky Sport", ByTeams(SkySerieATitle()));

    [GeneratedRegex(@"^([^:|]+?)-([^:|]+?)(?:\s+\d+-\d+)?:\s*(?:gol e |gli )?highlights\s*\|\s*Serie A\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SkySerieATitle();

    // Sky Sport (Italia): "Lens-Como 1-9: gol e highlights | Champions League". Esclusi gli "extended highlights",
    // pubblicati solo per alcune partite, che doppierebbero il video dello stesso canale.
    public static YouTubeHighlights SkySportChampionsLeague(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCAvNr5DMVncoJnJAotXOjOQ", "Sky Sport", ByTeams(SkyChampionsLeagueTitle()));

    [GeneratedRegex(@"^([^:|]+?)-([^:|]+?)(?:\s+\d+-\d+)?:\s*(?:gol e |gli )?highlights\s*\|\s*Champions League\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SkyChampionsLeagueTitle();

    // DAZN Italia: "TRIS Parma, SPROFONDA la DEA : Atalanta-Parma 0-3 | Serie A Enilive | DAZN Highlights".
    // La parte finale varia molto ("| DAZN", "| DAZN | Highlights", "| Highlights DAZN"). Le partite di stagioni
    // passate ripubblicate riportano l'anno dopo "Serie A" ("Serie A 2025-26") e così restano escluse.
    public static YouTubeHighlights DaznSerieA(HttpClient http, string apiKey) =>
        new(http, apiKey, "UC7N8IuYFhU8Hs1ruCTsXTbg", "DAZN", ByTeams(DaznSerieATitle()));

    [GeneratedRegex(@"(?:^|[:|])\s*([^:|]+?)-([^:|]+?)(?:\s+\d+-\d+)?\s*\|\s*Serie A(?: Enilive)?\s*\|\s*(?:DAZN|Highlights)", RegexOptions.IgnoreCase)]
    private static partial Regex DaznSerieATitle();

    // Canale ufficiale Formula 1, in inglese: "Race Highlights | 2026 Mexico City Grand Prix" -> race.
    // Esclusi FP ed "Extended Highlights" (gare storiche ripubblicate).
    public static YouTubeHighlights FormulaOne(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCB_qr75-ydFVKSF9Dmo6izg", "Formula 1", BySession(title =>
            title.StartsWith("Sprint Qualifying Highlights", StringComparison.OrdinalIgnoreCase) ? "sprint_qualifying"
            : title.StartsWith("Sprint Highlights", StringComparison.OrdinalIgnoreCase) ? "sprint"
            : title.StartsWith("Qualifying Highlights", StringComparison.OrdinalIgnoreCase) ? "qualifying"
            : title.StartsWith("Race Highlights", StringComparison.OrdinalIgnoreCase) ? "race"
            : null));

    // Sky Sport F1 (Italia), in italiano. I titoli variano molto ("gli highlights della gara",
    // "gli highlights del GP di Singapore", "qualifica Sprint", "qualifiche shootout per la Sprint"):
    // si cercano parole chiave. Esclusi prove libere, sintesi lunga e altre categorie (F1 Academy, F2, F3).
    public static YouTubeHighlights SkySportF1(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCMQ7Gx6v-pQy_gsRoMJYzOA", "Sky Sport F1", BySession(title =>
            !Contains(title, "highlights") || SkyExcluded().IsMatch(title) ? null
            : Contains(title, "qualific") && Contains(title, "sprint") ? "sprint_qualifying"
            : Contains(title, "sprint") ? "sprint"
            : Contains(title, "qualific") ? "qualifying"
            : "race"));

    [GeneratedRegex(@"sintesi|libere|\bFP\d|academy|\bF2\b|\bF3\b", RegexOptions.IgnoreCase)]
    private static partial Regex SkyExcluded();

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    private sealed record Upload(string VideoId, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt);

    public async Task<IReadOnlyList<SportEvent>> AttachAsync(IReadOnlyList<SportEvent> events, CancellationToken ct)
    {
        // Ogni canale aggiunge al massimo un video per sessione, accanto a quelli degli altri canali.
        bool Needs(SportEvent e) =>
            e.Status == EventStatus.Finished && Sessions.Contains(e.Session) && e.Videos.All(v => v.Source != sourceName);

        var waiting = events.Where(Needs).ToList();
        if (waiting.Count == 0) return events;

        // Si leggono i caricamenti dal più recente fino alla sessione più vecchia ancora senza video.
        var oldest = waiting.Min(e => e.StartsAt);
        var uploads = await ReadUploadsSinceAsync(oldest, ct);

        var found = new Dictionary<string, Upload>();
        foreach (var upload in uploads.OrderBy(u => u.PublishedAt))
        {
            var isEvent = matcher(upload.Title);
            if (isEvent is null) continue;

            // StartsWith: la stagione può essere "2026" o "2026-27".
            var match = waiting
                .Where(e => isEvent(e) && e.StartsAt <= upload.PublishedAt && upload.PublishedAt - e.StartsAt <= MaxDelay)
                .Where(e => YearPattern().Match(upload.Title) is not { Success: true } year || e.Season.StartsWith(year.Value, StringComparison.Ordinal))
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
            return e with { Videos = [.. e.Videos, new Video(u.VideoId, u.Title, u.ThumbnailUrl, u.PublishedAt, sourceName, d.Duration, d.Embeddable ? null : false)] };
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

    private sealed record Details(string Duration, bool BlockedInItaly, bool Embeddable);

    private async Task<Dictionary<string, Details>> GetDetailsAsync(List<string> ids, CancellationToken ct)
    {
        var details = new Dictionary<string, Details>();
        foreach (var chunk in ids.Chunk(50))
        {
            var data = await HttpJson.GetAsync(http,
                $"{BaseUrl}/videos?part=contentDetails,status&id={string.Join(',', chunk)}&key={apiKey}", "YouTube", 0, ct);
            foreach (var v in data["items"]!.AsArray())
            {
                var content = v!["contentDetails"]!;
                var restriction = content["regionRestriction"];
                var blocked = restriction?["blocked"]?.AsArray().Any(c => c?.GetValue<string>() == "IT") == true
                              || restriction?["allowed"] is { } allowed && allowed.AsArray().All(c => c?.GetValue<string>() != "IT");
                var embeddable = v["status"]?["embeddable"]?.GetValue<bool>() ?? true;
                details[v["id"]!.GetValue<string>()] = new Details(FormatDuration(content["duration"]!.GetValue<string>()), blocked, embeddable);
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
