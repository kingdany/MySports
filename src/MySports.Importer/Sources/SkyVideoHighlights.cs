using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Highlights dal sito video.sky.it, per le partite che Sky non mette su YouTube: in Europa League il canale YouTube ha
// quasi solo quelle delle italiane, il sito tutte. I video del sito non si possono incorporare (no_embedded), quindi
// si salvano con Url e Embeddable false e l'app apre la pagina.
// Non c'è un'API documentata: la pagina di una sezione (https://video.sky.it/sport/calcio/europa-league, poi ?pag=2,
// ?pag=3...) è fatta con Next.js e ha nel JSON di __NEXT_DATA__ i suoi 16 video, dal più recente, con titolo, data,
// miniatura, durata e indirizzo. Le sitemap del sito sono ferme a gennaio 2026 e /api/ è escluso dal robots.txt, le
// pagine delle sezioni no. Verificato a novembre 2026: pagina 60 era a febbraio 2025.
// I titoli sono quelli del canale YouTube di Sky ("Benfica-Lech Poznan 3-1: gol e highlights"), quindi valgono le sue
// regole e i suoi alias; le altre voci della sezione (interviste, conferenze, gol singoli) non le rispettano.
public sealed partial class SkyVideoHighlights(
    HttpClient http, string section, string sourceName, Func<string, Func<SportEvent, bool>?> matcher) : IHighlightsSource
{
    private const string BaseUrl = "https://video.sky.it/sport/calcio/";
    // Un giro normale legge 1-2 pagine. Un recupero arriva più indietro: per l'Europa League 2026-27 da settembre 15 pagine,
    // per la Champions League da ottobre 2025 107 (16 video per pagina, la Champions ne ha di più).
    private const int MaxPages = 300;
    private static readonly TimeSpan PageDelay = TimeSpan.FromSeconds(1); // per non caricare il sito
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public string SourceName => sourceName;

    // Le stesse regole del canale YouTube di Sky.
    public static SkyVideoHighlights EuropaLeague(HttpClient http) =>
        new(http, "europa-league", "Sky Video", YouTubeHighlights.SkyEuropaLeagueMatcher);

    public static SkyVideoHighlights ChampionsLeague(HttpClient http) =>
        new(http, "champions-league", "Sky Video", YouTubeHighlights.SkyChampionsLeagueMatcher);

    public bool Matches(SportEvent e, Video video, IReadOnlyList<SportEvent> events) =>
        YouTubeHighlights.SessionFor(matcher, events, video.Title, video.PublishedAt)?.Id == e.Id;

    private sealed record Item(string Id, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt, string? Duration, string Url);

    public async Task<IReadOnlyList<SportEvent>> AttachAsync(
        IReadOnlyList<SportEvent> events, bool recoverOld, IReadOnlySet<string> preferred, CancellationToken ct)
    {
        // Come per YouTube: solo le sessioni degli ultimi giorni, tranne nel recupero.
        var now = DateTimeOffset.UtcNow;
        bool Needs(SportEvent e) =>
            e.Status == EventStatus.Finished && YouTubeHighlights.Sessions.Contains(e.Session)
            && e.Videos.All(v => v.Source != sourceName && !preferred.Contains(v.Source ?? ""))
            && (recoverOld || now - e.StartsAt <= YouTubeHighlights.MaxDelay);

        var waiting = events.Where(Needs).ToList();
        if (waiting.Count == 0) return events;

        var items = await ReadSinceAsync(waiting.Min(e => e.StartsAt), ct);

        var found = new Dictionary<string, Item>();
        foreach (var item in items.OrderBy(i => i.PublishedAt))
            if (YouTubeHighlights.SessionFor(matcher, waiting, item.Title, item.PublishedAt) is { } match)
                found.TryAdd(match.Id, item);

        var result = events
            .Select(e => found.TryGetValue(e.Id, out var i)
                ? e with { Videos = [.. e.Videos, new Video(i.Id, i.Title, i.ThumbnailUrl, i.PublishedAt, sourceName, i.Duration, false, i.Url)] }
                : e)
            .ToList();

        Console.WriteLine($"Video {sourceName} {section}: {found.Count} highlights abbinati");
        var missing = waiting.Where(e => !found.ContainsKey(e.Id)).Select(e => $"{e.StartsAt:yyyy-MM-dd HH:mm} {e.Title}").ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine($"Video {sourceName} {section}, ancora senza video: {missing.Count}");
            foreach (var m in missing.Take(10)) Console.WriteLine($"  {m}");
            if (missing.Count > 10) Console.WriteLine($"  ... e altri {missing.Count - 10}");
        }
        // I titoli che rispettano la regola ma non hanno trovato una partita: di solito un nome scritto diversamente (da
        // aggiungere agli alias di YouTubeHighlights). Solo nel periodo delle partite cercate: la sezione ha anche le altre
        // stagioni.
        var (from, to) = (waiting.Min(e => e.StartsAt), waiting.Max(e => e.StartsAt) + YouTubeHighlights.MaxDelay);
        var unmatched = items
            .Where(i => i.PublishedAt >= from && i.PublishedAt <= to && matcher(i.Title) is not null
                        && !events.Any(e => Matches(e, ToVideo(i), events)))
            .Select(i => $"{i.PublishedAt:yyyy-MM-dd HH:mm} {i.Title}")
            .ToList();
        if (unmatched.Count > 0)
        {
            Console.WriteLine($"Video {sourceName} {section}, highlights senza partita: {unmatched.Count}");
            foreach (var u in unmatched.Take(20)) Console.WriteLine($"  {u}");
            if (unmatched.Count > 20) Console.WriteLine($"  ... e altri {unmatched.Count - 20}");
        }
        return result;
    }

    private Video ToVideo(Item i) => new(i.Id, i.Title, i.ThumbnailUrl, i.PublishedAt, sourceName, i.Duration, false, i.Url);

    // Le pagine della sezione dalla più recente, fino alla prima che arriva prima di "since".
    private async Task<List<Item>> ReadSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        var items = new List<Item>();
        var stop = $"limite di {MaxPages} pagine";
        var page = 1;
        for (; page <= MaxPages; page++)
        {
            if (page > 1) await Task.Delay(PageDelay, ct);
            var url = BaseUrl + section + (page == 1 ? "" : $"?pag={page}");
            List<Item> pageItems;
            try
            {
                pageItems = ParsePage(await HttpJson.GetTextAsync(http, url, "Sky Video", 2, ct));
            }
            catch (Exception ex) when (page > 1)
            {
                // Dopo la prima pagina si tiene quello che si è letto; sulla prima è un errore (sito cambiato o giù).
                Console.Error.WriteLine($"AVVISO video {sourceName} {section}, pagina {page}: {ex.Message}");
                stop = "errore";
                break;
            }
            if (pageItems.Count == 0)
            {
                stop = "pagina vuota";
                break;
            }
            items.AddRange(pageItems);
            if (pageItems.Min(i => i.PublishedAt) < since)
            {
                stop = "arrivata alla sessione più vecchia";
                break;
            }
        }
        Console.WriteLine($"Video {sourceName} {section}: letti {items.Count} video in {Math.Min(page, MaxPages)} pagine" +
                          (items.Count == 0 ? "" : $", dal {items.Max(i => i.PublishedAt):yyyy-MM-dd HH:mm} al {items.Min(i => i.PublishedAt):yyyy-MM-dd HH:mm}") +
                          $", lettura fermata: {stop}");
        return items;
    }

    // I video della pagina, da props.pageProps.initialState.subSubsectionPage.videoList. Errore se il JSON non c'è o ha
    // un'altra forma: vuol dire che il sito è cambiato.
    private static List<Item> ParsePage(string html)
    {
        var json = NextData().Match(html);
        if (!json.Success) throw new InvalidOperationException("la pagina non ha più __NEXT_DATA__");
        var list = JsonNode.Parse(json.Groups[1].Value)?["props"]?["pageProps"]?["initialState"]?["subSubsectionPage"]?["videoList"]
                   ?? throw new InvalidOperationException("__NEXT_DATA__ non ha più subSubsectionPage.videoList");
        var items = new List<Item>();
        foreach (var v in list.AsArray())
        {
            if (v?["asset_id"] is not { } id || v["title"]?.GetValue<string>() is not { } title
                || v["page_url"]?.GetValue<string>() is not { } pageUrl
                || ParseDate(v["publication_date"]?.GetValue<string>() ?? v["val_from"]?.GetValue<string>()) is not { } published)
                continue;
            items.Add(new Item(
                id.ToString(),
                title.Trim(),
                v["video_still"]?.GetValue<string>() ?? v["thumb"]?.GetValue<string>() ?? "",
                published,
                FormatDuration(v["duration"]?.GetValue<string>()),
                pageUrl));
        }
        return items;
    }

    [GeneratedRegex(@"<script id=""__NEXT_DATA__""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextData();

    // "06/11/2026 00:24:53", ora italiana.
    private static DateTimeOffset? ParseDate(string? text)
    {
        if (!DateTime.TryParseExact(text, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return null;
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }

    // "00:01:43" -> "1:43", "01:02:03" -> "1:02:03"; nessuna durata se manca o è zero.
    private static string? FormatDuration(string? text)
    {
        if (!TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var t) || t == TimeSpan.Zero) return null;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

}
