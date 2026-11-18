using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
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
    // A metà playlist YouTube a volte risponde 404 (playlistNotFound) su una pagina che poco dopo funziona: si riprova,
    // poi ci si ferma a quello che si è letto, invece di perdere tutto il canale.
    private const int MaxPageNotFoundRetries = 2;
    // La playlist dei caricamenti dà al massimo gli ultimi 20.000 video del canale: DAZN Italia (circa 31.700) arriva così
    // solo a novembre 2024, Sky Sport (circa 36.900) a settembre 2024.
    private const int UploadsLimit = 20_000;
    // Ricerca: al massimo circa 500 risultati per richiesta, quindi si cerca a periodi di MaxDelay (un turno di Serie A su
    // DAZN sono circa 85 video, 2 pagine). Senza parole chiave: con "Serie A highlights" la ricerca perdeva video
    // ("... | Serie A Enilive | DAZN Higlights") senza restituirne meno.
    private const int MaxSearchPages = 10;
    // La ricerca per periodo però non restituisce tutti i video del periodo, e non sempre gli stessi: Roma-Inter del
    // 10/02/2024 (DAZN, alle 22:00) mancava dalla prima pagina tra un video delle 23:28 e uno delle 21:00, e due recuperi
    // completi non l'hanno trovata. Cercando le due squadre ("Roma Inter") esce. Quindi le partite rimaste senza video si
    // cercano una per una, con le squadre come parole chiave, quando in una finestra ne restano al massimo
    // MaxTargetedSessions: una ricerca per partita costa meno di un periodo intero. Con più partite in attesa (una stagione
    // recuperata per la prima volta, un turno intero) il periodo costa meno. Le ricerche mirate si fanno per prime, così la
    // quota non finisce nei periodi, che per le partite rimaste dopo un recupero trovano ormai poco. Con 3 restavano ai
    // periodi le partite ravvicinate (4 dal 2 al 5/02/2024), che i periodi non trovavano; un turno intero resta ai periodi.
    private const int MaxTargetedSessions = 5;
    // Ricerche per partita: nomi brevi, poi un nome usato dai canali (TeamAliases, es. "Hellas" per "Verona").
    private const int MaxTargetedQueries = 2;
    // Le ricerche hanno una quota giornaliera a parte ("Search Queries per day", 100 richieste) che non tocca quella delle
    // altre chiamate: questo è il massimo di pagine per esecuzione, per tutti i canali insieme. Se la quota finisce prima,
    // la ricerca fallisce ma i dettagli dei video funzionano ancora. Una stagione intera oltre il limite (es. la Serie A
    // 2023-24 su DAZN, circa 140 pagine) ne richiede di più: il recupero si ferma, salva quello che ha trovato e al giro
    // dopo (il giorno seguente) riprende dalle sessioni ancora senza video.
    // Quando la quota finisce YouTube risponde 429 "rateLimitExceeded" con limite "Search Queries per day" (verificato il
    // 12/11/2026), quindi non si riprova: attendere non serve fino al giorno dopo.
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
        ["Hellas"] = "Verona", // DAZN: "Hellas-Empoli 1-4"
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
    // canale ("Mantova-Cosenza 3-2 | Serie BKT", "| LaLiga |"), cercata solo dopo il primo "|": prima c'è testo libero, che
    // può nominarne una ("la Roma chiude con l'Europa League: Torino-Roma 0-2 | DAZN"). Esclusi sempre Serie A Femminile e Primavera.
    public static YouTubeHighlights DaznSerieA(HttpClient http, string apiKey) =>
        new(http, apiKey, "UC7N8IuYFhU8Hs1ruCTsXTbg", "DAZN", title =>
        {
            var labels = title.IndexOf('|') is var bar and >= 0 ? title[bar..] : "";
            if (!Contains(title, "DAZN") || DaznRerun().IsMatch(title) || DaznExcluded().IsMatch(title)
                || !SerieAMention().IsMatch(title) && DaznOtherCompetition().IsMatch(labels)) return null;
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
    private SportEvent? SessionFor(IEnumerable<SportEvent> events, string title, DateTimeOffset publishedAt) =>
        matcher(title) is not { } isEvent ? null
        : events
            .Where(e => e.Status == EventStatus.Finished && Sessions.Contains(e.Session) && isEvent(e))
            .Where(e => e.StartsAt <= publishedAt && publishedAt - e.StartsAt <= MaxDelay)
            .Where(e => YearMatches(e, title))
            .MaxBy(e => e.StartsAt);

    // Solo per le stagioni di un anno (F1, MotoGP), dove l'anno nel titolo è quello del campionato e scarta le gare storiche
    // ripubblicate ("2019 ... Grand Prix"). Nel calcio ("2026-27") squadre, risultato e data bastano, e un anno nel testo
    // libero può essere altro ("show del classe 2006") o l'anno solare, diverso da quello d'inizio della stagione.
    private static bool YearMatches(SportEvent e, string title) =>
        e.Season.Contains('-') || YearPattern().Match(title) is not { Success: true } year || e.Season == year.Value;

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
                // Le pagine fatte servono a confrontare il punto di arresto con la quota nella console di Google Cloud.
                Console.Error.WriteLine($"AVVISO ricerca video {sourceName}, dopo {searchPagesUsed} pagine di ricerca " +
                                        $"in questa esecuzione: {ex.Message}");
            }
        }

        var found = new Dictionary<string, Upload>();
        var nearMisses = new List<string>();
        foreach (var upload in uploads.OrderBy(u => u.PublishedAt))
        {
            if (SessionFor(waiting, upload.Title, upload.PublishedAt) is { } match) found.TryAdd(match.Id, upload);
            else if (NearMiss(waiting, upload) is { } reason) nearMisses.Add(reason);
        }
        LogSome("titolo valido ma video scartato", nearMisses);

        // Durata e disponibilità in Italia: i video bloccati non si mostrano.
        var details = found.Count == 0 ? [] : await GetDetailsAsync(found.Values.Select(u => u.VideoId).ToList(), ct);
        var attached = 0;
        var unavailable = new List<string>();
        var result = events.Select(e =>
        {
            if (!found.TryGetValue(e.Id, out var u)) return e;
            if (!details.TryGetValue(u.VideoId, out var d) || d.BlockedInItaly)
            {
                unavailable.Add($"{u.Title} ({(d is null ? "non più disponibile" : "bloccato in Italia")})");
                return e;
            }
            attached++;
            return e with { Videos = [.. e.Videos, new Video(u.VideoId, u.Title, u.ThumbnailUrl, u.PublishedAt, sourceName, d.Duration, d.Embeddable ? null : false)] };
        }).ToList();

        Console.WriteLine($"Video {sourceName}: {attached} highlights abbinati");
        LogSome("video non mostrabile", unavailable);
        LogSome("ancora senza video", waiting.Where(e => !found.ContainsKey(e.Id))
            .Select(e => $"{e.StartsAt:yyyy-MM-dd HH:mm} {e.Title} [{e.Session}]").ToList());
        return result;
    }

    // Per capire perché una sessione resta senza video: un video il cui titolo indica una sessione in attesa, ma pubblicato
    // poco prima dell'inizio, poco oltre MaxDelay o con un altro anno nel titolo (es. un orario sbagliato nei dati).
    // Solo i casi vicini: per F1 e MotoGP il titolo indica tutte le gare della stagione, e gli altri sono video di altri GP.
    private string? NearMiss(List<SportEvent> waiting, Upload upload)
    {
        if (matcher(upload.Title) is not { } isEvent) return null;
        var near = waiting
            .Where(e => isEvent(e) && upload.PublishedAt >= e.StartsAt - TimeSpan.FromDays(1)
                        && upload.PublishedAt <= e.StartsAt + MaxDelay + TimeSpan.FromDays(2))
            .MinBy(e => (upload.PublishedAt - e.StartsAt).Duration());
        if (near is null) return null;
        var reason = upload.PublishedAt < near.StartsAt ? "pubblicato prima dell'inizio"
            : upload.PublishedAt - near.StartsAt > MaxDelay ? "pubblicato oltre 3 giorni dopo"
            : "anno nel titolo diverso dalla stagione";
        return $"{upload.Title} ({upload.PublishedAt:yyyy-MM-dd HH:mm}) per {near.Title} [{near.Session}] del {near.StartsAt:yyyy-MM-dd HH:mm}: {reason}";
    }

    // Al massimo 10 righe per elenco, per non riempire il log di un recupero.
    private void LogSome(string what, List<string> items)
    {
        if (items.Count == 0) return;
        Console.WriteLine($"Video {sourceName}, {what}: {items.Count}");
        foreach (var item in items.Take(10)) Console.WriteLine($"  {item}");
        if (items.Count > 10) Console.WriteLine($"  ... e altri {items.Count - 10}");
    }

    private async Task<List<Upload>> ReadUploadsSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        // La playlist "caricamenti" di un canale ha lo stesso id del canale con "UU" al posto di "UC".
        var playlistId = "UU" + channelId[2..];
        var uploads = new List<Upload>();
        string? pageToken = null;
        var pages = 0;
        var failed = false;
        for (var page = 0; page < MaxPages; page++)
        {
            pages++;
            var data = await ReadUploadsPageAsync(playlistId, pageToken, ct);
            if (data is null)
            {
                failed = true;
                break;
            }

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
            if (pageToken is null || uploads.Count == 0 || uploads[^1].PublishedAt < since) break;
        }

        // Quanto si è letto e perché ci si è fermati: se un video manca, si vede se la lettura era arrivata alla sua data.
        var stop = failed ? "YouTube ha risposto 404 a metà playlist anche riprovando"
            : uploads.Count > 0 && uploads[^1].PublishedAt < since ? "arrivata alla sessione più vecchia"
            : pageToken is null ? $"fine della playlist ({uploads.Count} video)"
            : $"limite di {MaxPages} pagine";
        Console.WriteLine($"Video {sourceName}: letti {uploads.Count} caricamenti in {pages} pagine" +
                          (uploads.Count == 0 ? "" : $", dal {uploads[0].PublishedAt:yyyy-MM-dd HH:mm} al {uploads[^1].PublishedAt:yyyy-MM-dd HH:mm}") +
                          $", lettura fermata: {stop}");

        // La lettura si ferma al primo video più vecchio della sessione cercata, quindi conta che la playlist sia in ordine
        // di pubblicazione. Un video fuori ordine (es. programmato: YouTube potrebbe ordinarlo per data di caricamento)
        // può restare oltre il punto di arresto.
        var outOfOrder = uploads.Zip(uploads.Skip(1)).Where(p => p.Second.PublishedAt > p.First.PublishedAt)
            .Select(p => $"{p.Second.Title} ({p.Second.PublishedAt:yyyy-MM-dd HH:mm}) dopo uno del {p.First.PublishedAt:yyyy-MM-dd HH:mm}")
            .ToList();
        LogSome("caricamenti fuori ordine di data", outOfOrder);
        return uploads;
    }

    // Una pagina dei caricamenti; null se dopo la prima pagina YouTube continua a rispondere 404 (MaxPageNotFoundRetries).
    // Sulla prima pagina il 404 vuol dire davvero che la playlist non esiste, e resta un errore.
    private async Task<JsonNode?> ReadUploadsPageAsync(string playlistId, string? pageToken, CancellationToken ct)
    {
        var url = $"{BaseUrl}/playlistItems?part=snippet&maxResults=50&playlistId={playlistId}&key={apiKey}" +
                  (pageToken is null ? "" : $"&pageToken={pageToken}");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await HttpJson.GetAsync(http, url, "YouTube", 0, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && pageToken is not null)
            {
                Console.Error.WriteLine($"AVVISO video {sourceName}: {ex.Message}");
                if (attempt >= MaxPageNotFoundRetries) return null;
                await Task.Delay(TimeSpan.FromSeconds(10 * (attempt + 1)), ct);
            }
        }
    }

    // Cerca i video pubblicati prima di "before" nei MaxDelay dopo le sessioni ancora senza video, e li aggiunge a uploads.
    // Le finestre che si toccano si uniscono. Una finestra con poche partite di calcio si cerca partita per partita
    // (MaxTargetedSessions), le altre si dividono in periodi di MaxDelay.
    private async Task SearchBeforeAsync(List<SportEvent> waiting, DateTimeOffset before, List<Upload> uploads, CancellationToken ct)
    {
        var windows = new List<(DateTimeOffset From, DateTimeOffset To, List<SportEvent> Sessions)>();
        foreach (var e in waiting.Where(e => e.StartsAt < before).OrderBy(e => e.StartsAt))
        {
            var to = e.StartsAt + MaxDelay < before ? e.StartsAt + MaxDelay : before;
            if (windows.Count > 0 && e.StartsAt <= windows[^1].To)
            {
                windows[^1].Sessions.Add(e);
                windows[^1] = (windows[^1].From, to > windows[^1].To ? to : windows[^1].To, windows[^1].Sessions);
            }
            else
                windows.Add((e.StartsAt, to, [e]));
        }

        var targeted = new List<SportEvent>();
        var periods = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var (from, to, sessions) in windows)
        {
            if (sessions.Count <= MaxTargetedSessions && sessions.All(e => e.HomeTeam?.ShortName is not null && e.AwayTeam?.ShortName is not null))
                targeted.AddRange(sessions);
            else
                for (var start = from; start < to; start += MaxDelay)
                    periods.Add((start, start + MaxDelay < to ? start + MaxDelay : to));
        }

        var known = uploads.Select(u => u.VideoId).ToHashSet();

        int searchedMatches = 0, foundMatches = 0, targetedPages = 0;
        foreach (var e in targeted)
        {
            if (searchPagesUsed >= SearchBudget) break;
            searchedMatches++;
            var end = e.StartsAt + MaxDelay < before ? e.StartsAt + MaxDelay : before;
            var queries = TeamNames(e.HomeTeam).SelectMany(home => TeamNames(e.AwayTeam).Select(away => $"{home} {away}")).Take(MaxTargetedQueries);
            foreach (var query in queries)
            {
                if (searchPagesUsed >= SearchBudget) break;
                searchPagesUsed++;
                targetedPages++;
                var data = await SearchAsync(e.StartsAt, end, query, null, ct);
                var added = AddSearchResults(data, known, uploads);
                if (added.Any(u => SessionFor(waiting, u.Title, u.PublishedAt)?.Id == e.Id))
                {
                    foundMatches++;
                    break;
                }
            }
        }
        if (targeted.Count > 0)
            Console.WriteLine($"Video {sourceName}: oltre la playlist dei caricamenti, cercate {searchedMatches} partite su {targeted.Count} " +
                              $"una per una ({targetedPages} pagine), trovate {foundMatches}");

        int searched = 0, pages = 0, addedVideos = 0;
        foreach (var (start, end) in periods)
        {
            if (searchPagesUsed >= SearchBudget) break;
            searched++;
            string? pageToken = null;
            for (var page = 0; page < MaxSearchPages && searchPagesUsed < SearchBudget; page++)
            {
                searchPagesUsed++;
                var data = await SearchAsync(start, end, null, pageToken, ct);
                pages++;
                addedVideos += AddSearchResults(data, known, uploads).Count;
                pageToken = data["nextPageToken"]?.GetValue<string>();
                if (pageToken is null) break;
            }
        }
        if (periods.Count > 0)
            Console.WriteLine($"Video {sourceName}: oltre la playlist dei caricamenti, cercati {searched} periodi ({pages} pagine), {addedVideos} video");

        if (searchPagesUsed >= SearchBudget && (searchedMatches < targeted.Count || searched < periods.Count))
            Console.WriteLine($"Video {sourceName}: ricerca fermata a {SearchBudget} pagine per esecuzione, da cercare con il prossimo " +
                              $"recupero {targeted.Count - searchedMatches} partite su {targeted.Count} e {periods.Count - searched} periodi su {periods.Count}");
    }

    // Una pagina di ricerca nel canale tra start ed end, ordinata per data; query: parole chiave, null per tutti i video.
    // regionCode=IT: senza, la ricerca esclude i video non visibili nel paese da cui parte la richiesta, e i
    // runner di GitHub Actions sono negli Stati Uniti (DAZN Italia pubblica i video solo per l'Italia).
    private Task<JsonNode> SearchAsync(DateTimeOffset start, DateTimeOffset end, string? query, string? pageToken, CancellationToken ct) =>
        HttpJson.GetAsync(http,
            $"{BaseUrl}/search?part=snippet&type=video&order=date&maxResults=50&regionCode=IT&channelId={channelId}" +
            $"&publishedAfter={Rfc3339(start)}&publishedBefore={Rfc3339(end)}&key={apiKey}" +
            (query is null ? "" : $"&q={Uri.EscapeDataString(query)}") +
            (pageToken is null ? "" : $"&pageToken={pageToken}"), "YouTube", 0, ct);

    // Aggiunge a uploads i video della pagina non ancora visti e li restituisce.
    private static List<Upload> AddSearchResults(JsonNode data, HashSet<string> known, List<Upload> uploads)
    {
        var added = new List<Upload>();
        foreach (var item in data["items"]!.AsArray())
        {
            var id = item!["id"]?["videoId"]?.GetValue<string>();
            if (id is null || !known.Add(id)) continue;
            var s = item["snippet"]!;
            var thumbs = s["thumbnails"];
            added.Add(new Upload(
                id,
                WebUtility.HtmlDecode(s["title"]!.GetValue<string>()), // la ricerca dà i titoli con le entità HTML ("&#39;")
                (thumbs?["high"] ?? thumbs?["medium"] ?? thumbs?["default"])?["url"]?.GetValue<string>() ?? "",
                DateTimeOffset.Parse(s["publishedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture)));
        }
        uploads.AddRange(added);
        return added;
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
