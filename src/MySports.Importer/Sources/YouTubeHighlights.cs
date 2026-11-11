using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Aggiunge agli eventi gli highlights pubblicati da un canale YouTube ufficiale (YouTube Data API v3).
// I video si abbinano con una regola sul titolo, diversa per ogni canale, e per data di pubblicazione.
// F1 e MotoGP: per tipo di sessione, perché i nomi dei GP nei titoli non sono regolari (es. "GP Usa", "GP d' Ungheria").
// Calcio: per squadre di casa e trasferta.
// matcher: titolo -> condizione sull'evento, oppure null se il video non interessa.
public sealed partial class YouTubeHighlights(
    HttpClient http, string apiKey, string channelId, string sourceName, Func<string, Func<SportEvent, bool>?> matcher)
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";
    // 50 video per pagina, 1 unità di quota ciascuna. I canali Sky Sport e DAZN pubblicano circa 1000 video al mese:
    // il limite serve solo al recupero delle stagioni passate, i giri normali leggono pochi giorni.
    private const int MaxPages = 1000;
    // La playlist dei caricamenti dà al massimo gli ultimi 20.000 video del canale: DAZN Italia (circa 31.700) arriva così
    // solo a novembre 2024, Sky Sport (circa 36.900) a settembre 2024.
    private const int UploadsLimit = 20_000;
    // Ricerca: al massimo circa 500 risultati per richiesta, quindi si cerca a periodi di MaxDelay (un turno di Serie A su
    // DAZN sono circa 85 video, 2 pagine). Senza parole chiave: con "Serie A highlights" la ricerca perdeva video
    // ("... | Serie A Enilive | DAZN Higlights") senza restituirne meno.
    private const int MaxSearchPages = 10;
    // Le ricerche hanno una quota giornaliera a parte ("Search Queries per day", 100 richieste) che non tocca quella delle
    // altre chiamate: questo è il massimo di pagine per esecuzione, per tutti i canali insieme. Se la quota finisce prima,
    // la ricerca fallisce ma i dettagli dei video funzionano ancora. Una stagione intera oltre il limite (es. la Serie A
    // 2023-24 su DAZN, circa 140 pagine) ne richiede di più: il recupero si ferma, salva quello che ha trovato e al giro
    // dopo (il giorno seguente) riprende dalle sessioni ancora senza video.
    private const int SearchBudget = 100;
    private static int searchPagesUsed;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(3); // pubblicazione entro 3 giorni dalla sessione
    private static readonly HashSet<string> Sessions = ["race", "qualifying", "sprint", "sprint_qualifying", "match"];

    public string SourceName => sourceName;

    // classify: titolo -> sessione ("race", "qualifying", "sprint", "sprint_qualifying") oppure null.
    private static Func<string, Func<SportEvent, bool>?> BySession(Func<string, string?> classify) =>
        title => classify(title) is { } session ? e => e.Session == session : null;

    // La regola deve catturare "Casa-Trasferta" in un solo gruppo. Si prova ogni trattino, perché alcuni nomi
    // ne contengono uno ("Union Saint-Gilloise-Marsiglia").
    private static Func<string, Func<SportEvent, bool>?> ByTeams(Regex rule) =>
        title => rule.Match(title) is { Success: true } m
            ? e => TeamSplits(m.Groups[1].Value).Any(p => IsTeam(e.HomeTeam, p.Home) && IsTeam(e.AwayTeam, p.Away))
            : null;

    private static IEnumerable<(string Home, string Away)> TeamSplits(string teams)
    {
        for (var i = teams.IndexOf('-'); i >= 0; i = teams.IndexOf('-', i + 1))
            yield return (teams[..i], teams[(i + 1)..]);
    }

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
        ["Sporting CP"] = "Sporting",
        ["Sporting Lisbona"] = "Sporting",
        ["Bodo/Glimt"] = "Bodø/Glimt",
        ["Bodo Glimt"] = "Bodø/Glimt",
        ["Bodo"] = "Bodø/Glimt",
        ["Shakhtar Donetsk"] = "Shakhtar",
        ["Bayer Leverkusen"] = "Leverkusen",
        ["Qarabag"] = "Qarabağ Ağdam",
        ["Olympiacos"] = "Olympiakos",
        ["Marsiglia"] = "Marseille",
        ["Pafos"] = "Paphos FC",
        ["Copenhagen"] = "København",
        ["Copenaghen"] = "København",
        ["Kairat"] = "FK Kairat",
        ["Kairat Almaty"] = "FK Kairat",
        ["Eintracht"] = "Frankfurt",
        ["Eintracht Francoforte"] = "Frankfurt",
        ["Athletic Club"] = "Athletic",
        ["Athletic Bilbao"] = "Athletic",
        ["Union ST Gilloise"] = "Union SG",
        ["Union Saint-Gilloise"] = "Union SG",
        ["Bruges"] = "Club Brugge",
        ["Brugge"] = "Club Brugge",
        ["PSV Eindhoven"] = "PSV",
        ["Atletico"] = "Atletico Madrid",
        ["Hellas Verona"] = "Verona",
        ["Lask Linz"] = "LASK",
        ["Sabah Baku"] = "Sabah",
        ["Juve"] = "Juventus",
        ["Dinamo Zagabria"] = "Dinamo Zagreb",
        ["Stella Rossa"] = "Crvena Zvedza", // la fonte scrive "Zvedza"
        ["Salisburgo"] = "RB Salzburg",
        ["Sparta Praga"] = "Sparta Praha",
        ["Real"] = "Real Madrid",
    };

    // Sky Sport (Italia): "Como-Venezia 1-0: gol e highlights | Serie A". A volte senza punteggio, con
    // "gli highlights", con la virgola al posto dei due punti o con il punteggio dopo ("Inter-Juventus: 3-2 gol e highlights"),
    // con "Serie A" ripetuto ("gol e highlights Serie A | Serie A") o scritto "SerieA".
    // Il canale ha anche altri sport ("Basket Serie A, ...") e clip delle singole azioni.
    // Di riserva a DAZN, per le partite senza il suo video.
    public static YouTubeHighlights SkySportSerieA(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCAvNr5DMVncoJnJAotXOjOQ", "Sky Sport", ByTeams(SkySerieATitle()));

    [GeneratedRegex(@"^([^:|]+?-[^:|]+?)(?:\s+\d+-\d+)?[:,]\s*(?:\d+-\d+\s+)?(?:gol e |gli )?highlights(?:\s+Serie\s?A)?\s*\|\s*Serie\s?A\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SkySerieATitle();

    // Sky Sport (Italia): "Lens-Como 1-9: gol e highlights | Champions League". Dopo i supplementari o i rigori
    // il punteggio è seguito da "dts" o "dcr", anche tra parentesi ("Sporting-Bodø/Glimt 5-0 dts: ...",
    // "Liverpool-PSG 0-1 (1-5 d.c.r): ..."), e a volte manca "| Champions League" ("Atalanta-Bayern Monaco 1-6: gol e
    // highlights"), è attaccato ("| ChampionsLeague") o è "| Finale Champions League": il titolo deve comunque finire lì,
    // quindi le partite "| Serie A" restano escluse. Tra il punteggio e "highlights" può esserci una frase chiusa da punto
    // o virgola ("Arsenal-Psg 0-1, gol di Dembelé. Gli HIGHLIGHTS | ..."), e a volte "highlights" manca del tutto
    // ("Celtic-Slovan Bratislava 5-1 | Champions League"): allora servono il punteggio e "Champions League".
    // Esclusi gli "extended highlights" e gli "highlights lunghi", pubblicati solo per alcune partite, che
    // doppierebbero il video dello stesso canale.
    public static YouTubeHighlights SkySportChampionsLeague(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCAvNr5DMVncoJnJAotXOjOQ", "Sky Sport", ByTeams(SkyChampionsLeagueTitle()));

    [GeneratedRegex(@"^([^:|]+?-[^:|]+?)(?:\s+\d+-\d+(?:\s+d\.?[tc]\.?[sr]\.?|\s*\(\d+-\d+\s+d\.?[tc]\.?[sr]\.?\))?)?(?:[:,]\s*(?:[^|:]*?[.,]\s*)?(?:\d+-\d+\s+)?(?:gol e |gli )?highlights(?:\s*\|\s*(?:Finale\s+)?Champions\s?League)?|(?<=\d)\s*\|\s*(?:Finale\s+)?Champions\s?League)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SkyChampionsLeagueTitle();

    // Prime Video Sport (Italia), di riserva a Sky: trasmette una partita il mercoledì. "PORTO - NAPOLI gol e highlights |
    // UEFA Champions League", a volte con il trattino lungo e senza spazi ("NAPOLI–CHELSEA"), con il punteggio
    // ("Ajax - Inter 0-2 Gol e Highlights | ..."), con "| Gol e Highlights" ("VILLARREAL – JUVENTUS | Gol e Highlights | ...")
    // o con "v" ("Benfica v Fenerbahce 1-0 Highlights | UEFA Champions League Playoff"). Nel 2024-25 scriveva
    // "Gol & Highlights" e a volte l'andata o i rigori tra parentesi ("PSG - Arsenal  2-1 (3-1) Gol & Highlights | ...",
    // "Atletico Madrid-Real Madrid 1-0 (2-4 d.c.r.) Gol & Highlights | ..."). Il canale ha anche l'NBA
    // ("NBA, Chicago Bulls-Philadelphia 76ers | Highlights"), esclusa perché dopo deve esserci "Champions League".
    public static YouTubeHighlights PrimeVideoChampionsLeague(HttpClient http, string apiKey)
    {
        var byTeams = ByTeams(PrimeChampionsLeagueTitle());
        return new(http, apiKey, "UCc-ZZMy22fhnmfSgPb80-1w", "Prime Video", title => byTeams(PrimeDash().Replace(title, "-")));
    }

    [GeneratedRegex(@"\s*–\s*|\s+v\s+")]
    private static partial Regex PrimeDash();

    [GeneratedRegex(@"^([^:|]+?-[^:|]+?)(?:\s+\d+-\d+)?(?:\s*\([^)]*\))?\s*(?:\|\s*)?(?:gol (?:e|&) )?highlights\s*\|[^|]*Champions League", RegexOptions.IgnoreCase)]
    private static partial Regex PrimeChampionsLeagueTitle();

    // DAZN Italia: "TRIS Parma, SPROFONDA la DEA : Atalanta-Parma 0-3 | Serie A Enilive | DAZN Highlights".
    // Prima delle squadre c'è un testo libero, separato in tanti modi ("È tornato RAFA LEAO! Cagliari-Milan 0-1",
    // "vince ITALIANO Cremonese-Bologna 1-2"), e anche la parte finale varia ("| DAZN", "Serie A Enilive ! DAZN",
    // "3-0 Serie A Enilive | DAZN Highlights"): invece di ricavare i nomi dal titolo si cerca "Casa-Trasferta"
    // seguito da "|", "!" o "Serie A", con il punteggio in mezzo oppure "Highlights" nel titolo ("Udinese-Inter | Serie A
    // Enilive | DAZN Highlights"): così restano escluse analisi e interviste ("Chivu parla... | Inter-Como | Serie A Enilive | DAZN").
    // Le partite di stagioni passate ripubblicate riportano l'anno dopo "Serie A" ("Serie A 2025-26") e restano escluse.
    // "Serie A" a volte è attaccato ("SerieAEnilive") e la Juventus a volte è "Juve" (in TeamAliases).
    // A volte "Serie A" manca ("... Milan-Torino 2-2 | DAZN Highlights"): allora non deve esserci un'altra competizione del
    // canale ("Mantova-Cosenza 3-2 | Serie BKT", "| LaLiga |"). Esclusi sempre Serie A Femminile e Primavera.
    public static YouTubeHighlights DaznSerieA(HttpClient http, string apiKey) =>
        new(http, apiKey, "UC7N8IuYFhU8Hs1ruCTsXTbg", "DAZN", title =>
        {
            if (!Contains(title, "DAZN") || DaznRerun().IsMatch(title) || DaznExcluded().IsMatch(title)
                || !SerieAMention().IsMatch(title) && DaznOtherCompetition().IsMatch(title)) return null;
            var plain = Plain(title).ToLowerInvariant().Replace('–', '-'); // a volte con il trattino lungo
            var highlights = Contains(title, "highlights");
            return e => TeamNames(e.HomeTeam).Any(home => TeamNames(e.AwayTeam).Any(away => HasMatch(plain, home, away, highlights)));
        });

    [GeneratedRegex(@"\bSerie\s?A", RegexOptions.IgnoreCase)]
    private static partial Regex SerieAMention();

    [GeneratedRegex(@"\bSerie\s?A\s+(?:19|20)\d{2}", RegexOptions.IgnoreCase)]
    private static partial Regex DaznRerun();

    [GeneratedRegex(@"femminile|women|primavera", RegexOptions.IgnoreCase)]
    private static partial Regex DaznExcluded();

    [GeneratedRegex(@"\bSerie\s?[BC]|\bLa\s?Liga\b|\bLiga\b|\bCoppa\b|Supercoppa|Champions|Europa League|Conference|Bundesliga|Premier|Ligue|\bUEFA\b|\bFIFA\b", RegexOptions.IgnoreCase)]
    private static partial Regex DaznOtherCompetition();
    // Dopo "Casa-Trasferta": punteggio (facoltativo) e poi "|", "!" o "Serie A".
    [GeneratedRegex(@"^\s*(\d+\s*-\s*\d+\s*)?(?:[|!]|serie\s?a)")]
    private static partial Regex AfterTeams();

    // Nome breve della squadra e nomi che i canali usano al suo posto ("Hellas Verona" per "Verona"), senza accenti.
    private static IEnumerable<string> TeamNames(Team? team) =>
        team?.ShortName is not { } shortName ? []
        : TeamAliases.Where(a => string.Equals(Plain(a.Value), Plain(shortName), StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Key).Prepend(shortName).Select(n => Plain(n).ToLowerInvariant()).Distinct();

    // "...! cagliari-milan 0-1 | serie a ..." contiene la partita cagliari-milan, a inizio parola; senza punteggio
    // vale solo se il titolo dice "highlights".
    private static bool HasMatch(string title, string home, string away, bool highlights)
    {
        var pair = $"{home}-{away}";
        for (var i = title.IndexOf(pair, StringComparison.Ordinal); i >= 0; i = title.IndexOf(pair, i + 1, StringComparison.Ordinal))
        {
            if (i > 0 && char.IsLetter(title[i - 1])) continue;
            var after = AfterTeams().Match(title[(i + pair.Length)..]);
            if (after.Success && (after.Groups[1].Success || highlights)) return true;
        }
        return false;
    }

    // Un video già salvato resta solo se spetta ancora a quella sessione: deve rispettare la regola del canale ed essere
    // la sessione che gli si darebbe oggi tra quelle della stagione. Quando una regola o un orario cambiano (es. un video
    // della gara finito sulla Sprint perché la gara risultava non ancora iniziata), i video presi per sbaglio spariscono
    // anche dalle stagioni già pubblicate.
    public bool Matches(SportEvent e, Video video, IReadOnlyList<SportEvent> events) =>
        SessionFor(events, video.Title, video.PublishedAt)?.Id == e.Id;

    // Sessione a cui spetta un video: tra quelle concluse che il titolo indica e iniziate al massimo MaxDelay prima della
    // pubblicazione, la più recente (un video da gara MotoGP vale anche per la Sprint del giorno prima).
    // StartsWith: la stagione può essere "2026" o "2026-27".
    private SportEvent? SessionFor(IEnumerable<SportEvent> events, string title, DateTimeOffset publishedAt) =>
        matcher(title) is not { } isEvent ? null
        : events
            .Where(e => e.Status == EventStatus.Finished && Sessions.Contains(e.Session) && isEvent(e))
            .Where(e => e.StartsAt <= publishedAt && publishedAt - e.StartsAt <= MaxDelay)
            .Where(e => YearPattern().Match(title) is not { Success: true } year || e.Season.StartsWith(year.Value, StringComparison.Ordinal))
            .MaxBy(e => e.StartsAt);

    // Canale ufficiale Formula 1, in inglese, di riserva per le sessioni senza video Sky:
    // "Race Highlights | 2026 Mexico City Grand Prix" -> race. Esclusi FP ed "Extended Highlights" (gare storiche ripubblicate).
    public static YouTubeHighlights FormulaOne(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCB_qr75-ydFVKSF9Dmo6izg", "Formula 1", BySession(title =>
            title.StartsWith("Sprint Qualifying Highlights", StringComparison.OrdinalIgnoreCase) ? "sprint_qualifying"
            : title.StartsWith("Sprint Highlights", StringComparison.OrdinalIgnoreCase) ? "sprint"
            : title.StartsWith("Qualifying Highlights", StringComparison.OrdinalIgnoreCase) ? "qualifying"
            : title.StartsWith("Race Highlights", StringComparison.OrdinalIgnoreCase) ? "race"
            : null));

    // Sky Sport F1 (Italia), in italiano. I titoli variano molto ("gli highlights della gara",
    // "gli highlights del GP di Singapore", "qualifica Sprint", "qualifiche shootout per la Sprint", a volte "shootout"
    // senza "Sprint" e scritto male, "shootuot"): si cercano parole chiave. Esclusi prove libere, sintesi lunga e altre
    // categorie (F1 Academy, F2, F3, i rally: "WRC Estonia, gli highlights del sabato", "Rally Messico, gli highlights ...").
    public static YouTubeHighlights SkySportF1(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCMQ7Gx6v-pQy_gsRoMJYzOA", "Sky Sport F1", BySession(title =>
            !Contains(title, "highlights") || SkyExcluded().IsMatch(title) ? null
            : Contains(title, "qualific") && (Contains(title, "sprint") || Contains(title, "shoot")) ? "sprint_qualifying"
            : Contains(title, "sprint") ? "sprint"
            : Contains(title, "qualific") ? "qualifying"
            : "race"));

    [GeneratedRegex(@"sintesi|libere|\bFP\d|academy|\bF2\b|\bF3\b|\bWRC\b|\bRally", RegexOptions.IgnoreCase)]
    private static partial Regex SkyExcluded();

    // Sky Sport MotoGP (Italia), in italiano. I titoli variano ("MotoGP, GP del Qatar: gli highlights della gara a Lusail",
    // "MotoGP, gli highlights delle qualifiche del GP di Spagna", "MotoGP a Misano, gli highlights del GP di San Marino"):
    // si cercano parole chiave, e il GP senza altro è la gara. "MotoGP" può stare anche in fondo ("Marquez (Ducati) vince
    // GP Argentina MotoGP: gli highlights") o mancare ("Bastianini (Ducati) vince il GP Gran Bretagna: gli highlights",
    // "Highlights Sprint Misano: ..."): allora serve "GP" o "Sprint", e senza "MotoGP" passano anche le altre serie del
    // canale (Superbike, motocross: "SBK, GP del Portogallo: gli highlights di Gara 1", "MXGP, GP Fiandre: ..."), escluse
    // per nome. Esclusi anche sintesi e highlights lunghi, pre-qualifiche, libere, retropodio, conferenze stampa e le altre
    // classi, che a inizio stagione erano nei titoli "MotoGP, ..." ("gli highlights del GP della Thailandia della Moto2").
    // A volte la Sprint è chiamata "gara" ("MotoGP, GP di Aragon: gli highlights della gara in Spagna", il sabato): un video
    // da gara vale anche per la Sprint, e tra le sessioni già iniziate si prende la più recente, quindi il sabato la Sprint.
    public static YouTubeHighlights SkySportMotoGP(HttpClient http, string apiKey) =>
        new(http, apiKey, "UCGlmF5tM5DeT5Dl1BQVItYg", "Sky Sport MotoGP", title =>
            !MotoGPMention().IsMatch(title) || !Contains(title, "highlights") || SkyMotoGPExcluded().IsMatch(title) ? null
            : Contains(title, "qualific") ? e => e.Session == "qualifying"
            : Contains(title, "sprint") ? e => e.Session == "sprint"
            : e => e.Session is "race" or "sprint");

    [GeneratedRegex(@"\bMotoGP\b|\bGP\b|\bSprint\b", RegexOptions.IgnoreCase)]
    private static partial Regex MotoGPMention();

    [GeneratedRegex(@"sintesi|lunghi|pre[- ]?qualific|libere|warm[- ]?up|retropodio|conferenza|\bMoto\s?[23E]\b|superbike|SBK\b|MXGP|motocross", RegexOptions.IgnoreCase)]
    private static partial Regex SkyMotoGPExcluded();

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    private sealed record Upload(string VideoId, string Title, string ThumbnailUrl, DateTimeOffset PublishedAt);

    // recoverOld: cerca anche le sessioni vecchie, per una competizione nuova o una stagione passata.
    // preferred: canali che vengono prima di questo; una sessione che ha già un loro video non si cerca.
    public async Task<IReadOnlyList<SportEvent>> AttachAsync(
        IReadOnlyList<SportEvent> events, bool recoverOld, IReadOnlySet<string> preferred, CancellationToken ct)
    {
        // Ogni canale aggiunge al massimo un video per sessione.
        // Solo sessioni recenti: per un video si accetta al massimo MaxDelay, e una sola sessione vecchia
        // rimasta senza video costringerebbe a rileggere a ogni giro tutti i caricamenti fino alla sua data.
        var now = DateTimeOffset.UtcNow;
        bool Needs(SportEvent e) =>
            e.Status == EventStatus.Finished && Sessions.Contains(e.Session)
            && e.Videos.All(v => v.Source != sourceName && !preferred.Contains(v.Source ?? ""))
            && (recoverOld || now - e.StartsAt <= MaxDelay);

        var waiting = events.Where(Needs).ToList();
        if (waiting.Count == 0) return events;

        // Si leggono i caricamenti dal più recente fino alla sessione più vecchia ancora senza video.
        var oldest = waiting.Min(e => e.StartsAt);
        var uploads = await ReadUploadsSinceAsync(oldest, ct);

        // Se la playlist si è fermata al limite prima di arrivarci, nel recupero le sessioni più vecchie si cercano.
        // Una playlist finita prima del limite è arrivata al primo video del canale: prima non c'è niente da cercare.
        if (recoverOld && uploads.Count >= UploadsLimit && uploads[^1].PublishedAt >= oldest)
        {
            try
            {
                await SearchBeforeAsync(waiting, uploads[^1].PublishedAt, uploads, ct);
            }
            catch (Exception ex)
            {
                // Quota finita o errore: si abbina quello che si è trovato fin qui.
                Console.Error.WriteLine($"AVVISO ricerca video {sourceName}: {ex.Message}");
            }
        }

        var found = new Dictionary<string, Upload>();
        foreach (var upload in uploads.OrderBy(u => u.PublishedAt))
        {
            if (SessionFor(waiting, upload.Title, upload.PublishedAt) is { } match) found.TryAdd(match.Id, upload);
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

    // Cerca i video pubblicati prima di "before" nei MaxDelay dopo le sessioni ancora senza video, e li aggiunge a uploads.
    // Le finestre che si toccano si uniscono, poi si dividono in periodi di MaxDelay.
    private async Task SearchBeforeAsync(List<SportEvent> waiting, DateTimeOffset before, List<Upload> uploads, CancellationToken ct)
    {
        var windows = new List<(DateTimeOffset From, DateTimeOffset To)>();
        foreach (var e in waiting.Where(e => e.StartsAt < before).OrderBy(e => e.StartsAt))
        {
            var to = e.StartsAt + MaxDelay < before ? e.StartsAt + MaxDelay : before;
            if (windows.Count > 0 && e.StartsAt <= windows[^1].To)
                windows[^1] = (windows[^1].From, to > windows[^1].To ? to : windows[^1].To);
            else
                windows.Add((e.StartsAt, to));
        }

        var periods = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var (from, to) in windows)
            for (var start = from; start < to; start += MaxDelay)
                periods.Add((start, start + MaxDelay < to ? start + MaxDelay : to));

        var known = uploads.Select(u => u.VideoId).ToHashSet();
        int searched = 0, pages = 0, added = 0;
        foreach (var (start, end) in periods)
        {
            if (searchPagesUsed >= SearchBudget)
            {
                Console.WriteLine($"Video {sourceName}: ricerca fermata a {SearchBudget} pagine per esecuzione, " +
                                  $"{periods.Count - searched} periodi su {periods.Count} da cercare con il prossimo recupero");
                break;
            }
            searched++;
            string? pageToken = null;
            for (var page = 0; page < MaxSearchPages && searchPagesUsed < SearchBudget; page++)
            {
                searchPagesUsed++;
                // regionCode=IT: senza, la ricerca esclude i video non visibili nel paese da cui parte la richiesta, e i
                // runner di GitHub Actions sono negli Stati Uniti (DAZN Italia pubblica i video solo per l'Italia).
                var data = await HttpJson.GetAsync(http,
                    $"{BaseUrl}/search?part=snippet&type=video&order=date&maxResults=50&regionCode=IT&channelId={channelId}" +
                    $"&publishedAfter={Rfc3339(start)}&publishedBefore={Rfc3339(end)}&key={apiKey}" +
                    (pageToken is null ? "" : $"&pageToken={pageToken}"), "YouTube", 0, ct);
                pages++;

                foreach (var item in data["items"]!.AsArray())
                {
                    var id = item!["id"]?["videoId"]?.GetValue<string>();
                    if (id is null || !known.Add(id)) continue;
                    var s = item["snippet"]!;
                    var thumbs = s["thumbnails"];
                    uploads.Add(new Upload(
                        id,
                        WebUtility.HtmlDecode(s["title"]!.GetValue<string>()), // la ricerca dà i titoli con le entità HTML ("&#39;")
                        (thumbs?["high"] ?? thumbs?["medium"] ?? thumbs?["default"])?["url"]?.GetValue<string>() ?? "",
                        DateTimeOffset.Parse(s["publishedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture)));
                    added++;
                }

                pageToken = data["nextPageToken"]?.GetValue<string>();
                if (pageToken is null) break;
            }
        }
        Console.WriteLine($"Video {sourceName}: oltre la playlist dei caricamenti, cercati {searched} periodi ({pages} pagine), {added} video");
    }

    private static string Rfc3339(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

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
